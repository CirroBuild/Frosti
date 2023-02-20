using System;
using CommandLine;
using CommandLine.Text;
using Newtonsoft.Json;

namespace Frosti.Shared;

public class ArgumentFlags
{
    //[Option('c', "cloud", HelpText = "Please specify the cloud with -c. See docs on getting started here: {doc-link}", Default = Clouds.Azure)]
    //public string Cloud { get; set; } = Clouds.Azure;

    [Option('n', "name", HelpText = "The name to override the default project prefix for the infrastructure. See docs: {doc-link}")]
    public string ProjectName { get; set; } = string.Empty;

    [Option('e', "environment", HelpText = "The environment to setup the infrastructure. One of 'dev', 'ppe', or 'prod'. See docs: {insert doc link}", Default = Environments.Dev)]
    public string Enviornment { get; set; } = Environments.Dev;

    [Option('o', "optOut", HelpText = "Specfiy --o to opt out of frosti usage tracking")]
    public bool OptOut { get; set; } = false;

    [Option('r', "runOn", HelpText = "Used to identify where frosti is run from")]
    public string RunOn { get; set; } = RunOnOpts.Local;

    [Option('b', "beta", HelpText = "Use to setup PPE env for Beta")]
    public bool Beta { get; set; } = false;

    //[Option('l', "location", HelpText = "The primary region for the resources. Please see doc {insert doc link}", Default = Locations.NorthAmerica)]
    //public string Location { get; set; } = Locations.NorthAmerica;

    //[Option('f', "framework", HelpText = "The language or framework the project is written in, i.e. dotnet, python etc. See doc here {link}")]
    //public string? Framework { get; set; }

    [Option('s', "subscriptionId", HelpText = "The subscription Id to be used. Default is to use the primary subscription")]
    public string? SubscriptionId { get; set; }

    [Option('a', "autoConnect", HelpText = "Set to true if you want frosti to connect the resource by editing your code. See docs on getting started here: {doc-link}", Default = false)]
    public bool AutoConnect { get; set; } = false;
}