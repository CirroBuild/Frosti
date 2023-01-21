﻿using System.Security.Cryptography;
using Azure.Core;
using CommandLine;
using CommandLine.Text;
using Newtonsoft.Json;

namespace Frosti;

public static class Clouds
{
    public const string Azure = "azure";
    public const string AWS = "aws";
    public const string GCP = "gcp";

    public static readonly List<string> Supported = new() { Azure, AWS, GCP };
}

public static class Frameworks
{
    public const string DotNet = "dotnet";
    public const string Java = "java";
    public const string Python = "python";
    public const string Go = "go";

    public static readonly List<string> AzureSupported = new() { DotNet };
    public static readonly List<string> AWSSupported = new() {  };
    public static readonly List<string> GCPSupported = new() {  };
}

public static class Environments
{
    public const string Dev = "dev";
    public const string S1 = "s1";
    public const string P1 = "p1";

    public static readonly List<string> Supported = new() { Dev, S1, P1 };
}

public static class Locations
{
    public const string NorthAmerica = "northamerica";
    public const string SouthAmerica = "southamerica";
    public const string Europe = "europe";
    public const string Africa = "africa";
    public const string Asia = "asia";
    public const string Australia = "australia";

    public static readonly Dictionary<string, int> EnvLocationCount = new()
    {
        {Environments.Dev , 1},
        {Environments.S1, 2},
        {Environments.P1, 4}
    };
}

public static class Constants
{
    public static string GetUniqueString(string sub, string rgPrefix)
    {
        var text = $"{sub}-{rgPrefix}";
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        byte[] textData = System.Text.Encoding.UTF8.GetBytes(text);
        byte[] hash = SHA256.HashData(textData);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();
    }
}