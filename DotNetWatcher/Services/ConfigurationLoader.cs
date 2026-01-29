using DotNetWatcher.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;


namespace DotNetWatcher.Services
{
    public static class ConfigurationLoader
    {
        public static AppConfiguration Load()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var config = configuration.GetSection("AppConfiguration").Get<AppConfiguration>()
                ?? new AppConfiguration();

            // GitHub token must be provided via environment variable for security : pwsh $env:DOTNETWATCHER_GITHUB_TOKEN = "your-token-here"
            var envToken = Environment.GetEnvironmentVariable("DOTNETWATCHER_GITHUB_TOKEN");
            if (!string.IsNullOrWhiteSpace(envToken))
            {
                config = config with { GitHubToken = envToken };
            }

            return config;
        }
    }
}