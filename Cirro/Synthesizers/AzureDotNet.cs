using System;
using System.Text.RegularExpressions;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Cirro.Provisioners;
using Microsoft.Graph;

namespace Cirro.Synthesizers;
public static class AzureDotNet
{
    public static async Task<int> Synthesize(string projectName, string env, string? subId)
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

        Console.WriteLine($"projectName: {projectName}");

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

        if (services.Contains(AzureServices.WebApp) || services.Contains(AzureServices.FunctionApp))
        {
            var regex = new Regex("<TargetFramework>(.*)</TargetFramework>");   //since combining csproj, could have multiple target frameworks here
            var v = regex.Match(csprojData);
            var s = v.Groups[1].ToString();

            configs.Add("__LINUXVERSION__", "DOTNETCORE|" + s.Replace("net", ""));
        }

        Console.WriteLine("Completed Interpretting");

        await AzureProvisioner.Provision(projectName, env, subscription, configs, services);

        //Connet step
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


        //SQL server creation looks at a server login. Considering quering that via questions to terminal
        //https://learn.microsoft.com/en-us/azure/azure-sql/database/single-database-create-arm-template-quickstart?view=azuresql

        return 0;
    }
}