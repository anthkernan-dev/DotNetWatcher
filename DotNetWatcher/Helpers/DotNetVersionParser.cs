using System.Text.RegularExpressions;

namespace DotNetWatcher.Helpers;

public partial class DotNetVersionParser
{
    [GeneratedRegex(@"^(?:\.NET|net)\s*(\d+)(?:\.(\d+))?(?:\.(\d+))?", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex VersionRegex();

    public bool TryParse(string input, out Version version)
    {
        version = new Version(0, 0);

        if (string.IsNullOrWhiteSpace(input))
            return false;

        var match = VersionRegex().Match(input);
        if (!match.Success)
            return false;

        if (!int.TryParse(match.Groups[1].Value, out var major))
            return false;

        var minor = 0;
        if (match.Groups[2].Success)
            _ = int.TryParse(match.Groups[2].Value, out minor);

        // Also capture patch if present
        var patch = 0;
        if (match.Groups.Count > 3 && match.Groups[3].Success)
            _ = int.TryParse(match.Groups[3].Value, out patch);

        version = new Version(major, minor, patch);
        return true;
    }

    public IEnumerable<string> SortVersions(IEnumerable<string> versions)
    {
        return versions
            .Select(v => new { Text = v, IsParsed = TryParse(v, out var parsed), Parsed = parsed })
            .OrderByDescending(v => v.IsParsed)
            .ThenByDescending(v => v.Parsed.Major)
            .ThenByDescending(v => v.Parsed.Minor)
            .ThenByDescending(v => v.Parsed.Build)
            .ThenBy(v => v.Text, StringComparer.OrdinalIgnoreCase)
            .Select(v => v.Text);
    }
}