using System;
namespace Frosti.Connectors;

	public class AzureDotnetConnector
{
    public static async Task Connect(string env, Dictionary<string, string> configs, HashSet<string> services)
    {

        var appsettingFile = System.IO.Directory.GetFiles(Environment.CurrentDirectory, "appsettings.json", SearchOption.AllDirectories).FirstOrDefault();
        var programFile = System.IO.Directory.GetFiles(Environment.CurrentDirectory, "Program.cs", SearchOption.AllDirectories).FirstOrDefault();

        if (string.IsNullOrEmpty(appsettingFile) || string.IsNullOrEmpty(programFile))
        {
            throw new Exception($"No appsettings.json and/or Program.cs file is found within this project. Cannot automatically connect resources.");
        }

        var gitIgnoreFile = appsettingFile.Replace("appsettings.json", ".gitignore");
        var frostiServicesFile = appsettingFile.Replace("appsettings.json", ".frostiservices");
        var localKvFile = appsettingFile.Replace("appsettings.json", ".frostilocalkv");
        var programFileOld = programFile.Replace("Program.cs", "Program.Save.cs");

        if (System.IO.File.Exists(programFileOld) == false)
        {
            System.IO.File.Copy(programFile, programFileOld);
        }

        var frostiAppSettingsFile = appsettingFile.Replace("appsettings", "appsettings.frosti");

        if (System.IO.File.Exists(frostiAppSettingsFile) == false)
        {
            System.IO.File.AppendAllLines(gitIgnoreFile, new string[]
            {
            "appsettings.frosti.json",
            ".frostiservices",
            ".frostilocalkv",
            "Program.Save.cs"
            });
        }

        if (System.IO.File.Exists(localKvFile) == false || System.IO.File.ReadAllText(localKvFile) != configs["__KEYVAULTNAME__"])
        {
            System.IO.File.WriteAllLines(frostiAppSettingsFile, new string[] { "{", $"\t\"KV_Endpoint\": \"{configs["__KEYVAULTNAME__"]}\"", "}" });
        }


        //still need to look into delete service connections if they got removed
        var newServices = services.Where(x => System.IO.File.ReadAllLines(frostiServicesFile).Contains(x) == false);
        if (newServices.Count() != 0)
        {
            Console.WriteLine($"Connecting {string.Join(", ", newServices)}");
            var currentProgram = System.IO.File.ReadAllLines(programFile);
            var usings = AzureServices.SdkToServices.Where(x => newServices.Contains(x.Value)).Select(x => $"using {x.Key};");
            var connections = AzureProgramConnections.ServiceToConnectionCode.Where(x => newServices.Contains(x.Key)).SelectMany(x => x.Value);

            var counter = 0;
            string? line;

            using var programFileReader = new StreamReader(programFile);
            while ((line = await programFileReader.ReadLineAsync()) != null)
            {
                counter++;
                if (line.Contains("var builder"))
                {
                    break;
                }
            }

            //dont include using if already included. 
            var finalProgram = usings.Concat(currentProgram.Take(counter)).Concat(connections).Concat(currentProgram.Skip(counter)); //need to fix union
            System.IO.File.WriteAllLines(programFile, finalProgram);
            System.IO.File.AppendAllLines(frostiServicesFile, newServices);
        }

        Console.WriteLine("Completed Linking");
    }
}

