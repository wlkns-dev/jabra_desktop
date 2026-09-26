using System.Reflection;
using Avalonia.Controls;

namespace JabraDesktop.App;

internal static class TrayStatusCompatibility
{
    const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static bool TrySetActive(TrayIcon? trayIcon)
    {
        if (trayIcon is null) return false;

        // Avalonia 11.3.x copies ToolTipText into Status, which KDE does not accept as an SNI state.
        var implementation = typeof(TrayIcon).GetField("_impl", InstanceMembers)?.GetValue(trayIcon);
        if (implementation is null) return false;
        var itemField = FindField(implementation.GetType(), "_statusNotifierItemDbusObj");
        return SetStatusActive(itemField?.GetValue(implementation));
    }

    internal static bool SetStatusActive(object? statusNotifierItem)
    {
        if (statusNotifierItem is null) return false;
        var type = statusNotifierItem.GetType();
        var statusProperty = type.GetProperty("Status", InstanceMembers);
        var invalidateAll = type.GetMethod("InvalidateAll", InstanceMembers, binder: null, Type.EmptyTypes, modifiers: null);
        if (statusProperty?.CanWrite != true || invalidateAll is null) return false;

        if (Equals(statusProperty.GetValue(statusNotifierItem), "Active")) return true;
        statusProperty.SetValue(statusNotifierItem, "Active");
        invalidateAll.Invoke(statusNotifierItem, null);
        return true;
    }

    static FieldInfo? FindField(Type type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var field = current.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field is not null) return field;
        }
        return null;
    }
}
