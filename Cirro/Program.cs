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
using Cirro.Shared;
using Cirro.Synthesizers;
using Microsoft.Graph;

namespace Cirro;
public class Parser
{
    public static async Task<int> Main(string[] args)
    {
        var flags = CommandLine.Parser.Default.ParseArguments<ArgumentFlags>(args);

        var cloud = flags.Value.Cloud.ToLower();
        var projectName = flags.Value.ProjectName;
        var env = flags.Value.Enviornment.ToLower();
        var framework = flags.Value.Framework?.ToLower();
        var subId = flags.Value.SubscriptionId;

        if (projectName.Length > 9 || projectName.Length < 5)
        {
            Console.WriteLine("The name to prefix the infrastrucutre needs to be between 5-10 character. Please see doc {insert doc link}");
            return 1;
        }

        if (string.IsNullOrEmpty(framework))
        {
            framework = cloud == Constants.Azure ? Constants.DotNet :
            cloud == Constants.AWS ? Constants.Java :
            cloud == Constants.GCP ? Constants.Go : string.Empty;

            Console.WriteLine($"Setting default framework to use {framework}. To use a different framework, provide it with -f. See doc link");
        }

        switch (cloud)
        {
            case Constants.Azure:
                switch (framework)
                {
                    case Constants.DotNet:
                        return await AzureDotNet.Synthesize(projectName, env, subId);
                    default:
                        Console.WriteLine($"{framework} is not yet supported for {Constants.Azure}. Supported frameworks are: {string.Join(", ", Supported.Azure.Frameworks)}. See doc for more details: link");
                        return 1;
                }

            case Constants.AWS:
                switch (framework)
                {
                    case Constants.Java:
                        return await AWSJava.Synthesize(projectName, env, subId);
                    default:
                        Console.WriteLine($"{framework} is not yet supported for {Constants.AWS}. Supported frameworks are: {string.Join(", ", Supported.AWS.Frameworks)}. See doc for more details: link");
                        return 1;
                }

            case Constants.GCP:
                switch (framework)
                {
                    case Constants.Go:
                        return await GCPGo.Synthesize(projectName, env, subId);
                    default:
                        Console.WriteLine($"{framework} is not yet supported for {Constants.GCP}. Supported frameworks are: {string.Join(", ", Supported.GCP.Frameworks)}. See doc for more details: link");
                        return 1;
                }

            default:
                Console.WriteLine($"{cloud} is not an acceptable value for cloud. Supported clouds are: {string.Join(", ", Supported.Clouds)}");
                return 1;
        }
    }
}