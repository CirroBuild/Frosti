using System.Diagnostics;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;

public class Parser
{
    public static async Task<int> Main(string[] args)
    {
        var supportedEnvs = new List<string>() {"test", "dev", "prod" };
        var services = new HashSet<string>();
        var configs = new Dictionary<string, string>();
        var csprojData = "";
        var env = "";

        try
        {
            var filepath = args[0];
            env = args[1];

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
        var projName = Path.GetFileName(args[0]).Replace(".csproj", "") + "Auto";

        Console.WriteLine($"ProjectName: {projName}");

        //Should be able to move a lot of this to a const dictionary
        if (csprojData.Contains("<Project Sdk=\"Microsoft.NET.Sdk.Web\">"))
        {
            services.Add("WebApp");
            configs.Add("__WEBAPPNAME__", projName);

            configs.Add("__WEBAPPSKU__", env == "dev" ? "F1" : "S1");

        }
        else if (csprojData.Contains("Microsoft.NET.Sdk.Functions"))
        {
            services.Add("FunctionApp");
            configs.Add("__FUNCTIONAPPNAME__", projName);
        }

        if (csprojData.Contains("Azure.Storage"))
        {
            services.Add("Storage");
            configs.Add("__STORAGENAME__", projName.ToLower() + "storage");
        }

        if (csprojData.Contains("Microsoft.Azure.Cosmos"))
        {
            services.Add("Cosmos");
            configs.Add("__COSMOSNAME__", projName.ToLower() + "cosmos");
        }

        if (services.Contains("WebApp") || services.Contains("FunctionApp"))
        {
            var regex = new Regex("<TargetFramework>(.*)</TargetFramework>");
            var v = regex.Match(csprojData);
            var s = v.Groups[1].ToString();

            configs.Add("__LINUXVERSION__", "DOTNETCORE|" + s.Replace("net", ""));
        }

        configs.Add("\"__SERVICES__\"", "[\"" + string.Join("\",\"", services) + "\"]");

        var client = new ArmClient(new AzureCliCredential());
        SubscriptionResource subscription;

        if (args.Length < 3)
        {
            subscription = await client.GetDefaultSubscriptionAsync();
        }
        else
        {
            var subs = client.GetSubscriptions();
            subscription = subs.FirstOrDefault(x => x.Data.SubscriptionId == args[2]) ?? await client.GetDefaultSubscriptionAsync();
        }

        Console.WriteLine($"Using subscription: {subscription.Data.SubscriptionId}");
        //Console.WriteLine($"Configs: {string.Join(" ",configs)}");
        Console.WriteLine($"Services to be provisioned: {string.Join(", ",services)}");

        Console.WriteLine("Is this correct? (Y/n)");
        string userinput = Console.ReadLine();

        if (userinput.Trim().ToLower() != "y" && userinput.Trim().ToLower() != "yes")
        {
            Console.WriteLine("Exiting. Nothing has been provisioned.");
            return 0;
        }

        var globalTemplate = File.ReadAllText($"Data/Global.json");
        var globalParameters = File.ReadAllText($"Data/Global.Parameters.json");
        var regionalTemplate = File.ReadAllText($"Data/Regional.json");
        var regionalParameters = File.ReadAllText($"Data/Regional.Parameters.json");

        //Global Deploy
        Console.WriteLine($"Creating the Global Resource Group");
        var resourceGroups = subscription.GetResourceGroups();
        var operation = await resourceGroups.CreateOrUpdateAsync(WaitUntil.Completed, $"{projName}-Global", new ResourceGroupData(AzureLocation.CentralUS));
        var resourceGroup = operation.Value;
        var ArmDeploymentCollection = resourceGroup.GetArmDeployments();

        await Provision(ArmDeploymentCollection, configs, globalTemplate, globalParameters, "Global");

        //Regional Deploys
        Console.WriteLine($"Creating the Global Resource Group");
        operation = await resourceGroups.CreateOrUpdateAsync(WaitUntil.Completed, $"{projName}-{AzureLocation.CentralUS}", new ResourceGroupData(AzureLocation.CentralUS));
        resourceGroup = operation.Value;
        ArmDeploymentCollection = resourceGroup.GetArmDeployments();

        await Provision(ArmDeploymentCollection, configs, regionalTemplate, regionalParameters, AzureLocation.CentralUS.ToString());

        //Link Regional resources to Global resources

        Console.WriteLine($"Completed Provisioning");
        return 0;
    }

    private static async Task Provision(ArmDeploymentCollection ArmDeploymentCollection, Dictionary<string, string> configs, string template, string parameters, string region)
    {
        Console.WriteLine($"Deploying the {region} Resources. This may take a while.");

        foreach (var config in configs)
        {
            parameters = parameters.Replace(config.Key, config.Value);
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
}