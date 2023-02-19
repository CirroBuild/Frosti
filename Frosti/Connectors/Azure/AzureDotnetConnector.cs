using System;
using System.Reflection;
using Newtonsoft.Json.Linq;
using static Microsoft.Graph.Constants;

namespace Frosti.Connectors;

	public class AzureDotnetConnector
{
    public static async Task Connect(Dictionary<string, string> configs, HashSet<string> services)
    {
        var appsettingFile = System.IO.Directory.GetFiles(Environment.CurrentDirectory, "appsettings.json", SearchOption.AllDirectories).FirstOrDefault();
        var appSettingsPrefix = "appsettings.json";
        var kvUrl = $"https://{configs["__KEYVAULTNAME__"]}.vault.azure.net/";

        if (File.Exists(appsettingFile))
        {
            var gitIgnoreFile = appsettingFile.Replace(appSettingsPrefix, ".gitignore");
            var frostiAppSettingsFile = appsettingFile.Replace(appSettingsPrefix, "appsettings.frosti.json");

            if (File.Exists(frostiAppSettingsFile) == false)
            {
                File.AppendAllLines(gitIgnoreFile, new string[]
                {
                    "appsettings.frosti.json"
                });
            }

            File.WriteAllLines(frostiAppSettingsFile, new string[] { "{", $"\t\"KV_Endpoint\": \"{kvUrl}\"", "}" });
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
                    File.WriteAllText(appsettingFile, result);
                }
            }
        }

        var frostiDelta = appsettingFile.Replace(appSettingsPrefix, "frosti.delta");
        File.WriteAllText(frostiDelta, string.Join(", ", services));


        Directory.CreateDirectory(".github/workflows/");
        var pipelineFile = $"Frosti.Data.Azure.pipeline.frosti.yml";
        var assembly = Assembly.GetExecutingAssembly();
        using var pipelineStrean = new StreamReader(assembly.GetManifestResourceStream(pipelineFile));

        var pipeline = await pipelineStrean.ReadToEndAsync();
        pipeline = pipeline.Replace("__CSPROJNAME__", configs["__CSPROJNAME__"]);
        pipeline = pipeline.Replace("__SUBSCRIPTION_ID__", configs["__SUBSCRIPTION_ID__"]);
        await File.WriteAllTextAsync(".github/workflows/frosti.yml", pipeline);

        Console.WriteLine("Completed Linking");
    }
}

