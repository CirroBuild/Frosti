using System;
using System.Security.Cryptography.X509Certificates;
using Azure.Core;

namespace Cirro;

public static class Services
{
    public static readonly string WebApp = "WebApp";
    public static readonly string FunctionApp = "FunctionApp";
    public static readonly string Storage = "Storage";
    public static readonly string ServiceBus = "ServiceBus";
    public static readonly string EventHubs = "EventHubs";
    public static readonly string ApplicationInsights = "ApplicationInsights";
    public static readonly string Cosmos = "Cosmos";
    public static readonly string Redis = "Redis";
    public static readonly string SQL = "SQL";
    public static readonly string MySql = "MySql";
    public static readonly string PostgreSQL = "PostgreSQL";
    public static readonly string KeyVault = "KeyVault";
    public static readonly string ManagedIdentity = "ManagedIdentity";
    public static readonly string DevUser = "DevUser";

    public static readonly Dictionary<string, string> SdkToServices = new()
    {
        {"<Project Sdk=\"Microsoft.NET.Sdk.Web\">", WebApp},
        {"Microsoft.NET.Sdk.Functions", FunctionApp},
        {"Azure.Storage", Storage},                               //Blobs, Queues, Files, DataLake (seperate sdks exist. Needed?)
        {"Azure.Security.KeyVault", KeyVault},
        {"Azure.Messaging.ServiceBus", ServiceBus},
        {"Azure.Messaging.EventHubs", EventHubs},
        {"Microsoft.ApplicationInsights", ApplicationInsights},
        {"Microsoft.Azure.Cosmos", Cosmos},
        {"StackExchange.Redis", Redis},
        {"Microsoft.Data.SqlClient", SQL},                        //Managed Instance for premium? https://learn.microsoft.com/en-us/azure/azure-sql/managed-instance/create-template-quickstart?view=azuresql&tabs=azure-powershell
        {"MySql.Data", MySql},                                    //Flexible server for premium? https://learn.microsoft.com/en-us/azure/templates/microsoft.dbformysql/flexibleservers?pivots=deployment-language-arm-template
        {"Npgsql", PostgreSQL}
        //Figure out MariaDb
    };
};


public static class Enviornments
{
    public static readonly List<string> SupportedEnviornments = new() { "test", "dev", "prod" };
};

public static class Locations
{
    public static readonly Dictionary<string, string> ShortName = new()
    {
        {AzureLocation.CentralUS, "cus"},
        {AzureLocation.EastAsia, "ea"},
        {AzureLocation.WestEurope, "weu"},
    };
};

public class ServiceNames
{
    public ServiceNames(string infraPrefix, string env, string uniqueString, AzureLocation loc)
    {
        InfraPrefix = infraPrefix;
        Enviornment = env;
        Location = loc;
        UniqueString = uniqueString;
    }

    public string InfraPrefix { get; set; }
    public string Enviornment { get; set; }
    public AzureLocation Location { get; set; }
    public string UniqueString { get; set; }

    public string ResourcePrefix => $"{InfraPrefix}-{Enviornment}-{Locations.ShortName[Location]}"; //takes up 20 chars
    public string ResourcePrefixNoHyphen => $"{InfraPrefix}{Enviornment}{Locations.ShortName[Location]}".ToLower();
    public string ResourcePrefixLower => $"{InfraPrefix.Substring(0,7)}-{Enviornment}-{Locations.ShortName[Location]}".ToLower();

    public KeyValuePair<string, string> WebApp => new("__WEBAPPNAME__", $"{ResourcePrefix}-WebApp{UniqueString}".Substring(0, 32));
    public KeyValuePair<string, string> FunctionApp => new("__FUNCTIONAPPNAME__", $"{ResourcePrefix}-FunctionApp{UniqueString}".Substring(0, 32));
    public KeyValuePair<string, string> Storage => new("__STORAGENAME__", $"{ResourcePrefixNoHyphen}storage{UniqueString}".Substring(0, 24));
    public KeyValuePair<string, string> ServiceBus => new("__SERVICEBUSNAME__", $"{ResourcePrefix}-ServiceBus-{UniqueString}".Substring(0, 50));
    public KeyValuePair<string, string> EventHubs => new("__EVENTHUBSNAME__", $"{ResourcePrefix}-EventHub-{UniqueString}".Substring(0, 50));
    public KeyValuePair<string, string> ApplicationInsights => new("__APPINSIGHTSNAME__", $"{ResourcePrefix}-AI-{UniqueString}".Substring(0, 50));
    public KeyValuePair<string, string> Cosmos => new("__COSMOSNAME__", $"{ResourcePrefixLower}-cosmos-{UniqueString}".Substring(0, 44));
    public KeyValuePair<string, string> Redis => new("__REDISNAME__", $"{ResourcePrefix}-Redis-{UniqueString}".Substring(0, 63));
    public KeyValuePair<string, string> SQL => new("__SQLNAME__", $"{ResourcePrefix}-SQL-{UniqueString}".Substring(0, 63));
    public KeyValuePair<string, string> MySql => new("__MYSQLNAME__", $"{ResourcePrefix}-MySQL-{UniqueString}".Substring(0,63));
    public KeyValuePair<string, string> PostgreSQL => new("__POSTGRESQLNAME__", $"{ResourcePrefix}-PostgreSql-{UniqueString}".Substring(0, 63));
    public KeyValuePair<string, string> KeyVault => new("__KEYVAULTNAME__", $"{ResourcePrefix}-KV{UniqueString}".Substring(0, 24));
    public KeyValuePair<string, string> ManagedIdentity => new("__MANAGEDIDENTITYNAME__", $"{ResourcePrefix}-ManagedIdentity-{UniqueString}".Substring(0, 50));

    public Dictionary<string, KeyValuePair<string, string>> ServiceNameMap => new()
    {
        {Services.WebApp, WebApp},
        {Services.FunctionApp, FunctionApp},
        {Services.Storage, Storage},
        {Services.ServiceBus, ServiceBus},
        {Services.EventHubs, EventHubs},
        {Services.ApplicationInsights, ApplicationInsights},
        {Services.Cosmos, Cosmos},
        {Services.Redis, Redis},
        {Services.SQL, SQL},
        {Services.MySql, MySql},
        {Services.PostgreSQL, PostgreSQL},
        {Services.KeyVault, KeyVault},
        {Services.ManagedIdentity, ManagedIdentity}
    };
};