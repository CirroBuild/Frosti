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

public class Parser
{
    private static class SupportedServices
    {
        public static readonly string WebApp = "WebApp";
        public static readonly string FunctionApp = "FunctionApp";
        public static readonly string Storage = "Storage";
        public static readonly string ServiceBus = "ServiceBus";
        public static readonly string EventHubs = "EventHubs";
        public static readonly string ApplicationInsights = "ApplicationInsights";
        public static readonly string Cosmos = "Cosmos";
        public static readonly string Redis = "Redis";
        public static readonly string SQL = "SQL";
        public static readonly string MySql = "MySql";
        public static readonly string PostgreSQL = "PostgreSQL";
        public static readonly string KeyVault = "KeyVault";
    };

    private static readonly Dictionary<string, string> SdkToServices = new Dictionary<string, string>()
    {
        {"<Project Sdk=\"Microsoft.NET.Sdk.Web\">", SupportedServices.WebApp},
        {"Microsoft.NET.Sdk.Functions", SupportedServices.FunctionApp},
        {"Azure.Storage", SupportedServices.Storage},                               //Blobs, Queues, Files, DataLake (seperate sdks exist. Needed?)
        {"Azure.Security.KeyVault", SupportedServices.KeyVault},
        {"Azure.Messaging.ServiceBus", SupportedServices.ServiceBus},
        {"Azure.Messaging.EventHubs", SupportedServices.EventHubs},
        {"Microsoft.ApplicationInsights", SupportedServices.ApplicationInsights},
        {"Microsoft.Azure.Cosmos", SupportedServices.Cosmos},
        {"StackExchange.Redis", SupportedServices.Redis},
        {"Microsoft.Data.SqlClient", SupportedServices.SQL},                        //Managed Instance for premium? https://learn.microsoft.com/en-us/azure/azure-sql/managed-instance/create-template-quickstart?view=azuresql&tabs=azure-powershell
        {"MySql.Data", SupportedServices.MySql},                                    //Flexible server for premium? https://learn.microsoft.com/en-us/azure/templates/microsoft.dbformysql/flexibleservers?pivots=deployment-language-arm-template
        {"Npgsql", SupportedServices.PostgreSQL}
        //Figure out MariaDb
    };

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

        foreach (var sdkToService in SdkToServices)
        {
            if (csprojData.Contains(sdkToService.Key))
            {
                services.Add(sdkToService.Value);
            }
        }

        //Should be able to move a lot of this to a const dictionary
        if (services.Contains(SupportedServices.WebApp))
        {
            configs.Add("__WEBAPPSKU__", env == "dev" ? "F1" : "S1");

        }
        if (services.Contains(SupportedServices.FunctionApp))   //need to support multiple csprojs for this
        {
            configs.Add("__FUNCTIONAPPSKU__", env == "dev" ? "Y1" : "EP1");
        }

        if (services.Contains(SupportedServices.WebApp) || services.Contains(SupportedServices.FunctionApp))
        {
            var regex = new Regex("<TargetFramework>(.*)</TargetFramework>");
            var v = regex.Match(csprojData);
            var s = v.Groups[1].ToString();

            configs.Add("__LINUXVERSION__", "DOTNETCORE|" + s.Replace("net", ""));
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
                services.Add("DevUser");
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
        Console.WriteLine($"Services to be provisioned: {string.Join(", ",services)}");

        Console.WriteLine("Is this correct? (Y/n)");
        string userinput = Console.ReadLine();

        if (userinput.Trim().ToLower() != "y" && userinput.Trim().ToLower() != "yes")
        {
            Console.WriteLine("Exiting. Nothing has been provisioned.");
            return 0;
        }

        var globalTemplate = System.IO.File.ReadAllText($"Data/Global.json");
        var globalParameters = System.IO.File.ReadAllText($"Data/Global.Parameters.json");
        var regionalTemplate = System.IO.File.ReadAllText($"Data/Regional.json");
        var regionalParameters = System.IO.File.ReadAllText($"Data/Regional.Parameters.json");
        var linkTemplate = System.IO.File.ReadAllText($"Data/Link.json");
        var linkParameters = System.IO.File.ReadAllText($"Data/Link.Parameters.json");

        var resourceGroups = subscription.GetResourceGroups();

        //Global Deploy
        Console.WriteLine($"Creating the Global Resource Group");
        var operation = await resourceGroups.CreateOrUpdateAsync(WaitUntil.Completed, $"{projName}-Global", new ResourceGroupData(AzureLocation.CentralUS));
        var globalResourceGroup = operation.Value;
        var ArmDeploymentCollection = globalResourceGroup.GetArmDeployments();

        //await Provision(ArmDeploymentCollection, configs, globalTemplate, globalParameters, "Global");

        //Regional Deploys
        Console.WriteLine($"Creating the {AzureLocation.CentralUS} Resource Group");
        operation = await resourceGroups.CreateOrUpdateAsync(WaitUntil.Completed, $"{projName}-{AzureLocation.CentralUS}", new ResourceGroupData(AzureLocation.CentralUS));
        var regionalResourceGroup = operation.Value;
        ArmDeploymentCollection = regionalResourceGroup.GetArmDeployments();

        //await Provision(ArmDeploymentCollection, configs, regionalTemplate, regionalParameters, AzureLocation.CentralUS.ToString());

        //Link resources
        ArmDeploymentCollection = regionalResourceGroup.GetArmDeployments();
        await Provision(ArmDeploymentCollection, configs, linkTemplate, linkParameters, "Linking");


        //Add appsettings.dev etc. here
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