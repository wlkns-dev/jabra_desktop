using JabraDesktop.App;

namespace JabraDesktop.Tests;

public sealed class ConsentTermsTests
{
    [Theory]
    [InlineData(UiLanguage.German)]
    [InlineData(UiLanguage.English)]
    public void TermsContainRequiredSdkClauses(UiLanguage language)
    {
        var terms = ConsentTerms.GetText(language);

        Assert.Contains("GN Audio", terms, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(language == UiLanguage.German ? "Jabra-Entwickler" : "Jabra Developer", terms, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(language == UiLanguage.German ? "Produkt von GN Audio" : "GN Audio product", terms, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(language == UiLanguage.German ? "Gewährleistung" : "warrant", terms, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(language == UiLanguage.German ? "haften" : "liable", terms, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(language == UiLanguage.German ? "Gesetz" : "law", terms, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://developer.jabra.com/legal/license-agreement", terms);
    }
}
