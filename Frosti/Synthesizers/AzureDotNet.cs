using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Frosti.Provisioners;
using Microsoft.Graph;

namespace Frosti.Synthesizers;
public static class AzureDotNet
{
    public static async Task<int> Synthesize(string projectName, string env, string? subId)
    {
        var services = new HashSet<string>() { AzureServices.ManagedIdentity, AzureServices.KeyVault };
        var configs = new Dictionary<string, string>();
        var credential = new AzureCliCredential();

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

        var gitIgnoreFile = appsettingFile.Replace("appsettings.json", ".gitignore");
        var FrostiServicesFile = appsettingFile.Replace("appsettings.json", ".Frostiservices");
        var localKvFile = appsettingFile.Replace("appsettings.json", ".Frostilocalkv");
        var programFileOld = programFile.Replace("Program.cs", "Program.Save.cs");

        if (System.IO.File.Exists(programFileOld) == false)
        {
            System.IO.File.Copy(programFile, programFileOld);
        }

        //TEST ONLY. DELETE
        configs.Add("__KEYVAULTNAME__", "https://aperitest-dev-cus-kv0f0f.vault.azure.net/");

        var FrostiAppsettingsFile = appsettingFile.Replace("appsettings", "appsettings.Frosti");

        if (System.IO.File.Exists(FrostiAppsettingsFile) == false)
        {
            System.IO.File.AppendAllLines(gitIgnoreFile, new string[]
            {
                "appsettings.Frosti.json",
                ".Frostiservices",
                ".Frostilocalkv",
                "Program.Save.cs"
            });
        }

        if (System.IO.File.Exists(localKvFile) == false || System.IO.File.ReadAllText(localKvFile) != configs["__KEYVAULTNAME__"])
        {
            System.IO.File.WriteAllLines(FrostiAppsettingsFile, new string[] { "{", $"\t\"KV_Endpoint\": \"{configs["__KEYVAULTNAME__"]}\"", "}" });
        }


        //still need to look into delete service connections if they got removed
        var newServices = services.Where(x=> System.IO.File.ReadAllLines(FrostiServicesFile).Contains(x) == false);
        if (newServices.Count() != 0)
        {
            Console.WriteLine($"Connecting {string.Join(", ", newServices)}");
            var currentProgram = System.IO.File.ReadAllLines(programFile);
            var usings = AzureServices.SdkToServices.Where(x => newServices.Contains(x.Value)).Select(x => $"using {x.Key};");
            var connections = AzureProgramConnections.ServiceToConnectionCode.Where(x => newServices.Contains(x.Key)).SelectMany(x => x.Value);

            var counter = 0;
            string? line;

            using var programFileReader = new StreamReader(programFile);
            while ((line = await programFileReader.ReadLineAsync()) != null)
            {
                counter++;
                if (line.Contains("var builder"))
                {
                    break;
                }
            }

            var finalProgram = usings.Concat(currentProgram.Take(counter)).Concat(connections).Concat(currentProgram.Skip(counter)); //need to fix union
            System.IO.File.WriteAllLines(programFile, finalProgram);
            System.IO.File.AppendAllLines(FrostiServicesFile, newServices);
        }

        Console.WriteLine("Completed Connecting");
        return 0;
    }
}