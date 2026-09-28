using Jabra.NET.Sdk.Properties;
using JabraDesktop.Core;

namespace JabraDesktop.Jabra;

internal static class DevicePropertyValueUpdate
{
    public static DeviceProperties Apply(DeviceProperties current, DevicePropertyCapability capability, PropertyValue? value) =>
        capability.ValueKind switch
        {
            DevicePropertyValueKind.Firmware => current with { FirmwareApplicable = true, Firmware = value is null ? null : PropertyValueMapper.Firmware(value) },
            DevicePropertyValueKind.BatteryPercent => current with { BatteryApplicable = true, BatteryPercent = value is null ? null : PropertyValueMapper.BatteryPercent(value) },
            _ => current
        };

    public static DeviceProperties? TryApply(DeviceProperties current, DevicePropertyCapability capability, PropertyValue? value, bool isAttached) =>
        isAttached ? Apply(current, capability, value) : null;

    public static DeviceInfo? TryApply(DeviceInfo current, DevicePropertyCapability capability, PropertyValue? value, bool isAttached)
    {
        if (!isAttached) return null;
        var properties = Apply(current.Properties ?? new DeviceProperties(), capability, value);
        return current with { Properties = properties, Firmware = properties.Firmware, BatteryPercent = properties.BatteryPercent };
    }
}
