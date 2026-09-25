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
