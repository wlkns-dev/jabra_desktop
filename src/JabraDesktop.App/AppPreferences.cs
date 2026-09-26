namespace JabraDesktop.App;

public enum UiLanguage
{
    German,
    English
}

public enum ThemePreference
{
    System,
    Light,
    Dark
}

public sealed record AppPreferences(
    UiLanguage? Language = null,
    ThemePreference Theme = ThemePreference.System,
    string? AcceptedTermsVersion = null,
    DateTimeOffset? AcceptedTermsAt = null);
