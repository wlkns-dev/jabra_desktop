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
    static readonly DevicePropertyCapability link380Firmware = new("firmwareVersion", DevicePropertyValueKind.Firmware);
    static readonly DevicePropertyCapability evolve75SeBattery = new("batteryLevel", DevicePropertyValueKind.BatteryPercent);

    public static DevicePropertyCapability? Find(DeviceInfo device) =>
        (device.Role, device.VendorId, device.ProductId) switch
        {
            (DeviceRole.Dongle, 0x0B0E, 0x24C7) => link380Firmware,
            (DeviceRole.Headset, 0x0B0E, 0x2502) => evolve75SeBattery,
            _ => null
        };
}
