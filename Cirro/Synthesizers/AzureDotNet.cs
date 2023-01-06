using System;
using System.Text;
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
        var credential = new AzureCliCredential();

        Console.WriteLine($"projectName: {projectName}");

        await Interpret(env, credential, configs, services);
        await AzureProvisioner.Provision(projectName, env, subId, credential, configs, services);
        await Connect(env, configs, services);

        return 0;
    }

    public static async Task<int> Interpret(string env, AzureCliCredential credential, Dictionary<string, string> configs, HashSet<string> services)
    {

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
        }


        foreach (var sdkToService in AzureServices.SdkToServices)
        {
            if (csprojData.Contains(sdkToService.Key))
            {
                services.Add(sdkToService.Value);
            }
        }

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

        if (services.Contains(AzureServices.WebApp) || services.Contains(AzureServices.FunctionApp))
        {
            var regex = new Regex("<TargetFramework>(.*)</TargetFramework>");   //since combining csproj, could have multiple target frameworks here
            var v = regex.Match(csprojData);
            var s = v.Groups[1].ToString();

            configs.Add("__LINUXVERSION__", "DOTNETCORE|" + s.Replace("net", ""));
        }

        configs.Add("\"__SERVICES__\"", "[\"" + string.Join("\",\"", services) + "\"]");

        Console.WriteLine("Completed Interpretting");
        return 0;
    }

    public static async Task<int> Connect(string env, Dictionary<string, string> configs, HashSet<string> services)
    {

        var appsettingFile = System.IO.Directory.GetFiles(Environment.CurrentDirectory, "appsettings.json", SearchOption.AllDirectories).FirstOrDefault();
        var programFile = System.IO.Directory.GetFiles(Environment.CurrentDirectory, "Program.cs", SearchOption.AllDirectories).FirstOrDefault();

        if (string.IsNullOrEmpty(appsettingFile) || string.IsNullOrEmpty(programFile))
        {
            Console.WriteLine($"No appsettings.json and/or Program.cs file is found within this project. Cannot automatically connect resources.");
            return 1;
        }

        var cirroAppsettingsFile = appsettingFile.Replace("appsettings", "appsettings.cirro");
        var gitIgnoreFile = appsettingFile.Replace("appsettings.json", ".gitignore");
        var programFileOld = appsettingFile.Replace("Program.cs", "Program.old.cs");


        if (System.IO.File.Exists(cirroAppsettingsFile) == false)
        {
            System.IO.File.Create(cirroAppsettingsFile);
        }
        if (System.IO.File.Exists(gitIgnoreFile) == false)
        {
            System.IO.File.Create(gitIgnoreFile);
        }

        if (System.IO.File.Exists(programFileOld) == false)
        {
            System.IO.File.Copy(programFile, programFileOld);
        }

        using var appsettingsWriter = new StreamWriter(cirroAppsettingsFile);
        await appsettingsWriter.WriteLineAsync("{");
        await appsettingsWriter.WriteLineAsync($"\"KV_Endpoint\": \"{configs["__KEYVAULTNAME__"]}\"");
        await appsettingsWriter.WriteLineAsync("}");

        using var gitIgnoreWriter = new StreamWriter(gitIgnoreFile);
        await gitIgnoreWriter.WriteLineAsync("appsettings.cirro.json");


        //Connet step
        /*
         * Shouldn't use any specific resource names, should use config and services only to connect using configuration builder
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