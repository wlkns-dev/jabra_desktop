using Jabra.NET.Sdk.Core;
using Jabra.NET.Sdk.Core.Types;

namespace JabraDesktop.Jabra;

internal static class DevicePropertiesInitialization
{
    public static async Task<bool> TryInitializeAsync(IDevicePropertiesReader reader, IApi api, CancellationToken token)
    {
        try
        {
            await reader.InitializeAsync(api, token);
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }
}
