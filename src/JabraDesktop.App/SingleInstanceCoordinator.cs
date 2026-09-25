using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;

namespace JabraDesktop.App;

internal sealed class SingleInstanceCoordinator : IAsyncDisposable
{
    const string ActivationMessage = "show";
    readonly string pipeName = $"jabra-desktop-{GetUserId()}";
    readonly CancellationTokenSource lifetime = new();
    FileStream? ownershipLock;
    Task? serverTask;
    bool disposed;

    [DllImport("libc", EntryPoint = "getuid")]
    static extern uint GetUserId();

    public async Task<bool> TryBecomePrimaryAsync(Func<Task> onActivate, CancellationToken token)
    {
        var lockPath = ResolveLockPath();
        var lockStream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        try
        {
            File.SetUnixFileMode(lockPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            lockStream.Lock(0, 1);
            ownershipLock = lockStream;
        }
        catch (IOException)
        {
            lockStream.Dispose();
            if (await NotifyExistingAsync(token)) return false;
            throw new IOException("Die laufende Jabra-Desktop-Instanz konnte nicht aktiviert werden.");
        }

        try
        {
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            serverTask = ServeAsync(onActivate, ready, firstPipeInstance: true, lifetime.Token);
            try
            {
                await ready.Task.WaitAsync(token);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                await IgnoreStartupFailureAsync(serverTask);
                if (await NotifyExistingAsync(token))
                {
                    ownershipLock.Dispose();
                    ownershipLock = null;
                    return false;
                }

                // The lock proves that no current version owns this name; an unbound pathname is stale.
                ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                serverTask = ServeAsync(onActivate, ready, firstPipeInstance: false, lifetime.Token);
                await ready.Task.WaitAsync(token);
                return true;
            }
        }
        catch
        {
            ownershipLock?.Dispose();
            ownershipLock = null;
            throw;
        }
    }

    static string ResolveLockPath()
    {
        var runtimeDirectory = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (!string.IsNullOrWhiteSpace(runtimeDirectory) && Path.IsPathFullyQualified(runtimeDirectory)
            && Directory.Exists(runtimeDirectory))
            return Path.Combine(runtimeDirectory, $"jabra-desktop-{GetUserId()}.lock");

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
            throw new InvalidOperationException("Das Benutzerverzeichnis konnte nicht bestimmt werden.");
        var privateDirectory = Path.Combine(home, ".cache", "jabra-desktop");
        Directory.CreateDirectory(privateDirectory);
        File.SetUnixFileMode(privateDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return Path.Combine(privateDirectory, $"jabra-desktop-{GetUserId()}.lock");
    }

    async Task ServeAsync(Func<Task> onActivate, TaskCompletionSource ready, bool firstPipeInstance, CancellationToken token)
    {
        try
        {
            var options = PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly;
            if (firstPipeInstance) options |= PipeOptions.FirstPipeInstance;
            await using var server = new NamedPipeServerStream(
                pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, options);

            var connection = server.WaitForConnectionAsync(token);
            ready.TrySetResult();
            while (true)
            {
                await connection;
                try
                {
                    using var reader = new StreamReader(server, Encoding.UTF8, false, 1024, leaveOpen: true);
                    if (await reader.ReadLineAsync(token) == ActivationMessage)
                        await onActivate();
                }
                catch (IOException)
                {
                    // A broken activation client must not take down the primary instance.
                }

                if (server.IsConnected) server.Disconnect();
                connection = server.WaitForConnectionAsync(token);
            }
        }
        catch (Exception ex)
        {
            ready.TrySetException(ex);
            if (ex is not OperationCanceledException) throw;
        }
    }

    async Task<bool> NotifyExistingAsync(CancellationToken token)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await using var client = new NamedPipeClientStream(
                    ".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
                await client.ConnectAsync(500, token);
                await using var writer = new StreamWriter(client, Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true };
                await writer.WriteLineAsync(ActivationMessage);
                return true;
            }
            catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
            {
                if (attempt == 2) return false;
                await Task.Delay(100, token);
            }
        }
        return false;
    }

    static async Task IgnoreStartupFailureAsync(Task? task)
    {
        if (task is null) return;
        try { await task; }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (OperationCanceledException) { }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        lifetime.Cancel();
        if (serverTask is not null)
        {
            try { await serverTask; }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        ownershipLock?.Dispose();
        ownershipLock = null;
        lifetime.Dispose();
    }
}
