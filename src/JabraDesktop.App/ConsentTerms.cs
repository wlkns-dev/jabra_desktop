namespace JabraDesktop.App;

public static class ConsentTerms
{
    public const string CurrentVersion = "jabra-sdk-terms-1";

    public static string GetText(UiLanguage language)
    {
        var resourceName = language switch
        {
            UiLanguage.German => "JabraDesktop.App.Resources.Terms.de.md",
            UiLanguage.English => "JabraDesktop.App.Resources.Terms.en.md",
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Unsupported UI language.")
        };

        using var stream = typeof(ConsentTerms).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded consent resource: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }
}
