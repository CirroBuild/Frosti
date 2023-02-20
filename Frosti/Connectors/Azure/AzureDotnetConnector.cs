using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using Azure.Identity;
using Microsoft.Graph;
using Newtonsoft.Json.Linq;
using static Microsoft.Graph.Constants;

namespace Frosti.Connectors;

	public class AzureDotnetConnector
{
    public static async Task Connect(Dictionary<string, string> configs, HashSet<string> services, AzureCliCredential credential, bool beta)
    {
        var appsettingFile = System.IO.Directory.GetFiles(Environment.CurrentDirectory, "appsettings.json", SearchOption.AllDirectories).FirstOrDefault();
        var appSettingsPrefix = "appsettings.json";
        var kvUrl = $"https://{configs["__KEYVAULTNAME__"]}.vault.azure.net/";

        if (System.IO.File.Exists(appsettingFile))
        {
            var gitIgnoreFile = appsettingFile.Replace(appSettingsPrefix, ".gitignore");
            var frostiAppSettingsFile = appsettingFile.Replace(appSettingsPrefix, "appsettings.frosti.json");

            if (System.IO.File.Exists(frostiAppSettingsFile) == false)
            {
                System.IO.File.AppendAllLines(gitIgnoreFile, new string[]
                {
                    "appsettings.frosti.json"
                });
            }

            System.IO.File.WriteAllLines(frostiAppSettingsFile, new string[] { "{", $"\t\"KV_Endpoint\": \"{kvUrl}\"", "}" });
        }
        else
        {
            appsettingFile = System.IO.Directory.GetFiles(Environment.CurrentDirectory, "local.settings.json", SearchOption.AllDirectories).FirstOrDefault();
            appSettingsPrefix = "local.settings.json";

            if (string.IsNullOrEmpty(appsettingFile))
            {
                throw new Exception($"No appsettings.json and/or local.settings.json file is found within this project. Cannot automatically connect resources.");
            }

            string result = string.Empty;
            using (var r = new StreamReader(appsettingFile))
            {
                var json = r.ReadToEnd();
                var jobj = JObject.Parse(json);
                var vals = jobj["Values"] as JObject;
                if (vals.ContainsKey("KV_Endpoint") == false)
                {
                    vals.Add(new JProperty("KV_Endpoint", kvUrl));
                    result = jobj.ToString();
                    System.IO.File.WriteAllText(appsettingFile, result);
                }
            }
        }

        if (beta)
        {
            var accessToken = await credential.GetTokenAsync(new Azure.Core.TokenRequestContext(new string[] { "https://graph.microsoft.com/.default" }));
            using HttpClient client = new();
            //client.DefaultRequestHeaders.Authorization =
            //    new AuthenticationHeaderValue("Bearer", accessToken.Token);

            var graphClient = new GraphServiceClient(credential);
            var user = await graphClient.Me
                .Request()
                .GetAsync();

            var response = await client.GetAsync($"https://frostifu-ppe-eus-functionappc1ed.azurewebsites.net/api/GetWorkflow?id={user.Id}");
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Console.WriteLine("Looks like you're not signed up for the Beta. Please sign up at https://frostibuild.com/enterprise/");
                return;
            }

            response.EnsureSuccessStatusCode();

            System.IO.Directory.CreateDirectory(".github/workflows/");

            var pipeline = await response.Content.ReadAsStringAsync();
            pipeline = pipeline.Replace("__CSPROJNAME__", configs["__CSPROJNAME__"]);
            pipeline = pipeline.Replace("__SUBSCRIPTION_ID__", configs["__SUBSCRIPTION_ID__"]);
            await System.IO.File.WriteAllTextAsync(".github/workflows/frosti.yml", pipeline);
        }


        var frostiDelta = appsettingFile.Replace(appSettingsPrefix, "frosti.delta");
        System.IO.File.WriteAllText(frostiDelta, string.Join(", ", services));

        Console.WriteLine("Completed Linking");
    }
}

