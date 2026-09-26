using JabraDesktop.Core;
using Jabra.NET.Sdk.DevicePairing;
using SdkDeviceType = global::Jabra.NET.Sdk.Core.Types.DeviceType;
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
    public static DeviceRole Role(SdkDeviceType type) => type switch
    {
        SdkDeviceType.Dongle => DeviceRole.Dongle,
        SdkDeviceType.Headset => DeviceRole.Headset,
        SdkDeviceType.None or SdkDeviceType.NotInit or SdkDeviceType.NotGn => DeviceRole.Unknown,
        _ => DeviceRole.Other
    };
    public static int? OptionalUsbId(int id) => id > 0 ? id : null;
}
