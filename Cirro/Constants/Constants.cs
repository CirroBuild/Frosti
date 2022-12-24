using Azure.Core;

namespace Cirro;
public static class Constants
{
    public static readonly List<string> SupportedClouds = new() { "azure", "aws", "gcp" };
    public static readonly List<string> SupportedEnviornments = new() { "test", "dev", "prod" };
};