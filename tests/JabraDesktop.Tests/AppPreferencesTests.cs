using JabraDesktop.App;

namespace JabraDesktop.Tests;

public sealed class AppPreferencesTests
{
    [Fact]
    public void MissingSettingsUseSystemThemeAndNoExplicitLanguageOrConsent()
    {
        var store = new AppPreferencesStore(Path.Combine(NewDirectory(), "settings.json"));

        var settings = store.Load();

        Assert.Null(settings.Language);
        Assert.Equal(ThemePreference.System, settings.Theme);
        Assert.Null(settings.AcceptedTermsVersion);
        Assert.Null(settings.AcceptedTermsAt);
    }

    [Fact]
    public void SettingsRoundTripPreservesLanguageThemeAndConsent()
    {
        var store = new AppPreferencesStore(Path.Combine(NewDirectory(), "settings.json"));
        var saved = new AppPreferences(
            UiLanguage.German,
            ThemePreference.Dark,
            "jabra-sdk-terms-1",
            DateTimeOffset.UnixEpoch);

        store.Save(saved);

        Assert.Equal(saved, store.Load());
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"Language\":\"French\",\"Theme\":\"Neon\",\"AcceptedTermsVersion\":\"jabra-sdk-terms-1\"}")]
    public void InvalidSettingsUseSafeDefaults(string json)
    {
        var path = Path.Combine(NewDirectory(), "settings.json");
        File.WriteAllText(path, json);
        var store = new AppPreferencesStore(path);

        var settings = store.Load();

        Assert.Null(settings.Language);
        Assert.Equal(ThemePreference.System, settings.Theme);
        Assert.Null(settings.AcceptedTermsVersion);
        Assert.Null(settings.AcceptedTermsAt);
    }

    static string NewDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"jabra-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
