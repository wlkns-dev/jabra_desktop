using Jabra.NET.Sdk.Core;
using Jabra.NET.Sdk.Core.Types;

namespace JabraDesktop.Jabra;

internal static class DevicePropertiesInitialization
{
    static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    public static Task<bool> TryInitializeAsync(IDevicePropertiesReader reader, IApi api, CancellationToken token) =>
        TryInitializeAsync(reader, api, token, DefaultTimeout);

    public static async Task<bool> TryInitializeAsync(IDevicePropertiesReader reader, IApi api, CancellationToken token, TimeSpan timeout)
    {
        var initialization = Task.Run(() => reader.InitializeAsync(api, token), CancellationToken.None);
        try
        {
            await initialization.WaitAsync(timeout, token);
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            Observe(initialization);
            throw;
        }
        catch (TimeoutException)
        {
            Observe(initialization);
            return false;
        }
        catch
        {
            return false;
        }
    }

    static void Observe(Task task) => _ = task.ContinueWith(completed => _ = completed.Exception,
        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
}
