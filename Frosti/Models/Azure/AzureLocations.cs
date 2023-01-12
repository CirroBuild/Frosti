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
        {AzureLocation.EastUS, "eus"},
        {AzureLocation.CentralUS, "cus"},
        {AzureLocation.WestUS, "wus"},
        {AzureLocation.NorthCentralUS, "ncus"},
        {AzureLocation.SouthCentralUS, "scus"},
        {AzureLocation.WestCentralUS, "wcus"},
        {AzureLocation.EastAsia, "ea"},
        {AzureLocation.NorthEurope, "neu"},
        {AzureLocation.WestEurope, "weu"},
        {AzureLocation.GermanyCentral, "gec"},
        {AzureLocation.FranceCentral, "frc"},
        {AzureLocation.UKSouth, "uks"},
    };
};