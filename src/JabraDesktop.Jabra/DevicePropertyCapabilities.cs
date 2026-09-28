using System.Runtime.CompilerServices;
using JabraDesktop.Core;

[assembly: InternalsVisibleTo("JabraDesktop.Tests")]

namespace JabraDesktop.Jabra;

internal enum DevicePropertyValueKind
{
    Firmware,
    BatteryPercent
}

internal sealed record DevicePropertyCapability(string PropertyName, DevicePropertyValueKind ValueKind);

internal static class DevicePropertyCapabilities
{
    static readonly DevicePropertyCapability firmware = new("firmwareVersion", DevicePropertyValueKind.Firmware);
    static readonly DevicePropertyCapability battery = new("batteryLevel", DevicePropertyValueKind.BatteryPercent);

    public static IReadOnlyList<DevicePropertyCapability> For(DeviceInfo device) => device.Role switch
    {
        DeviceRole.Dongle => [firmware],
        DeviceRole.Headset or DeviceRole.Other => [firmware, battery],
        _ => []
    };

    public static DevicePropertyCapability? Find(DeviceInfo device) => For(device).FirstOrDefault();
}
