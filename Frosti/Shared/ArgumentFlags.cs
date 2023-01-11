using System;
using CommandLine;
using CommandLine.Text;
using Newtonsoft.Json;

namespace Frosti.Shared;

public class ArgumentFlags
{
    [Option('c', "cloud", HelpText = "Please specify the cloud with -c. See docs on getting started here: {doc-link}", Default = Clouds.Azure)]
    public string Cloud { get; set; }

    [Option('n', "name", Required = true, HelpText = "Please specify the name to prefix the infrastructure with -n. See docs on getting started here: {doc-link}")]
    public string ProjectName { get; set; }

    [Option('e', "sku", HelpText = "Please specify the name to prefix the infrastructure with -e. Please see doc {insert doc link}", Default = Environments.Dev)]
    public string Enviornment { get; set; }

    [Option('l', "location", HelpText = "Please specify the primary region for the resources with -l. Please see doc {insert doc link}", Default = Locations.NorthAmerica)]
    public string Location { get; set; }

    [Option('f', "framework", HelpText = "The language or framework the project is written in, i.e. dotnet, python etc. See doc here {link}")]
    public string? Framework { get; set; }

    [Option('s', "subscriptionId", HelpText = "The subscription Id to be used for azure...change this later")]
    public string? SubscriptionId { get; set; }
}