﻿using Azure.Core;
using Microsoft.Graph;
using Microsoft.Graph.CallRecords;
using Microsoft.Identity.Client.Extensions.Msal;
using static Azure.Core.HttpHeader;

namespace Frosti;
public static class AzureServices   //dotnet specific the sdks to service
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

    public static readonly Dictionary<string, string> DotnetSdkToServices = new()
    {
        {"<Project Sdk=\"Microsoft.NET.Sdk.Web\">", WebApp},
        {"Microsoft.NET.Sdk.Functions", FunctionApp},
        {"Azure.Storage", Storage},                               //Blobs, Queues, Files, DataLake (seperate sdks exist. Needed?)
        {"Azure.Security.KeyVault.Secrets", KeyVault},
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

    public static readonly Dictionary<string, string> DjangoSdkToServices = new()
    {
        {"azure.functions", FunctionApp},
        {"storages.backends.azure_storage", Storage},
        {"azure.keyvault.secrets", KeyVault},
        {"AzureLogHandler", ApplicationInsights},
        {"azure.cosmos", Cosmos},
        {"django.db.backends.postgresql", PostgreSQL}
        //Figure out MariaDb
    };
};

public class AzureServiceNames
{
    public AzureServiceNames(string projectName, string env, string uniqueString, AzureLocation loc)
    {
        ProjectName = projectName;
        Enviornment = env;
        Location = loc;
        UniqueString = uniqueString;
    }

    public string ProjectName { get; set; }
    public string Enviornment { get; set; }
    public AzureLocation Location { get; set; }
    public string UniqueString { get; set; }

    public string ResourcePrefix => $"{ProjectName}-{Enviornment}-{AzureLocations.ShortName[Location]}"; //takes up 20 chars
    public string ResourcePrefixNoHyphen => $"{ProjectName}{Enviornment}{AzureLocations.ShortName[Location]}".ToLower();
    public string ResourcePrefixLower => $"{ProjectName.Substring(0,7)}-{Enviornment}-{AzureLocations.ShortName[Location]}".ToLower();

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
    public KeyValuePair<string, string> ManagedIdentity => new("__MANAGEDIDENTITYNAME__", $"{ProjectName}-{Enviornment}-ManagedIdentity-{UniqueString}".Substring(0, 50)); //shouldnt be region specfici via resource prefix

    public Dictionary<string, KeyValuePair<string, string>> ServiceNameMap => new()
    {
        {AzureServices.WebApp, WebApp},
        {AzureServices.FunctionApp, FunctionApp},
        {AzureServices.Storage, Storage},
        {AzureServices.ServiceBus, ServiceBus},
        {AzureServices.EventHubs, EventHubs},
        {AzureServices.ApplicationInsights, ApplicationInsights},
        {AzureServices.Cosmos, Cosmos},
        {AzureServices.Redis, Redis},
        {AzureServices.SQL, SQL},
        {AzureServices.MySql, MySql},
        {AzureServices.PostgreSQL, PostgreSQL},
        {AzureServices.KeyVault, KeyVault},
        {AzureServices.ManagedIdentity, ManagedIdentity}
    };
};

public static class AzureProgramConnections //this is dotnet specific
{
    public static Dictionary<string, string[]> ServiceToConnectionCode => new()
    {
        {
            AzureServices.DevUser, new string[]
            {
                "if (builder.Environment.IsDevelopment())",
                "{",
                "\tbuilder.Configuration.AddJsonFile(\"appsettings.Frosti.json\");",
                "}"
            }
        },
        {
            AzureServices.KeyVault, new string[]
            {
                "var options = new SecretClientOptions()",
                "{",
                "\tRetry =",
                "\t{",
                "\t\tDelay= TimeSpan.FromSeconds(2),",
                "\t\tMaxDelay = TimeSpan.FromSeconds(16),",
                "\t\tMaxRetries = 5,",
                "\t\tMode = RetryMode.Exponential",
                "\t}",
                "};",
                "var secretClient = new SecretClient(new Uri(builder.Configuration[\"KV_ENDPOINT\"]), new DefaultAzureCredential(), options);",
            }
        },
        {
            AzureServices.Cosmos, new string[]
            {
                "var cosmosConnection = secretClient.GetSecret(\"CosmosConnection\").Value.Value;",
                "builder.Services.AddSingleton(s =>",
                "{",
                "\tCosmosClientOptions cosmosClientOptions = new CosmosClientOptions",
                "\t{",
                "\t\tMaxRetryAttemptsOnRateLimitedRequests = 3,",
                "\t\tMaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(60)",
                "\t};",
                "\treturn new CosmosClient(cosmosConnection);",
                "});",
            }
        }
    };
};