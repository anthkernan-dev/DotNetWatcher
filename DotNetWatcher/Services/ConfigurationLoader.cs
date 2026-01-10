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

            // Override GitHubToken from environment variable if present
            var envToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (!string.IsNullOrWhiteSpace(envToken))
            {
                config = config with { GitHubToken = envToken };
            }

            return config;
        }
    }
}