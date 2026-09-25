# Jabra Desktop: Arch-Paket und Tray-Integration

## Ziel

Jabra Desktop soll sich auf CachyOS und anderen Arch-Linux-Systemen wie eine normale Desktop-Anwendung installieren, starten, im Tray weiterlaufen und sauber entfernen lassen. Der erste Paketweg ist ein lokal gebautes Arch-Paket. Versionen werden in Git und im Paket nachvollziehbar geführt. Ein eigener Updater in der Anwendung ist zunächst nicht vorgesehen.

## Umfang und Erfolgskriterien

- Ein `pkg.tar.zst` lässt sich lokal mit `makepkg` aus dem vorhandenen selbständigen Linux-x64-Publish erstellen und mit `pacman -U` installieren.
- Das Paket installiert die Anwendung, den Desktop-Starter, das App-Symbol und die Jabra-udev-Regel an systemübliche Pfade. Bei der Deinstallation entfernt Pacman diese Paketdateien wieder.
- Die Anwendung besitzt auf KDE Plasma ein Tray-Menü mit „Öffnen“, einem optionalen Autostart-Schalter und „Beenden“.
- Das Schließen des Fensters blendet die Anwendung ins Tray aus. „Öffnen“ zeigt das bestehende Fenster; „Beenden“ beendet den Prozess und gibt das Jabra SDK frei.
- Es läuft höchstens eine Instanz, damit manueller Start und Autostart nicht mehrere SDK-Verbindungen öffnen. Wird die Anwendung erneut gestartet, wird das bestehende Fenster geöffnet.
- Autostart ist optional, standardmäßig aus. Wenn aktiviert, startet die Anwendung minimiert ins Tray.
- Paketversionen folgen einer nachvollziehbaren Git-Versionierung. Aktualisiert wird über Pacman beziehungsweise bei der lokalen Paketdatei manuell mit `pacman -U`.

## Gewählte Lösung

### Paketierung

Das Projekt erhält ein Arch-`PKGBUILD`, das das bereits erstellte selbständige Linux-x64-Publish paketiert. Die Paketdateien enthalten nur die Linux-Laufzeitdateien, keine Artefakte für andere Plattformen. Das Paket installiert die Anwendung unter `/opt/jabra-desktop`, einen ausführbaren Starter unter `/usr/bin`, die Desktop-Datei unter `/usr/share/applications`, ein Symbol unter `/usr/share/icons/hicolor` und die udev-Regel unter `/usr/lib/udev/rules.d`.

Zunächst lässt sich das Paket lokal mit `makepkg` bauen und anschließend mit `pacman -U` installieren. Die Anleitung dokumentiert beide Schritte, sodass ein technisch versierter Nutzer das Repository klonen und selbst bauen kann. Es wird zunächst kein AUR-Paket veröffentlicht.

Nach Klärung der Weitergabebedingungen der SDK-Binärdateien können fertige `.pkg.tar.zst`-Pakete als GitHub Releases bereitgestellt werden. Nutzer laden dann die passende Paketdatei herunter und installieren sie mit `pacman -U`; Updates erfolgen zunächst auf demselben Weg durch Download und Installation einer neueren Version. Ein automatisiertes Pacman-Repository bleibt eine mögliche spätere Ausbaustufe.

### Tray und Fensterlebenszyklus

Die Oberfläche nutzt Avalonias `TrayIcon` über StatusNotifier/AppIndicator. Das Menü enthält „Öffnen“, „Autostart“ (aktiviert/deaktiviert) und „Beenden“. Ein Fensterschließen versteckt das Hauptfenster, ohne die Gerätesitzung oder den Prozess zu beenden. Der Tray-Eintrag bleibt als sichtbarer Zugriffspunkt. Ein erneuter Anwendungsstart über Desktop-Menü oder Kommandozeile aktiviert die bereits laufende Instanz.

Der Tray-Eintrag muss über den regulären Avalonia-Lebenszyklus entsorgt werden, wenn die Anwendung tatsächlich beendet wird. Ein fehlender Tray-Host soll nicht den Start der Hauptanwendung verhindern; in dem Fall bleibt das Fenster normal bedienbar und „Schließen“ beendet die Anwendung wie üblich. Zielplattform für die erste Integration ist KDE Plasma unter Linux.

### Autostart

Der Schalter im Tray legt für den aktuellen Benutzer eine Autostart-Datei unter `~/.config/autostart` an oder entfernt sie. Standard ist deaktiviert. Ein automatischer Start verwendet denselben einzelnen Prozess und startet ohne sichtbares Hauptfenster. Die Paketinstallation aktiviert Autostart nicht eigenmächtig.

### Versionen und Updates

Änderungen werden in Git committet. Freigegebene Softwarestände erhalten annotierte Versions-Tags nach dem Muster `vMAJOR.MINOR.PATCH` (zum Beispiel `v0.1.0`). `pkgver` im PKGBUILD folgt der Softwareversion; `pkgrel` wird für reine Paketkorrekturen erhöht, solange die Softwareversion gleich bleibt.

Es gibt vorerst keinen eigenen In-App-Updater und keine Hintergrundprüfung auf neue Versionen. Bei lokalem Paketbezug erstellt man eine neue Paketdatei und aktualisiert sie mit `sudo pacman -U <datei.pkg.tar.zst>`. Falls GitHub Releases eingerichtet werden, laden Nutzer dort die neuere Datei herunter und installieren sie ebenfalls mit `pacman -U`. Ein späteres Pacman-Repository könnte Paketaktualisierungen regulär bereitstellen, gehört aber nicht zu diesem Umfang.

## Fehlerfälle und Betriebsverhalten

- Scheitert das Schreiben der Autostart-Datei (etwa wegen Dateisystem- oder Berechtigungsfehlern), zeigt die Anwendung eine verständliche Fehlermeldung und lässt den bisherigen Schalterzustand unverändert.
- Ist die udev-Regel nach einer Installation noch nicht wirksam, wird auf das erforderliche Neuladen der Regeln oder erneute Anstecken des Dongles hingewiesen; es werden keine USB-Rechte zur Laufzeit verändert.
- Schlägt die zweite Instanz beim Kontakt mit der Hauptinstanz fehl, darf sie nicht zusätzlich das Jabra SDK initialisieren; sie soll mit einer klaren Fehlermeldung enden.
- Ein nicht verfügbarer Tray-Host darf keine stille Prozessinstanz ohne erreichbare Oberfläche hinterlassen.

## Nicht-Ziele

- AUR- oder offizielles Arch-Repository in dieser ersten Ausbaustufe.
- Eingebauter Updater, automatische Download- oder Updateprüfung.
- Autostart standardmäßig aktivieren.
- Unterstützung jeder Desktop-Umgebung vor KDE Plasma.
- Ändern von Jabra-Firmware oder proprietären Gerätefunktionen, die das vorhandene SDK nicht bereitstellt.

## Offene technische Randbedingung

Vor einer öffentlichen Veröffentlichung fertiger Pakete (zum Beispiel als GitHub Release) müssen die Weitergabebedingungen der verwendeten Jabra-SDK-Binärdateien geprüft werden. Lokale Paketierung, Installation und der dokumentierte Selbstbau bleiben davon unberührt.
