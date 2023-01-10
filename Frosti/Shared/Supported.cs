using System;
namespace Frosti.Shared;

public static class Supported
{
    public static readonly List<string> Clouds = new() { Constants.Azure, Constants.AWS, Constants.GCP };
    public static readonly List<string> Enviornments = new() { Constants.Dev, Constants.PPE, Constants.Prod };

    public static class Azure
    {
        public static readonly List<string> Frameworks = new() { Constants.DotNet };
    }

    public static class AWS
    {
        public static readonly List<string> Frameworks = new() { };
    }

    public static class GCP
    {
        public static readonly List<string> Frameworks = new() { };
    }
}

