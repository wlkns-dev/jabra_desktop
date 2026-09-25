# Jabra Desktop für Linux

Deutschsprachige Desktop-App zum Verwalten von Jabra-Bluetooth-Dongles.
Mit Gerätesuche, Koppeln, Verbinden, Trennen und gezieltem Entkoppeln,
heller/dunkler Oberfläche und automatischer Geräteerkennung.

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

Das Tray-Menü kann den Autostart für den aktuellen Benutzer ein- und
ausschalten. Er ist standardmäßig aus. Der Eintrag liegt in
`${XDG_CONFIG_HOME:-$HOME/.config}/autostart/jabra-desktop.desktop`. Beim
Entfernen des Pakets bleibt diese persönliche Einstellung erhalten und kann bei
Bedarf manuell gelöscht werden.

Ein lokales Update wird aus einem neueren Checkout gebaut und mit Pacman
installiert:

```bash
git pull
./scripts/package.sh
package_file="$(cd packaging && makepkg --packagelist)"
sudo pacman -U "$package_file"
```

Softwarestände verwenden annotierte Git-Tags wie `v0.1.0`. Die Version in
`Directory.Build.props` und `pkgver` im PKGBUILD müssen dazu passen; für eine
reine Paketkorrektur wird stattdessen `pkgrel` erhöht. Einen eingebauten
Updater gibt es nicht. Fertige Pakete sollen später als GitHub Releases
verfügbar sein, sobald die Weitergabebedingungen der Jabra-SDK-Binärdateien
geklärt sind. Bis dahin ist lokaler Selbstbau der vorgesehene Weg. AUR und ein
automatisiertes Pacman-Repository sind noch nicht eingerichtet.

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

1. Link-Dongle einstecken; links den gewünschten Dongle auswählen.
2. Gespeicherte Geräte rechts verbinden oder trennen.
3. Zum Hinzufügen das Headset in seinen Pairing-Modus schalten und
   **Gerät hinzufügen** wählen. Suche nach spätestens 30 Sekunden beendet;
   mit **Suche beenden** kann sie früher beendet werden.
4. Anschließend am gefundenen Gerät **Koppeln** drücken.
5. Über **⋯ → Entkoppeln** lässt sich eine einzelne Kopplung nach Bestätigung entfernen.

Der Status wird nach Aktionen und im Ruhezustand alle vier Sekunden aktualisiert.
Ein unbekannter Status wird niemals als bestätigte Verbindung ausgegeben.

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

Die Diagnose zeigt Namen und Status, keine Bluetooth-Adressen oder Seriennummern.
CLI-Verbindungsaktionen verwenden Dongle- und Peer-Indizes aus derselben
Geräteabfrage: `connect 0 1`, `disconnect 0 1`, `unpair 0 1 --confirm`.
Zum Koppeln neuer Geräte bevorzugt die grafische Oberfläche verwenden. Alternativ sucht `pair 0 "Exakter Gerätename"` 30 Sekunden und koppelt nur bei genau einem passenden Treffer.

## Unterstützung und Grenzen

- Erkennung ist nicht auf Evolve 75 beschränkt. Bluetooth-Verwaltung wird nur
  angeboten, wenn das SDK die Funktion für das Gerät bereitstellt.
- Hardwarevalidierung: Link 380 und Evolve 75 SE; Details und noch offene Tests
  stehen in `docs/hardware-validation.md`.
- USB-Headsets erscheinen in der Übersicht, besitzen aber keine Dongle-Suche.
- Akku/Firmware stehen in dieser Version als „Nicht verfügbar“, solange kein
  Telemetrieadapter integriert ist. Es werden keine Werte erfunden.
- Fremdhersteller werden bei Suchtreffern nicht ausgefiltert; Kompatibilität
  ist experimentell. DECT, BlueZ, Firmware-Updates und Audioeinstellungen folgen
  gegebenenfalls separat.
- Vor öffentlicher Verteilung die Jabra-Weitergabebedingungen klären, siehe
  `THIRD-PARTY-NOTICES.md`. Das lokale Paket wurde nicht veröffentlicht.
