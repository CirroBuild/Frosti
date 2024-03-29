﻿using System;
using Azure.Identity;
using Microsoft.Graph;
using System.Text.RegularExpressions;
using System.Net;
using static Microsoft.Graph.Constants;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Frosti.Interpreters;

public static class AzureDotnetInterpreter
{
    public static async Task<string> Interpret(string env, AzureCliCredential credential, Dictionary<string, string> configs, HashSet<string> services)
    {

        var csprojName = "";
        var csprojData = "";

        var csprojFiles = System.IO.Directory.GetFiles(Environment.CurrentDirectory, "*.csproj", SearchOption.AllDirectories);
        if (csprojFiles.Length < 1)
        {
            Console.WriteLine($"No csproj file is found within this project. Please go to the root directory and try again");
            throw new Exception("Can't find file");
        }
        //Console.WriteLine($"Launched from {Environment.CurrentDirectory}"); // <- find all csproj in current directory and combine them as string
        //Console.WriteLine($"Physical location {AppDomain.CurrentDomain.BaseDirectory}");
        //Console.WriteLine($"AppContext.BaseDir {AppContext.BaseDirectory}");
        //return 0;

        try
        {
            foreach (var csprojFile in csprojFiles) //instead of appending, each csproj should be inetterpreted, provisioned and connected independently
            {
                using var reader = new StreamReader(csprojFile);
                csprojData += await reader.ReadToEndAsync();
                csprojName = Path.GetFileName(csprojFile).Replace(".csproj", "");

                if (configs.ContainsKey("") == false)
                {
                    configs.Add("__CSPROJNAME__", csprojName);
                }

                Regex rgx = new Regex("[^a-zA-Z0-9 -]");
                csprojName = rgx.Replace(csprojName, "");

            }
        }
        catch (IOException e)
        {
            TextWriter errorWriter = Console.Error;
            errorWriter.WriteLine(e.Message);
        }


        foreach (var sdkToService in AzureServices.DotnetSdkToServices)
        {
            if (csprojData.Contains(sdkToService.Key))
            {
                services.Add(sdkToService.Value);
            }
        }

        if (env.Equals(Environments.Dev))
        {
            var graphClient = new GraphServiceClient(credential);
            var user = await graphClient.Me
                .Request()
                .GetAsync();

            if (user != null)
            {
                configs.Add("__USERNAME__", string.IsNullOrEmpty(user.Surname) ? user.Id : user.Surname);
                configs.Add("__UPN__", user.UserPrincipalName);
                configs.Add("__USERPRINCIPALID__", user.Id);
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

        Console.WriteLine($"Completed Interpretting Project {csprojName}");

        return csprojName.Replace(" ", "");
    }
}

