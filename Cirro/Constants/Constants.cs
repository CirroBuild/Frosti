using System.Security.Cryptography;
using Azure.Core;

namespace Cirro;
public static class Supported
{
    public static readonly List<string> Clouds = new() { "azure", "aws", "gcp" };
    public static readonly List<string> Enviornments = new() { "test", "dev", "prod" };
    public static readonly List<string> Frameworks = new() { "dotnet", "python", "java" };
};

public static class Shared
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