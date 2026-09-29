using System.Globalization;
using JabraDesktop.App;
using JabraDesktop.App.ViewModels;
using JabraDesktop.Core;

namespace JabraDesktop.Tests;

public sealed class LocalizationServiceTests
{
    [Fact]
    public void UnconfirmedDeviceNameUsesSelectedLanguage()
    {
        Assert.Contains("bestätigt",new LocalizationService(UiLanguage.German).TranslateSessionError("device-name-unconfirmed"));
        Assert.Contains("confirmed",new LocalizationService(UiLanguage.English).TranslateSessionError("device-name-unconfirmed"));
    }
    [Fact]
    public void HierarchyLabelsExistInBothLanguages()
    {
        Assert.Equal("Weitere Geräte",UiTextCatalog.Get(UiLanguage.German,UiText.StandaloneDevices));
        Assert.Equal("Other devices",UiTextCatalog.Get(UiLanguage.English,UiText.StandaloneDevices));
        Assert.Contains("dongle",UiTextCatalog.Get(UiLanguage.English,UiText.DongleRefreshError),StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dongle",UiTextCatalog.Get(UiLanguage.German,UiText.DongleRefreshError),StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public void ProductInformationLabelsExistInBothLanguages()
    {
        Assert.Equal("TEILENUMMER",UiTextCatalog.Get(UiLanguage.German,UiText.PartNumber));
        Assert.Equal("PART NUMBER",UiTextCatalog.Get(UiLanguage.English,UiText.PartNumber));
        Assert.Equal("AUDIOGERÄTENAME",UiTextCatalog.Get(UiLanguage.German,UiText.AudioName));
        Assert.Equal("AUDIO DEVICE NAME",UiTextCatalog.Get(UiLanguage.English,UiText.AudioName));
        Assert.Equal("VERBUNDENES TELEFON",UiTextCatalog.Get(UiLanguage.German,UiText.MobilePhone));
        Assert.Equal("CONNECTED PHONE",UiTextCatalog.Get(UiLanguage.English,UiText.MobilePhone));
    }
    [Theory]
    [InlineData(UiLanguage.German, UiText.RefreshDeviceStatus, "Gerätestatus aktualisieren")]
    [InlineData(UiLanguage.German, UiText.RefreshingDeviceStatus, "Status wird aktualisiert …")]
    [InlineData(UiLanguage.English, UiText.RefreshDeviceStatus, "Refresh device status")]
    [InlineData(UiLanguage.English, UiText.RefreshingDeviceStatus, "Refreshing status …")]
    public void PropertyRefreshLabelsAreLocalized(UiLanguage language, UiText key, string expected)
    {
        Assert.Equal(expected, UiTextCatalog.Get(language, key));
    }

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

    [Theory]
    [InlineData(UiLanguage.German, DeviceRole.Dongle, "Bluetooth-Dongle")]
    [InlineData(UiLanguage.English, DeviceRole.Dongle, "Bluetooth dongle")]
    [InlineData(UiLanguage.German, DeviceRole.Headset, "Headset")]
    [InlineData(UiLanguage.English, DeviceRole.Headset, "Headset")]
    [InlineData(UiLanguage.German, DeviceRole.Other, "Jabra-Gerät")]
    [InlineData(UiLanguage.English, DeviceRole.Other, "Jabra device")]
    [InlineData(UiLanguage.German, DeviceRole.Unknown, "Unbekannter Gerätetyp")]
    [InlineData(UiLanguage.English, DeviceRole.Unknown, "Unknown device type")]
    public void DeviceRoleLabelUsesTheSelectedLanguage(UiLanguage language, DeviceRole role, string expected)
    {
        var texts=new LocalizationService(language);
        var item=new DeviceListItemViewModel(new DeviceInfo("id","Jabra",false,Role:role),texts);

        Assert.Equal(expected,item.RoleLabel);
    }

    [Fact]
    public void DeviceIdentifierIsFormattedOnlyWhenBothIdsAreAvailable()
    {
        var texts=new LocalizationService(UiLanguage.German);
        var complete=new DeviceListItemViewModel(new DeviceInfo("id","Link",true,VendorId:2830,ProductId:9415),texts);
        var missingVendor=new DeviceListItemViewModel(new DeviceInfo("id2","Link",true,ProductId:9415),texts);
        var missingProduct=new DeviceListItemViewModel(new DeviceInfo("id3","Link",true,VendorId:2830),texts);

        Assert.Equal("VID 0B0E · PID 24C7",complete.IdentifierText);
        Assert.True(complete.HasIdentifier);
        Assert.Equal(string.Empty,missingVendor.IdentifierText);
        Assert.False(missingVendor.HasIdentifier);
        Assert.Equal(string.Empty,missingProduct.IdentifierText);
        Assert.False(missingProduct.HasIdentifier);
    }

    [Fact]
    public async Task ViewModelAndPeerLabelsUpdateWithoutRecreatingTheWindow()
    {
        var texts = new LocalizationService(UiLanguage.German);
        var backend=new FakeBackend();
        await using var session = new DeviceSession(backend);
        using var viewModel = new MainViewModel(session, action => action(), texts);
        backend.EmitDevices(new DeviceInfo("dongle","Link 380",true,Role:DeviceRole.Dongle,VendorId:2830,ProductId:9415));
        var deviceRow=Assert.Single(viewModel.Devices);
        var deviceRowChanges=new List<string?>();
        deviceRow.PropertyChanged += (_,args)=>deviceRowChanges.Add(args.PropertyName);
        var row = new PeerRow(new PeerInfo("peer", "Headset", LinkState.Disconnected), found: false, viewModel);
        viewModel.Peers.Add(row);
        var viewModelChanges = new List<string?>();
        var rowChanges = new List<string?>();
        viewModel.PropertyChanged += (_, args) => viewModelChanges.Add(args.PropertyName);
        row.PropertyChanged += (_, args) => rowChanges.Add(args.PropertyName);

        Assert.Equal("Link 380", viewModel.DeviceTitle);
        Assert.Equal("Bereit zum Verbinden", viewModel.StatusText);
        Assert.Equal("Bluetooth-Dongle · Geräteverwaltung verfügbar", viewModel.DeviceSubtitle);
        Assert.Equal("Verbinden", row.ActionLabel);
        Assert.Equal("Nicht verbunden", row.Status);
        Assert.Equal("Bluetooth-Dongle", deviceRow.RoleLabel);

        texts.SetLanguage(UiLanguage.English);

        Assert.Equal("Link 380", viewModel.DeviceTitle);
        Assert.Equal("Ready to connect", viewModel.StatusText);
        Assert.Equal("Bluetooth dongle · Device management available", viewModel.DeviceSubtitle);
        Assert.Equal("Connect", row.ActionLabel);
        Assert.Equal("Disconnected", row.Status);
        Assert.Equal("Bluetooth dongle", deviceRow.RoleLabel);
        Assert.Contains(nameof(MainViewModel.DeviceTitle), viewModelChanges);
        Assert.Contains(nameof(DeviceListItemViewModel.RoleLabel), deviceRowChanges);
        Assert.Contains(nameof(PeerRow.ActionLabel), rowChanges);
        Assert.Contains(nameof(PeerRow.Status), rowChanges);
    }
}
