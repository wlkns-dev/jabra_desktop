using System.Text;

namespace JabraDesktop.App;

internal sealed class AutostartManager
{
    const string EntryName = "jabra-desktop.desktop";
    const string InstalledCommand = "/usr/bin/jabra-desktop";
    readonly string entryPath = ResolveEntryPath();

    public bool IsAvailable => File.Exists(InstalledCommand);
    public bool IsEnabled => File.Exists(entryPath);

    public void SetEnabled(bool enabled)
    {
        if (!enabled)
        {
            File.Delete(entryPath);
            return;
        }

        if (!IsAvailable)
            throw new InvalidOperationException("Autostart ist erst nach der Paketinstallation verfügbar.");

        var directory = Path.GetDirectoryName(entryPath)
            ?? throw new InvalidOperationException("Der XDG-Autostartpfad konnte nicht bestimmt werden.");
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{entryPath}.tmp-{Environment.ProcessId}-{Guid.NewGuid():N}";
        try
        {
            File.WriteAllText(temporaryPath, DesktopEntry, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, entryPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    static string ResolveEntryPath()
    {
        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrWhiteSpace(configHome) || !Path.IsPathFullyQualified(configHome))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(home))
                throw new InvalidOperationException("Das Benutzerverzeichnis konnte nicht bestimmt werden.");
            configHome = Path.Combine(home, ".config");
        }
        return Path.Combine(configHome, "autostart", EntryName);
    }

    const string DesktopEntry = """
        [Desktop Entry]
        Type=Application
        Name=Jabra Desktop
        Comment=Jabra-Dongle und Headsets verwalten
        Exec=/usr/bin/jabra-desktop --autostart
        Icon=jabra-desktop
        Terminal=false
        X-GNOME-Autostart-enabled=true
        """ + "\n";
}
