# DotNetWatcher

A Windows desktop application that monitors .NET security advisories from the official [dotnet/announcements](https://github.com/dotnet/announcements) repository and alerts you when vulnerabilities affect your production .NET versions.

![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat&logo=dotnet)
![WPF](https://img.shields.io/badge/WPF-Desktop-0078D4?style=flat&logo=windows)
![License](https://img.shields.io/badge/license-MIT-green)

## Features

-  **Real-time Monitoring** - Fetches security advisories from GitHub with automatic pagination
-  **Impact Assessment** - Automatically identifies advisories affecting your monitored .NET versions
-  **Smart Filtering** - Filter by CVE, issue number, title, version, impact status, or acknowledgment state
-  **Sorting Options** - Sort by newest, impact level, or CVE presence
-  **Advisory Details** - View executive summary, affected software, mitigation factors, and FAQ (parsed from markdown)
-  **Acknowledgment Tracking** - Mark advisories as acknowledged to track your review progress
-  **Issue Creation** - Create remediation issues directly in your GitHub repository
-  **Toast Notifications** - Get notified when new advisories are detected
-  **Visual Studio Dark Theme** - Native dark theme with custom title bar matching VS aesthetic
-  **System Info** - Displays your installed .NET SDK and runtime versions in the status bar

## Screenshots

![DotNetWatcher Main Window]([assets/DotNetWatcher.png](https://github.com/anthkernan-dev/DotNetWatcher/blob/master/DotNetWatcher/Assets/DotNetWatcher.png))

## Getting Started

### Prerequisites

- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later

### Installation

1. Clone the repository:
```bash
git clone https://github.com/yourusername/DotNetWatcher.git
cd DotNetWatcher
```

2. Build and run:
```bash
dotnet build
dotnet run --project DotNetWatcher
```

Or open `DotNetWatcher.sln` in Visual Studio 2022+ and press F5.

### GitHub Token (Recommended)

To avoid GitHub API rate limiting (60 requests/hour anonymous vs 5,000/hour authenticated), set a `GITHUB_TOKEN` environment variable:

```powershell
# PowerShell (session)
$env:GITHUB_TOKEN = "your_github_token_here"

# Or set permanently via System Properties > Environment Variables
```

Your token only needs read access to public repositories (no scopes required for public repos).

If you want to create issues, then the token will need the `repo` scope for private repositories or `public_repo` for public repositories and be able to `create` issues.

##  Configuration

Edit `appsettings.json` to customize monitored versions and other settings:

```json
{
  "MonitoredVersions": [".NET 8.0", ".NET 9.0"],  // Add your versions
  "PollingInterval": "01:00:00",                  // Example: 1 hour
  "GitHubToken": "your_github_token_here"
}
```

You can also create an `appsettings.user.json` file (gitignored) for personal settings that won't be committed.

##  Usage

1. **Launch the application** - Advisories are loaded automatically on startup
2. **Review advisories** - Click on any advisory in the left panel to view details
3. **Filter results** - Use the search box, checkboxes, or dropdowns to narrow results
4. **Acknowledge** - Click "Mark as Acknowledged" to track reviewed advisories
5. **Create issues** - Click "Create Remediation Issue" to create a tracking issue in your repo

### Filter Options

| Filter | Description |
|--------|-------------|
| **Search** | Filter by CVE ID, issue number, title, or version |
| **Impact** | Show only advisories affecting your monitored versions |
| **Unacknowledged** | Show only advisories you haven't reviewed |
| **New since last run** | Show advisories published since your last session |
| **Version** | Filter by specific .NET version |
| **Sort by** | Newest, Impact, or CVE |

##  How It Works

1. **Data Source**: Queries the GitHub REST API for open issues with the `Security` label from [dotnet/announcements](https://github.com/dotnet/announcements/issues?q=is%3Aissue+state%3Aopen+label%3ASecurity)

2. **Version Extraction**: Parses issue labels (e.g., `.NET 8.0`, `.NET 9.0`) to determine affected versions

3. **CVE Parsing**: Extracts CVE identifiers from issue titles using regex pattern `CVE-\d{4}-\d+`

4. **Markdown Parsing**: Extracts structured sections (Executive Summary, Affected Software, Mitigation Factors, FAQ) from advisory body

5. **Impact Classification**:
   - **Impact** (Red) - Advisory affects one of your configured monitored versions
   - **Potential** (Amber) - Advisory doesn't match your versions but may still be relevant

6. **Action Recommendation**: Automatically suggests action based on impact and available mitigations:
   - **Upgrade** - Update to patched version
   - **Mitigate** - Workarounds available
   - **Monitor** - Keep watching

##  Project Structure

```
DotNetWatcher/
 Converters/
   - BooleanToVisibilityConverter.cs
   - MarkdownToFlowDocumentConverter.cs
   - PriorityToColorConverter.cs
 Helpers/
   - DotNetVersionParser.cs       # Parses .NET version strings
 Models/
   - AppConfiguration.cs
   - GitHubIssue.cs
   - SecurityAdvisory.cs
 Services/
   - AcknowledgementService.cs    # Tracks acknowledged advisories
   - ConfigurationLoader.cs       # Loads app configuration from JSON
   - GitHubAdvisoryService.cs     # Fetches advisories from GitHub
   - GitHubIssueService.cs        # Creates remediation issues
 Helpers/
   - DotNetVersionHelper.cs       # Detects installed .NET versions
 MainWindow.xaml                  # UI layout
 MainWindow.xaml.cs               # Main application logic
 App.xaml
```

## Roadmap

- [x] Real-time advisory monitoring
- [x] Impact classification
- [x] Acknowledgment tracking
- [x] Toast notifications
- [x] Custom dark theme title bar
- [x] Pagination support
- [x] System tray integration
- [x] Configurable polling interval with background refresh
- [ ] Settings UI for version management
- [ ] Export to CSV/JSON
- [ ] Start with Windows option

##  Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

##  License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

##  Acknowledgments

- [dotnet/announcements](https://github.com/dotnet/announcements) - Official .NET security advisory source
- [GitHub REST API](https://docs.github.com/en/rest) - API for fetching issues
- Visual Studio - UI design inspiration
