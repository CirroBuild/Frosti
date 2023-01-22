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
using Frosti.Shared;
using Frosti.Synthesizers;
using Microsoft.Graph;

namespace Frosti;
public class Parser
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || (args.Length > 0 && args[0] != "provision"))
        {
            Console.WriteLine("Something doesn't seem right. Did you mean to run the command `frosti provision`?");
            return 1;
        }

        var flags = CommandLine.Parser.Default.ParseArguments<ArgumentFlags>(args);

        var cloud = Clouds.Azure; //flags.Value.Cloud.ToLower();
        var projectName = flags.Value.ProjectName;
        var env = flags.Value.Enviornment.ToLower();
        var framework = Frameworks.DotNet; //flags.Value.Framework?.ToLower();
        var subId = flags.Value.SubscriptionId;
        var autoConnect = flags.Value.AutoConnect;
        var primaryLocation = Locations.NorthAmerica; //flags.Value.Location;
        //for values above, check if they are one of expected values

        if (string.IsNullOrWhiteSpace(projectName) == false && (projectName.Length > 10 || projectName.Length < 5))
        {
            Console.WriteLine("The name to prefix the infrastrucutre needs to be between 5-10 character. Please see doc {insert doc link}");
            return 1;
        }

        if (Environments.Supported.Contains(env) == false)
        {
            Console.WriteLine($"{env} is not supported. Supported envioronments are: {string.Join(", ", Environments.Supported)}");
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

        switch (cloud)
        {
            case Clouds.Azure:
                switch (framework)
                {
                    case Frameworks.DotNet:
                        return await AzureDotNet.Synthesize(projectName, env, subId, autoConnect, primaryLocation);
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
}