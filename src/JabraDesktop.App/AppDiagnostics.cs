using System.Diagnostics;
using System.Text;

namespace JabraDesktop.App;

internal static class AppDiagnostics
{
    public static void Initialize()
    {
        try
        {
            var stateDirectory = Environment.GetEnvironmentVariable("XDG_STATE_HOME");
            if (string.IsNullOrWhiteSpace(stateDirectory))
                stateDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "jabra-desktop");
            else
                stateDirectory = Path.Combine(stateDirectory, "jabra-desktop");

            Directory.CreateDirectory(stateDirectory);
            var logPath = Path.Combine(stateDirectory, "diagnostics.log");
            if (File.Exists(logPath) && new FileInfo(logPath).Length > 1_000_000)
                File.Move(logPath, logPath + ".1", overwrite: true);

            var writer = new StreamWriter(logPath, append: true, new UTF8Encoding(false)) { AutoFlush = true };
            Trace.Listeners.Add(new TextWriterTraceListener(writer));
            Trace.AutoFlush = true;
            Trace.WriteLine($"{DateTimeOffset.Now:O} event=application-start version={AppVersion.Display}");
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"{DateTimeOffset.Now:O} event=diagnostic-log-unavailable error={ex.GetType().Name}");
        }
    }
}
