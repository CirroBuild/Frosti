using System;
using Azure.Identity;
using Frosti.Interpreters;
using Frosti.Connectors;
using Frosti.Provisioners;

namespace Frosti.Synthesizers;
public static class AzureDjango
{
    public static async Task<int> Synthesize(HttpClient httpClient, string projectName, string env, string? subId, bool autoConnect, string primaryLocation, bool optOut)
    {
        var services = new HashSet<string>() { AzureServices.ManagedIdentity, AzureServices.KeyVault };
        var configs = new Dictionary<string, string>();
        var credential = new AzureCliCredential();

        try
        {
            configs.Add("__CUSTOM_NAME__", projectName);
            configs.Add("__FRAMEWORK__", "django");
            await AzureDjangoInterpreter.Interpret(env, credential, configs, services);
        }
        catch
        {
            return 1;
        }


        var provisioned = await AzureProvisioner.Provision(projectName, env, subId, credential, configs, services, primaryLocation);
        if (provisioned && env.Equals(Environments.Dev))
        {
            await AzureDjangoConnector.Connect(httpClient, configs, services, env, optOut);
        }

        return 0;
    }
}