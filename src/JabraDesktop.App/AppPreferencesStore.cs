using System.Text.Json;
using System.Text.Json.Serialization;

namespace JabraDesktop.App;

public sealed class AppPreferencesStore
{
    static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    readonly string filePath;

    public AppPreferencesStore(string? filePath = null)
    {
        this.filePath = filePath ?? ResolveFilePath();
    }

    public AppPreferences Load()
    {
        if (!File.Exists(filePath)) return new AppPreferences();

        try
        {
            var preferences = JsonSerializer.Deserialize<AppPreferences>(File.ReadAllText(filePath), SerializerOptions);
            return IsValid(preferences) ? preferences! : new AppPreferences();
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppPreferences();
        }
    }

    public void Save(AppPreferences value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!IsValid(value)) throw new ArgumentException("Die Benutzereinstellungen enthalten ungültige Werte.", nameof(value));

        var directory = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException("Das Verzeichnis für Benutzereinstellungen konnte nicht bestimmt werden.");
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{filePath}.tmp-{Environment.ProcessId}-{Guid.NewGuid():N}";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(value, SerializerOptions));
            File.Move(temporaryPath, filePath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    static bool IsValid(AppPreferences? value)
    {
        if (value is null || !Enum.IsDefined(value.Theme)) return false;
        if (value.Language is { } language && !Enum.IsDefined(language)) return false;
        if ((value.AcceptedTermsVersion is null) != (value.AcceptedTermsAt is null)) return false;
        return value.AcceptedTermsVersion is null || !string.IsNullOrWhiteSpace(value.AcceptedTermsVersion);
    }

    static string ResolveFilePath()
    {
        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrWhiteSpace(configHome) || !Path.IsPathFullyQualified(configHome))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(home))
                throw new InvalidOperationException("Das Benutzerverzeichnis konnte nicht bestimmt werden.");
            configHome = Path.Combine(home, ".config");
        }

        return Path.Combine(configHome, "jabra-desktop", "settings.json");
    }
}
