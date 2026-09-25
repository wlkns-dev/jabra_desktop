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

public class App : Application
{
    SingleInstanceCoordinator? instanceCoordinator;
    DeviceSession? session;
    MainViewModel? viewModel;
    MainWindow? mainWindow;
    DispatcherTimer? refreshTimer;
    bool activationPending;
    bool cleanupStarted;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
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
            mainWindow.Closed += (_, _) => desktop.Shutdown();

            mainWindow.Show();
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
        mainWindow.Show();
        if (mainWindow.WindowState == WindowState.Minimized) mainWindow.WindowState = WindowState.Normal;
        mainWindow.Activate();
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
