using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DotNetWatcher.Models;

public sealed class SecurityAdvisory : INotifyPropertyChanged
{
    public int IssueNumber { get; init; }
    public string Title { get; init; } = string.Empty;
    public string CveId { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public List<string> AffectedVersions { get; init; } = [];
    public ImpactStatus Impact { get; init; }
    public bool IsNew { get; init; }

    public string Body { get; init; } = string.Empty;

    public string ExecutiveSummary { get; init; } = string.Empty;
    public string AffectedSoftware { get; init; } = string.Empty;
    public string MitigationFactors { get; init; } = string.Empty;
    public string AdvisoryFaq { get; init; } = string.Empty;

    public ActionRecommendation ActionRecommendation { get; init; }

    public string ActionRecommendationDisplay => ActionRecommendation switch
    {
        ActionRecommendation.Upgrade => "Upgrade",
        ActionRecommendation.Mitigate => "Mitigate",
        _ => "Monitor",
    };

    private bool _isAcknowledged;

    public bool IsAcknowledged
    {
        get => _isAcknowledged;
        set
        {
            if (_isAcknowledged != value)
            {
                _isAcknowledged = value;
                OnPropertyChanged();
            }
        }
    }

    public string CveDisplay =>
        !string.IsNullOrWhiteSpace(CveId)
            ? CveId
                : (Title?.Contains("Updates", StringComparison.OrdinalIgnoreCase) == true
                    ? "Monthly .NET Update"
                        : string.Empty);

    public bool HasCve => !string.IsNullOrWhiteSpace(CveId);

    public Uri? MsrcUri =>
        HasCve ? new Uri($"https://msrc.microsoft.com/update-guide/vulnerability/{CveId}") : null;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public enum ImpactStatus
{
    Potential,
    Impact,
}

public enum ActionRecommendation
{
    Monitor,
    Mitigate,
    Upgrade,
}