﻿using System;
using Azure.Identity;
using Microsoft.Graph;
using System.Text.RegularExpressions;

namespace Frosti.Interpreters;

public static class AzureDotnetInterpreter
{
    public static async Task Interpret(string env, AzureCliCredential credential, Dictionary<string, string> configs, HashSet<string> services)
    {

        var csprojData = "";

        var csprojFiles = System.IO.Directory.GetFiles(Environment.CurrentDirectory, "*.csproj", SearchOption.AllDirectories);
        if (csprojFiles.Length < 1)
        {
            Console.WriteLine($"No csproj file is found within this project. Please go to the root directory and try again");
        }
        //Console.WriteLine($"Launched from {Environment.CurrentDirectory}"); // <- find all csproj in current directory and combine them as string
        //Console.WriteLine($"Physical location {AppDomain.CurrentDomain.BaseDirectory}");
        //Console.WriteLine($"AppContext.BaseDir {AppContext.BaseDirectory}");
        //return 0;

        try
        {
            foreach (var csprojFile in csprojFiles)
            {
                using var reader = new StreamReader(csprojFile);
                csprojData += await reader.ReadToEndAsync();
            }
        }
        catch (IOException e)
        {
            TextWriter errorWriter = Console.Error;
            errorWriter.WriteLine(e.Message);
        }


        foreach (var sdkToService in AzureServices.SdkToServices)
        {
            if (csprojData.Contains(sdkToService.Key))
            {
                services.Add(sdkToService.Value);
            }
        }

        if (env.Equals(Environments.Dev))
        {
            var graphClient = new GraphServiceClient(credential);
            var users = await graphClient.Users
                .Request()
                .GetAsync();
            var principalId = users.FirstOrDefault()?.Id;

            if (principalId != null)
            {
                configs.Add("__USERPRINCIPALID__", principalId);
                services.Add(AzureServices.DevUser);
            }

            services.Remove(AzureServices.WebApp);
            services.Remove(AzureServices.FunctionApp);
        }

        if (services.Contains(AzureServices.WebApp) || services.Contains(AzureServices.FunctionApp))
        {
            var regex = new Regex("<TargetFramework>(.*)</TargetFramework>");   //since combining csproj, could have multiple target frameworks here
            var v = regex.Match(csprojData);
            var s = v.Groups[1].ToString();

            configs.Add("__LINUXVERSION__", "DOTNETCORE|" + s.Replace("net", ""));
        }

        configs.Add("\"__SERVICES__\"", "[\"" + string.Join("\",\"", services) + "\"]");

        Console.WriteLine("Completed Interpretting");
    }
}

