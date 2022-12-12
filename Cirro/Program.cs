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
        configs.Add("__PROJECTNAME__", projName);

        //Should be able to move a lot of this to a const dictionary
        if (csprojData.Contains("<Project Sdk=\"Microsoft.NET.Sdk.Web\">"))
        {
            services.Add("WebApp");
            configs.Add("__WEBAPPSKU__", env == "dev" ? "F1" : "S1");

        }
        else if (csprojData.Contains("Microsoft.NET.Sdk.Functions"))
        {
            services.Add("FunctionApp");
            configs.Add("__FUNCTIONAPPSKU__", env == "dev" ? "Y1" : "EP1");
        }

        if (csprojData.Contains("Azure.Storage"))
        {
            services.Add("Storage");
        }

        if (csprojData.Contains("Azure.Storage.Blobs"))
        {
            services.Add("Blob");
        }

        if (csprojData.Contains("Azure.Storage.Queues"))
        {
            services.Add("Queues");
        }

        if (csprojData.Contains("Azure.Storage.Files.Shares"))
        {
            services.Add("Files");
        }

        if (csprojData.Contains("Azure.Storage.Files.DataLake"))
        {
            services.Add("DataLake");
        }

        if (csprojData.Contains("Azure.Messaging.ServiceBus"))
        {
            services.Add("ServiceBus");
        }

        if (csprojData.Contains("Azure.Messaging.EventHubs"))
        {
            services.Add("EventHubs");
        }

        if (csprojData.Contains("Microsoft.ApplicationInsights"))
        {
            services.Add("ApplicationInsights");
        }

        if (csprojData.Contains("Microsoft.Azure.Cosmos"))
        {
            services.Add("Cosmos");
        }

        if (csprojData.Contains("StackExchange.Redis"))
        {
            services.Add("Redis");
        }

        //Managed Instance for premium? https://learn.microsoft.com/en-us/azure/azure-sql/managed-instance/create-template-quickstart?view=azuresql&tabs=azure-powershell
        if (csprojData.Contains("Microsoft.Data.SqlClient"))
        {
            services.Add("SQL");
        }

        //Flexible server for premium? https://learn.microsoft.com/en-us/azure/templates/microsoft.dbformysql/flexibleservers?pivots=deployment-language-arm-template
        if (csprojData.Contains("MySql.Data"))
        {
            services.Add("MySql");
        }

        if (csprojData.Contains("Npgsql"))
        {
            services.Add("PostgreSQL");
        }

        //Figure out MariaDb

        if (csprojData.Contains("Azure.Security.KeyVault"))
        {
            services.Add("KeyVault");
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
        Console.WriteLine($"Creating the {AzureLocation.CentralUS} Resource Group");
        operation = await resourceGroups.CreateOrUpdateAsync(WaitUntil.Completed, $"{projName}-{AzureLocation.CentralUS}", new ResourceGroupData(AzureLocation.CentralUS));
        resourceGroup = operation.Value;
        ArmDeploymentCollection = resourceGroup.GetArmDeployments();

        await Provision(ArmDeploymentCollection, configs, regionalTemplate, regionalParameters, AzureLocation.CentralUS.ToString());

        //Link Regional resources to Global resources
            //Add appsettings etc. here
        Console.WriteLine($"Completed Provisioning");

        //Split out global region and primary region


        //Could Have a deploy failover region section


        //SQL server creation looks at a server login. Considering quering that via questions to terminal
        //https://learn.microsoft.com/en-us/azure/azure-sql/database/single-database-create-arm-template-quickstart?view=azuresql

        //Consider adding 00 to end of service names to iterate, but since template for everyone would it help?

        //dev then give access to resource groups? instead of role assignment to managed identity
        //https://learn.microsoft.com/en-us/azure/role-based-access-control/role-assignments-template

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