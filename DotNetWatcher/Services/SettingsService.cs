using DotNetWatcher.Models;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetWatcher.Services;

public static class SettingsService
{
    private static readonly string SettingsFilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "appsettings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void SaveConfiguration(AppConfiguration configuration)
    {
        try
        {
            // Read existing file to preserve structure
            var json = File.Exists(SettingsFilePath)
                ? File.ReadAllText(SettingsFilePath)
                : "{}";

            // Parse existing JSON
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            // Create new configuration object
            var configObject = new Dictionary<string, object>
            {
                ["AppConfiguration"] = new Dictionary<string, object>
                {
                    ["MonitoredVersions"] = configuration.MonitoredVersions,
                    ["PollingIntervalHours"] = configuration.PollingIntervalHours,
                    ["StartWithWindows"] = configuration.StartWithWindows,
                    ["StartMinimized"] = configuration.StartMinimized,
                    ["TargetRepoOwner"] = configuration.TargetRepoOwner,
                    ["TargetRepoName"] = configuration.TargetRepoName,
                    ["GitHubAdvisoryUri"] = configuration.GitHubAdvisoryUri
                }
            };

            // Serialize and write to file
            var jsonString = JsonSerializer.Serialize(configObject, JsonOptions);
            File.WriteAllText(SettingsFilePath, jsonString);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save configuration: {ex.Message}", ex);
        }
    }
}
