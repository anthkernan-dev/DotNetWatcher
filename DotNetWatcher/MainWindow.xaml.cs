using DotNetWatcher.Models;
using DotNetWatcher.Services;
using DotNetWatcher.Helpers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Input;
using System.Drawing.Imaging.Effects;
using Microsoft.Extensions.Configuration;


namespace DotNetWatcher;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly GitHubAdvisoryService _advisoryService;
    private readonly AcknowledgementService _acknowledgementService;
    private readonly GitHubIssueService _issueService;
    private readonly AppConfiguration _configuration;
    private SecurityAdvisory? _selectedAdvisory;
    private readonly DispatcherTimer _toastTimer;
    private readonly DispatcherTimer _pollingTimer;
    private HashSet<int> _knownIssueNumbers = [];
    private readonly DotNetVersionParser _versionParser = new();    

    private DateTimeOffset _lastRunUtc;

    public ObservableCollection<SecurityAdvisory> Advisories { get; } = [];

    public string AppVersionDisplay { get; } = GetAppVersionDisplay();

    private static string GetAppVersionDisplay()
    {
        var assembly = typeof(MainWindow).Assembly;

        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        return assembly.GetName().Version?.ToString() ?? "Unknown";
    }

    public ICollectionView AdvisoriesView { get; }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!string.Equals(_searchText, value, StringComparison.Ordinal))
            {
                _searchText = value;
                OnPropertyChanged();
                AdvisoriesView.Refresh();
            }
        }
    }

    private void AdvisoriesListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { }

    private bool _filterImpactOnly;
    public bool FilterImpactOnly
    {
        get => _filterImpactOnly;
        set
        {
            if (_filterImpactOnly != value)
            {
                _filterImpactOnly = value;
                OnPropertyChanged();
                AdvisoriesView.Refresh();
            }
        }
    }

    private bool _filterUnacknowledgedOnly;
    public bool FilterUnacknowledgedOnly
    {
        get => _filterUnacknowledgedOnly;
        set
        {
            if (_filterUnacknowledgedOnly != value)
            {
                _filterUnacknowledgedOnly = value;
                OnPropertyChanged();
                AdvisoriesView.Refresh();
            }
        }
    }

    private bool _filterNewSinceLastRunOnly;
    public bool FilterNewSinceLastRunOnly
    {
        get => _filterNewSinceLastRunOnly;
        set
        {
            if (_filterNewSinceLastRunOnly != value)
            {
                _filterNewSinceLastRunOnly = value;
                OnPropertyChanged();
                AdvisoriesView.Refresh();
            }
        }
    }

    private string? _selectedVersionFilter;
    public string? SelectedVersionFilter
    {
        get => _selectedVersionFilter;
        set
        {
            if (!string.Equals(_selectedVersionFilter, value, StringComparison.Ordinal))
            {
                _selectedVersionFilter = value;
                OnPropertyChanged();
                AdvisoriesView.Refresh();
            }
        }
    }

    public ObservableCollection<string> VersionFilterOptions { get; } = ["(All)"];

    private AdvisorySortMode _sortMode = AdvisorySortMode.Newest;
    public AdvisorySortMode SortMode
    {
        get => _sortMode;
        set
        {
            if (_sortMode != value)
            {
                _sortMode = value;
                OnPropertyChanged();
                ApplySort();
            }
        }
    }

    public Array SortModeOptions => Enum.GetValues(typeof(AdvisorySortMode));

    public SecurityAdvisory? SelectedAdvisory
    {
        get => _selectedAdvisory;
        set
        {
            if (_selectedAdvisory != value)
            {
                _selectedAdvisory = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedAdvisory));
            }
        }
    }

    public bool HasSelectedAdvisory => SelectedAdvisory is not null;

    private int _impactCount;
    private int _potentialImpact;
    private int _unacknowledgedCount;

    public int ImpactCount
    {
        get => _impactCount;
        set { _impactCount = value; OnPropertyChanged(); }
    }

    public int PotentialImpact
    {
        get => _potentialImpact;
        set { _potentialImpact = value; OnPropertyChanged(); }
    }

    public int UnacknowledgedCount
    {
        get => _unacknowledgedCount;
        set { _unacknowledgedCount = value; OnPropertyChanged(); }
    }

    // Status bar properties for dotnet info
    private string _dotnetVersion = "Loading...";
    public string DotnetVersion
    {
        get => _dotnetVersion;
        set { _dotnetVersion = value; OnPropertyChanged(); }
    }

    private string _dotnetRuntimeInfo = string.Empty;
    public string DotnetRuntimeInfo
    {
        get => _dotnetRuntimeInfo;
        set { _dotnetRuntimeInfo = value; OnPropertyChanged(); }
    }

    // Loading indicator
    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    private string _loadingMessage = "Loading advisories...";
    public string LoadingMessage
    {
        get => _loadingMessage;
        set { _loadingMessage = value; OnPropertyChanged(); }
    }

    // Toast notification
    private bool _isToastVisible;
    public bool IsToastVisible
    {
        get => _isToastVisible;
        set { _isToastVisible = value; OnPropertyChanged(); }
    }

    private string _toastMessage = string.Empty;
    public string ToastMessage
    {
        get => _toastMessage;
        set { _toastMessage = value; OnPropertyChanged(); }
    }

    private string _toastDetails = string.Empty;
    public string ToastDetails
    {
        get => _toastDetails;
        set { _toastDetails = value; OnPropertyChanged(); }
    }

    // Dynamic monitored versions display
    public string MonitoredVersionsDisplay =>
        string.Join(", ", _configuration.MonitoredVersions ?? [".NET 8.0"]);

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool _allowClose;

    public event EventHandler? RequestAppExit;
    public event EventHandler? RequestRefresh;

    private bool _exitRequested;
    private bool _hasShownTrayHint;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _configuration = ConfigurationLoader.Load();
        _advisoryService = new GitHubAdvisoryService(_configuration);
        _acknowledgementService = new AcknowledgementService();
        _issueService = new GitHubIssueService(
            _configuration.GitHubToken ?? string.Empty,
            _configuration.TargetRepoOwner,
            _configuration.TargetRepoName);

        _lastRunUtc = _acknowledgementService.GetLastRunUtc();

        AdvisoriesView = CollectionViewSource.GetDefaultView(Advisories);
        AdvisoriesView.Filter = AdvisoryFilter;

        ApplySort();

        // Toast auto-hide timer
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _toastTimer.Tick += (s, e) => { HideToast(); };

        // Polling timer for automatic advisory refresh
        _pollingTimer = new DispatcherTimer { Interval = _configuration.PollingInterval };
        _pollingTimer.Tick += async (s, e) => await PollForAdvisoriesAsync();

        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
        StateChanged += MainWindow_StateChanged;

        SelectedVersionFilter = "(All)";

        Closing += MainWindow_Closing;
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _pollingTimer.Stop();
        _acknowledgementService.SetLastRunUtc(DateTimeOffset.UtcNow);
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        IsLoading = true;
        LoadingMessage = "Loading advisories...";

        try
        {
            // Load dotnet info in parallel with advisories
            var dotnetInfoTask = LoadDotnetInfoAsync();
            var advisoriesTask = LoadAdvisoriesAsync(isInitialLoad: true);

            await Task.WhenAll(dotnetInfoTask, advisoriesTask);

            _pollingTimer.Start();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadDotnetInfoAsync()
    {
        try
        {
            var info = await GetDotnetInfoAsync();
            DotnetVersion = info.SdkVersion;
            DotnetRuntimeInfo = info.RuntimeInfo;
        }
        catch
        {
            DotnetVersion = "Unknown";
            DotnetRuntimeInfo = "Unable to retrieve .NET information";
        }
    }

    private static async Task<(string SdkVersion, string RuntimeInfo)> GetDotnetInfoAsync()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "--info",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process is null)
            return ("Unknown", "Failed to start dotnet process");

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        // Parse the output to extract key information
        var sdkVersion = "Unknown";
        var runtimes = new List<string>();

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var inRuntimeSection = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Look for SDK version
            if (trimmed.StartsWith("Version:", StringComparison.OrdinalIgnoreCase) && !inRuntimeSection)
            {
                sdkVersion = trimmed.Replace("Version:", "", StringComparison.OrdinalIgnoreCase).Trim();
                continue;
            }

            // Track sections
            if (trimmed.Contains(".NET runtimes installed", StringComparison.OrdinalIgnoreCase))
            {
                inRuntimeSection = true;
                continue;
            }

            if (trimmed.Contains(".NET SDKs installed", StringComparison.OrdinalIgnoreCase))
            {
                inRuntimeSection = false;
                continue;
            }

            // Collect runtime info
            if (inRuntimeSection && trimmed.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase))
            {
                // Extract just the runtime name and version (e.g., "Microsoft.NETCore.App 8.0.0")
                var match = Regex.Match(trimmed, @"(Microsoft\.\S+)\s+(\d+\.\d+\.\d+)");
                if (match.Success)
                {
                    var runtimeName = match.Groups[1].Value;
                    var version = match.Groups[2].Value;

                    // Shorten the runtime names for display
                    var shortName = runtimeName switch
                    {
                        "Microsoft.NETCore.App" => ".NET",
                        "Microsoft.AspNetCore.App" => "ASP.NET",
                        "Microsoft.WindowsDesktop.App" => "Desktop",
                        _ => runtimeName.Replace("Microsoft.", "")
                    };

                    runtimes.Add($"{shortName} {version}");
                }
            }

            // Stop parsing after certain sections
            if (trimmed.Contains("Other architectures", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("Environment variables", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }

        var runtimeInfo = runtimes.Count > 0
            ? string.Join(" | ", runtimes.Distinct())
            : "No runtimes found";

        return ($"SDK {sdkVersion}", runtimeInfo);
    }

    private bool AdvisoryFilter(object obj)
    {
        if (obj is not SecurityAdvisory advisory)
            return false;

        if (FilterImpactOnly && advisory.Impact != ImpactStatus.Impact)
            return false;

        if (FilterUnacknowledgedOnly && advisory.IsAcknowledged)
            return false;

        if (FilterNewSinceLastRunOnly)
        {
            var createdUtc = new DateTimeOffset(advisory.CreatedAt, TimeSpan.Zero);
            if (createdUtc <= _lastRunUtc)
                return false;
        }

        if (!string.IsNullOrWhiteSpace(SelectedVersionFilter) &&
            !string.Equals(SelectedVersionFilter, "(All)", StringComparison.Ordinal))
        {
            if (advisory.AffectedVersions is null ||
                !advisory.AffectedVersions.Contains(SelectedVersionFilter, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var query = SearchText.Trim();

            var matchesCve = advisory.CveId?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;
            var matchesTitle = advisory.Title?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;
            var matchesVersions = advisory.AffectedVersions?.Any(v => v.Contains(query, StringComparison.OrdinalIgnoreCase)) == true;

            // Also match by issue number
            var matchesIssueNumber = advisory.IssueNumber.ToString().Contains(query, StringComparison.OrdinalIgnoreCase);

            if (!matchesCve && !matchesTitle && !matchesVersions && !matchesIssueNumber)
                return false;
        }

        return true;
    }

    private void ApplySort()
    {
        AdvisoriesView.SortDescriptions.Clear();

        switch (SortMode)
        {
            case AdvisorySortMode.Impact:
                AdvisoriesView.SortDescriptions.Add(new SortDescription(nameof(SecurityAdvisory.Impact), ListSortDirection.Descending));
                AdvisoriesView.SortDescriptions.Add(new SortDescription(nameof(SecurityAdvisory.CreatedAt), ListSortDirection.Descending));
                break;

            case AdvisorySortMode.CVE:
                AdvisoriesView.SortDescriptions.Add(new SortDescription(nameof(SecurityAdvisory.CveId), ListSortDirection.Descending));
                AdvisoriesView.SortDescriptions.Add(new SortDescription(nameof(SecurityAdvisory.CreatedAt), ListSortDirection.Descending));
                break;

            default:
                AdvisoriesView.SortDescriptions.Add(new SortDescription(nameof(SecurityAdvisory.CreatedAt), ListSortDirection.Descending));
                break;
        }

        AdvisoriesView.Refresh();
    }

    private async Task PollForAdvisoriesAsync()
    {
        try
        {
            await LoadAdvisoriesAsync(isInitialLoad: false);
        }
        catch (HttpRequestException)
        {
            // Silent fail, no need to interrupt the user experience
        }
        catch (Exception)
        {
            MessageBox.Show("An unexpected error occurred while polling for advisories.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task LoadAdvisoriesAsync(bool isInitialLoad = false)
    {
        try
        {
            var advisories = await _advisoryService.FetchAdvisoriesAsync();

            // Check for new advisories (only after initial load)
            var newAdvisories = new List<SecurityAdvisory>();
            if (!isInitialLoad && _knownIssueNumbers.Count > 0)
            {
                newAdvisories = advisories
                    .Where(a => !_knownIssueNumbers.Contains(a.IssueNumber))
                    .ToList();
            }

            Advisories.Clear();
            _knownIssueNumbers.Clear();

            foreach (var advisory in advisories)
            {
                advisory.IsAcknowledged = _acknowledgementService.IsAcknowledged(advisory.IssueNumber);
                advisory.PropertyChanged += Advisory_PropertyChanged;
                Advisories.Add(advisory);
                _knownIssueNumbers.Add(advisory.IssueNumber);
            }

            UpdateVersionFilterOptions(advisories);
            UpdateCounts();

            if (Advisories.Count > 0 && SelectedAdvisory is null)
            {
                SelectedAdvisory = Advisories[0];
                AdvisoriesListView.SelectedItem = Advisories[0];
            }

            AdvisoriesView.Refresh();

            // Show toast for new advisories
            if (newAdvisories.Count > 0)
            {
                var impactCount = newAdvisories.Count(a => a.Impact == ImpactStatus.Impact);
                ShowNewAdvisoriesNotification(newAdvisories.Count, impactCount);
            }
        }
        catch (HttpRequestException ex)
        {
            MessageBox.Show($"Failed to fetch security advisories: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateVersionFilterOptions(List<SecurityAdvisory> advisories)
    {
        var uniqueVersions = advisories
            .SelectMany(a => a.AffectedVersions ?? [])
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var versions = _versionParser.SortVersions(uniqueVersions).ToList();

        VersionFilterOptions.Clear();
        VersionFilterOptions.Add("(All)");

        foreach (var version in versions)
        {
            VersionFilterOptions.Add(version);
        }

        if (SelectedVersionFilter is null ||
            !VersionFilterOptions.Any(v => v.Equals(SelectedVersionFilter, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedVersionFilter = "(All)";
        }
    }

    private void Advisory_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SecurityAdvisory.IsAcknowledged))
        {
            UpdateCounts();
            AdvisoriesView.Refresh();
            OnPropertyChanged(nameof(SelectedAdvisory));
        }
    }

    private void UpdateCounts()
    {
        ImpactCount = Advisories.Count(a => a.Impact == ImpactStatus.Impact);
        PotentialImpact = Advisories.Count(a => a.Impact == ImpactStatus.Potential);
        UnacknowledgedCount = Advisories.Count(a => !a.IsAcknowledged);
    }

    #region Toast Notification

    private void ShowNewAdvisoriesNotification(int count, int impactCount)
    {
        ToastMessage = count == 1
            ? "🔔 1 New Security Advisory"
            : $"🔔 {count} New Security Advisories";

        ToastDetails = impactCount > 0
            ? $"{impactCount} affecting your monitored versions"
            : "Review recommended";

        IsToastVisible = true;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void HideToast()
    {
        _toastTimer.Stop();
        IsToastVisible = false;
    }

    private void ToastClose_Click(object sender, RoutedEventArgs e)
    {
        HideToast();
    }

    #endregion

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void Acknowledge_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedAdvisory is null) return;

        _acknowledgementService.Acknowledge(SelectedAdvisory.IssueNumber);
        SelectedAdvisory.IsAcknowledged = true;
    }

    private async void CreateIssue_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedAdvisory is null) return;

        try
        {
            if (sender is Button button)
            {
                button.IsEnabled = false;
                button.Content = "⏳ Creating...";
            }

            var result = await _issueService.CreateRemediationIssueAsync(SelectedAdvisory);

            if (result.Success)
            {
                var openIssue = MessageBox.Show(
                    $"Issue #{result.IssueNumber} created successfully!\n\nWould you like to open it in your browser?",
                    "Issue Created",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (openIssue == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(result.IssueUrl) { UseShellExecute = true });
                }
            }
            else
            {
                MessageBox.Show(result.ErrorMessage, "Failed to Create Issue", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (sender is Button button)
            {
                button.IsEnabled = true;
                button.Content = "🔧 Create Remediation Issue";
            }
        }
    }

    #region Title Bar Button Handlers

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_configuration)
        {
            Owner = this
        };

        if (settingsWindow.ShowDialog() == true && settingsWindow.SettingsChanged)
        {
            // Settings were saved, show message and prompt to restart
            var result = MessageBox.Show(
                "Settings have been saved. The application needs to restart to apply the changes.\n\nRestart now?",
                "Settings Saved",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Restart the application
                System.Diagnostics.Process.Start(
                    System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName!);
                Application.Current.Shutdown();
            }
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        // Minimize to tray.
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    #endregion

    public void SetExitRequested()
    {
        _exitRequested = true;
    }

    public async Task RefreshAdvisoriesFromTrayAsync()
    {
        IsLoading = true;
        LoadingMessage = "Refreshing advisories...";

        try
        {
            await LoadAdvisoriesAsync(isInitialLoad: false);

            if(!_pollingTimer.IsEnabled)
            {
                _pollingTimer.Start();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        // If tray menu "Exit" was chosen, allow close.
        if (_exitRequested)
        {
            return;
        }

        // Otherwise hide-to-tray.
        e.Cancel = true;
        Hide();

        if (!_hasShownTrayHint)
        {
            _hasShownTrayHint = true;
            MessageBox.Show(
                "DotNetWatcher is still running in the system tray.\n\nUse the tray icon to Open, Refresh, or Exit.",
                "DotNetWatcher",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    internal void ExitApplication()
    {
        _allowClose = true;
        Close();
    }
}

public enum AdvisorySortMode
{
    Newest,
    Impact,
    CVE,
}