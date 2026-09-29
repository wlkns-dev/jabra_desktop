using System.Globalization;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using JabraDesktop.Core;

namespace JabraDesktop.App;

public sealed class LocalizationService(UiLanguage initialLanguage) : INotifyPropertyChanged
{
    UiLanguage language = initialLanguage;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;

    public UiLanguage Language => language;
    public string this[UiText key] => UiTextCatalog.Get(language, key);
    public string this[string key] => Enum.TryParse<UiText>(key, ignoreCase: false, out var text)
        ? this[text]
        : throw new ArgumentOutOfRangeException(nameof(key), key, "No translation is defined for this UI text.");
    public CultureInfo Culture => language == UiLanguage.German
        ? CultureInfo.GetCultureInfo("de-DE")
        : CultureInfo.GetCultureInfo("en-US");

    public void SetLanguage(UiLanguage value)
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported UI language.");
        if (language == value) return;

        language = value;
        OnPropertyChanged(nameof(Language));
        OnPropertyChanged("Item[]");
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Format(UiText key, params object[] values) => string.Format(Culture, this[key], values);

    public string? TranslateSessionError(string? error) => error switch
    {
        "USB-Zugriff verweigert. Jabra-Zugriffsregel installieren und Dongle neu einstecken." => this[UiText.UsbAccessDenied],
        "Das Gerät antwortet nicht rechtzeitig. Verbindungsstatus aktualisieren und erneut versuchen." => this[UiText.DeviceTimeout],
        "Eine benötigte Systembibliothek fehlt. Bitte die Einrichtung in der README prüfen." => this[UiText.MissingSystemLibrary],
        "Bitte einen Bluetooth-Dongle mit unterstützter Geräteverwaltung auswählen." => this[UiText.SelectSupportedDongle],
        "Gerät nicht mehr verfügbar. Bitte aktualisieren oder erneut suchen." => this[UiText.PeerNoLongerAvailable],
        DeviceSession.NameUnconfirmedError => this[UiText.DeviceNameUnconfirmed],
        _ => error
    };

    public static UiLanguage DetectLanguage(CultureInfo culture) =>
        string.Equals(culture.TwoLetterISOLanguageName, "de", StringComparison.OrdinalIgnoreCase)
            ? UiLanguage.German
            : UiLanguage.English;

    void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
