using System.Text.RegularExpressions;

public class Parser
{
    public static async Task<int> Main(string[] args)
    {
        var services = new HashSet<string>();
        var configs = new Dictionary<string, string>();

        try
        {
            var filepath = args[0];
            var env = args[1];

            // Attempt to open input file.
            using var reader = new StreamReader(filepath);
            var csprojData = await reader.ReadToEndAsync();

            var projName = Path.GetFileName(args[0]).Replace(".csproj", "");

            Console.WriteLine($"ProjectName: {projName}");

            if (csprojData.Contains("<Project Sdk=\"Microsoft.NET.Sdk.Web\">"))
            {
                services.Add("WebApp");
                configs.Add("__WEBAPPNAME__", projName);

                configs.Add("__WEBAPPSKU__", env == "dev" ? "B1" : "S1");

            }
            else if (csprojData.Contains("Microsoft.NET.Sdk.Functions"))
            {
                services.Add("FunctionApp");
                configs.Add("__FUNCTIONAPPNAME__", projName);
            }

            if (csprojData.Contains("Azure.Storage"))
            {
                services.Add("Storage");
                configs.Add("__STORAGENAME__", projName.ToLower() + "storage");
            }

            if (csprojData.Contains("Microsoft.Azure.Cosmos"))
            {
                services.Add("Cosmos");
                configs.Add("__COSMOSNAME__", projName.ToLower() + "cosmos");
            }

            if (services.Contains("WebApp") || services.Contains("FunctionApp"))
            {
                var regex = new Regex("<TargetFramework>(.*)</TargetFramework>");
                var v = regex.Match(csprojData);
                var s = v.Groups[1].ToString();

                configs.Add("__LINUXVERSION__", "DOTNETCORE|" + s.Replace("net", ""));
            }
        }
        catch (IOException e)
        {
            TextWriter errorWriter = Console.Error;
            errorWriter.WriteLine(e.Message);
            return 1;
        }

        Console.WriteLine($"Configs: {string.Join(" ",configs)}");
        Console.WriteLine($"Services: {string.Join(" ",services)}");
        Console.WriteLine($"Completed Parsing Args {args[0]}.");
        return 0;
    }
}