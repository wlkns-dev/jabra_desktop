using Avalonia;
namespace JabraDesktop.App;
internal static class Program
{
    internal static bool StartHidden { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        AppDiagnostics.Initialize();
        StartHidden = args.Contains("--autostart", StringComparer.Ordinal);
        var avaloniaArgs = args.Where(arg => !string.Equals(arg, "--autostart", StringComparison.Ordinal)).ToArray();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(avaloniaArgs);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect()
        .With(new X11PlatformOptions { RenderingMode = [X11RenderingMode.Software] })
        .LogToTrace();
}
