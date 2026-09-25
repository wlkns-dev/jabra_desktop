using JabraDesktop.Core;
using Jabra.NET.Sdk.DevicePairing;
namespace JabraDesktop.Jabra;
public static class DeviceMapper
{
    public static string DisplayName(string? name) => string.IsNullOrWhiteSpace(name) ? "Unbenanntes Gerät" : name.Trim();
    public static LinkState State(BluetoothConnectionStatus status) => status switch
    {
        BluetoothConnectionStatus.CONNECTED => LinkState.Connected,
        BluetoothConnectionStatus.DISCONNECTED => LinkState.Disconnected,
        _ => LinkState.Unknown
    };
}
