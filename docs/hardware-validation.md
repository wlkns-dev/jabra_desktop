# Hardwareprüfung – 25.09.2026

System: CachyOS x86_64, KDE. SDK 4.9.1.1, DevicePairing 3.1.1.2,
Linux DeviceConnector 2.1.5. Kein Partner-Key gesetzt.

| Prüfung | Ergebnis |
|---|---|
| Native Bibliotheken auflösbar | Bestanden (`ldd`) |
| SDK startet als normaler Benutzer | Bestanden |
| Link 380 erkannt (USB 0b0e:24c7) | Bestanden |
| Headset erkannt | Bestanden, SDK meldet Jabra Evolve 75 SE |
| Gespeicherte Kopplungen lesen | Bestanden, drei Einträge; ein verbundener |
| USB-Zugriff vor Regel | Erwarteter Fehler: Permission denied für hiddev2 |
| USB-Zugriff nach 70-jabra-desktop.rules + gezieltem udev-Trigger | Bestanden |
| Geräteanbindung über gemeinsame Probe | Bestanden, Exitcode 0 |
| Automatische Suche an echter Hardware | Bestanden, zusätzlicher Treffer Jabra Evolve 75 |
| Neue Kopplung | Bestanden: zusätzliches Evolve im Pairing-Modus gefunden, PairAndConnect erfolgreich, Status Connected |
| Trennen / Wiederverbinden | Bestanden: gezieltes Disconnect und Connect, jeweils Exitcode 0; Status wieder Connected |
| Entkoppeln | Nicht durchgeführt; bestehende Kopplungen erhalten |
| Abziehen / Wiederanstecken | Softwarezustände automatisiert geprüft; manueller End-to-End-Test offen |
| Andere Modelle / Fremdhersteller | Nicht geprüft |

Die erste isolierte SDK-Probe erkannte auch das Headset und warf dort beim
Dongle-Cast die dokumentierte InvalidBluetoothDeviceException. Der endgültige
Adapter behandelt nicht kompatible Geräte als reine Geräteübersicht;
die gemeinsame Probe liest die Liste ohne diesen Fehler.

GUI: Unter KDE geöffnet und ausschließlich das App-Fenster per Screenshot geprüft. Echte Geräte und Kopplungszustände sichtbar.

Endzustand nach vom Nutzer gewünschtem Pairing-Test: Jabra Evolve 75 verbunden, Jabra Evolve HO getrennt, alle drei gespeicherten Kopplungen erhalten. Die Suche und neue Kopplung wurden über die gemeinsame Adapter-Probe getestet; die Oberfläche separat visuell geprüft.

Bei längerem GPU-Rendering stürzte der AMD-Treiber mit `amdgpu: The CS has been rejected (-12)` ab. Die finale App verwendet deshalb Avalonia-Software-Rendering unter XWayland.

Release-Tests: 29 bestanden, 0 fehlgeschlagen. Unabhängiger Code-Review abgeschlossen; vier relevante Lebenszyklusprobleme mit Regressionstests behoben.

Finales eigenständiges Linux-Paket gestartet und Fenster erneut geprüft: Darstellung unter Software-Rendering läuft. Beim späteren Screenshot war wieder Evolve HO verbunden; die App zeigt den jeweils vom Dongle gelieferten Zustand. Die erfolgreiche Kopplung von Evolve 75 wurde unmittelbar durch erneutes Auslesen bestätigt, eine dauerhafte Priorisierung bestimmter Headsets ist nicht implementiert.

Offener kleiner UI-Punkt: Beim Wechsel des ausgewählten Dongles während einer laufenden Suche kann die Suchanzeige bis zum Ende des Abbruchs kurz sichtbar bleiben. Geräteaktionen sind weiterhin gesperrt und dem richtigen Dongle zugeordnet.

## Gerätebestand-Release 0.3.0 – Prüfung am 27.09.2026

System: CachyOS x86_64, Kernel `7.2.7-1-cachyos`, KDE. Jabra SDK `4.9.1.1`, DevicePairing `3.1.1.2`, Device Connector Linux `2.1.5`. Kein Partner-Key gesetzt.

| Dongle | Angeschlossenes USB-Gerät | SDK-Rolle und VID:PID | Geräteverwaltung | Kopplungsliste | Suche/Pair-Aktionen |
|---|---|---|---|---|---|
| Link 380, Variante UC/MS nicht ermittelt | Evolve 75 SE | Dongle `0B0E:24C7`; Headset `0B0E:2502` | Bestanden | Bestanden, vier Einträge; ein verbundener | In dieser Bestandsaufnahme nicht ausgeführt; Pair/Connect/Disconnect wurden in der obigen Hardwareprüfung erfolgreich getestet |
| Link 370 | — | — | Nicht getestet; bei der Prüfung nicht angeschlossen | Nicht getestet | Nicht getestet |
| Link 370 und Link 380 gleichzeitig | — | — | Nicht getestet | Nicht getestet | Nicht getestet |

Der aktuelle Link-380-Probe-Lauf meldete zusätzlich die Kopplungen `Jabra Evolve 75`, `Jabra Evolve HO`, `Evolve 1` und `SW - Jabra Speak 710`; nur `Evolve 1` war dabei als verbunden gemeldet. Die automatische Suche wurde nicht gestartet, weil kein Testgerät absichtlich im Pairing-Modus war. Seriennummern und Bluetooth-Adressen wurden nicht protokolliert.

Die App listet Geräte anhand ihrer SDK-Rolle und VID:PID. Das bestätigt die Geräteerkennung für die oben geprüfte Kombination, aber keine pauschale Kompatibilität aller Link- oder Evolve-Varianten.

## Geräteeigenschaften-Release 0.4.0 – Prüfung am 27.09.2026

Das Properties-Modul `Jabra.NET.Sdk.Properties 2.5.1.1` wurde integriert. Die
SDK-Dokumentation nennt `firmwareVersion` für Link 380 und `batteryLevel` für
Evolve-Familien. Der lokale Hardwarelauf (`Probe -- list`) fand während der
Releaseprüfung keine angeschlossenen Geräte (Exitcode 2), daher wurden keine
Eigenschaftswerte live gelesen.

| Eigenschaft | Freigabe | Ergebnis auf lokaler Hardware |
|---|---|---|
| Link 380 `0B0E:24C7`, Rolle Dongle, `firmwareVersion` | Dokumentiert und exakt freigeschaltet | Nicht getestet; Link 380 war beim Lauf nicht angeschlossen |
| Evolve 75 SE `0B0E:2502`, Rolle Headset, `batteryLevel` | Familienunterstützung dokumentiert; konkrete Kombination unbestätigt | Nicht getestet; Headset war beim Lauf nicht angeschlossen |
| Link 370 und alle anderen Geräte | Keine Freigabe in 0.4.0 | Nicht getestet |

Eigenschaften werden nur bei Start und explizitem Tastendruck gelesen. Es gibt
keine periodischen Eigenschaftenabfragen oder Schreibzugriffe. Ungültige Werte,
Fehler und Zeitüberschreitungen bleiben nicht verfügbar. Der Adapter verwendet
Jabra Properties 2.5.1.1 zusammen mit Core 4.9.1.1 und Linux Device Connector
2.1.5. Ein erneuter Live-Test mit eingestecktem Link 380 und separat mit dem
Evolve 75 SE ist für die Hardwarebestätigung offen.

## Multi-Dongle 0.5.0 – Prüfung am 28.09.2026

Link 370 und Link 380 waren gleichzeitig angeschlossen. Die read-only
`Probe inventory` lieferte dreimal hintereinander beide Kopplungslisten mit
je vier Einträgen. Die aktiven Endgeräte wurden über SDK-Elternverbindung und
Bluetooth-Child-Metadaten zugeordnet, nicht anhand ihres Anzeigenamens.

| Gerät | Firmware | Akku | Zuordnung |
|---|---|---|---|
| Link 370 | 1.87.0 | nicht anwendbar | Dongle |
| Speak 710 | 1.40.0 | 53 % | Link 370, verbunden |
| Link 380 | 1.16.0 | nicht anwendbar | Dongle |
| Evolve 75 SE (Kopplungsname Evolve 1) | 1.1.0 | 94 % | Link 380, verbunden |

Speak 710 und Evolve wurden nacheinander über ihren jeweiligen Dongle getrennt
und wieder verbunden. Die anschließend ausgelesene Kopplungsliste bestätigte
jeweils `Disconnected` und danach `Connected`; beide Verbindungen wurden
wiederhergestellt. Die gespeicherten Kopplungen blieben erhalten.

Physisches USB-Abziehen und echtes Entkoppeln wurden in diesem Lauf nicht
vorgenommen. Entfernen während einer Abfrage, getrennte Fehlerzustände,
Aktionsziele und Bestätigung beim Entkoppeln werden automatisiert geprüft.
Ein gebautes Ubuntu-Paket ist keine Bestätigung eines Ubuntu-Laufzeittests;
der lokale Hardwaretest erfolgt auf CachyOS/KDE.
