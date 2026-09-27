using Jabra.NET.Sdk.Properties;
using JabraDesktop.Core;

namespace JabraDesktop.Jabra;

internal static class DevicePropertyValueUpdate
{
    public static DeviceInfo? TryApply(DeviceInfo current, DevicePropertyCapability capability, PropertyValue? value, bool isAttached)
    {
        if (!isAttached) return null;
        return capability.ValueKind switch
        {
            DevicePropertyValueKind.Firmware => current with { Firmware = value is null ? null : PropertyValueMapper.Firmware(value) },
            DevicePropertyValueKind.BatteryPercent => current with { BatteryPercent = value is null ? null : PropertyValueMapper.BatteryPercent(value) },
            _ => current
        };
    }
}
