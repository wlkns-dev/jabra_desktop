using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;

namespace JabraDesktop.App;

internal sealed class SingleInstanceCoordinator : IAsyncDisposable
{
    const string ActivationMessage = "show";
    readonly string pipeName = $"jabra-desktop-{GetUserId()}";
    readonly CancellationTokenSource lifetime = new();
    Task? serverTask;

    [DllImport("libc", EntryPoint = "getuid")]
    static extern uint GetUserId();

    public async Task<bool> TryBecomePrimaryAsync(Func<Task> onActivate, CancellationToken token)
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        serverTask = ServeAsync(onActivate, ready, lifetime.Token);

        try
        {
            await ready.Task.WaitAsync(token);
            return true;
        }
        catch (IOException)
        {
            lifetime.Cancel();
            try { await serverTask; }
            catch (IOException) { }
            catch (OperationCanceledException) { }

            if (await NotifyExistingAsync(token)) return false;
            throw new IOException("Eine zweite Jabra-Desktop-Instanz läuft, konnte aber nicht aktiviert werden.");
        }
    }

    async Task ServeAsync(Func<Task> onActivate, TaskCompletionSource ready, CancellationToken token)
    {
        try
        {
            await using var server = new NamedPipeServerStream(
                pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

            var connection = server.WaitForConnectionAsync(token);
            ready.TrySetResult();
            while (true)
            {
                await connection;
                using (var reader = new StreamReader(server, Encoding.UTF8, false, 1024, leaveOpen: true))
                {
                    var command = await reader.ReadLineAsync(token);
                    if (command == ActivationMessage) await onActivate();
                }
                server.Disconnect();
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
            catch (TimeoutException) when (attempt < 2)
            {
                await Task.Delay(100, token);
            }
            catch (IOException) when (attempt < 2)
            {
                await Task.Delay(100, token);
            }
        }
        return false;
    }

    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        if (serverTask is not null)
        {
            try { await serverTask; }
            catch (OperationCanceledException) { }
            catch (IOException) { }
        }
        lifetime.Dispose();
    }
}
