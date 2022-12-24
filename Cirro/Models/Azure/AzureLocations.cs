using Azure.Core;

namespace Cirro;
public static class AzureLocations
{
    public static readonly Dictionary<string, string> ShortName = new()
    {
        {AzureLocation.CentralUS, "cus"},
        {AzureLocation.WestCentralUS, "wcus"},
        {AzureLocation.EastAsia, "ea"},
        {AzureLocation.WestEurope, "weu"},
    };
};