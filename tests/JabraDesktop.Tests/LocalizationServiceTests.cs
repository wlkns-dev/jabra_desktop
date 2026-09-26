using System.Globalization;
using JabraDesktop.App;
using JabraDesktop.App.ViewModels;
using JabraDesktop.Core;

namespace JabraDesktop.Tests;

public sealed class LocalizationServiceTests
{
    [Fact]
    public void UnsupportedSystemCultureDefaultsToEnglish()
    {
        Assert.Equal(UiLanguage.English, LocalizationService.DetectLanguage(CultureInfo.GetCultureInfo("fr-FR")));
    }

    [Fact]
    public void GermanSystemCultureDefaultsToGerman()
    {
        Assert.Equal(UiLanguage.German, LocalizationService.DetectLanguage(CultureInfo.GetCultureInfo("de-CH")));
    }

    [Fact]
    public void SetLanguageChangesTheTextImmediately()
    {
        var texts = new LocalizationService(UiLanguage.German);
        Assert.Equal("Öffnen", texts[UiText.Open]);

        texts.SetLanguage(UiLanguage.English);

        Assert.Equal("Open", texts[UiText.Open]);
    }

    [Fact]
    public void LanguageChangeRaisesExactlyOneNotification()
    {
        var texts = new LocalizationService(UiLanguage.German);
        var changes = 0;
        texts.LanguageChanged += (_, _) => changes++;

        texts.SetLanguage(UiLanguage.English);
        texts.SetLanguage(UiLanguage.English);

        Assert.Equal(1, changes);
    }

    [Theory]
    [InlineData(UiLanguage.German)]
    [InlineData(UiLanguage.English)]
    public void EveryUiTextHasAValue(UiLanguage language)
    {
        foreach (var key in Enum.GetValues<UiText>())
            Assert.False(string.IsNullOrWhiteSpace(UiTextCatalog.Get(language, key)), $"Missing {language} translation for {key}.");
    }

    [Fact]
    public async Task ViewModelAndPeerLabelsUpdateWithoutRecreatingTheWindow()
    {
        var texts = new LocalizationService(UiLanguage.German);
        await using var session = new DeviceSession(new FakeBackend());
        using var viewModel = new MainViewModel(session, action => action(), texts);
        var row = new PeerRow(new PeerInfo("peer", "Headset", LinkState.Disconnected), found: false, viewModel);
        viewModel.Peers.Add(row);
        var viewModelChanges = new List<string?>();
        var rowChanges = new List<string?>();
        viewModel.PropertyChanged += (_, args) => viewModelChanges.Add(args.PropertyName);
        row.PropertyChanged += (_, args) => rowChanges.Add(args.PropertyName);

        Assert.Equal("Deine Geräte, verbunden.", viewModel.DeviceTitle);
        Assert.Equal("Geräteübersicht", viewModel.StatusText);
        Assert.Equal("Verbinden", row.ActionLabel);
        Assert.Equal("Nicht verbunden", row.Status);

        texts.SetLanguage(UiLanguage.English);

        Assert.Equal("Your connected devices.", viewModel.DeviceTitle);
        Assert.Equal("Device overview", viewModel.StatusText);
        Assert.Equal("Connect", row.ActionLabel);
        Assert.Equal("Disconnected", row.Status);
        Assert.Contains(nameof(MainViewModel.DeviceTitle), viewModelChanges);
        Assert.Contains(nameof(PeerRow.ActionLabel), rowChanges);
        Assert.Contains(nameof(PeerRow.Status), rowChanges);
    }
}
