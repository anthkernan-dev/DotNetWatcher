using DotNetWatcher.Models;
using DotNetWatcher.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace DotNetWatcher;

public partial class SettingsWindow : Window, INotifyPropertyChanged
{
    private readonly AppConfiguration _originalConfiguration;
    public AppConfiguration Configuration { get; private set; }
    public bool SettingsChanged { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public SettingsWindow(AppConfiguration currentConfiguration)
    {
        InitializeComponent();
        _originalConfiguration = currentConfiguration;
        Configuration = currentConfiguration;
        LoadSettings();
    }

    private void LoadSettings()
    {
        // Load monitored versions as comma-separated string
        MonitoredVersionsTextBox.Text = string.Join(", ", Configuration.MonitoredVersions);
        
        // Load polling interval
        PollingIntervalTextBox.Text = Configuration.PollingIntervalHours.ToString();
        
        // Load startup options
        StartWithWindowsCheckBox.IsChecked = Configuration.StartWithWindows;
        StartMinimizedCheckBox.IsChecked = Configuration.StartMinimized;
        
        // Load GitHub integration settings
        TargetRepoOwnerTextBox.Text = Configuration.TargetRepoOwner;
        TargetRepoNameTextBox.Text = Configuration.TargetRepoName;
        
        // Load advisory source
        GitHubAdvisoryUriTextBox.Text = Configuration.GitHubAdvisoryUri;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateSettings())
        {
            return;
        }

        try
        {
            // Parse monitored versions
            var versions = MonitoredVersionsTextBox.Text
                .Split(',')
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList();

            if (versions.Count == 0)
            {
                MessageBox.Show(
                    "Please specify at least one .NET version to monitor.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Parse polling interval
            if (!int.TryParse(PollingIntervalTextBox.Text, out int pollingInterval) || pollingInterval < 1)
            {
                MessageBox.Show(
                    "Polling interval must be a positive number.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Create new configuration
            Configuration = new AppConfiguration
            {
                MonitoredVersions = versions,
                PollingIntervalHours = pollingInterval,
                StartWithWindows = StartWithWindowsCheckBox.IsChecked ?? false,
                StartMinimized = StartMinimizedCheckBox.IsChecked ?? false,
                TargetRepoOwner = TargetRepoOwnerTextBox.Text.Trim(),
                TargetRepoName = TargetRepoNameTextBox.Text.Trim(),
                GitHubAdvisoryUri = GitHubAdvisoryUriTextBox.Text.Trim(),
                GitHubToken = _originalConfiguration.GitHubToken // Preserve token from environment/config
            };

            // Save to file
            SettingsService.SaveConfiguration(Configuration);

            SettingsChanged = true;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to save settings: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private bool ValidateSettings()
    {
        // Validate monitored versions field is not empty
        if (string.IsNullOrWhiteSpace(MonitoredVersionsTextBox.Text))
        {
            MessageBox.Show(
                "Please specify at least one .NET version to monitor.",
                "Validation Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            MonitoredVersionsTextBox.Focus();
            return false;
        }

        // Validate polling interval
        if (string.IsNullOrWhiteSpace(PollingIntervalTextBox.Text))
        {
            MessageBox.Show(
                "Please specify a polling interval.",
                "Validation Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            PollingIntervalTextBox.Focus();
            return false;
        }

        // Validate GitHub advisory URI
        if (string.IsNullOrWhiteSpace(GitHubAdvisoryUriTextBox.Text))
        {
            MessageBox.Show(
                "Please specify a GitHub advisory URI.",
                "Validation Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            GitHubAdvisoryUriTextBox.Focus();
            return false;
        }

        // Validate URI format
        if (!Uri.TryCreate(GitHubAdvisoryUriTextBox.Text, UriKind.Absolute, out _))
        {
            MessageBox.Show(
                "GitHub advisory URI must be a valid URL.",
                "Validation Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            GitHubAdvisoryUriTextBox.Focus();
            return false;
        }

        return true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsChanged = false;
        DialogResult = false;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsChanged = false;
        DialogResult = false;
        Close();
    }
}
