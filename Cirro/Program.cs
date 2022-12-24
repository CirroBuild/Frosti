using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using Microsoft.Graph;

namespace Cirro;
public class Parser
{
    public static async Task<int> Main(string[] args)
    {
        var cloud = args.Length > 0 ? args[0] : "";
        if (Constants.SupportedClouds.Contains(cloud) == false)
        {
            Console.WriteLine($"Please specify the cloud to create the infrastructure. Select from: {string.Join(", ", Constants.SupportedClouds)}");
            return 1;
        }

        if (args.Length < 2 || args[1].Length > 9 || args[1].Length < 5)
        {
            Console.WriteLine("The Infra Prefix is missing or isn't between 5-10 character. Please see doc {insert doc link}");
            return 1;
        }
        var infraPrefix = args[1];

        var env = args.Length > 2 ? args[2] : "";
        if (Constants.SupportedEnviornments.Contains(env) == false)
        {
            Console.WriteLine($"Enviornment is missing or {env} is not a supported enviornment. Please select from: {string.Join(", ", Constants.SupportedEnviornments)}");
            return 1;
        }

        var subId = args.Length > 3 ? args[3] : null;

        switch (cloud)
        {
            case "azure":
                return await ProvisionAzure(infraPrefix, env, subId);
            case "aws":
                return ProvisionAWS(infraPrefix, env);
            case "gcp":
                return ProvisionGCP(infraPrefix, env);
        }

        return 1;
    }

    private static int ProvisionAWS(string infraPrefix, string env)
    {
        Console.WriteLine("Automatically provisioning AWS resources is not currently supported. It's coming soon! Please try provisioning via azure for now.");
        return 0;
    }

    private static int ProvisionGCP(string infraPrefix, string env)
    {
        Console.WriteLine("Automatically provisioning GCP resources is not currently supported. It's coming soon! Please try provisioning via azure for now.");
        return 0;
    }

    private static async Task<int> ProvisionAzure(string infraPrefix, string env, string? subId)
    {
        var services = new HashSet<string>() { AzureServices.ManagedIdentity, AzureServices.KeyVault };
        var configs = new Dictionary<string, string>();
        var csprojData = "";

        var csprojFiles = System.IO.Directory.GetFiles(Environment.CurrentDirectory, "*.csproj", SearchOption.AllDirectories);
        if (csprojFiles.Length < 1)
        {
            Console.WriteLine($"No csproj file is found within this project. Please go to the root directory and try again");
        }
        //Console.WriteLine($"Launched from {Environment.CurrentDirectory}"); // <- find all csproj in current directory and combine them as string
        //Console.WriteLine($"Physical location {AppDomain.CurrentDomain.BaseDirectory}");
        //Console.WriteLine($"AppContext.BaseDir {AppContext.BaseDirectory}");
        //return 0;

        try
        {
            foreach (var csprojFile in csprojFiles)
            {
                using var reader = new StreamReader(csprojFile);
                csprojData += await reader.ReadToEndAsync();
            }
        }
        catch (IOException e)
        {
            TextWriter errorWriter = Console.Error;
            errorWriter.WriteLine(e.Message);
            return 1;
        }

        Console.WriteLine($"InfraPrefix: {infraPrefix}");

        foreach (var sdkToService in AzureServices.SdkToServices)
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
                services.Add(AzureServices.DevUser);
            }

            services.Remove(AzureServices.WebApp);
            services.Remove(AzureServices.FunctionApp);
        }

        configs.Add("\"__SERVICES__\"", "[\"" + string.Join("\",\"", services) + "\"]");

        var client = new ArmClient(credential);
        SubscriptionResource? subscription;

        if (subId == null)
        {
            subscription = await client.GetDefaultSubscriptionAsync();
        }
        else
        {
            var subs = client.GetSubscriptions();
            subscription = subs.FirstOrDefault(x => x.Data.SubscriptionId == subId);    //shouldn't default, should throw error instead

            if (subscription == null)
            {
                subscription = await client.GetDefaultSubscriptionAsync();
                Console.WriteLine($"{subId} can't be found under your user account. Would you like to use your default subscription of {subscription.Data.Id}? (Y/n)");
                string? input = Console.ReadLine();

                if (input?.Trim().ToLower() != "y" && input?.Trim().ToLower() != "yes")
                {
                    Console.WriteLine("Exiting. Nothing has been provisioned.");
                    return 0;
                }
            }
        }

        Console.WriteLine($"Using subscription: {subscription.Data.SubscriptionId}");
        List<string> ignoreServices = new() { AzureServices.DevUser, AzureServices.ManagedIdentity, AzureServices.KeyVault };
        Console.WriteLine($"Services to be provisioned: Managed Identity (required), Keyvault (required), {string.Join(", ", services.Where(x => ignoreServices.Contains(x) == false))}");

        Console.WriteLine("Is this correct? (Y/n)");
        string? userinput = Console.ReadLine();

        if (userinput?.Trim().ToLower() != "y" && userinput?.Trim().ToLower() != "yes")
        {
            Console.WriteLine("Exiting. Nothing has been provisioned.");
            return 0;
        }

        var resourceGroups = subscription.GetResourceGroups();
        var rgPrefix = $"{infraPrefix}-{env}";
        var uniqueString = GetUniqueString(subscription.Data.SubscriptionId, rgPrefix);

        if (services.Contains(AzureServices.WebApp) || services.Contains(AzureServices.FunctionApp))
        {
            var regex = new Regex("<TargetFramework>(.*)</TargetFramework>");   //since combining csproj, could have multiple target frameworks here
            var v = regex.Match(csprojData);
            var s = v.Groups[1].ToString();

            configs.Add("__LINUXVERSION__", "DOTNETCORE|" + s.Replace("net", ""));
            configs.Add("__WEBPLANNAME__", $"{infraPrefix}-WebPlan-{uniqueString}".Substring(0, 40));
            configs.Add("__FUNCTIONPLANNAME__", $"{infraPrefix}-FunctionPlan-{uniqueString}".Substring(0, 40));
        }

        var primaryRegionResourceGroupName = $"{rgPrefix}-Primary";
        configs.Add("__PRIMARYRGNAME__", primaryRegionResourceGroupName);

        //Primary Deploy
        Console.WriteLine($"Deploying the Global Resources. This may take a while.");
        var operation = await resourceGroups.CreateOrUpdateAsync(WaitUntil.Completed, primaryRegionResourceGroupName, new ResourceGroupData(AzureLocation.CentralUS));
        var primaryResourceGroup = operation.Value;
        var ArmDeploymentCollection = primaryResourceGroup.GetArmDeployments();

        var primaryServiceNames = new AzureServiceNames(infraPrefix, env, uniqueString, AzureLocation.CentralUS);
        await Provision(ArmDeploymentCollection, primaryServiceNames, configs, services, env, "Primary");

        //Regional Deploys
        Console.WriteLine($"Deploying the Regional Resources. This may take a while.");
        operation = await resourceGroups.CreateOrUpdateAsync(WaitUntil.Completed, $"{rgPrefix}-{AzureLocation.CentralUS}", new ResourceGroupData(AzureLocation.CentralUS));
        var regionalResourceGroup = operation.Value;
        ArmDeploymentCollection = regionalResourceGroup.GetArmDeployments();

        var regionalServiceNames = new AzureServiceNames(infraPrefix, env, uniqueString, AzureLocation.CentralUS);
        await Provision(ArmDeploymentCollection, regionalServiceNames, configs, services, env, "Regional");

        //Link resources. Regional cuz KV is regional
        //SSL Cert creation https://github.com/Azure/azure-quickstart-templates/tree/master/application-workloads/umbraco/umbraco-webapp-simple
        Console.WriteLine($"Linking the Resources");
        await Provision(ArmDeploymentCollection, regionalServiceNames, configs, services, env, "Link");

        if (env == "dev")
        {
            var kvEndpoint = "\"KV_ENDPOINT\": \"https://aperitest-dev-cus-kv0f0f.vault.azure.net/\"";
            System.IO.File.WriteAllText($"{Environment.CurrentDirectory}/Dev.Keyvault.json", kvEndpoint);
        }

        /*
        //If Dev, add kv endpoint to appsettings.local/appsettings.dev. STAGE 2 issue
        //builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);
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

        //SQL server creation looks at a server login. Considering quering that via questions to terminal
        //https://learn.microsoft.com/en-us/azure/azure-sql/database/single-database-create-arm-template-quickstart?view=azuresql

        return 0;
    }

    private static async Task Provision(ArmDeploymentCollection ArmDeploymentCollection, AzureServiceNames servceNames, Dictionary<string, string> configs, HashSet<string> services, string env, string templateName)
    {
        var templateFile = $"{AppDomain.CurrentDomain.BaseDirectory}Data/{env}/{templateName}.json";
        var parameterFile = $"{AppDomain.CurrentDomain.BaseDirectory}Data/{env}/{templateName}.Parameters.json";

        if (System.IO.File.Exists(templateFile))
        {
            return;
        }

        var template = System.IO.File.ReadAllText(templateFile);
        var parameters = System.IO.File.ReadAllText(parameterFile);

        foreach (var config in configs)
        {
            parameters = parameters.Replace(config.Key, config.Value);
        }

        //Need to do it this way with the double map because service name cahnges with region
        foreach (var service in services)
        {
            if (servceNames.ServiceNameMap.ContainsKey(service))
            {
                var serviceName = servceNames.ServiceNameMap[service];
                parameters = parameters.Replace(serviceName.Key, serviceName.Value);
            }
        }

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