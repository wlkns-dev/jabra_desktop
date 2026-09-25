# Jabra Desktop für Linux – Entwurf

Stand: 25.09.2026. Zur Durchsicht; noch keine Implementierung.

## Ziel und Ausgangslage

Eine moderne deutschsprachige Desktop-App soll den Jabra-Dongle verwalten,
Geräte suchen, koppeln, verbinden und trennen. Zielsystem ist CachyOS mit KDE.
Erkannt wurde ein Jabra Link 380 (USB 0b0e:24c7); vorhanden ist laut Nutzer
ein Evolve 75. Weitere Jabra-Geräte sollen soweit technisch möglich unterstützt
werden. Fremdhersteller können in einem zweiten Schritt folgen.

## Architektur und Alternativen

Empfehlung: C# mit Avalonia für die Oberfläche und dem offiziellen Jabra
.NET SDK für Gerätezugriff und Bluetooth-Pairing. Dadurch bleibt die Anwendung
in einer Sprache, und die Geräteanbindung nutzt die dokumentierten APIs.
Der aktuelle Rechner hat noch kein über PATH erreichbares .NET SDK.

Alternative: Qt/QML mit separatem .NET-Geräteprozess. Gute KDE-Anbindung,
aber zusätzliche Prozesskommunikation und zwei Technologie-Stacks.
Alternative: Weboberfläche in einer Desktop-Hülle mit Jabra JavaScript SDK.
Dessen benötigte Pairing-Funktionen müssten zunächst separat geprüft werden;
für diese Aufgabe ist die dokumentierte .NET-Pairing-API die bessere Basis.

Die App besteht aus drei klar getrennten Teilen:

- Desktop-Oberfläche und ViewModels für Auswahl, Status und Bedienung.
- Geräte-Service für Erkennung, Fähigkeiten, Suche und Verbindungsaktionen.
- Jabra-Adapter, der SDK-Objekte und Ereignisse auf eigene Modelle abbildet.

Ein austauschbarer Testadapter ermöglicht automatisierte Tests ohne Hardware.
Er wird im normalen Betrieb niemals als Ersatz für eine fehlende Verbindung
eingeschaltet. Der reale Zustand stammt ausschließlich aus dem SDK.

## Oberfläche und Bedienung

Ein ruhiges Layout mit heller und dunkler Darstellung, deutlichen Statusfarben,
Tastaturbedienung und skalierbaren Abständen. Links stehen erkannte Geräte;
rechts erscheinen Details und die zum ausgewählten Gerät passenden Aktionen.
Beim Dongle zeigt die Hauptansicht gespeicherte und verbundene Headsets.

„Gerät hinzufügen“ öffnet eine Suche mit Hinweis auf den Pairing-Modus des
Headsets, Fortschrittsanzeige und Ergebnisliste. Eine Suche läuft höchstens
30 Sekunden. Der Nutzer wählt ausdrücklich das Gerät zum Koppeln aus.
Gespeicherte Geräte können verbunden, getrennt oder nach Bestätigung
entkoppelt werden. Die App verwaltet mehrere Dongles durch explizite Auswahl.

Gerätename und Verbindungsstatus gehören zum Mindestumfang. Akku- und
Firmwareinformationen werden nur angezeigt, wenn das SDK sie liefert;
unbekannte Werte bleiben als unbekannt erkennbar.

## Unterstützungsstrategie

Die Erkennung wird nicht auf Evolve 75 oder eine feste USB-Produkt-ID beschränkt.
Alle vom SDK erkannten Jabra-Geräte können in der Übersicht erscheinen.
Bluetooth-Verwaltung wird nur für Geräte angeboten, für die das SDK ein
Bluetooth-Dongle-Interface bereitstellt. Ein USB-Headset erhält keine
Suchfunktion. DECT-Verwaltung ist eine spätere Erweiterung.

Verbindliches erstes Hardware-Testziel ist Link 380 mit Evolve 75. Andere
Modelle werden als ungetestet dokumentiert, bis echte Tests vorliegen.
„Alle Jabra-Geräte“ ist kein belastbares Kompatibilitätsversprechen.
Fremdgeräte werden bei vorhandenen SDK-Suchergebnissen nicht künstlich
ausgefiltert; ihre Kopplung, Audiofunktion und Telemetrie sind zunächst
experimentell. Eine separate BlueZ-Anbindung folgt gegebenenfalls in Phase 2.

## Zustände und Fehlerbehandlung

SDK-Ereignisse aktualisieren die Oberfläche auf dem UI-Thread. Geräte werden
über stabile Kennungen zugeordnet, Suchtreffer über Bluetooth-Adressen
zusammengeführt. Pro Dongle darf nur eine widersprüchliche Aktion gleichzeitig
laufen. Beim Abziehen werden zugehörige Operationen und Abonnements beendet;
verspätete Ergebnisse dürfen keinen alten Verbindungsstatus wiederherstellen.

Fehlende Berechtigungen, fehlender Dongle, nicht unterstützte Funktion,
Zeitüberschreitung und SDK-Startfehler erhalten jeweils konkrete Meldungen.
Die Oberfläche bleibt während des Gerätezugriffs bedienbar. Erfolg wird erst
nach SDK-Bestätigung beziehungsweise anschließender Zustandsabfrage angezeigt.

Die App läuft ohne root. Falls erforderlich, wird eine eng gefasste udev-Regel
mit Installationsanleitung geliefert. Änderungen am System werden nicht
heimlich vorgenommen. Lokale Diagnosen sollen keine Seriennummern oder
Bluetooth-Adressen unnötig protokollieren.

## Machbarkeit zuerst

Vor dem Ausbau der Oberfläche wird die echte SDK-Anbindung unter CachyOS
geprüft: Start, Geräteerkennung, Pairing-Liste, Suche und Verbindung. Jabra
nennt Ubuntu ab 22.04 als unterstützte Linux-Plattform; CachyOS muss praktisch
validiert werden. Auch native SDK-Abhängigkeiten, Partner-Key-Verhalten und
Weitergabebedingungen werden vor Paketierung geprüft. Ein SDK-Hindernis wird
offengelegt und führt zu einer angepassten Entscheidung, nicht zu einer nur
simulierten fertigen App.

## Tests und Abnahme

Automatisierte Tests prüfen Such-Deduplizierung, Zustandsübergänge,
Fehlerweitergabe, Sperrung konkurrierender Aktionen sowie Geräteverlust während
einer Operation. Build und Tests müssen erfolgreich durchlaufen.

Am echten Link 380/Evolve 75 werden Erkennen, gespeicherte Geräte, Suche im
Pairing-Modus, Koppeln, Trennen, Wiederverbinden sowie Abziehen und erneutes
Einstecken geprüft. Änderungen einer bestehenden Kopplung erfolgen gezielt
im Hardwaretest. Ohne diesen Test wird nur Softwarevalidierung gemeldet.

Lieferumfang: Quellcode, Start- und Build-Anleitung, Desktop-Eintrag und ein
lokal startbares Linux-Paket beziehungsweise Veröffentlichungsverzeichnis.
Firmware-Updates, Autostart, Tray-Steuerung und umfangreiche Audio-Einstellungen
sind außerhalb der ersten Version.

## Quellen

- https://developer.jabra.com/sdks-and-tools/dotnet
- https://github.com/gnaudio/jabra-dotnet-bt-pairing-sample
- https://sdk.jabra.com/dotnet/docs/Sdk.DevicePairing/3.1.1/articles/bluetooth.html
- https://docs.avaloniaui.net/docs/overview/supported-platforms

## Nächster Schritt

Nach Durchsicht dieses Entwurfs wird der konkrete Implementierungsplan erstellt.
Das Arbeitsverzeichnis enthält derzeit kein nutzbares Git-Repository;
der Entwurf ist daher als Datei gespeichert und nicht committed.
