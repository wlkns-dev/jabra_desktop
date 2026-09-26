# Jabra Desktop 0.2.0: Zustimmung, Sprache und Tray-Einstellungen

## Ziel

Einen installierbaren Release-Stand vorbereiten, der die Jabra-SDK-Bedingungen vor dem SDK-Start anzeigt und eine aktive Zustimmung verlangt. Die Oberfläche soll Deutsch und Englisch unterstützen, die ausgewählte Sprache dynamisch wechseln, die Darstellung im Tray-Menü anbieten und die Programmversion dort anzeigen.

## Nutzer und Erfolgskriterien

- Beim ersten Start sowie nach einer Änderung der angezeigten SDK-Bedingungen muss der lokale Benutzer zustimmen, bevor Jabra-SDK-Komponenten geladen oder initialisiert werden.
- Bei Ablehnung endet die Anwendung ohne Hardwarezugriff.
- Die Erstwahl der Oberfläche folgt der Betriebssystemsprache: Deutsch bei einer deutschen Locale, sonst Englisch. Der Benutzer kann die Sprache jederzeit im Tray-Menü ändern; die Wahl bleibt für diesen Benutzer gespeichert.
- Die Darstellung kann im Tray-Menü auf System, Hell oder Dunkel gestellt werden. Die Einstellung wird pro Benutzer gespeichert und ohne Neustart angewandt.
- Die Tray-Menüzeile zur Programmversion verwendet dieselbe Buildversion wie Paket und Release.
- Alle eigenen sichtbaren Texte, einschließlich Zustimmung, Menüs, Geräteaktionen, Status, Fehlerdialoge und Suchzustände, sind in beiden unterstützten Sprachen verfügbar.

## Entwurf

### Zustimmungsablauf

Der vorhandene Startpfad prüft nach dem Single-Instance-Schritt die Zustimmung, bevor `JabraBackend` beziehungsweise `DeviceSession` erzeugt wird. Ohne gültige Zustimmung erscheint ein Fenster mit einem scrollbaren, auf Deutsch oder Englisch lokalisierten Text, einem expliziten Zustimmungsbutton und einer Ablehnungsaktion. Bei Ablehnung wird die Anwendung beendet. Kann die Zustimmung nicht sicher gespeichert werden, startet das SDK ebenfalls nicht; dem Benutzer wird ein verständlicher Fehler angezeigt.

Die Zustimmung wird im XDG-Konfigurationsverzeichnis des aktuellen Benutzers mit einer eigenen Terms-Version und Zeitstempel gespeichert. Eine neue Terms-Version erzwingt erneut die Zustimmung. Die UI zeigt die Jabra-Bedingungen und die projektspezifischen Nutzungshinweise direkt an; dieselben Texte werden zusätzlich im Arch-Paket unter `/usr/share/licenses/jabra-desktop/` installiert.

Der Text beschränkt die Jabra-SDK-Nutzung auf die Verwendung im Zusammenhang mit GN-Audio-Produkten und nennt die von Jabra geforderten Ausschlüsse von Gewährleistung und Haftung für GN Audio und Drittanbieter, jeweils soweit gesetzlich zulässig. Der Text behauptet keine Einschränkung zwingender gesetzlicher Rechte des Benutzers. Quelle und Geltungsfassung der Jabra-Bedingungen werden im Text verlinkt. Diese UI erfüllt die technische Zustimmungsschranke; sie stellt keine juristische Beratung dar.

### Sprache

Eine kleine, interne Lokalisierungskomponente hält die aktuelle Sprache und liefert die eigenen UI-Texte aus einem zentralen Deutsch-/Englisch-Katalog. XDG-Benutzereinstellungen speichern nur eine explizit vom Benutzer gewählte Sprache. Ohne gespeicherte Auswahl entscheidet `CurrentUICulture`; Deutsch-Locale ergibt Deutsch, alle anderen Locales fallen auf Englisch zurück. Ein Sprachwechsel aktualisiert offene UI-Texte, Tray-Menü und ViewModel-Status ohne Neustart.

SDK-Fehlercodes und interne Fehlertexte erhalten lokalisierte App-Texte, soweit sie durch die Anwendung erzeugt werden. Herstellertexte, die das SDK selbst liefert, werden unverändert weitergegeben.

### Tray-Menü, Darstellung und Version

Das Tray-Menü enthält:

- Fenster öffnen
- Sprache: Deutsch / English
- Darstellung: System / Hell / Dunkel
- Autostart
- eine deaktivierte Versionszeile, zum Beispiel `Jabra Desktop 0.2.0`
- Beenden

Die bestehende Theme-Einstellung aus der Seitenleiste wird entfernt. Bei „System“ folgt Avalonia der Systemvariante. Hell/Dunkel ändern die laufende Oberfläche sofort. Benutzerpräferenzen liegen unter `${XDG_CONFIG_HOME:-$HOME/.config}/jabra-desktop/`.

Die Versionszeile liest `AssemblyInformationalVersion` beziehungsweise die zentrale MSBuild-Version. `Directory.Build.props`, `packaging/PKGBUILD`, annotiertes Git-Tag und Release sollen dieselbe Version `0.2.0` verwenden.

### Fehlerverhalten und Datenschutz

- Fehler beim Laden der Einstellungen führen zu Standardwerten; die Zustimmung wird dagegen fail-closed behandelt.
- Ein Fehler beim Schreiben einer neuen Zustimmung verhindert die SDK-Initialisierung und bietet eine klare Meldung.
- Die Anwendung speichert nur notwendige Sprache, Theme und Terms-Zustimmung. Keine Bluetooth-Adressen, Seriennummern oder zusätzlichen Telemetriedaten werden dafür erhoben.
- Ablehnen ist gleichwertig mit Beenden und hinterlässt keine Zustimmung.

## Dateien und Verantwortlichkeiten

- `src/JabraDesktop.App/App.axaml.cs`: Zustimmung vor Backend-Erzeugung einhängen; lokalisierte Startup-Dialoge.
- `src/JabraDesktop.App/App.axaml`: dynamisch erzeugtes Tray-Menü einschließlich Sprache, Darstellung und Versionszeile.
- `src/JabraDesktop.App/Views/MainWindow.axaml` und `MainWindow.axaml.cs`: lokalisierbare UI und Entfernen des Sidebar-Theme-Schalters.
- `src/JabraDesktop.App/ViewModels/MainViewModel.cs`: lokalisierte Status- und Aktionsbeschriftungen aktualisieren.
- Neue fokussierte Komponenten unter `src/JabraDesktop.App/`: Einstellungen, Locale-Katalog und Terms-Zustimmung.
- `src/JabraDesktop.App/JabraDesktop.App.csproj` oder Ressourcenordner: deutsche und englische Terms-Texte einbetten.
- `packaging/PKGBUILD`: Terms-Dateien unter `/usr/share/licenses/jabra-desktop` mit installieren.
- `Directory.Build.props`, `packaging/PKGBUILD`, `README.md`, `THIRD-PARTY-NOTICES.md`: Release-Version und Installationshinweise synchronisieren.
- `tests/JabraDesktop.Tests/`: Verhalten der Spracheinstellung, Präferenzspeicherung und Zustimmungsschranke absichern.

## Teststrategie

- Unit-Tests: System-Locale-Auswahl, Sprachwechsel und persistierte Sprachpräferenz.
- Unit-Tests: System/Hell/Dunkel als persistente Auswahl; ungültige oder fehlende Settings werden sicher auf Defaults zurückgesetzt.
- Unit-Tests: keine Zustimmung, Ablehnung und nicht speicherbare Zustimmung blockieren Backend-Start; gültige aktuelle Zustimmung erlaubt ihn; Terms-Versionswechsel fordert erneut Zustimmung.
- Unit-Tests: Versionsanzeige bezieht die zentrale Assemblyversion.
- Gesamttests: `./scripts/dotnet.sh test -m:1`, Release-Build/Publish und Erzeugen des Arch-Pakets.
- Paketprüfung: Lizenztexte und Terms-Dateien sind enthalten, und der Starter funktioniert weiterhin.
- Manueller UI-Test: DE/EN-Wechsel im laufenden Fenster, Darstellung System/Hell/Dunkel, Zustimmung/Ablehnung sowie Tray-Menü mit Versionszeile unter der installierten Desktop-Sitzung.

## Veröffentlichung

Der Stand wird als `0.2.0` vorbereitet, weil er neue Nutzerzustimmung, zwei Sprachen und neue Tray-Einstellungen einführt. Vor Veröffentlichung werden Tag und Paketversion abgeglichen und das erzeugte Paket mit Prüfsumme als Asset des GitHub Releases `v0.2.0` hochgeladen. Das bisherige `v0.1.1`-Paket bleibt unverändert.

## Offene Abwägungen

- Die Zustimmung bezieht sich auf eine klar versionierte lokale Wiedergabe der relevanten Jabra- und Drittanbieterbedingungen. Bei einer später materiell veränderten Jabra-Bedingung muss die `TermsVersion` angehoben werden.
- Die deutsche Fassung und ihre englische Übersetzung sollen inhaltlich deckungsgleich sein und dürfen den von Jabra verlangten Umfang nicht erweitern.
