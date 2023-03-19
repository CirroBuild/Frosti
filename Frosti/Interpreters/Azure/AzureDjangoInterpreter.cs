using System;
using Azure.Identity;
using Microsoft.Graph;
using System.Text.RegularExpressions;
using System.Net;
using static Microsoft.Graph.Constants;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Frosti.Interpreters;

public static class AzureDjangoInterpreter
{
    public static async Task Interpret(string env, AzureCliCredential credential, Dictionary<string, string> configs, HashSet<string> services)
    {

        var pyData = "";

        var settingsPyFile = System.IO.Directory.GetFiles(Environment.CurrentDirectory, "settings.py", SearchOption.AllDirectories).FirstOrDefault();
        if (settingsPyFile == null || settingsPyFile.Length < 1)
        {
            Console.WriteLine($"No settings.py file is found within this project. Please go to the root directory and try again");
            throw new Exception("Can't find file");
        }

        try
        {
            using var reader = new StreamReader(settingsPyFile);
            pyData += await reader.ReadToEndAsync();
        }
        catch (IOException e)
        {
            TextWriter errorWriter = Console.Error;
            errorWriter.WriteLine(e.Message);
        }


        foreach (var sdkToService in AzureServices.DjangoSdkToServices)
        {
            if (pyData.Contains(sdkToService.Key))
            {
                services.Add(sdkToService.Value);
            }
        }

        if (services.Contains(AzureServices.FunctionApp) == false)
        {
            services.Add(AzureServices.WebApp); //automatically add webapp for django if func app not there
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

        configs.Add("\"__SERVICES__\"", "[\"" + string.Join("\",\"", services) + "\"]");

        Console.WriteLine($"Completed Interpretting");
    }
}

