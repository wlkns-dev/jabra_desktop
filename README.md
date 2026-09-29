# Jabra Desktop für Linux

Deutsch/englische Desktop-App zum Verwalten mehrerer Jabra-Bluetooth-Dongles.
Dongles erscheinen mit eingerückten, zugehörigen Headsets und Speakern. Geräte
lassen sich suchen, koppeln, verbinden, trennen und gezielt entkoppeln. Die App
bietet eine helle/dunkle Oberfläche und automatische Geräteerkennung.

## Starten

Im Projektverzeichnis:

```bash
./scripts/run.sh
```

Das fertig veröffentlichte Programm liegt nach dem Build unter
`artifacts/linux-x64/JabraDesktop.App` und enthält die .NET-Laufzeit.
Es benötigt keinen laufenden Webserver. Die SDK-Gerätekommunikation verwendet
einen lokalen Jabra-Hilfsprozess. Die App als normaler Benutzer starten.

Der paketierte Desktop-Eintrag verwendet den festen Starter `jabra-desktop`.
Wenn du aus dem Quellcheckout arbeitest, starte die App mit `./scripts/run.sh`;
der paketierte Menüeintrag ist für die Systeminstallation gedacht.

## Arch-Paket erstellen und installieren

Auf Arch Linux oder CachyOS wird `base-devel` für `makepkg` benötigt. Außerdem
muss das .NET-10-SDK verfügbar sein; die obige Entwicklungsanleitung richtet
Version 10.0.401 lokal unter `.tools/dotnet` ein. Aus einem zugänglichen
Checkout:

```bash
./scripts/package.sh
package_file="$(cd packaging && makepkg --packagelist)"
sudo pacman -U "$package_file"
```

Das Paket installiert die App nach `/opt/jabra-desktop`, den Starter
`/usr/bin/jabra-desktop`, den Menüeintrag, das Icon, die udev-Regel und die
Drittanbieterhinweise. Danach kann Jabra Desktop über das Anwendungsmenü oder
mit `jabra-desktop` gestartet werden. Falls der Dongle noch keinen Zugriff hat,
die udev-Regeln neu laden und den Dongle erneut anstecken:

```bash
sudo udevadm control --reload-rules
```

Beim ersten Start zeigt Jabra Desktop die Nutzungsbedingungen für die
eingebundenen Jabra-Komponenten. Die Geräteverwaltung startet erst nach
Zustimmung. Die deutsche und englische Fassung wird mit dem Paket unter
`/usr/share/licenses/jabra-desktop/` installiert.

Das Tray-Menü kann Sprache (Deutsch/English), Darstellung (System/Hell/Dunkel)
und den Autostart für den aktuellen Benutzer einstellen. Sprache und Darstellung
werden gespeichert und sofort angewandt. Die Programmversion steht ebenfalls
im Tray-Menü. „Öffnen“ stellt das Fenster wieder her; „Beenden“ schließt die
Anwendung vollständig. Ein Fensterschluss blendet sie ins Tray aus, solange
eine Tray-Umgebung verfügbar ist. Fällt diese weg, wird ein verborgenes Fenster
wieder eingeblendet und der nächste Fensterschluss beendet die Anwendung.
Autostart ist standardmäßig aus. Der Eintrag liegt in
`${XDG_CONFIG_HOME:-$HOME/.config}/autostart/jabra-desktop.desktop`. Beim
Entfernen des Pakets bleibt diese persönliche Einstellung erhalten und kann bei
Bedarf manuell gelöscht werden.

Ein Update aus einem Checkout wird lokal gebaut und mit Pacman installiert:

```bash
git pull
./scripts/package.sh
package_file="$(cd packaging && makepkg --packagelist)"
sudo pacman -U "$package_file"
```

Fertige Arch-Pakete werden als Assets in den [GitHub Releases](https://github.com/wlkns-dev/jabra_desktop/releases)
bereitgestellt. Für Release `v0.5.0` lädst du das Paket herunter und installierst
es zum Beispiel so:

```bash
curl -fLO https://github.com/wlkns-dev/jabra_desktop/releases/download/v0.5.0/jabra-desktop-0.5.0-1-x86_64.pkg.tar.zst
sudo pacman -U ./jabra-desktop-0.5.0-1-x86_64.pkg.tar.zst
```

## Ubuntu-Paket

Für Ubuntu 22.04, 24.04 und 26.04 auf amd64 kannst du das `.deb` aus dem
[GitHub-Release](https://github.com/wlkns-dev/jabra_desktop/releases) laden
und mit APT installieren:

```bash
sudo apt install ./jabra-desktop_0.5.0_amd64.deb
```

Aus einem Quellcheckout lässt sich das Paket mit `./scripts/package-deb.sh`
erstellen. Nach der Installation der USB-Regel den Dongle einmal abziehen und
wieder einstecken, damit die Geräteberechtigung greift.

Softwarestände verwenden annotierte Git-Tags wie `v0.5.0`. Die Version in
`Directory.Build.props`, `pkgver` im PKGBUILD, Paket und Release bleibt synchron;
für eine reine Paketkorrektur wird `pkgrel` erhöht. Einen eingebauten Updater
gibt es nicht. AUR und ein automatisiertes Pacman-Repository sind noch nicht
eingerichtet.

Deinstallation:

```bash
sudo pacman -R jabra-desktop
```

Falls der Autostart zuvor aktiviert war, kann die persönliche Datei zusätzlich
entfernt werden:

```bash
rm -f "${XDG_CONFIG_HOME:-$HOME/.config}/autostart/jabra-desktop.desktop"
```

## Geräte verwenden

1. Einen oder mehrere Link-Dongles einstecken. Jeder Dongle erscheint links als
   eigene Gruppe; gerade verbundene Geräte stehen darunter. Getrennte,
   gespeicherte Geräte stehen in der Kopplungsübersicht des Dongles.
2. Ein Headset oder einen Speaker auswählen. Rechts erscheinen Status, passende
   Gerätewerte und die Aktionen Verbinden/Trennen sowie Entkoppeln. Bei Auswahl
   des Dongles erscheinen Suche und Kopplungsübersicht.
3. Zum Hinzufügen das Headset in seinen Pairing-Modus schalten und
   **Gerät hinzufügen** wählen. Suche nach spätestens 30 Sekunden beendet;
   mit **Suche beenden** kann sie früher beendet werden.
4. Anschließend am gefundenen Gerät **Koppeln** drücken.
5. Über **⋯ → Entkoppeln** lässt sich eine einzelne Kopplung nach Bestätigung entfernen.
6. Unterstützt ein verbundenes Headset die beschreibbare Eigenschaft
   `bluetoothName`, kannst du seinen Gerätenamen über **⋯ → Gerätenamen
   ändern** oder in der Geräteansicht ändern. Der Name wird im Headset gespeichert;
   bei der Suche kann er erst nach einer erneuten Verbindung erscheinen. Für
   getrennte oder nicht unterstützte Geräte wird die Aktion nicht angeboten.

Alle Dongle-Gruppen werden beim Start und anschließend alle vier Sekunden aktualisiert.
Ein unbekannter Status wird niemals als bestätigte Verbindung ausgegeben.

Firmware wird für Dongles und Endgeräte, der Akku für Headsets und Speaker
angezeigt, sofern das SDK die jeweilige Eigenschaft liefert. Ist eine Eigenschaft
für die Geräteklasse relevant, aber nicht lesbar, erscheint „Nicht verfügbar“.
Gepaarte Endgeräte werden anhand der SDK-Verbindung dem passenden Dongle
zugeordnet. Ihre Werte werden im Hintergrund höchstens alle 30 Sekunden sowie
auf Knopfdruck gelesen. Stellt das SDK kein auslesbares Endgerät bereit, bleiben
die Werte ausdrücklich nicht verfügbar. Dongles zeigen kein Akku-Feld.

## USB-Berechtigungen

Bei „USB-Zugriff verweigert“ einmalig die eng auf Jabra-HID-Geräte begrenzte
Regel installieren:

```bash
sudo ./scripts/install-udev.sh
```

Danach Dongle abziehen und wieder einstecken. Die Regel gewährt dem aktiven
lokalen Desktop-Benutzer Zugriff (`uaccess`); keine weltweiten Schreibrechte.
Auf diesem Rechner wurde sie im Rahmen der Einrichtung bereits installiert.
Die App benötigt keine Root-Rechte. Rückbau: die Datei
`/etc/udev/rules.d/70-jabra-desktop.rules` entfernen, udev-Regeln neu laden und
Dongle erneut einstecken.

## Entwickeln und testen

.NET 10.0.401 liegt lokal unter `.tools/dotnet`. Der Wrapper verwendet lokale
CLI- und NuGet-Verzeichnisse und deaktiviert .NET-CLI-Telemetrie.

```bash
./scripts/dotnet.sh restore
./scripts/dotnet.sh build -m:1
./scripts/dotnet.sh test -m:1
./scripts/publish.sh
```

Für einen neuen Checkout zunächst das offizielle Microsoft-Skript
https://dot.net/v1/dotnet-install.sh herunterladen und mit
`--version 10.0.401 --install-dir "$PWD/.tools/dotnet" --no-path` ausführen.
Linux-Bibliotheken: glibc, libudev, libstdc++, ICU, OpenSSL sowie X11,
Fontconfig und die von Avalonia/Skia benötigten Grafikbibliotheken. Unter KDE
Wayland läuft diese Version über XWayland mit Software-Rendering. Dies vermeidet einen beim lokalen Test beobachteten AMD-GPU-Absturz. CachyOS wurde lokal geprüft;
Jabra nennt offiziell Ubuntu ab 22.04 als Linux-Ziel.

Optional kann ein eigener Jabra-Partner-Schlüssel per `JABRA_PARTNER_KEY`
gesetzt werden. Die lokale Erkennung und das Lesen der Kopplungsliste wurden
hier ohne Schlüssel getestet. Es wird kein fremder Schlüssel mitgeliefert.

## Diagnose

```bash
./scripts/dotnet.sh run --project src/JabraDesktop.Probe -- peers
./scripts/dotnet.sh run --project src/JabraDesktop.Probe -- scan
```

Die Diagnose zeigt Namen, Geräterolle, verfügbare VID:PID-Kennungen und Status,
keine Bluetooth-Adressen oder Seriennummern.
CLI-Verbindungsaktionen verwenden Dongle- und Peer-Indizes aus derselben
Geräteabfrage: `connect 0 1`, `disconnect 0 1`, `unpair 0 1 --confirm`.
Zum Koppeln neuer Geräte bevorzugt die grafische Oberfläche verwenden. Alternativ sucht `pair 0 "Exakter Gerätename"` 30 Sekunden und koppelt nur bei genau einem passenden Treffer.

## Unterstützung und Grenzen

- Erkennung ist nicht auf Evolve 75 beschränkt. Bluetooth-Verwaltung wird nur
  angeboten, wenn das SDK die Funktion für das Gerät bereitstellt.
- Die Geräteübersicht zeigt die SDK-Rolle und die VID:PID-Kennung, sofern beide
  Werte verfügbar sind. Diese Kennungen unterscheiden Gerätemodelle, nicht zwei
  baugleiche Dongles.
- Hardwarevalidierung: Link 370 mit Speak 710 und Link 380 mit Evolve 75 SE
  gleichzeitig, einschließlich Firmware- und Akkudaten; Details stehen in
  `docs/hardware-validation.md`.
- USB-Headsets erscheinen in der Übersicht, besitzen aber keine Dongle-Suche.
- Akku/Firmware werden nach Geräteklasse abgefragt. Manche Jabra-Modelle oder
  Bluetooth-Profile liefern einzelne Werte über das SDK nicht.
- Fremdhersteller werden bei Suchtreffern nicht ausgefiltert; Kompatibilität
  ist experimentell. DECT, BlueZ, Firmware-Updates und Audioeinstellungen folgen
  gegebenenfalls separat.
- Die Nutzungsbedingungen für Jabra-Komponenten stehen in Deutsch und Englisch
  im installierten Lizenzverzeichnis; Details zu Drittanbieter-Komponenten und
  offiziellen Jabra-Bedingungen stehen in `THIRD-PARTY-NOTICES.md`.
