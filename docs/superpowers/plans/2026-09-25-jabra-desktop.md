# Jabra Desktop Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eine lokal startbare Linux-Desktop-App zum Verwalten eines Jabra-Dongles und zum Suchen, Koppeln und Verbinden von Headsets liefern.

**Architecture:** Avalonia-Oberfläche mit separatem Geräte-Service und Jabra-SDK-Adapter. Ein Konsolenprogramm prüft zuerst denselben Adapter an echter Hardware. Automatisierte Tests verwenden einen ausdrücklich injizierten Testadapter.

**Tech Stack:** C#, .NET 10 LTS als Build-Ziel, Avalonia, Jabra.NET.Sdk und Jabra.NET.Sdk.DevicePairing, xUnit. Die exakten kompatiblen Paketversionen werden beim ersten Restore festgeschrieben; als bekannte SDK-Basis dienen 4.9.1.1 und 3.1.1.2 aus dem Herstellerbeispiel.

**Spec:** [Freigegebener Entwurf](../specs/2026-09-25-jabra-desktop-design.md)

## Global Constraints

- Eine moderne deutschsprachige Desktop-App soll den Jabra-Dongle verwalten, Geräte suchen, koppeln, verbinden und trennen.
- Zielsystem ist CachyOS mit KDE.
- Verbindliches erstes Hardware-Testziel ist Link 380 mit Evolve 75.
- Eine Suche läuft höchstens 30 Sekunden.
- Die App läuft ohne root.
- Der reale Zustand stammt ausschließlich aus dem SDK.
- „Alle Jabra-Geräte“ ist kein belastbares Kompatibilitätsversprechen.
- Firmware-Updates, Autostart, Tray-Steuerung und umfangreiche Audio-Einstellungen sind außerhalb der ersten Version.
- Produktcode erst nach Plan-Durchsicht. Vor Ausführung geltende AGENTS.md prüfen; derzeit existiert kein nutzbares Git-Repository. Keine Änderungen in den geschützten Verzeichnissen .git, .agents oder .codex erzwingen. Commits nur bei tatsächlich verfügbarem Repository.

## Review Focus

1. Zwei Dongles mit gleichem Namen: Auswahl und Aktionen bleiben anhand eindeutiger IDs getrennt (Aufgabe 2).
2. Dongle wird während einer Suche abgezogen und wieder eingesteckt: alte Ergebnisse erscheinen nicht in der neuen Sitzung (Aufgabe 2).
3. Ein Vorgang läuft nach UI-Zeitüberschreitung im SDK weiter: keine zweite widersprüchliche Aktion starten; Zustand erneut abfragen (Aufgabe 3).
4. Ein Headset hat keinen Namen oder einen langen Unicode-Namen: verständliche, bedienbare Darstellung ohne Namens-basierte Identität (Aufgabe 4).
5. Fehlende USB-Rechte oder native Bibliotheken: konkrete Startdiagnose statt leerer Oberfläche oder simuliertem Erfolg (Aufgaben 1 und 5).

## Dateistruktur

```text
JabraDesktop.sln
global.json                              SDK-Version
Directory.Build.props                    Nullable, gemeinsame Build-Einstellungen
src/JabraDesktop.Core/
  Models.cs                              unveränderliche Geräte- und Statusmodelle
  IDeviceBackend.cs                      Adaptervertrag
  DeviceSession.cs                       Auswahl, Suche, Zustände und Aktionssperre
src/JabraDesktop.Jabra/
  JabraBackend.cs                        SDK-Start, Erkennung und Freigabe
  BluetoothOperations.cs                 Pairing und Verbindungsaktionen
  DeviceMapper.cs                        SDK-Daten und Fähigkeiten übersetzen
src/JabraDesktop.Probe/Program.cs         Hardwarediagnose und gezielte Aktionen
src/JabraDesktop.App/
  Program.cs / App.axaml / App.axaml.cs   Desktop-Lebenszyklus
  Views/MainWindow.axaml / .cs            Hauptfenster und Entkoppelbestätigung
  ViewModels/MainViewModel.cs             UI-Bindings, Commands, UI-Dispatcher
  Styles/Theme.axaml                      Farben, Abstände und Zustände
tests/JabraDesktop.Tests/
  FakeBackend.cs                         kontrollierbare Ereignisse und Aufrufe
  DiscoveryTests.cs / OperationTests.cs / ViewModelTests.cs
scripts/publish.sh                       Linux-Veröffentlichung
packaging/jabra-desktop.desktop          Desktop-Eintrag
packaging/99-jabra-desktop.rules          nur falls Hardwaretest Bedarf belegt
README.md                               Start, Einrichtung, Grenzen
docs/hardware-validation.md              echte Prüfergebnisse und Testmatrix
```

## Aufgabe 1: SDK-Anbindung und echte Erkennung

**Dateien:** Solution, Projekte Core/Jabra/Probe/Tests, Build-Konfiguration, `Models.cs`, `IDeviceBackend.cs`, `JabraBackend.cs`, `DeviceMapper.cs`, Probe und Hardwareprotokoll.

**Vertrag:** Eigene IDs sind Strings; Adressen bleiben ausschließlich im Adapter in ihrem SDK-Typ. Das verhindert Annahmen über deren Darstellung.

```csharp
public enum LinkState { Unknown, Disconnected, Connected }
public enum DeviceAction { Pair, Connect, Disconnect, Unpair }
public record DeviceInfo(string Id, string Name, bool CanPair,
    int? BatteryPercent = null, string? Firmware = null);
public record PeerInfo(string Id, string Name, LinkState State);
public interface IDeviceBackend : IAsyncDisposable
{
    event Action<IReadOnlyList<DeviceInfo>>? DevicesChanged;
    Task StartAsync(CancellationToken token);
    Task<IReadOnlyList<PeerInfo>> GetPeersAsync(string dongleId, CancellationToken token);
    IAsyncEnumerable<PeerInfo> ScanAsync(string dongleId, CancellationToken token);
    Task ExecuteAsync(string dongleId, string peerId, DeviceAction action,
        CancellationToken token);
}
```

- [ ] Prüfen, ob eine vorhandene .NET-Installation nutzbar ist; andernfalls .NET 10 lokal unter `.tools/dotnet` installieren. Netz- oder Systemzugriffe nach den geltenden Tool-Berechtigungen durchführen. Keine globale Installation voraussetzen.
- [ ] Projekte anlegen, gemeinsame `net10.0`, Nullable und implizite Usings setzen. SDK-Pakete mit obigen Ausgangsversionen wiederherstellen, Kompatibilität und Nutzungs-/Weitergabebedingungen prüfen. Tatsächliche Versionen in Projektdateien fixieren; SDK in `global.json` fixieren.
- [ ] Adapter initialisiert SDK mit eigenem App-Namen und optionalem `JABRA_PARTNER_KEY`. Ohne Schlüssel reale Startfähigkeit testen; bei Ablehnung konkrete Einrichtungsmeldung liefern. Keine fremden Schlüssel übernehmen.
- [ ] SDK-Subscriptions vor dem Start registrieren. `DeviceAdded`/`DeviceRemoved` erfassen, Fähigkeiten über Bluetooth-Modul ermitteln, keine Modellnamenliste verwenden. Namensfallback „Unbenanntes Gerät“. Fehlende Telemetrie als null abbilden. Subscription-Fehler beobachten und beim Beenden aufräumen.
- [ ] Probe-Befehle `list` und `peers <dongle-id>` implementieren, mit 15 Sekunden Startlimit. Rückgabecode 0 für Erfolg, 1 für Fehler, 2 für nicht verfügbares Zielgerät; standardmäßig keine Seriennummern/Adressen ausgeben.
- [ ] `dotnet build JabraDesktop.sln` ausführen. Danach `dotnet run --project src/JabraDesktop.Probe -- list` und gezielte Peer-Abfrage an Link 380. Native Abhängigkeiten und USB-Berechtigungen prüfen; mögliche udev-Regel auf Jabra und aktive lokale Sitzung eingrenzen.
- [ ] Ergebnisse in `docs/hardware-validation.md` mit Datum und Geräte-/SDK-Versionen dokumentieren. Ist der echte Zugriff blockiert, Ursache und notwendige Änderung klären, bevor eine vollständige Hardwarefunktion behauptet oder die UI ausgebaut wird.

## Aufgabe 2: Sitzungen, Auswahl und Suche

**Dateien:** `DeviceSession.cs`, `FakeBackend.cs`, `DiscoveryTests.cs`, Scan-Teil des Adapters.

**Vertrag:** `DeviceSession(IDeviceBackend backend)` implementiert `IAsyncDisposable`; bietet `Select(string id)`, `Task ScanAsync(CancellationToken)`, `Devices`, `Peers`, `Results`, `IsBusy`, `Error` und `event Action? Changed`. `Select` löscht alte Peer-/Suchlisten und invalidiert den Sitzungsgenerator. `FakeBackend` bietet `EmitDevices`, `EmitScanResult` und `CompleteScan` zur deterministischen Steuerung.

- [ ] Tests für doppelte Namen, Such-Deduplizierung anhand ID und alte Ereignisse nach Auswahlwechsel schreiben. Beispiel eines unabhängigen Vertragstests:

```csharp
[Fact]
public void SameNamesRemainSeparateDevices()
{
    var backend = new FakeBackend();
    var session = new DeviceSession(backend);
    backend.EmitDevices(new DeviceInfo("a", "Link", true),
                        new DeviceInfo("b", "Link", true));
    Assert.Equal(2, session.Devices.Count);
    session.Select("b");
    Assert.Empty(session.Results);
}
```

- [ ] `dotnet test --filter FullyQualifiedName~DiscoveryTests` ausführen und fehlendes Verhalten als Fehlschlag bestätigen.
- [ ] Sitzungen mit monotonem Generationenzähler implementieren. Jedes asynchrone Ergebnis nur bei gleicher Generation übernehmen. Entfernen des ausgewählten Geräts invalidiert die Sitzung und löst Cancellation aus. Erneut eingesteckte Geräte erhalten keine alte Operationsinstanz.
- [ ] SDK-Scan als `IAsyncEnumerable` über Channel oder äquivalente geordnete Ereignisbrücke abbilden. SDK-Suchdauer 30 Sekunden; Abbruch stoppt den Scan explizit und gibt Subscription frei. Treffer-ID im Adapter auf Bluetooth-Adresse abbilden. Fehler und Abschluss müssen den Enumerator beenden.
- [ ] Tests ergänzen: zweimal derselbe Treffer ergibt eine Zeile; Entfernen während Suche verwirft späte Treffer; zwei Dongles erhalten getrennte Listen. Scan-Abbruch muss `IsBusy` zurücksetzen.
- [ ] DiscoveryTests erneut ausführen; Probe um `scan <dongle-id>` ergänzen und echte Suche prüfen, sobald Evolve 75 im Pairing-Modus ist. Ohne aktiven Pairing-Modus sind null Treffer kein Fehler und kein positiver Hardware-Abnahmetest.

## Aufgabe 3: Koppeln und Verbindungen

**Dateien:** `BluetoothOperations.cs`, Aktionslogik in `DeviceSession.cs`, `OperationTests.cs`, Probe.

**Vertrag:** `DeviceSession.RunAsync(string peerId, DeviceAction action, CancellationToken token)` verwendet den selektierten Dongle. `FakeBackend` besitzt `Calls`, `OperationCompletion` als kontrollierbares TaskCompletionSource und `NextError` für Fehlerfälle. Call-Einträge enthalten Dongle-ID, Peer-ID und Aktion.

- [ ] Tests für doppelte Aktionen, Zielzuordnung und Fehlerzustände schreiben:

```csharp
[Fact]
public async Task BackendFailureDoesNotClaimConnection()
{
    var backend = new FakeBackend { NextError = new IOException("USB getrennt") };
    var session = new DeviceSession(backend);
    backend.EmitDevices(new DeviceInfo("a", "Link", true));
    session.Select("a");
    await session.RunAsync("peer", DeviceAction.Connect, CancellationToken.None);
    Assert.NotNull(session.Error);
    Assert.False(session.IsBusy);
    Assert.DoesNotContain(session.Peers, p => p.State == LinkState.Connected);
}
```

- [ ] `dotnet test --filter FullyQualifiedName~OperationTests` ausführen; erwartete Fehlschläge bestätigen.
- [ ] Pro Dongle eine Aktionssperre implementieren. Pairing verwendet gespeicherten Suchtreffer, andere Aktionen aktuelle Pairing-Einträge. Vor Unpair bei bestätigter Verbindung gezielt trennen. Nach jeder Aktion Pairing-Liste erneut lesen, auch nach Timeout sofern Dongle vorhanden. Unbekannte SDK-Statuswerte bleiben Unknown.
- [ ] SDK-Zeitlimits explizit setzen: Pairing 30 Sekunden, Connect 15 Sekunden. Wenn Cancellation nur lokales Warten beendet, SDK-Task weiter beobachten und Dongle bis dessen Abschluss sperren. Keine unbeobachteten Tasks; Geräteverlust invalidiert Ergebnisse.
- [ ] Weitere Tests: zweite Aktion während blockiertem SDK-Task wird abgewiesen; später Abschluss nach Geräteverlust ändert nichts; Timeout plus anschließend bestätigte Verbindung wird wahrheitsgemäß dargestellt; unbekannte Peer-ID löst keine SDK-Aktion aus.
- [ ] Tests ausführen und Probe um `pair`, `connect`, `disconnect`, `unpair` mit jeweils expliziter Dongle- und Peer-ID ergänzen. Keine Sammel-Löschfunktion implementieren.

## Aufgabe 4: Desktop-Oberfläche

**Dateien:** Avalonia-App, Views, MainViewModel, Theme und `ViewModelTests.cs`.

**Vertrag:** `MainViewModel(DeviceSession session, Action<Action> dispatch)` bindet Session-Daten über ObservableCollections und INotifyPropertyChanged. Der Desktop injiziert den Avalonia-Dispatcher, Tests führen Aktionen direkt aus. Commands delegieren ausschließlich an Session; Fehler werden deutsch übersetzt.

- [ ] ViewModel-Tests zunächst rot ausführen: fehlender Dongle deaktiviert Aktionen; `CanPair=false` deaktiviert Suche; laufender Vorgang deaktiviert kollidierende Aktionen; Geräteverlust leert die Auswahl.
- [ ] Hauptfenster mit linker Geräteliste und rechter Detailansicht implementieren. Zentrale Aktionen „Gerät hinzufügen“, „Verbinden“, „Trennen“, „Entkoppeln“. Suchbereich mit Pairing-Hinweis, Abbrechen und Ergebnisliste. Entkoppeln benötigt einen Dialog mit konkretem Gerätenamen.

```xml
<Grid ColumnDefinitions="240,*" Margin="24" ColumnSpacing="24">
  <ListBox ItemsSource="{Binding Devices}" SelectedItem="{Binding SelectedDevice}" />
  <StackPanel Grid.Column="1" Spacing="16">
    <TextBlock Text="{Binding SelectedDevice.Name}" FontSize="24" />
    <TextBlock Text="{Binding StatusText}" TextWrapping="Wrap" />
    <Button Content="Gerät hinzufügen" Command="{Binding ScanCommand}" />
    <TextBlock Text="{Binding Error}" TextWrapping="Wrap" />
  </StackPanel>
</Grid>
```

- [ ] Das Layout um Peer-Zeilen mit statusabhängigen Commands, Suchergebnisse und optionale Akku-/Firmwarefelder ergänzen. Theme verwendet Avalonia-Ressourcen für Hell/Dunkel und sichtbare Fokuszustände. Keine rein farbliche Statuscodierung.
- [ ] Tests für unbekannten Akku, leeren Namen und langen Unicode-Namen hinzufügen. Öffentliche ViewModel-Properties und Command-Namen stimmen mit XAML-Bindings überein; compiled bindings bevorzugen.
- [ ] `dotnet test` und `dotnet build` ausführen. App unter KDE öffnen und Tastaturnavigation, kleine Fenstergröße, Skalierung sowie Hell/Dunkel visuell prüfen. GUI-Start gemäß Umgebung freigeben lassen, falls erforderlich.

## Aufgabe 5: Veröffentlichung und Hardwareabnahme

**Dateien:** `scripts/publish.sh`, Desktop-Eintrag, bei Bedarf udev-Regel, README und Hardwareprotokoll.

- [ ] Veröffentlichungs-Skript mit Fehlerabbruch anlegen:

```bash
#!/usr/bin/env bash
set -euo pipefail
dotnet publish src/JabraDesktop.App -c Release -r linux-x64 \
  --self-contained true -o artifacts/linux-x64
```

- [ ] Desktop-Eintrag als Vorlage mit dokumentiertem absoluten Installationspfad liefern. README enthält Build, Start, native Bibliotheken, USB-Rechte, Partner-Key-Konfiguration, bekannte Grenzen und Deinstallation. Kein automatischer Root-Start; keine automatische systemweite Installation.
- [ ] Startfehler mit injiziertem fehlschlagendem Backend testen: Meldung statt leerer Geräteliste, Wiederholen möglich, keine falschen Beispielgeräte. Bei SDK-Problemen genaue Ursache in Diagnose ablegen, sensible Kennungen maskieren.
- [ ] `dotnet test JabraDesktop.sln -c Release` und `bash scripts/publish.sh` ausführen. Veröffentlichung direkt starten und SDK-Laufzeitdateien auf Vollständigkeit prüfen. Lizenz-/Weitergabeprüfung vor dem Verteilen des Pakets dokumentieren.
- [ ] Mit dem Nutzer das Headset in Pairing-Modus bringen und reale Aktionen prüfen: Erkennen, gespeicherte Geräte, Suche, Koppeln, Trennen, Verbinden, gezieltes Entkoppeln nur im angekündigten Testablauf, USB-Abziehen und Wiederanstecken. Ergebnis jedes Schrittes im Hardwareprotokoll als bestanden, fehlgeschlagen oder nicht getestet markieren.
- [ ] Gesamtdiff auf Anforderungen und Lebenszyklusfehler prüfen; gefundene Fehler beheben und betroffene Tests wiederholen. Abschluss nennt Startbefehl, tatsächliche Testergebnisse und verbleibende Hardwaregrenzen.

## Ausführung und Review

Empfehlung: direkte Ausführung durch den Hauptagenten in dieser Sitzung, weil Hardware-Anbindung, Service und Oberfläche stark voneinander abhängen. Alternativ ist Umsetzung mit getrennten Implementierungs- und Review-Agenten möglich. Vor Implementierung werden Plan und Ausführungsart vom Nutzer bestätigt. Bei direkter Ausführung folgt abschließend ein unabhängiger Review gemäß Ausführungs-Skill.

## Quellen für die Umsetzung

- https://developer.jabra.com/sdks-and-tools/dotnet
- https://sdk.jabra.com/dotnet/docs/Sdk.DevicePairing/3.1.1/articles/bluetooth.html
- https://github.com/gnaudio/jabra-dotnet-bt-pairing-sample
- https://docs.avaloniaui.net/docs/supported-platforms

Selbstprüfung: Alle Bereiche des Entwurfs sind Aufgaben zugeordnet; die fünf Review-Fälle haben konkrete Tests. Zusätzliche Hardware wird nicht als getestet ausgegeben. Keine native API-Signatur außerhalb der dokumentierten Pairing-API voraussetzen; installierte Paketmetadaten sind bei Implementierung maßgeblich.
