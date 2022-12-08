using System.Diagnostics;
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
        var services = new HashSet<string>();
        var configs = new Dictionary<string, string>();

        try
        {
            var filepath = args[0];
            var env = args[1];

            //check args, i.e. is env of expected value, is filePath openable etc.

            // Attempt to open input file.
            using var reader = new StreamReader(filepath);
            var csprojData = await reader.ReadToEndAsync();

            var projName = Path.GetFileName(args[0]).Replace(".csproj", "") + "Auto";

            Console.WriteLine($"ProjectName: {projName}");

            //Should be able to move a lot of this to a const dictionary
            if (csprojData.Contains("<Project Sdk=\"Microsoft.NET.Sdk.Web\">"))
            {
                services.Add("WebApp");
                configs.Add("__WEBAPPNAME__", projName);

                configs.Add("__WEBAPPSKU__", env == "dev" ? "B1" : "S1");

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
        }
        catch (IOException e)
        {
            TextWriter errorWriter = Console.Error;
            errorWriter.WriteLine(e.Message);
            return 1;
        }

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
        Console.WriteLine($"Configs: {string.Join(" ",configs)}");
        Console.WriteLine($"Services: {string.Join(" ",services)}");
        Console.WriteLine($"Completed Parsing");

        Console.WriteLine($"Creating the Resource Group");
        var resourceGroups = subscription.GetResourceGroups();

        // With the collection, we can create a new resource group with an specific name
        var resourceGroupName = "GlobalRsGrp";
        var location = AzureLocation.CentralUS;
        var resourceGroupData = new ResourceGroupData(location);
        var operation = await resourceGroups.CreateOrUpdateAsync(WaitUntil.Completed, resourceGroupName, resourceGroupData);
        var resourceGroup = operation.Value;

        //var resourceGroup = client.GetResourceGroupResource(new Azure.Core.ResourceIdentifier("/subscriptions/<Subscriptionid>/resourceGroups/<resourcegroupname>"));
        var ArmDeploymentCollection = resourceGroup.GetArmDeployments();
        var deploymentName = Guid.NewGuid().ToString();

        var globalTemplate = File.ReadAllText($"Data/Global.json");
        var globalParameters = File.ReadAllText($"Data/Global.Parameters.json");
        var regionalTemplate = File.ReadAllText($"Data/Regional.json");
        var regionalParameters = File.ReadAllText($"Data/Regional.Parameters.json");


        //Global Deploy

        globalParameters = TransformParamters(globalParameters, configs);

        Console.WriteLine($"Deploying the Global Resources. This may take a while.");
        var input = new ArmDeploymentContent(new ArmDeploymentProperties(ArmDeploymentMode.Incremental)
        {
            Template = BinaryData.FromString(globalTemplate),
            Parameters = BinaryData.FromString(globalParameters)
        });
        var lro = await ArmDeploymentCollection.CreateOrUpdateAsync(WaitUntil.Started, deploymentName, input);
        await lro.UpdateStatusAsync();

        var sw = new Stopwatch();
        sw.Start();

        while (lro.HasCompleted == false)
        {
            await Task.Delay(10000);

            Console.WriteLine($"Still Deploying Global Resources. Checking again in 10 seconds. Total elapsed time: " + sw.Elapsed.Minutes + "mins");
            await lro.UpdateStatusAsync();
        }

        sw.Stop();

        //Regional Deploys
        //Link Deploy

        Console.WriteLine($"Completed Provisioning");
        return 0;
    }

    private static string TransformParamters(string parameters, Dictionary<string, string> configs)
    {
        foreach (var config in configs)
        {
            parameters = parameters.Replace(config.Key, config.Value);
        }

        return parameters;
    }
}