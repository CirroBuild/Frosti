using System.Security.Cryptography;
using Azure.Core;
using CommandLine;
using CommandLine.Text;
using Newtonsoft.Json;

namespace Frosti;

public static class Constants
{
    public const string Azure = "azure";
    public const string AWS = "aws";
    public const string GCP = "gcp";


    public const string DotNet = "dotnet";
    public const string Java = "java";
    public const string Python = "python";
    public const string Go = "go";

    public const string Dev = "dev";
    public const string PPE = "ppe";
    public const string Prod = "prod";

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