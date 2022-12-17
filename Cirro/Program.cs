using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using Microsoft.Graph;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cirro;
public class Parser
{

    public static async Task<int> Main(string[] args)
    {
        var supportedEnvs = new List<string>() { "test", "dev", "prod" };
        var services = new HashSet<string>() { Services.ManagedIdentity, Services.KeyVault };
        var configs = new Dictionary<string, string>();
        var infraPrefix = args[0];
        var csprojData = "";
        var env = "";

        if (args.Length < 1 || args[0].Length > 9)
        {
            Console.WriteLine("The Infra Prefix is missing or longer than 10 character. Please see doc {insert doc link}");
            return 0;
        }

        Console.WriteLine($"Launched from {Environment.CurrentDirectory}"); // <- find all csproj in current directory and combine them as string
        //Console.WriteLine($"Physical location {AppDomain.CurrentDomain.BaseDirectory}");
        //Console.WriteLine($"AppContext.BaseDir {AppContext.BaseDirectory}");
        //return 0;

        try
        {
            var filepath = args[1];
            env = args[2];

            //check args, i.e. is env of expected value, is filePath openable etc.
            if (supportedEnvs.Contains(env) == false)
            {
                Console.WriteLine($"Enviornment {env} is not a supported enviornment. Please selet from: {string.Join(", ", supportedEnvs)}");
                return 1;
            }

            // Attempt to open input file.
            using var reader = new StreamReader(filepath);
            csprojData = await reader.ReadToEndAsync();
        }
        catch (IOException e)
        {
            TextWriter errorWriter = Console.Error;
            errorWriter.WriteLine(e.Message);
            return 1;
        }

        Console.WriteLine($"InfraPrefix: {infraPrefix}");
        configs.Add("__PROJECTNAME__", infraPrefix);

        foreach (var sdkToService in Services.SdkToServices)
        {
            if (csprojData.Contains(sdkToService.Key))
            {
                services.Add(sdkToService.Value);
            }
        }

        var credential = new AzureCliCredential();

        if (env.Equals("dev"))
        {
            var graphClient = new GraphServiceClient(credential);
            var users = await graphClient.Users
                .Request()
                .GetAsync();
            var principalId = users.FirstOrDefault()?.Id;

            if (principalId != null)
            {
                configs.Add("__USERPRINCIPALID__", principalId);
                services.Add(Services.DevUser);
            }
        }

        configs.Add("\"__SERVICES__\"", "[\"" + string.Join("\",\"", services) + "\"]");

        var client = new ArmClient(credential);
        SubscriptionResource subscription;

        if (args.Length < 3)
        {
            subscription = await client.GetDefaultSubscriptionAsync();
        }
        else
        {
            var subs = client.GetSubscriptions();
            subscription = subs.FirstOrDefault(x => x.Data.SubscriptionId == args[2]) ?? await client.GetDefaultSubscriptionAsync();    //shouldn't default, should throw error instead
        }

        Console.WriteLine($"Using subscription: {subscription.Data.SubscriptionId}");
        //Console.WriteLine($"Configs: {string.Join(" ",configs)}");
        List<string> ignoreServices = new() { Services.DevUser, Services.ManagedIdentity, Services.KeyVault };
        Console.WriteLine($"Services to be provisioned: Managed Identity (required), Keyvault (required), {string.Join(", ",services.Where(x=> ignoreServices.Contains(x) == false))}");

        Console.WriteLine("Is this correct? (Y/n)");
        string userinput = Console.ReadLine();

        if (userinput.Trim().ToLower() != "y" && userinput.Trim().ToLower() != "yes")
        {
            Console.WriteLine("Exiting. Nothing has been provisioned.");
            return 0;
        }

        var resourceGroups = subscription.GetResourceGroups();
        var rgPrefix = $"{infraPrefix}-{env}";
        var uniqueString = GetUniqueString(subscription.Data.SubscriptionId, rgPrefix);

        if (services.Contains(Services.WebApp) || services.Contains(Services.FunctionApp))
        {
            var regex = new Regex("<TargetFramework>(.*)</TargetFramework>");
            var v = regex.Match(csprojData);
            var s = v.Groups[1].ToString();

            configs.Add("__LINUXVERSION__", "DOTNETCORE|" + s.Replace("net", ""));
            configs.Add("__WEBPLANNAME__", $"{infraPrefix}-WebPlan-{uniqueString}".Substring(0,40));
            configs.Add("__FUNCTIONPLANNAME__", $"{infraPrefix}-FunctionPlan-{uniqueString}".Substring(0,40));
        }

        var primaryRegionResourceGroupName = $"{rgPrefix}-Primary";
        configs.Add("__PRIMARYRGNAME__", primaryRegionResourceGroupName);

        //Primary Deploy
        Console.WriteLine($"Creating the Primary Region Resource Group");
        var operation = await resourceGroups.CreateOrUpdateAsync(WaitUntil.Completed, primaryRegionResourceGroupName, new ResourceGroupData(AzureLocation.CentralUS));
        var primaryResourceGroup = operation.Value;
        var ArmDeploymentCollection = primaryResourceGroup.GetArmDeployments();

        var primaryServiceNames = new ServiceNames(infraPrefix, env, uniqueString, AzureLocation.CentralUS);
        await Provision(ArmDeploymentCollection, primaryServiceNames, configs, services, "Primary");

        //Regional Deploys
        Console.WriteLine($"Creating the {AzureLocation.CentralUS} Resource Group");
        operation = await resourceGroups.CreateOrUpdateAsync(WaitUntil.Completed, $"{rgPrefix}-{AzureLocation.CentralUS}", new ResourceGroupData(AzureLocation.CentralUS));
        var regionalResourceGroup = operation.Value;
        ArmDeploymentCollection = regionalResourceGroup.GetArmDeployments();

        var regionalServiceNames = new ServiceNames(infraPrefix, env, uniqueString, AzureLocation.CentralUS);
        await Provision(ArmDeploymentCollection, regionalServiceNames, configs, services, "Regional");

        //System.IO.File.Exists to see if Failover json exists

        //Link resources
        ArmDeploymentCollection = regionalResourceGroup.GetArmDeployments();

        var linkServiceNames = new ServiceNames(infraPrefix, env, uniqueString, AzureLocation.CentralUS);
        await Provision(ArmDeploymentCollection, linkServiceNames, configs, services, "Link");


        /*
        //If Dev, add kv endpoint to appsettings.local/appsettings.dev. STAGE 2 issue

        //Console.WriteLine(AppContext.BaseDirectory);
        var initialJson = System.IO.File.ReadAllText("appsettings.Developer.json");
        var array = JArray.Parse(initialJson);

        var itemToAdd = new JObject();
        itemToAdd["id"] = 1234;
        itemToAdd["name"] = "carl2";
        array.Add(itemToAdd);

        var jsonToOutput = JsonConvert.SerializeObject(array, Formatting.Indented);
        */

        Console.WriteLine($"Completed Provisioning");

        //Split out primary region and primary region


        //Could Have a deploy failover region section


        //SQL server creation looks at a server login. Considering quering that via questions to terminal
        //https://learn.microsoft.com/en-us/azure/azure-sql/database/single-database-create-arm-template-quickstart?view=azuresql

        //Consider adding 00 to end of service names to iterate, but since template for everyone would it help?

        //dev then give access to resource groups? instead of role assignment to managed identity
        //https://learn.microsoft.com/en-us/azure/role-based-access-control/role-assignments-template

        return 0;
    }

    private static async Task Provision(ArmDeploymentCollection ArmDeploymentCollection, ServiceNames servceNames, Dictionary<string, string> configs, HashSet<string> services, string templateName)
    {
        Console.WriteLine($"Deploying the {templateName} Resources. This may take a while.");

        var template = System.IO.File.ReadAllText($"Data/{templateName}.json");
        var parameters = System.IO.File.ReadAllText($"Data/{templateName}.Parameters.json");

        foreach (var config in configs)
        {
            parameters = parameters.Replace(config.Key, config.Value);
        }

        foreach (var service in services)
        {
            if (servceNames.ServiceNameMap.ContainsKey(service))
            {
                var serviceName = servceNames.ServiceNameMap[service];
                parameters = parameters.Replace(serviceName.Key, serviceName.Value);
            }
        }

        Console.WriteLine(parameters);

        return;

        var deploymentName = Guid.NewGuid().ToString();
        var input = new ArmDeploymentContent(new ArmDeploymentProperties(ArmDeploymentMode.Incremental)
        {
            Template = BinaryData.FromString(template),
            Parameters = BinaryData.FromString(parameters)
        });

        var lro = await ArmDeploymentCollection.CreateOrUpdateAsync(WaitUntil.Started, deploymentName, input);
        await lro.UpdateStatusAsync();
        var sw = new Stopwatch();
        sw.Start();

        while (lro.HasCompleted == false)
        {
            await Task.Delay(10000);

            Console.WriteLine($"Still running. Checking again in 10 secs. Total elapsed time: " + sw.Elapsed.ToString("mm\\:ss"));
            await lro.UpdateStatusAsync();
        }

        sw.Stop();
    }

    private static string GetUniqueString(string sub, string rgPrefix)
    {
        var text = $"{sub}-{rgPrefix}";
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        byte[] textData = System.Text.Encoding.UTF8.GetBytes(text);
        byte[] hash = SHA256.HashData(textData);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();
    }
}