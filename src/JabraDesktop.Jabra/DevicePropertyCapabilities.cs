using System.Runtime.CompilerServices;
using JabraDesktop.Core;

[assembly: InternalsVisibleTo("JabraDesktop.Tests")]

namespace JabraDesktop.Jabra;

internal enum DevicePropertyValueKind
{
    Firmware,
    BatteryPercent,
    PartNumber,
    AudioName,
    MobilePhone
}

internal sealed record DevicePropertyCapability(string PropertyName, DevicePropertyValueKind ValueKind);

internal static class DevicePropertyCapabilities
{
    static readonly DevicePropertyCapability firmware = new("firmwareVersion", DevicePropertyValueKind.Firmware);
    static readonly DevicePropertyCapability battery = new("batteryLevel", DevicePropertyValueKind.BatteryPercent);
    static readonly DevicePropertyCapability partNumber = new("skuId", DevicePropertyValueKind.PartNumber);
    static readonly DevicePropertyCapability audioName = new("audioName", DevicePropertyValueKind.AudioName);
    static readonly DevicePropertyCapability mobilePhone = new("mobileDevice1", DevicePropertyValueKind.MobilePhone);

    public static IReadOnlyList<DevicePropertyCapability> For(DeviceInfo device) => device.Role switch
    {
        DeviceRole.Dongle => [firmware,audioName],
        DeviceRole.Headset => [firmware,battery,partNumber,mobilePhone],
        DeviceRole.Other => [firmware,battery,partNumber],
        _ => []
    };

    public static DevicePropertyCapability? Find(DeviceInfo device) => For(device).FirstOrDefault();
}
