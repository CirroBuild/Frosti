using System;
using Azure.Identity;
using Cirro.Provisioners;

namespace Cirro.Synthesizers;

public static class AWSJava
{
    public static async Task<int> Synthesize(string projectName, string env, string? subId)
    {
        Console.WriteLine("Automatically provisioning AWS resources is not currently supported. It's coming soon! Please try provisioning via azure for now.");
        return 0;
    }
}

