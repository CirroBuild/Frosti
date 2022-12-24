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
using Cirro.Synthesizers;
using Microsoft.Graph;

namespace Cirro;
public class Parser
{
    public static async Task<int> Main(string[] args)
    {
        var cloud = args.Length > 0 ? args[0] : "";
        if (Supported.Clouds.Contains(cloud) == false)
        {
            Console.WriteLine($"Please specify the cloud to create the infrastructure. Select from: {string.Join(", ", Supported.Clouds)}");
            return 1;
        }

        if (args.Length < 2 || args[1].Length > 9 || args[1].Length < 5)
        {
            Console.WriteLine("The Infra Prefix is missing or isn't between 5-10 character. Please see doc {insert doc link}");
            return 1;
        }
        var projectName = args[1];

        var env = args.Length > 2 ? args[2] : "";
        if (Supported.Enviornments.Contains(env) == false)
        {
            Console.WriteLine($"Enviornment is missing or {env} is not a supported enviornment. Please select from: {string.Join(", ", Supported.Enviornments)}");
            return 1;
        }

        var subId = args.Length > 3 ? args[3] : null;

        switch (cloud)
        {
            case "azure":
                return await AzureDotNet.Synthesize(projectName, env, subId);
            case "aws":
                return ProvisionAWS(projectName, env);
            case "gcp":
                return ProvisionGCP(projectName, env);
        }

        return 1;
    }

    private static int ProvisionAWS(string projectName, string env)
    {
        Console.WriteLine("Automatically provisioning AWS resources is not currently supported. It's coming soon! Please try provisioning via azure for now.");
        return 0;
    }

    private static int ProvisionGCP(string projectName, string env)
    {
        Console.WriteLine("Automatically provisioning GCP resources is not currently supported. It's coming soon! Please try provisioning via azure for now.");
        return 0;
    }
}