using System.IO;
using System.Text.Json;

namespace DotNetWatcher.Services;

public class AcknowledgementService
{
    private readonly string _filePath;
    private readonly string _lastRunFilePath;
    private HashSet<int> _acknowledgedIssues;

    public AcknowledgementService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DotNetWatcher");

        Directory.CreateDirectory(appDataPath);
        _filePath = Path.Combine(appDataPath, "acknowledged.json");
        _lastRunFilePath = Path.Combine(appDataPath, "lastRun.json");

        _acknowledgedIssues = LoadAcknowledgedIssues();
    }

    public bool IsAcknowledged(int issueNumber) => _acknowledgedIssues.Contains(issueNumber);

    public void Acknowledge(int issueNumber)
    {
        if (_acknowledgedIssues.Add(issueNumber))
        {
            SaveAcknowledgedIssues();
        }
    }

    public void Unacknowledge(int issueNumber)
    {
        if (_acknowledgedIssues.Remove(issueNumber))
        {
            SaveAcknowledgedIssues();
        }
    }

    public DateTimeOffset GetLastRunUtc()
    {
        try
        {
            if (!File.Exists(_lastRunFilePath))
                return DateTimeOffset.MinValue;

            var json = File.ReadAllText(_lastRunFilePath);
            var value = JsonSerializer.Deserialize<DateTimeOffset>(json);
            return value;
        }
        catch
        {
            return DateTimeOffset.MinValue;
        }
    }

    public void SetLastRunUtc(DateTimeOffset utcNow)
    {
        var json = JsonSerializer.Serialize(utcNow);
        File.WriteAllText(_lastRunFilePath, json);
    }

    private HashSet<int> LoadAcknowledgedIssues()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var issues = JsonSerializer.Deserialize<List<int>>(json);
                return issues is not null ? [.. issues] : [];
            }
        }
        catch
        {
            // If file is corrupted, start fresh
        }

        return [];
    }

    private void SaveAcknowledgedIssues()
    {
        var json = JsonSerializer.Serialize(_acknowledgedIssues.ToList());
        File.WriteAllText(_filePath, json);
    }
}