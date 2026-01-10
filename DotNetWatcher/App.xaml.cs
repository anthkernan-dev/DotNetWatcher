using Hardcodet.Wpf.TaskbarNotification;
using System.Windows;

namespace DotNetWatcher;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private bool _isExiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Force tray icon creation early (and validate resource lookups)
        _ = TrayIcon;

        ShowMainWindow();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            TrayIcon.Dispose();
        }
        catch
        {
            // Best-effort cleanup
        }

        base.OnExit(e);
    }

    private TaskbarIcon TrayIcon
        => (TaskbarIcon)Resources["TrayIcon"];

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            _mainWindow = new MainWindow();
            _mainWindow.RequestAppExit += MainWindow_RequestAppExit;
            _mainWindow.RequestRefresh += MainWindow_RequestRefresh;
        }

        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.Show();
        _mainWindow.Activate();
        _mainWindow.Focus();
    }

    private async void RefreshAdvisories()
    {
        if (_mainWindow is null)
        {
            ShowMainWindow();
        }

        if (_mainWindow is null)
        {
            return;
        }

        try
        {
            await _mainWindow.RefreshAdvisoriesFromTrayAsync();
        }
        catch
        {            
            TrayIcon.ShowBalloonTip("DotNetWatcher", "Refresh failed.", BalloonIcon.Error);
        }
    }

    private void ExitApplication()
    {
        _isExiting = true;

        if (_mainWindow is not null)
        {
            _mainWindow.SetExitRequested();
            _mainWindow.Close();
        }

        Shutdown();
    }

    private void TrayIcon_TrayMouseDoubleClick(object? sender, RoutedEventArgs e)
        => ShowMainWindow();

    private void TrayOpen_Click(object sender, RoutedEventArgs e)
        => ShowMainWindow();

    private void TrayRefresh_Click(object sender, RoutedEventArgs e)
        => RefreshAdvisories();

    private void TrayExit_Click(object sender, RoutedEventArgs e)
        => ExitApplication();

    private void MainWindow_RequestAppExit(object? sender, EventArgs e)
        => ExitApplication();

    private void MainWindow_RequestRefresh(object? sender, EventArgs e)
        => RefreshAdvisories();
}
