using System;
namespace Frosti.Synthesizers;

public class GCPGo
{
    public static async Task<int> Synthesize(string projectName, string env, string? subId)
    {
        Console.WriteLine("Automatically provisioning GCP resources is not currently supported. It's coming soon! Please try provisioning via azure for now.");
        return 0;
    }
}

