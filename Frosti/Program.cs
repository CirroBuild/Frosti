using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Web;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using Frosti.Shared;
using Frosti.Synthesizers;
using Microsoft.Graph;
using Microsoft.Identity.Client.Platforms.Features.DesktopOs.Kerberos;
using Microsoft.IdentityModel.Abstractions;

namespace Frosti;
public class Parser
{
    private static HttpClient httpClient = new HttpClient();

    public static async Task<int> Main(string[] args)
    {
        try
        {
            return await Parse(args);
        }
        catch(Exception e)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine("Error: " + e.Message);
            if (string.IsNullOrEmpty(e.InnerException?.Message) == false)
            {
                Console.WriteLine("Error: " + e.InnerException.Message);
            }
            Console.WriteLine("Error: Something went wrong. Please try again.\n");
            Console.ResetColor();

            await httpClient.GetAsync(
            $"https://frostifu-ppe-eus-functionappc1ed.azurewebsites.net/api/LogException?user={Dns.GetHostName()}&exception={e}&innerMesssage={e.InnerException}",
            new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token);

            return 1;
        }
    }

    public static async Task<int> Parse(string[] args)
    {

        if (args.Length > 0 && (args[0] == "-v" || args[0] == "--version"))
        {
            Console.WriteLine("v4..preview");
            return 0;
        }

        if (args.Length > 0 && args[0] == "signup" && args[1] == "beta")
        {
            var credential = new AzureCliCredential();
            var graphClient = new GraphServiceClient(credential);
                var user = await graphClient.Me
                    .Request()
                    .GetAsync();

            var response = await httpClient.GetStringAsync($"https://frostifu-ppe-eus-functionappc1ed.azurewebsites.net/api/IsBetaUser?id={user.Id}");
            var isBetaUser = bool.Parse(response);
            if (isBetaUser)
            {
                Console.WriteLine("You are already a beta user. Thanks for signing up!");
                return 0;
            }


            var betaUrl = $"https://www.frostibuild.com/checkout?oid={user.Id}";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                System.Diagnostics.Process.Start(new ProcessStartInfo("cmd", $"/c start {betaUrl}"));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                System.Diagnostics.Process.Start("open", betaUrl);
            }

            Console.WriteLine("Taking you to the signup page");
            return 0;
        }

        var flags = CommandLine.Parser.Default.ParseArguments<ArgumentFlags>(args);


        if (args.Length > 0 && (args[0] == "-h" || args[0] == "--help"))
        {
            return 0;
        }

        var optOut = flags.Value.OptOut;
        var env = flags.Value.Enviornment.ToLower();
        var cloud = Clouds.Azure; //flags.Value.Cloud.ToLower();
        var projectName = flags.Value.ProjectName;
        var framework = Frameworks.DotNet; //flags.Value.Framework?.ToLower();
        var subId = flags.Value.SubscriptionId;
        var autoConnect = flags.Value.AutoConnect;
        var primaryLocation = Locations.NorthAmerica; //flags.Value.Location;
        var runOn = flags.Value.RunOn;
        var beta = flags.Value.Beta;
        //for values above, check if they are one of expected values

        if (args.Length == 0 || (args.Length > 0 && args[0] != "provision"))
        {
            Console.WriteLine("Something doesn't seem right. Did you mean to run the command `frosti provision`?");
            return 1;
        }

        Console.WriteLine("Frosti is looking at what resources you need");

        if (Environments.Supported.Contains(env) == false)
        {
            Console.WriteLine($"{env} is not supported. Supported envioronments are: {string.Join(", ", Environments.Supported)}");
            return 1;
        }
        else if (env != Environments.Dev && runOn == RunOnOpts.Local)
        {
            Console.WriteLine($"{env} cannot be run on your local enviornment. Please use github for governance compliance.");
            return 1;
        }

        //check user authenticated to deploy ppe/prod here

        if (string.IsNullOrEmpty(framework))
        {
            framework =
                cloud == Clouds.Azure ? Frameworks.DotNet :
                cloud == Clouds.AWS ? Frameworks.Java :
                cloud == Clouds.GCP ? Frameworks.Go : string.Empty;

            Console.WriteLine($"Setting default framework to use {framework}. To use a different framework, provide it with -f. See doc link");
        }

        if (HasPyFiles())
        {
            framework = Frameworks.Django;

            if (env.Equals(Environments.Dev))
            {
                var projNameFile = System.IO.Directory.GetFiles(Environment.CurrentDirectory, ".frosti.projName", SearchOption.AllDirectories).FirstOrDefault();

                if (projNameFile == null)
                {
                    //only ask in dev, in ppe throw error that .frosti.projName is missing
                    Console.WriteLine($"What do you want to name the project? (5-10 characters)");
                    string? userinput = Console.ReadLine();
                    projectName = userinput.Replace(" ", "");
                }
                else
                {
                    projectName = System.IO.File.ReadAllText(projNameFile);
                }

            }
        }

        if (string.IsNullOrWhiteSpace(projectName) == false && (projectName.Length > 10 || projectName.Length < 5))
        {
            Console.WriteLine("The name to prefix the infrastrucutre needs to be between 5-10 character. Please see doc {insert doc link}");
            return 1;
        }


        switch (cloud)
        {
            case Clouds.Azure:
                switch (framework)
                {
                    case Frameworks.DotNet:
                        return await AzureDotNet.Synthesize(httpClient, projectName, env, subId, autoConnect, primaryLocation, optOut);
                    case Frameworks.Django:
                        return await AzureDjango.Synthesize(httpClient, projectName, env, subId, autoConnect, primaryLocation, optOut);
                    default:
                        Console.WriteLine($"{framework} is not yet supported for {Frameworks.AzureSupported}. Supported frameworks are: {string.Join(", ", Frameworks.AzureSupported)}. See doc for more details: link");
                        return 1;
                }

            case Clouds.AWS:
                switch (framework)
                {
                    case Frameworks.Java:
                        return await AWSJava.Synthesize(projectName, env, subId);
                    default:
                        Console.WriteLine($"{framework} is not yet supported for {Frameworks.AWSSupported}. Supported frameworks are: {string.Join(", ", Frameworks.AWSSupported)}. See doc for more details: link");
                        return 1;
                }

            case Clouds.GCP:
                switch (framework)
                {
                    case Frameworks.Go:
                        return await GCPGo.Synthesize(projectName, env, subId);
                    default:
                        Console.WriteLine($"{framework} is not yet supported for {Frameworks.GCPSupported}. Supported frameworks are: {string.Join(", ", Frameworks.GCPSupported)}. See doc for more details: link");
                        return 1;
                }

            default:
                Console.WriteLine($"{cloud} is not an acceptable value for cloud. Supported clouds are: {string.Join(", ", Clouds.Supported)}");
                return 1;
        }
    }

    static bool HasPyFiles()
    {
        var pyFiles = System.IO.Directory.GetFiles(Environment.CurrentDirectory, "*.py", SearchOption.AllDirectories);
        return pyFiles.Count() > 0;
    }
}