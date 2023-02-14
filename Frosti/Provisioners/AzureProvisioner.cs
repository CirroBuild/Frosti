using System;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using System.Text.RegularExpressions;
using Azure;
using Azure.Core;
using Azure.ResourceManager.Resources.Models;
using System.Diagnostics;
using System.Reflection;

namespace Frosti.Provisioners;
public static class AzureProvisioner
{
    public static async Task<bool> Provision(string projectName, string env, string? subId, AzureCliCredential credential, Dictionary<string, string> configs, HashSet<string> services, string primaryLocation)
    {

        SubscriptionResource? subscription;
        var client = new ArmClient(credential);
        //var email = client.name
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
                }
            }
        }

        Console.WriteLine($"Using subscription: {subscription.Data.SubscriptionId}");
        List<string> ignoreServices = new() { AzureServices.DevUser, AzureServices.ManagedIdentity, AzureServices.KeyVault };
        Console.WriteLine($"Services to be provisioned: Managed Identity (required), Keyvault (required), {string.Join(", ", services.Where(x => ignoreServices.Contains(x) == false))}");

        if (env == Environments.Dev)
        {
            Console.WriteLine("Is this correct? (Y/n)");
            string? userinput = Console.ReadLine();

            if (userinput?.Trim().ToLower() != "y" && userinput?.Trim().ToLower() != "yes")
            {
                Console.WriteLine("Exiting. Nothing has been provisioned. Please check the SDKs used and remove anything not needed.");
                return false;
            }
        }

        var resourceGroups = subscription.GetResourceGroups();

        var envSuffix = env == Environments.Dev ? configs["USERNAME"].Substring(0,4).ToLower() : env;
        var rgPrefix = $"{projectName}-{envSuffix}";
        var uniqueString = Constants.GetUniqueString(subscription.Data.SubscriptionId, rgPrefix);

        if (services.Contains(AzureServices.WebApp) || services.Contains(AzureServices.FunctionApp))
        {
            configs.Add("__WEBPLANNAME__", $"{projectName}-WebPlan-{uniqueString}".Substring(0, 40));
            configs.Add("__FUNCTIONPLANNAME__", $"{projectName}-FunctionPlan-{uniqueString}".Substring(0, 40));
        }

        if (services.Contains(AzureServices.SQL) || services.Contains(AzureServices.MySql) || services.Contains(AzureServices.PostgreSQL))
        {
            configs.Add("__SQLPASSWORD__", Guid.NewGuid().ToString());
        }

        var primaryRegionResourceGroupName = $"{rgPrefix}-Primary";
        configs.Add("__PRIMARYRGNAME__", primaryRegionResourceGroupName);

        //Define regions
        var regions = AzureLocations.Transformer[primaryLocation].Take(Locations.EnvLocationCount[env]);
        var primaryRegion = regions.First();

        ResourceGroupResource? primaryResourceGroup;

        //Primary Deploy
        Console.WriteLine($"Deploying the Global Resources. This may take a while.");
        var operation = await resourceGroups.CreateOrUpdateAsync(WaitUntil.Completed, primaryRegionResourceGroupName, new ResourceGroupData(primaryRegion));

        try
        {
            primaryResourceGroup = operation.Value;
        }
        catch
        {
            Console.WriteLine("It seems this resource group already exists in a different region. Nothing has been provisioned. Please try again with a different project name");
            return false;
        }

        var ArmDeploymentCollection = primaryResourceGroup.GetArmDeployments();
        var primaryServiceNames = new AzureServiceNames(projectName, envSuffix, uniqueString, primaryRegion);
        await DeployARM(ArmDeploymentCollection, primaryServiceNames, configs, services, env, "Primary");


        //Regional Deploys
        //parrallelize in the future
        foreach (var region in regions)
        {
            Console.WriteLine($"Deploying the {region} Resources. This may take a while.");
            operation = await resourceGroups.CreateOrUpdateAsync(WaitUntil.Completed, $"{rgPrefix}-{region}", new ResourceGroupData(region));
            var regionalResourceGroup = operation.Value;
            ArmDeploymentCollection = regionalResourceGroup.GetArmDeployments();

            var regionalServiceNames = new AzureServiceNames(projectName, envSuffix, uniqueString, region);
            await DeployARM(ArmDeploymentCollection, regionalServiceNames, configs, services, env, "Regional");

            //Link resources. Regional cuz KV is regional
            //SSL Cert creation https://github.com/Azure/azure-quickstart-templates/tree/master/application-workloads/umbraco/umbraco-webapp-simple
            Console.WriteLine($"Linking the {region} Resources. This may take a while.");
            await DeployARM(ArmDeploymentCollection, regionalServiceNames, configs, services, env, "Link");

            if (env == Environments.Dev)
            {
                configs.Add(regionalServiceNames.ServiceNameMap[AzureServices.KeyVault].Key, regionalServiceNames.ServiceNameMap[AzureServices.KeyVault].Value);
            }
        }

        /*
        if (env == Environments.P1)
        {
            //Do Failover Region here
        }
        */

        return true;
    }

    private static async Task DeployARM(ArmDeploymentCollection ArmDeploymentCollection, AzureServiceNames servceNames, Dictionary<string, string> configs, HashSet<string> services, string env, string templateName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var templateFile = $"Frosti.Data.Azure.{env}.{templateName}.json";
        var parameterFile = $"Frosti.Data.Azure.{env}.{templateName}.Parameters.json";

        using var templateStream = new StreamReader(assembly.GetManifestResourceStream(templateFile));
        using var paramteterStream = new StreamReader(assembly.GetManifestResourceStream(parameterFile));

        var template = await templateStream.ReadToEndAsync();
        var parameters = await paramteterStream.ReadToEndAsync();

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

                //make generic just use appname, set app_type in config and change yml file to easier logic
                if (templateName == "Regional" && serviceName.Key == "__WEBAPPNAME__")
                {
                    Console.WriteLine($"WebAppName:{serviceName.Value}");
                }

                if (templateName == "Regional" && serviceName.Key == "__FUNCTIONAPPNAME__")
                {
                    Console.WriteLine($"FunctionAppName:{serviceName.Value}");
                }
            }
        }

        //DEBUG
        /*
        Console.WriteLine(string.Join(",", parameters));
        return;
        */

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