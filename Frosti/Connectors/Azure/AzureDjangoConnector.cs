using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Web;
using Azure.Identity;
using Microsoft.Graph;
using Newtonsoft.Json.Linq;
using static Microsoft.Graph.Constants;

namespace Frosti.Connectors;

	public class AzureDjangoConnector
{
    public static async Task Connect(HttpClient httpClient, Dictionary<string, string> configs, HashSet<string> services, string env, bool optOut)
    {
        var pyFile = System.IO.Directory.GetFiles(Environment.CurrentDirectory, "manage.py", SearchOption.AllDirectories).FirstOrDefault();
        var appSettingsPrefix = "manage.py";
        var kvUrl = $"https://{configs["__KEYVAULTNAME__"]}.vault.azure.net/";

        var gitIgnoreFile = pyFile.Replace(appSettingsPrefix, ".gitignore");
        var frostiAppSettingsFile = pyFile.Replace(appSettingsPrefix, ".env");

        if (System.IO.File.Exists(frostiAppSettingsFile) == false)
        {
            System.IO.File.AppendAllLines(gitIgnoreFile, new string[]
            {
                ".env"
            });
        }

        System.IO.File.WriteAllLines(frostiAppSettingsFile, new string[] {$"KV_ENDPOINT=\"{kvUrl}\""});

        var frostiDelta = pyFile.Replace(appSettingsPrefix, "frosti.delta");
        System.IO.File.WriteAllText(frostiDelta, string.Join(", ", services));

        var projName = pyFile.Replace(appSettingsPrefix, ".frosti.projName");
        System.IO.File.WriteAllText(projName, configs["__CUSTOM_NAME__"]);

        if (env == Environments.Dev)
        {
            var isBeta = false;
            var response = await httpClient.GetAsync(
                $"https://frostifu-ppe-eus-functionappc1ed.azurewebsites.net/api/GetWorkflow?id=" +
                $"{configs["__USERPRINCIPALID__"]}&&subId={configs["__SUBSCRIPTION_ID__"]}&customName={configs["__CUSTOM_NAME__"]}&framework={"django"}");

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Console.WriteLine("Warning: Looks like you're not signed up for the Beta. Sign up with \'frosti signup beta\' for a standlone test env!");
            }
            else
            {
                response.EnsureSuccessStatusCode();

                System.IO.Directory.CreateDirectory(".github/workflows/");

                isBeta = true;
                var pipeline = await response.Content.ReadAsStringAsync();
                await System.IO.File.WriteAllTextAsync(".github/workflows/frosti.yml", pipeline);
            }

            if (optOut == false)
            {
                try
                {
                    await httpClient.GetAsync(
                        $"https://frostifu-ppe-eus-functionappc1ed.azurewebsites.net/api/LogUser?user={Dns.GetHostName()}&upn={HttpUtility.UrlEncode(configs["__UPN__"])}&isBeta={isBeta}",
                        new CancellationTokenSource(TimeSpan.FromSeconds(20)).Token);
                }
                catch
                {
                    Console.WriteLine("Warning: Cannot log user. This can be ignored.");
                }
            }
        }

        Console.WriteLine("Completed Linking");
    }
}

