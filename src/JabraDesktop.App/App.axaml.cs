using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using System.Globalization;
using System.Windows.Input;
using JabraDesktop.App.ViewModels;
using JabraDesktop.App.Views;
using JabraDesktop.Core;
using JabraDesktop.Jabra;

namespace JabraDesktop.App;

public partial class App : Application
{
    SingleInstanceCoordinator? instanceCoordinator;
    readonly AutostartManager autostartManager = new();
    readonly AppPreferencesStore preferencesStore = new();
    AppPreferences preferences = new();
    LocalizationService localization = new(LocalizationService.DetectLanguage(CultureInfo.CurrentUICulture));
    DeviceSession? session;
    MainViewModel? viewModel;
    MainWindow? mainWindow;
    DispatcherTimer? refreshTimer;
    DispatcherTimer? trayMonitorTimer;
    TrayIcon? trayIcon;
    NativeMenuItem? autostartMenuItem;
    bool activationPending;
    bool cleanupStarted;
    bool trayAvailable;
    bool shutdownRequested;
    bool trayProbeRunning;
    Task? cleanupTask;
    IClassicDesktopStyleApplicationLifetime? desktopLifetime;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        trayIcon = TrayIcon.GetIcons(this)?.FirstOrDefault();
        preferences = preferencesStore.Load();
        localization = new LocalizationService(preferences.Language ?? LocalizationService.DetectLanguage(CultureInfo.CurrentUICulture));
        RequestedThemeVariant = ToThemeVariant(preferences.Theme);
        RebuildTrayMenu();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktopLifetime = desktop;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
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

            var consentCoordinator = new ConsentStartupCoordinator(new TermsConsentGate(preferencesStore, ConsentTerms.CurrentVersion));
            var backend = await consentCoordinator.CreateBackendIfAcceptedAsync(
                _ => ShowConsentWindowAsync(desktop),
                () => new JabraBackend());
            if (backend is null)
            {
                await instanceCoordinator.DisposeAsync();
                desktop.Shutdown();
                return;
            }
            preferences = preferencesStore.Load();

            trayAvailable = await TrayAvailability.IsAvailableAsync(CancellationToken.None);
            if (trayIcon is not null) trayIcon.IsVisible = trayAvailable;
            if (trayAvailable) trayAvailable = await WaitForTrayStatusAsync(trayIcon, CancellationToken.None);
            RefreshAutostartMenu();

            session = new DeviceSession(backend);
            viewModel = new MainViewModel(session, action => Dispatcher.UIThread.Post(action), localization);
            mainWindow = new MainWindow { DataContext = viewModel };
            viewModel.ConfirmUnpair = mainWindow.ConfirmUnpair;
            viewModel.PromptBluetoothName = mainWindow.PromptBluetoothName;
            desktop.MainWindow = mainWindow;

            refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            refreshTimer.Tick += async (_, _) =>
            {
                if (viewModel is not null && !viewModel.IsScanning) await viewModel.RefreshAsync();
            };
            trayMonitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            trayMonitorTimer.Tick += async (_, _) => await RefreshTrayAvailabilityAsync();
            trayMonitorTimer.Start();
            mainWindow.Opened += async (_, _) =>
            {
                if (viewModel is not null) await viewModel.StartAsync();
                refreshTimer.Start();
            };
            mainWindow.Closing += (_, e) =>
            {
                if (shutdownRequested) return;
                e.Cancel = true;
                if (!trayAvailable)
                {
                    _ = ShutdownAndExitAsync();
                    return;
                }

                if (TrayStatusCompatibility.TrySetActive(trayIcon)) mainWindow.HideToTray();
                else trayAvailable = false;
            };

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

    async Task<bool> ShowConsentWindowAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var language = localization.Language;
        var window = new ConsentWindow(localization, language);
        var closed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => closed.TrySetResult(window.Accepted);
        desktop.MainWindow = window;
        window.Show();
        return await closed.Task;
    }

    void RebuildTrayMenu()
    {
        if (trayIcon is null) return;
        trayIcon.ToolTipText = $"{localization[UiText.AppName]} {AppVersion.Display}";
        var menu = new NativeMenu();
        menu.Items.Add(new NativeMenuItem { Header = localization[UiText.Open], Command = new TrayCommand(OpenFromTray) });
        var language = new NativeMenuItem { Header = localization[UiText.Language], Menu = new NativeMenu() };
        AddChoice(language.Menu, UiText.German, UiLanguage.German, preferences.Language ?? localization.Language);
        AddChoice(language.Menu, UiText.English, UiLanguage.English, preferences.Language ?? localization.Language);
        menu.Items.Add(language);
        var appearance = new NativeMenuItem { Header = localization[UiText.Appearance], Menu = new NativeMenu() };
        AddThemeChoice(appearance.Menu, ThemePreference.System);
        AddThemeChoice(appearance.Menu, ThemePreference.Light);
        AddThemeChoice(appearance.Menu, ThemePreference.Dark);
        menu.Items.Add(appearance);
        menu.Items.Add(new NativeMenuItemSeparator());
        autostartMenuItem = new NativeMenuItem
        {
            Header = autostartManager.IsAvailable ? localization[UiText.Autostart] : localization[UiText.AutostartUnavailable],
            ToggleType = NativeMenuItemToggleType.CheckBox,
            IsEnabled = autostartManager.IsAvailable,
            IsChecked = autostartManager.IsEnabled,
            Command = new TrayCommand(ToggleAutostart)
        };
        menu.Items.Add(autostartMenuItem);
        menu.Items.Add(new NativeMenuItem { Header = $"Jabra Desktop · {AppVersion.Display}", IsEnabled = false });
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(new NativeMenuItem { Header = localization[UiText.Quit], Command = new TrayCommand(QuitFromTray) });
        trayIcon.Menu = menu;
    }

    void AddChoice(NativeMenu menu, UiText label, UiLanguage language, UiLanguage selected)
    {
        menu.Items.Add(new NativeMenuItem
        {
            Header = $"{(selected == language ? "✓ " : "")} {localization[label]}",
            Command = new TrayCommand(async (_, _) => await SetLanguageAsync(language))
        });
    }

    void AddThemeChoice(NativeMenu menu, ThemePreference theme)
    {
        var label = theme switch
        {
            ThemePreference.System => UiText.System,
            ThemePreference.Light => UiText.Light,
            _ => UiText.Dark
        };
        menu.Items.Add(new NativeMenuItem
        {
            Header = $"{(preferences.Theme == theme ? "✓ " : "")} {localization[label]}",
            Command = new TrayCommand(async (_, _) => await SetThemeAsync(theme))
        });
    }

    async Task SetLanguageAsync(UiLanguage language)
    {
        var updated = preferences with { Language = language };
        try
        {
            preferencesStore.Save(updated);
            preferences = updated;
            localization.SetLanguage(language);
            RebuildTrayMenu();
        }
        catch (Exception error) { ShowAutostartError(localization[UiText.SettingsSaveFailure] + "\n" + error.Message); }
        await Task.CompletedTask;
    }

    async Task SetThemeAsync(ThemePreference theme)
    {
        var updated = preferences with { Theme = theme };
        try
        {
            preferencesStore.Save(updated);
            preferences = updated;
            RequestedThemeVariant = ToThemeVariant(theme);
            RebuildTrayMenu();
        }
        catch (Exception error) { ShowAutostartError(localization[UiText.SettingsSaveFailure] + "\n" + error.Message); }
        await Task.CompletedTask;
    }

    static ThemeVariant ToThemeVariant(ThemePreference theme) => theme switch
    {
        ThemePreference.Light => ThemeVariant.Light,
        ThemePreference.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default
    };

    void RefreshAutostartMenu()
    {
        if (autostartMenuItem is null) return;
        autostartMenuItem.IsEnabled = autostartManager.IsAvailable;
        autostartMenuItem.IsChecked = autostartManager.IsEnabled;
    }

    sealed class TrayCommand(Action<object?, EventArgs> action) : ICommand
    {
        event EventHandler? ICommand.CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => action(null, EventArgs.Empty);
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

    async void QuitFromTray(object? sender, EventArgs e)
    {
        await ShutdownAndExitAsync();
    }

    async Task RefreshTrayAvailabilityAsync()
    {
        if (trayProbeRunning || shutdownRequested) return;
        trayProbeRunning = true;
        try
        {
            var available = await TrayAvailability.IsAvailableAsync(CancellationToken.None);
            if (available)
            {
                if (trayIcon is not null) trayIcon.IsVisible = true;
                available = TrayStatusCompatibility.TrySetActive(trayIcon);
            }
            else if (trayIcon is not null)
            {
                trayIcon.IsVisible = false;
            }
            if (available == trayAvailable) return;
            trayAvailable = available;
            if (!available && mainWindow is { IsVisible: false }) mainWindow.ShowAndActivate();
        }
        finally { trayProbeRunning = false; }
    }

    static async Task<bool> WaitForTrayStatusAsync(TrayIcon? trayIcon, CancellationToken token)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (TrayStatusCompatibility.TrySetActive(trayIcon)) return true;
            await Task.Delay(TimeSpan.FromMilliseconds(100), token);
        }
        return false;
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
            ShowAutostartError(localization[UiText.AutostartFailureDetail] + "\n" + ex.Message, localization[UiText.AutostartErrorTitle]);
        }
    }

    void ShowAutostartError(string message, string? title = null)
    {
        var window = new Window
        {
            Title = title ?? localization[UiText.SettingsErrorTitle],
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
                    new Button { Content = localization[UiText.Close], HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right }
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
            Title = localization[UiText.StartupErrorTitle],
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 18,
                Children =
                {
                    new TextBlock { Text = error is IOException or UnauthorizedAccessException ? localization[UiText.ConsentStorageFailure] : error.Message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new Button { Content = localization[UiText.Close], HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right }
                }
            }
        };
        var close = (Button)((StackPanel)window.Content!).Children[1];
        close.Click += (_, _) => desktop.Shutdown();
        window.Closed += (_, _) => desktop.Shutdown();
        desktop.MainWindow = window;
        window.Show();
    }

    Task CleanupAsync() => cleanupTask ??= CleanupCoreAsync();

    async Task CleanupCoreAsync()
    {
        if (cleanupStarted) return;
        cleanupStarted = true;
        trayMonitorTimer?.Stop();
        refreshTimer?.Stop();
        viewModel?.Dispose();
        if (session is not null) await session.DisposeAsync();
        if (instanceCoordinator is not null) await instanceCoordinator.DisposeAsync();
    }

    async Task ShutdownAndExitAsync()
    {
        if (shutdownRequested) return;
        shutdownRequested = true;
        await CleanupAsync();
        mainWindow?.Close();
        desktopLifetime?.Shutdown();
    }
}
