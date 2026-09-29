using Jabra.NET.Sdk.Properties;

namespace JabraDesktop.Jabra;

internal static class PropertyValueMapper
{
    public static string? Firmware(PropertyValue value)
    {
        if (value is not StringPropertyValue) return null;
        var firmware = value.AsString().Trim();
        return string.IsNullOrEmpty(firmware) ? null : firmware;
    }

    public static int? BatteryPercent(PropertyValue value)
    {
        if (value is not IntegerPropertyValue) return null;
        var percent = value.AsInteger();
        return percent is >= 0 and <= 100 ? percent : null;
    }

    public static string? ProductText(PropertyValue value)
    {
        if(value is not StringPropertyValue) return null;
        var text=value.AsString().Trim();
        return text.Length==0 ? null : text;
    }
}
