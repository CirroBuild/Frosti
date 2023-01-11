using Azure.Core;

namespace Frosti;
public static class AzureLocations
{
    public static readonly Dictionary<string, string[]> Transformer = new()
    {
        {Locations.NorthAmerica , new string[] {AzureLocation.EastUS, AzureLocation.WestUS, AzureLocation.CentralUS, AzureLocation.NorthCentralUS, AzureLocation.SouthCentralUS } },
        {Locations.Europe, new string[] {AzureLocation.NorthEurope, AzureLocation.WestEurope, AzureLocation.GermanyCentral, AzureLocation.FranceCentral, AzureLocation.UKSouth }}
    };

    public static readonly Dictionary<string, string> ShortName = new()
    {
        {AzureLocation.CentralUS, "cus"},
        {AzureLocation.WestCentralUS, "wcus"},
        {AzureLocation.EastAsia, "ea"},
        {AzureLocation.WestEurope, "weu"},
    };
};