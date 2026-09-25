using System.Diagnostics;

namespace JabraDesktop.App;

internal static class TrayAvailability
{
    static readonly (string BusName, string Interface)[] Watchers =
    [
        ("org.kde.StatusNotifierWatcher", "org.kde.StatusNotifierWatcher"),
        ("org.freedesktop.StatusNotifierWatcher", "org.freedesktop.StatusNotifierWatcher")
    ];

    public static async Task<bool> IsAvailableAsync(CancellationToken token)
    {
        foreach (var (watcher, watcherInterface) in Watchers)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "busctl",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            foreach (var argument in new[]
            {
                "--user", "--no-pager", "get-property", watcher,
                "/StatusNotifierWatcher", watcherInterface, "IsStatusNotifierHostRegistered"
            }) process.StartInfo.ArgumentList.Add(argument);

            var started = false;
            try
            {
                started = process.Start();
                if (!started) continue;
                var outputTask = process.StandardOutput.ReadToEndAsync(token);
                var exitTask = process.WaitForExitAsync(token);
                await exitTask.WaitAsync(TimeSpan.FromSeconds(1), token);
                var output = await outputTask;
                if (process.ExitCode == 0 && output.Trim().Equals("b true", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException or TimeoutException)
            {
                if (started && !process.HasExited)
                {
                    try { process.Kill(entireProcessTree: true); }
                    catch (InvalidOperationException) { }
                }
            }
            catch (OperationCanceledException)
            {
                if (started && !process.HasExited)
                {
                    try { process.Kill(entireProcessTree: true); }
                    catch (InvalidOperationException) { }
                }
                throw;
            }
        }
        return false;
    }
}
