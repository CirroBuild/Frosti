using System;
using Azure.Identity;
using Frosti.Interpreters;
using Frosti.Connectors;
using Frosti.Provisioners;

namespace Frosti.Synthesizers;
public static class AzureDotNet
{
    public static async Task<int> Synthesize(HttpClient httpClient, string projectName, string env, string? subId, bool autoConnect, string primaryLocation, bool optOut)
    {
        var services = new HashSet<string>() { AzureServices.ManagedIdentity, AzureServices.KeyVault };
        var configs = new Dictionary<string, string>();
        var credential = new AzureCliCredential();

        try
        {
            var csProjName = await AzureDotnetInterpreter.Interpret(env, credential, configs, services);
            projectName = string.IsNullOrEmpty(projectName) ? csProjName.Substring(0, 8) : projectName;
        }
        catch
        {
            return 1;
        }


        var provisioned = await AzureProvisioner.Provision(projectName, env, subId, credential, configs, services, primaryLocation);
        if (provisioned)
        {
            if (env == Environments.Dev)
            {
                await AzureDotnetConnector.Connect(httpClient, configs, services, env, optOut);
            }
        }

        return 0;
    }
}