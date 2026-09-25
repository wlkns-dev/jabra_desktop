# Arch Package and Tray Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Jabra Desktop als versioniertes Arch-Paket mit KDE-Tray, optionalem Autostart und dokumentiertem Selbstbau installieren und betreiben.

**Architecture:** Die Avalonia-App verwaltet Tray, Fensterlebenszyklus und Benutzer-Autostart; eine benutzerspezifische Einzelinstanz koordiniert Starter und Autostart, bevor das Jabra-SDK initialisiert wird. Ein lokales PKGBUILD paketiert das Linux-x64-Publish sowie Launcher, Desktop-Datei, Symbol und udev-Regel; Git- und Paketversionen werden synchron gehalten.

**Tech Stack:** C#/.NET 10, Avalonia 11.3.10 `TrayIcon`, vorhandenes `Tmds.DBus.Protocol` für StatusNotifier-Verfügbarkeit, Bash, Arch `makepkg`/`pacman`, XDG Autostart.

**Spec:** [Arch-Paket und Tray-Integration](../specs/2026-09-25-arch-package-tray-design.md)

## Global Constraints

- Ziel sind CachyOS und andere Arch-Linux-Systeme; erste Tray-Zielplattform ist KDE Plasma unter Linux.
- Ein `pkg.tar.zst` lässt sich lokal mit `makepkg` aus dem vorhandenen selbständigen Linux-x64-Publish erstellen und mit `pacman -U` installieren.
- Das Paket installiert die Anwendung, den Desktop-Starter, das App-Symbol und die Jabra-udev-Regel an systemübliche Pfade. Bei der Deinstallation entfernt Pacman diese Paketdateien wieder.
- Das Tray-Menü enthält „Öffnen“, einen optionalen Autostart-Schalter und „Beenden“.
- Das Schließen des Fensters blendet die Anwendung ins Tray aus. „Öffnen“ zeigt das bestehende Fenster; „Beenden“ beendet den Prozess und gibt das Jabra SDK frei.
- Es läuft höchstens eine Instanz, damit manueller Start und Autostart nicht mehrere SDK-Verbindungen öffnen.
- Autostart ist optional, standardmäßig aus. Wenn aktiviert, startet die Anwendung minimiert ins Tray.
- Änderungen werden in Git committet. Freigegebene Softwarestände erhalten annotierte Versions-Tags nach dem Muster `vMAJOR.MINOR.PATCH`; `pkgver` folgt der Softwareversion und `pkgrel` steigt für Paketkorrekturen.
- Es gibt vorerst keinen eigenen In-App-Updater und keine Hintergrundprüfung auf neue Versionen.
- Fertige öffentliche Paketdownloads erfolgen erst nach Klärung der Weitergabebedingungen der Jabra-SDK-Binärdateien.

## Review Focus

1. Zweiter Start während die erste Instanz noch läuft: zweite Instanz initialisiert nicht das Jabra-SDK, sondern zeigt das existierende Fenster (Aufgabe 1; manuell mit zwei Starts prüfen).
2. Autostart-Datei kann nicht geschrieben oder entfernt werden: Tray-Schalterzustand bleibt konsistent und die UI zeigt eine konkrete Meldung (Aufgabe 2; mit nicht beschreibbarem Zielpfad simulieren).
3. Tray-Host fehlt oder wird beendet: Nutzer bleibt nicht mit einem unsichtbaren, unerreichbaren Prozess zurück (Aufgabe 3; Tray-Verhalten unter KDE sowie ohne StatusNotifier-Host prüfen, soweit verfügbar).
4. Paketinstallation, Upgrade und Entfernung: installierte Pfade und Starter verweisen nicht auf den Checkout, und Pacman entfernt keine fremden Nutzerdaten (Aufgabe 4; Paket lokal bauen, Paketinhalt ansehen und Installationspfade prüfen).
5. Git-Tag, App-Version und `pkgver` weichen voneinander ab: Paketversion und Anzeige bleiben nachvollziehbar (Aufgabe 5; Versionswerte vor einem Release vergleichen).

---

## Datei- und Komponentenkarte

| Datei | Verantwortung |
|---|---|
| `src/JabraDesktop.App/SingleInstanceCoordinator.cs` | Pro Benutzer eine primäre App-Instanz halten; sekundäre Aufrufe an sie weiterleiten. |
| `src/JabraDesktop.App/AutostartManager.cs` | XDG-Autostartdatei des aktuellen Benutzers atomar anlegen, entfernen und Zustand lesen. |
| `src/JabraDesktop.App/TrayAvailability.cs` | Vor verborgenem Start den StatusNotifierWatcher über die vorhandene D-Bus-Bibliothek erkennen. |
| `src/JabraDesktop.App/App.axaml` | Avalonia-Trayicon, Menü und gebündeltes Icon definieren. |
| `src/JabraDesktop.App/App.axaml.cs` | Single-Instance, Tray, Fenster und SDK-Sitzung in klarer Start-/Shutdown-Reihenfolge verbinden. |
| `src/JabraDesktop.App/Views/MainWindow.axaml.cs` | Fenster schließen in „ins Tray verstecken“ überführen und ein vorhandenes Fenster aktivieren. |
| `src/JabraDesktop.App/Assets/jabra-desktop.ico` | Tray-/Fenstersymbol als Avalonia `WindowIcon`-Ressource. |
| `packaging/jabra-desktop.svg` | Skalierbares Symbol für Arch-Desktop-Menü und Icon-Theme. |
| `Directory.Build.props` | Eine zentrale Softwareversion für alle .NET-Projekte definieren. |
| `packaging/PKGBUILD` | Vorhandenes Release-Publish in ein Arch-Paket mit systemweiten Assets überführen. |
| `packaging/jabra-desktop.desktop` | Installationspfad und Icon für Desktop-Menü festlegen. |
| `packaging/70-jabra-desktop.rules` | Bestehende Jabra-udev-Regel unter systemüblichem Namen bereitstellen. |
| `scripts/package.sh` | Release-Publish erzeugen und im Repo-Root `makepkg` ausführen. |
| `README.md` | Entwicklungsabhängigkeiten, Selbstbau, lokale Installation, Aktualisierung und Grenzen erklären. |
| `.gitignore` | Publish-/Paketartefakte ausschließen, Quellcode und Verpackungsdefinition versionieren. |

Die Projektdatei referenziert `Assets/jabra-desktop.ico` als Avalonia-Ressource; das Desktop-Menü nutzt das getrennte SVG-Icon. Paketmetadaten, Desktop-Datei und README verwenden denselben Befehlsnamen `jabra-desktop`. `PKGBUILD` verlangt das bereits veröffentlichte Verzeichnis `artifacts/linux-x64`; der dokumentierte Selbstbau führt deshalb erst `./scripts/publish.sh`, dann `makepkg -f` aus. So baut der PKGBUILD nicht verdeckt mit Netzwerkzugriff und lokalem SDK-Zustand.

## Task 1: Single-Instance-Koordination und Startargument

**Files:**
- Create: `src/JabraDesktop.App/SingleInstanceCoordinator.cs`
- Modify: `src/JabraDesktop.App/Program.cs`
- Modify: `src/JabraDesktop.App/App.axaml.cs`

**Interfaces:**
- Produces `SingleInstanceCoordinator.TryBecomePrimaryAsync(Func<Task> onActivate, CancellationToken token)`, das `true` für Primärinstanz zurückgibt und als Sekundärinstanz ein Aktivierungssignal an den Hauptprozess schickt und `false` zurückgibt.
- Produces `SingleInstanceCoordinator.DisposeAsync()` zum Beenden des benutzerspezifischen lokalen IPC-Servers.
- Produces Startoption `--autostart`; sie startet die primäre Instanz zunächst ohne sichtbares Fenster.
- Der IPC-Name enthält die numerische Benutzer-ID, damit lokale Benutzer sich nicht gegenseitig aktivieren können.

- [ ] Prüfe die .NET-10-Unterstützung für benannte Pipes unter Linux; verwende `NamedPipeServerStream` mit einem festen App-Namen plus UID, asynchronem Server und Nachricht `show`.
- [ ] Implementiere die Primärinstanz: Pipe-Server dauerhaft starten, `show`-Nachrichten auf den UI-Dispatcher posten und Server beim App-Shutdown abbrechen/entsorgen.
- [ ] Implementiere den Sekundärpfad: kurz mit vorhandener Pipe verbinden, `show` senden, normal beenden; wenn kein Server existiert, Primärserver übernehmen. Vor erfolgreicher Übernahme darf `JabraBackend` nicht erzeugt werden.
- [ ] Lies `--autostart` in `Program.Main(string[] args)` aus und übergib den Startmodus an `App`; behalte normale Startargumente kompatibel.
- [ ] Manuelle Abnahme: App zweimal starten; das erste Fenster wird aktiviert und es bleibt nur ein Jabra-Prozess bzw. eine SDK-Sitzung übrig. Zusätzlich einmal während Beenden den Starter erneut aufrufen und prüfen, dass eine neue Primärinstanz starten kann.
- [ ] Änderungen dieses Tasks committen: `git add src/JabraDesktop.App/Program.cs src/JabraDesktop.App/App.axaml.cs src/JabraDesktop.App/SingleInstanceCoordinator.cs && git commit -m "feat: enforce a single desktop instance"`.

## Task 2: XDG-Autostart

**Files:**
- Create: `src/JabraDesktop.App/AutostartManager.cs`
- Modify: `src/JabraDesktop.App/App.axaml.cs`

**Interfaces:**
- Produces `AutostartManager.IsEnabled` und `SetEnabled(bool enabled)`.
- Zielpfad ist `$XDG_CONFIG_HOME/autostart/jabra-desktop.desktop`, wenn `XDG_CONFIG_HOME` gesetzt und absolut ist; sonst `~/.config/autostart/jabra-desktop.desktop`.
- Datei startet `/usr/bin/jabra-desktop --autostart`; dieser Pfad gilt im installierten Paket. Die Anwendung muss beim Selbstbau ohne installiertes Paket den Autostart-Schalter als nicht verfügbar/mit Hinweis darstellen, statt einen defekten Eintrag zu erzeugen.

- [ ] Implementiere Pfadermittlung mit validiertem `XDG_CONFIG_HOME`-Fallback zu `Environment.SpecialFolder.UserProfile`.
- [ ] Implementiere atomisches Schreiben: Verzeichnis erstellen, temporäre Datei im selben Verzeichnis schreiben, dann ersetzen; `Exec` und Desktop-Datei-Metadaten für `jabra-desktop --autostart` setzen.
- [ ] Implementiere `SetEnabled(false)` durch Entfernen ausschließlich der eigenen Datei; andere Desktop-Autostartdateien bleiben unangetastet.
- [ ] Fehler aus IO-/Berechtigungsproblemen an Aufrufer durchreichen und UI-Schalter nur nach erfolgreicher Änderung aktualisieren.
- [ ] Manuelle Abnahme: aktivieren, Dateiinhalt und Pfad prüfen, deaktivieren und sicherstellen, dass nur `jabra-desktop.desktop` verschwindet; Schreibfehler provozieren und prüfen, dass der Schalter den alten Zustand behält.
- [ ] Änderungen dieses Tasks committen: `git add src/JabraDesktop.App/AutostartManager.cs src/JabraDesktop.App/App.axaml.cs && git commit -m "feat: manage per-user autostart"`.

## Task 3: Tray-Menü und Fensterlebenszyklus

**Files:**
- Create: `src/JabraDesktop.App/Assets/jabra-desktop.ico`
- Create: `packaging/jabra-desktop.svg`
- Create: `src/JabraDesktop.App/TrayAvailability.cs`
- Modify: `src/JabraDesktop.App/App.axaml`
- Modify: `src/JabraDesktop.App/JabraDesktop.App.csproj`
- Modify: `src/JabraDesktop.App/App.axaml.cs`
- Modify: `src/JabraDesktop.App/Views/MainWindow.axaml.cs`

**Interfaces:**
- Tray-Menü: `Öffnen`, `Autostart` als Check-/Toggle-Eintrag und `Beenden`.
- `MainWindow.HideToTray()` versteckt das Fenster, ohne Session/Backend zu entsorgen.
- `MainWindow.ShowAndActivate()` zeigt, normalisiert und aktiviert das vorhandene Fenster.
- `App` besitzt Trayicon und `DeviceSession` bis zum expliziten Beenden.

- [ ] Füge `Assets/jabra-desktop.ico` als `AvaloniaResource` zum App-Projekt hinzu und verwende es über `TrayIcon.Icon` vom Typ `WindowIcon`; installiere das getrennte SVG in den Linux-Icon-Theme-Pfad.
- [ ] Definiere in `App.axaml` ein `TrayIcon` mit Menü und Icon; setze Klickhandler für Öffnen, Autostart und Beenden auf App-Lifecycle-Aktionen.
- [ ] Initialisiere zuerst Einzelinstanz und Tray, dann `DeviceSession`/`JabraBackend`; bei sekundärem Start direkt zurückkehren.
- [ ] Registriere `Window.Closing`; standardmäßiges Schließen im installierten Tray-Betrieb abbrechen und `Hide()` aufrufen. Explizites Tray-„Beenden“ setzt Shutdown-Zustand, schließt das Fenster, stoppt Refresh-Timer, entsorgt Session und Tray und beendet den IPC-Server.
- [ ] Prüfe mit `Tmds.DBus.Protocol` am Session-Bus, ob `org.kde.StatusNotifierWatcher` oder `org.freedesktop.StatusNotifierWatcher` einen Besitzer hat. Öffnen per Tray oder IPC stellt das einzelne Fenster wieder her. Autostart-Modus startet nur bei verfügbarem Tray-Host verborgen; ohne Host startet das Fenster sichtbar und Schließen beendet normal.
- [ ] Verknüpfe Autostart-Menucheck mit `AutostartManager`; zeige bei Fehlern deutschsprachige Meldung und aktualisiere den Haken nur bei Erfolg.
- [ ] Manuelle Abnahme in KDE: normaler Start, Fenster schließen/Tray bleibt, Tray-Öffnen, zweiten Start, Autostart ein/aus, Start mit `--autostart`, explizites Beenden und erneutes Starten. Dongle-Erkennung muss beim Verstecken weiterlaufen und nach Tray-Beenden freigegeben werden. Wenn ein trayloser Testdesktop verfügbar ist, sicherstellen, dass `--autostart` dort sichtbar startet.
- [ ] Änderungen dieses Tasks committen: `git add src/JabraDesktop.App/App.axaml src/JabraDesktop.App/App.axaml.cs src/JabraDesktop.App/Views/MainWindow.axaml.cs src/JabraDesktop.App/JabraDesktop.App.csproj src/JabraDesktop.App/Assets/jabra-desktop.ico packaging/jabra-desktop.svg src/JabraDesktop.App/TrayAvailability.cs && git commit -m "feat: add system tray lifecycle"`.

## Task 4: Arch-Paket und reproduzierbarer Selbstbau

**Files:**
- Create: `packaging/PKGBUILD`
- Create: `scripts/package.sh`
- Modify: `packaging/jabra-desktop.desktop`
- Modify: `packaging/70-jabra-desktop.rules`
- Modify: `.gitignore`

**Interfaces:**
- Paketname: `jabra-desktop`; Binary/Starter: `/usr/bin/jabra-desktop`; App-Payload: `/opt/jabra-desktop`; desktop-Datei: `/usr/share/applications/jabra-desktop.desktop`; Symbol: `/usr/share/icons/hicolor/scalable/apps/jabra-desktop.svg`; Regel: `/usr/lib/udev/rules.d/70-jabra-desktop.rules`.
- `scripts/package.sh` führt `scripts/publish.sh` und danach `makepkg -f` aus dem Checkout aus.
- `PKGBUILD` akzeptiert lokal vorhandenes `artifacts/linux-x64`, installiert nur Linux-Publish-Inhalte und enthält keine Checkout-absoluten Pfade.

- [ ] Implementiere `packaging/PKGBUILD` mit `pkgname=jabra-desktop`, `pkgver=0.1.0`, `pkgrel=1`, `arch=('x86_64')`, `license=('custom')`, `depends=('glibc' 'gcc-libs' 'libudev.so')`, `options=(!strip)` und lokalen Quelldateien; übernimm die tatsächlichen Drittanbieterhinweise als installierte Lizenzdatei.
- [ ] In `package()`, kopiere den Publish-Inhalt nach `/opt/jabra-desktop`, lege `/usr/bin/jabra-desktop` als ausführbaren Wrapper an, installiere Desktop-Datei/SVG/udev-Regel in obige Pfade und setze Dateimodi. Nutze `install -Dm...` statt direkter Kopien in absolute Systempfade während Paketbau.
- [ ] Passe Desktop-Eintrag an installierte Pfade an: `Exec=jabra-desktop`, `Icon=jabra-desktop`; kein Benutzer-Checkout-Pfad.
- [ ] Ergänze `.gitignore` um `artifacts/`, `packaging/*.pkg.tar.*`, `packaging/src/`, `packaging/pkg/` und `packaging/*.log`; `PKGBUILD` bleibt versioniert.
- [ ] Ergänze `scripts/package.sh` mit `set -euo pipefail`, Repository-Root-Ermittlung, Aufruf von `scripts/publish.sh`, Wechsel in `packaging/` und `makepkg -f`.
- [ ] Manuelle Abnahme: `./scripts/package.sh`; Ergebnisdatei mit `pacman -Qip` inspizieren und mit `bsdtar -tf` Inhalt/Pfade prüfen. Paketinstallation und Entfernung auf dem Zielsystem erst nach sichtbarer Ausgabe/Berechtigungsfreigabe durchführen.
- [ ] Änderungen dieses Tasks committen: `git add packaging/PKGBUILD packaging/jabra-desktop.desktop packaging/70-jabra-desktop.rules scripts/package.sh .gitignore && git commit -m "build: package Jabra Desktop for Arch"`.

## Task 5: Versionierung, Nutzeranleitung und Release-Grenze

**Files:**
- Modify: `Directory.Build.props`
- Modify: `packaging/PKGBUILD`
- Modify: `README.md`
- Modify: `THIRD-PARTY-NOTICES.md` nur falls Installationspfad der Notices in Task 4 ergänzt werden muss.

**Interfaces:**
- .NET-Version aus zentralem MSBuild-Property `Version`.
- Paketversion `pkgver` stimmt mit der Softwareversion ohne führendes `v` überein; Paketkorrektur erhöht allein `pkgrel`.
- Keine Upload-Automation und keine In-App-Updateprüfung in dieser Stufe.

- [ ] Setze `Version` zentral in `Directory.Build.props` auf `0.1.0` und verweise in `PKGBUILD` auf denselben initialen Stand. Bei einer neuen Softwareversion `pkgver` passend zum annotierten Tag `vMAJOR.MINOR.PATCH` ändern; bei reiner Paketkorrektur ausschließlich `pkgrel` erhöhen.
- [ ] Schreibe README-Anleitung für CachyOS/Arch: Voraussetzungen (lokales .NET 10 SDK und `base-devel`), `git clone`, `./scripts/package.sh`, Paketdatei finden, `sudo pacman -U ./packaging/jabra-desktop-*.pkg.tar.zst`, Start über Menü/`jabra-desktop`, Entfernen mit `sudo pacman -R jabra-desktop`.
- [ ] Ergänze Updateanleitung: Paketkorrekturen/Softwareversion anhand Git-Tag, neue Paketdatei bauen und mit `pacman -U` installieren; erkläre, dass GitHub Release-Dateien erst nach Lizenzklärung veröffentlicht werden und Pacman-Repositories/In-App-Updater nicht Teil dieser Version sind.
- [ ] Dokumentiere, dass Autostart pro Benutzer gespeichert wird und beim Paketentfernen als Benutzerkonfiguration bestehen bleiben kann; README nennt Löschen von `~/.config/autostart/jabra-desktop.desktop` (oder XDG-Pfad) als manuelle Bereinigung.
- [ ] Manuelle Abnahme: `./scripts/dotnet.sh --version`/SDK-Ausgabe, `git describe --tags --always`, `Version` in `Directory.Build.props` und `pkgver` vergleichen; README-Kommandos gegen erzeugte Paketdatei und installierte Desktop-Datei abgleichen.
- [ ] README weist darauf hin, nach der Paketinstallation den Dongle erneut anzustecken, falls die udev-Regel noch nicht greift; sie erklärt auch das manuelle Neuladen der udev-Regeln, ohne Änderungen an Systemrechten zur Laufzeit.
- [ ] Änderungen dieses Tasks committen und nach Abschlussreview den annotierten Tag setzen: `git add Directory.Build.props packaging/PKGBUILD README.md && git commit -m "docs: document Arch package and releases"`, danach `git tag -a v0.1.0 -m "Jabra Desktop 0.1.0"`.

## Ausführungs-Handoff

Empfehlung: Native, direkte Umsetzung durch denselben Implementierer taskweise; Tray-Lebenszyklus, Einzelinstanz und Paketdateien hängen eng zusammen und müssen gemeinsam auf KDE geprüft werden. Nach Umsetzung folgt ein unabhängiger Review des Gesamtdiffs. Öffentliche Veröffentlichung oder Upload fertiger Jabra-SDK-Pakete ist ausdrücklich ausgenommen, bis Weitergabebedingungen geklärt sind.
