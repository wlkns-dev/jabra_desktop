using System.Text;

namespace JabraDesktop.Core;

public static class BluetoothNameRules
{
    public static string? Normalize(string? name)
    {
        var trimmed=name?.Trim();
        if(string.IsNullOrWhiteSpace(trimmed) || trimmed.Any(char.IsControl)
            || Encoding.UTF8.GetByteCount(trimmed)>64) return null;
        return trimmed;
    }
}
