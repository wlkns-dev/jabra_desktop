using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using JabraDesktop.App.ViewModels;
using JabraDesktop.App.Views;
using JabraDesktop.Core;
using JabraDesktop.Jabra;

namespace JabraDesktop.App;

public partial class App : Application
{
    SingleInstanceCoordinator? instanceCoordinator;
    readonly AutostartManager autostartManager = new();
    DeviceSession? session;
    MainViewModel? viewModel;
    MainWindow? mainWindow;
    DispatcherTimer? refreshTimer;
    TrayIcon? trayIcon;
    NativeMenuItem? autostartMenuItem;
    bool activationPending;
    bool cleanupStarted;
    bool trayAvailable;
    bool shutdownRequested;
    IClassicDesktopStyleApplicationLifetime? desktopLifetime;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        trayIcon = TrayIcon.GetIcons(this)?.FirstOrDefault();
        autostartMenuItem = trayIcon?.Menu?.Items.OfType<NativeMenuItem>()
            .FirstOrDefault(item => Equals(item.Header, "Autostart"));
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktopLifetime = desktop;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += async (_, _) => await CleanupAsync();
            _ = StartDesktopAsync(desktop);
        }
        base.OnFrameworkInitializationCompleted();
    }

    async Task StartDesktopAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        instanceCoordinator = new SingleInstanceCoordinator();
        try
        {
            var primary = await instanceCoordinator.TryBecomePrimaryAsync(QueueActivation, CancellationToken.None);
            if (!primary)
            {
                await instanceCoordinator.DisposeAsync();
                desktop.Shutdown();
                return;
            }

            trayAvailable = await TrayAvailability.IsAvailableAsync(CancellationToken.None);
            if (trayIcon is not null) trayIcon.IsVisible = trayAvailable;
            if (autostartMenuItem is not null)
            {
                autostartMenuItem.IsEnabled = autostartManager.IsAvailable;
                autostartMenuItem.IsChecked = autostartManager.IsEnabled;
            }

            session = new DeviceSession(new JabraBackend());
            viewModel = new MainViewModel(session, action => Dispatcher.UIThread.Post(action));
            mainWindow = new MainWindow { DataContext = viewModel };
            viewModel.ConfirmUnpair = mainWindow.ConfirmUnpair;
            desktop.MainWindow = mainWindow;

            refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            refreshTimer.Tick += async (_, _) =>
            {
                if (viewModel?.CanScan == true) await viewModel.RefreshAsync();
            };
            mainWindow.Opened += async (_, _) =>
            {
                if (viewModel is not null) await viewModel.StartAsync();
                refreshTimer.Start();
            };
            mainWindow.Closing += (_, e) =>
            {
                if (!trayAvailable || shutdownRequested) return;
                e.Cancel = true;
                mainWindow.HideToTray();
            };
            mainWindow.Closed += (_, _) => desktop.Shutdown();

            mainWindow.Show();
            if (Program.StartHidden && trayAvailable) mainWindow.HideToTray();
            if (activationPending) ActivateMainWindow();
        }
        catch (Exception ex)
        {
            await instanceCoordinator.DisposeAsync();
            ShowStartupError(desktop, ex);
        }
    }

    Task QueueActivation()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (mainWindow is null) activationPending = true;
            else ActivateMainWindow();
        });
        return Task.CompletedTask;
    }

    void ActivateMainWindow()
    {
        if (mainWindow is null) return;
        mainWindow.ShowAndActivate();
    }

    void OpenFromTray(object? sender, EventArgs e) => ActivateMainWindow();

    void QuitFromTray(object? sender, EventArgs e)
    {
        shutdownRequested = true;
        mainWindow?.Close();
        desktopLifetime?.Shutdown();
    }

    void ToggleAutostart(object? sender, EventArgs e)
    {
        var previousState = autostartManager.IsEnabled;
        try
        {
            autostartManager.SetEnabled(!previousState);
            if (autostartMenuItem is not null) autostartMenuItem.IsChecked = autostartManager.IsEnabled;
        }
        catch (Exception ex)
        {
            if (autostartMenuItem is not null) autostartMenuItem.IsChecked = previousState;
            ShowAutostartError(ex.Message);
        }
    }

    void ShowAutostartError(string message)
    {
        var window = new Window
        {
            Title = "Autostart konnte nicht geändert werden",
            Width = 420,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 18,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new Button { Content = "Schließen", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right }
                }
            }
        };
        ((Button)((StackPanel)window.Content!).Children[1]).Click += (_, _) => window.Close();
        window.Show();
    }

    void ShowStartupError(IClassicDesktopStyleApplicationLifetime desktop, Exception error)
    {
        var window = new Window
        {
            Title = "Jabra Desktop konnte nicht starten",
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 18,
                Children =
                {
                    new TextBlock { Text = error.Message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new Button { Content = "Schließen", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right }
                }
            }
        };
        var close = (Button)((StackPanel)window.Content!).Children[1];
        close.Click += (_, _) => desktop.Shutdown();
        window.Closed += (_, _) => desktop.Shutdown();
        desktop.MainWindow = window;
        window.Show();
    }

    async Task CleanupAsync()
    {
        if (cleanupStarted) return;
        cleanupStarted = true;
        refreshTimer?.Stop();
        viewModel?.Dispose();
        if (session is not null) await session.DisposeAsync();
        if (instanceCoordinator is not null) await instanceCoordinator.DisposeAsync();
    }
}
