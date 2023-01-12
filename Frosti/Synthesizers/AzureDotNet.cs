using System;
using Azure.Identity;
using Frosti.Interpreters;
using Frosti.Connectors;
using Frosti.Provisioners;

namespace Frosti.Synthesizers;
public static class AzureDotNet
{
    public static async Task<int> Synthesize(string projectName, string env, string? subId, bool autoConnect, string primaryLocation)
    {
        var services = new HashSet<string>() { AzureServices.ManagedIdentity, AzureServices.KeyVault };
        var configs = new Dictionary<string, string>();
        var credential = new AzureCliCredential();

        await AzureDotnetInterpreter.Interpret(env, credential, configs, services);
        var provisioned = await AzureProvisioner.Provision(projectName, env, subId, credential, configs, services, primaryLocation);
        if (provisioned && autoConnect)
        {
            await AzureDotnetConnector.Connect(env, configs, services);
        }

        return 0;
    }
}