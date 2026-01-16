using System;
using System.Collections.Generic;

namespace DotNetWatcher.Models;

public record AppConfiguration
{
    public List<string> MonitoredVersions { get; init; }
    public int PollingIntervalHours { get; init; } = 1;
    public TimeSpan PollingInterval => TimeSpan.FromHours(PollingIntervalHours);
    public bool StartWithWindows { get; init; }
    public bool StartMinimized { get; init; }
    public string? GitHubToken { get; init; }
    public string TargetRepoOwner { get; init; } = string.Empty;
    public string TargetRepoName { get; init; } = string.Empty;
    public string GitHubAdvisoryUri { get; init; } = "https://api.github.com/repos/dotnet/announcements/issues?state=open&labels=Security";
}
