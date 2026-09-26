# Jabra Desktop: Gerätefähigkeiten und inkrementelle Releases

## Ziel

Jabra Desktop soll die tatsächlich verfügbaren Funktionen je erkanntem Jabra-Dongle und Headset sichtbar und zuverlässig verwalten. Neue Funktionen werden anhand realer Hardware schrittweise ergänzt und in installierbaren Arch-Paketen sowie GitHub-Releases ausgeliefert.

## Nutzer und Erfolgskriterien

- Der Nutzer kann mehrere angeschlossene Jabra-Dongles auseinanderhalten und deren jeweils verwaltete Headsets sehen.
- Die Oberfläche zeigt nur Funktionen an, die für das konkrete Gerät und den aktuellen SDK-Pfad verfügbar sind.
- Fehlende oder nicht unterstützte Informationen werden als nicht verfügbar dargestellt, niemals geraten.
- Geräteunterschiede und Testergebnisse sind pro Modell, Dongle-Variante und relevanter Firmware dokumentiert.
- Jede ausgelieferte Stufe ist ein getestetes, installierbares Paket mit synchroner App-, Paket- und Tag-Version.
- Die erste Hardwarebasis umfasst mindestens den vorhandenen Link 370, Link 380 und die dem Nutzer verfügbaren Headsets; genaue Headset-Modelle werden beim Test inventarisiert.

## Bestehender Stand

Die App erkennt angeschlossene Jabra-Geräte dynamisch. Für Geräte, bei denen `BluetoothModule.CreateBluetoothDongle` erfolgreich einen Dongle erzeugt, kann sie Kopplungen auflisten, nach Headsets im Pairing-Modus suchen und Pair, Connect, Disconnect sowie Unpair ausführen. Andere Jabra-Geräte verbleiben in der Geräteübersicht. Die UI enthält bereits Felder für Akku und Firmware, der Backend-Adapter befüllt diese zurzeit aber nicht.

Die eingesetzten NuGet-Module sind `Jabra.NET.Sdk` 4.9.1.1 und `Jabra.NET.Sdk.DevicePairing` 3.1.1.2. Properties und Button Customization sind noch nicht eingebunden. Jabra dokumentiert Pairing mit BT- und DECT-Dongles; Properties und Button Customization sind optionale, modellabhängige SDK-Funktionen. Jabra nennt Ubuntu 22.04+ als unterstützte Linux-Distribution; Arch bleibt in diesem Projekt eine praktisch zu validierende Zielplattform.

## Entwurf

### Geräte- und Fähigkeitsmodell

Die Geräteübersicht unterscheidet mindestens physisches Jabra-Gerät, Dongle-Rolle, Modell-/Produktinformationen soweit zuverlässig vorhanden, verwaltete Headsets und verfügbare Aktionen. Fähigkeiten werden explizit durch den passenden Adapter ermittelt und nicht aus einem Anzeigenamen abgeleitet. Die Kernschicht bleibt SDK-unabhängig und erhält stabile, optionale Modellfelder und Fähigkeitsflags. Fehlschläge beim Prüfen eines optionalen Moduls dürfen die bestehende Erkennung und Kopplungsverwaltung nicht verhindern.

Seriennummern und Bluetooth-Adressen bleiben interne Adapterdaten und werden nicht in der UI, Diagnose oder Hardwarematrix protokolliert. Für lokale Tests können genaue Produktnamen, VID:PID, SDK-/Connector-Version und Firmwareversionen festgehalten werden, sofern auslesbar und ohne individuelle Kennungen.

### Hardwarematrix

`docs/hardware-validation.md` wird pro Release aktualisiert. Ein Eintrag beschreibt Dongle-Modell und Variante (UC/MS/Teams, sofern bekannt), Headset-Modell und Variante, OS/Kernel, Jabra SDK und Device Connector-Version, Erkennung, Pairing-List-Lesezugriff, Scan, Pair/Connect/Disconnect/Unpair, Properties-Verfügbarkeit, gelieferte Beispielwerte und offene Probleme. Nicht getestete Kombinationen bleiben ausdrücklich unbestätigt. Entkoppeln ist ein destruktiver Hardwaretest und wird nur an einer eigens ausgewählten Kopplung durchgeführt.

### Release-Stufen

#### 0.3.0 – Gerätebestand und Fähigkeiten

Ziel ist eine verlässliche Geräteübersicht für mehrere Dongles. Modelle und Verbindungsrollen werden nach Möglichkeit aus stabilen SDK-Metadaten erkannt. Verfügbare Bluetooth-/DECT-Verwaltung und nicht unterstützte Geräte werden verständlich unterschieden. Die Diagnose und Hardwarematrix decken vorhandene Link 370- und Link 380-Varianten sowie angeschlossene Headsets ab. Kein Modell wird allein anhand des Produktnamens als kompatibel markiert. Bestehende Such- und Verbindungsfunktionen bleiben erhalten.

#### 0.4.0 – Lesbare Gerätestatuswerte

Das optionale Properties-Modul wird evaluiert und zunächst nur lesend integriert. Zuerst werden Property-Erkennung und Werte für Firmware sowie Akku untersucht; nur tatsächlich verfügbare, korrekt typisierte Werte werden in der UI gezeigt. Eigenschaften werden anhand einer geprüften Capability-Liste pro Gerät erstellt. Nicht unterstützte Properties, Timeouts oder Geräteabzüge liefern einen begrenzten Zustand „nicht verfügbar“/„aktualisieren fehlgeschlagen“, ohne den Gerätemanager zu blockieren. Telemetriebeobachtung kommt nur hinzu, wenn die Hardware sie liefert und ihr Aktualisierungsverhalten sinnvoll ist.

#### 0.5.0 – Geräteeinstellungen

Nach dokumentierter Modellprüfung werden ausgewählte, risikoarme Einstellungen lesbar und änderbar gemacht. UI-Controls erscheinen nur für nachgewiesen unterstützte Properties. Vor dem Schreiben werden Wertebereiche validiert und potenzielle Neustarts oder Verbindungsunterbrechungen angezeigt. Schreiben ist transaktional; Fehler und Wiederverbindung werden nachvollziehbar behandelt. Dieser Release wird pro Gerätegruppe freigegeben, statt Unterstützung pauschal auf alle Jabra-Geräte auszuweiten.

#### 0.6.0 – Tasten- und LED-Anpassung

Das optionale Button Customization SDK-Modul wird für konkrete unterstützte Headsets untersucht. Funktionen und UI werden nur für getestete Button-/LED-Fähigkeiten angeboten. Vorhandene Zuordnungen werden vor Änderungen lesbar gemacht, wo das SDK dies erlaubt; Fehler dürfen keine partiellen Änderungen verschweigen.

#### Danach – Plattform- und Herstellererweiterungen

Easy Call Control ist ein möglicher separater Softphone-Integrationspfad und gehört nicht zum Kern der Dongle-Verwaltung. Für Nicht-Jabra-Geräte wird ein eigener Adapter beziehungsweise eine Linux-Standard-API untersucht; der Jabra-SDK selbst ist kein herstellerübergreifendes Geräte-API. Firmwareupdates werden nicht durch Properties ersetzt und bleiben außerhalb des Umfangs, bis eine unterstützte, sichere Offline-Update-Schnittstelle sowie Verteilungs- und Rechtefragen geklärt sind.

## Release- und Testprozess

Jede Stufe wird in einem eigenen Feature-Branch inkrementell umgesetzt. Jede Funktion erhält passende Kern- und Adaptertests mit Fake-Backends, anschließend werden die vorhandenen Tests ausgeführt. Hardwaretests werden auf realen Geräten gemäß Matrix protokolliert; nicht verfügbare Hardware blockiert nicht die SDK-unabhängigen Tests, wird aber als offene Hardwareabdeckung im Release dokumentiert.

Vor Veröffentlichung werden Versionen, Paketinhalt, Starter, Tray-Verhalten, deutsche und englische UI, Lizenzdateien, `git diff --check` und vollständige Tests geprüft. Der Arch-Paketbau muss erfolgreich sein. Danach wird ein annotiertes `vX.Y.Z`-Tag erstellt und GitHub Release mit Paket und SHA-256 veröffentlicht. Funktionsreleases erhöhen SemVer; reine Paketkorrekturen erhöhen `pkgrel`. Kein Release behauptet Modellunterstützung, die nicht in der Matrix verifiziert wurde.

## Dateien und Verantwortlichkeiten

- `src/JabraDesktop.Core/Models.cs`: stabile, optionale Geräte- und Fähigkeitsinformationen.
- `src/JabraDesktop.Core/IDeviceBackend.cs`: SDK-unabhängige Funktionen und Datenzugriff.
- `src/JabraDesktop.Jabra/JabraBackend.cs`: Jabra-Geräteerkennung, Pairing und optionale Properties-/Button-Adapter.
- `src/JabraDesktop.Jabra/DeviceMapper.cs`: sichere Normalisierung SDK-gelieferter Modell- und Statuswerte.
- `src/JabraDesktop.App/ViewModels/MainViewModel.cs` und `src/JabraDesktop.App/Views/MainWindow.axaml`: Geräteübersicht und capability-basierte Anzeige.
- `tests/JabraDesktop.Tests/`: Tests für optionale Fähigkeiten, Mehrfachgeräte, Fehler- und Geräteabzug.
- `docs/hardware-validation.md`: Versions- und Gerätekombinationsmatrix.
- `docs/releases/`: Nutzerhinweise, Hardwareabdeckung und Installationspaket für jeden Release.
- `README.md`: aktuelle Grenzen und Installationsanleitung.

## Akzeptanz für jeden Release

- Alle vorhandenen automatisierten Tests sind erfolgreich.
- Vorhandene Pairing- und Verbindungsabläufe bleiben funktionsfähig.
- Nicht unterstützte optionale Eigenschaften sind ein regulärer Zustand und lösen keinen App-Absturz aus.
- Mehrere Dongles werden eindeutig und unabhängig verwaltet.
- Die Hardwarematrix nennt genau die geprüften Modelle und Aktionen.
- Das Paket lässt sich auf dem Zielsystem aktualisieren und starten; Versionen von App, Paket, Tag und Release stimmen überein.

## Referenzen

- Jabra .NET SDK und Plattform-/Modulübersicht: https://developer.jabra.com/sdks-and-tools/dotnet
- Device Pairing: https://developer.jabra.com/sdks-and-tools/dotnet/device-pairing
- Properties: https://developer.jabra.com/sdks-and-tools/dotnet/properties
- Samples und API-Referenzen: https://developer.jabra.com/sdks-and-tools/dotnet/samples-and-reference
