# Funktionsvergleich mit Jabra Direct (Screenshots vom 29.09.2026)

Quelle: `attachments.zip` im Haupt-Checkout, sieben PNGs. Die Bilder zeigen
die Einstellungsseiten eines Link 370 und eines gekoppelten Jabra-Evolve-Geräts.
Sie belegen sichtbare UI-Funktionen, aber nicht deren technische Verfügbarkeit
auf jedem Jabra-Modell oder unter Linux. Die Abgrenzung unten bezieht sich auf
den Quellstand `feat/bluetooth-rename-0.5.1` (`542ea3a`); das veröffentlichte
Release `v0.5.0` enthält auch die Namensänderung noch nicht.

| Jabra-Direct-Seite | In den Bildern sichtbar | Unser Stand |
| --- | --- | --- |
| Dongle → Headset | Funkreichweite (Normal/Niedrig/Sehr niedrig) | Keine Einstellung dafür |
| Dongle → Softphone (PC) | Softphone-Integration, Klingelton im Headset, PC-Audio | Keine dieser drei Einstellungen |
| Dongle → Produktinformation | Firmware, Bluetooth-Audiogerätename, Teams-Zertifizierung | Nur Firmwareanzeige; die anderen beiden Angaben fehlen |
| Endgerät → Headset | Klangprofil, Sprachansagen, SafeTone, Stummschaltungs-Erinnerung, Sidetone und Pegel, Busylight, automatische Anrufablehnung, Tastentöne, automatischer Ruhemodus, Gerätename | Gerätenamensänderung im Feature-Branch; die übrigen Einstellungen fehlen |
| Endgerät → Softphone (PC) | Kabelgebundenes USB-Audio, Computeraudio priorisieren | Beide Einstellungen fehlen |
| Endgerät → Mobiltelefon | Name des verbundenen Telefons, im Bild „None“ | Anzeige fehlt; keine Telefonverwaltung gezeigt |
| Endgerät → Produktinformation | Firmware, Teilenummer | Firmwareanzeige vorhanden, Teilenummer fehlt |
| Einstellungsdialog | Änderungen sammeln, Speichern, Abbrechen, Zurücksetzen | Kein allgemeiner Einstellungsdialog und kein entsprechender Änderungs-/Reset-Workflow |

Unsere App hat dagegen schon Suche, Koppeln, Verbinden, Trennen und Entkoppeln
pro Dongle, einen Dongle-/Endgerätebaum, Akku- und Firmwarestatus, einen Tray,
Deutsch/Englisch und System-/Hell-/Dunkelmodus. Die Screenshots decken diese
Bereiche kaum ab; aus ihnen lässt sich also kein vollständiger Produktvergleich
ableiten. Ein Firmware-**Update** ist auf diesen Bildern nicht zu sehen und
darf daraus nicht als Jabra-Direct-Funktion in dieser Ansicht abgeleitet werden.

## Wichtige technische Abgrenzungen

- Der Nutzer bestätigt, dass „Device Name“ hier dem Bluetooth-Gerätenamen
  entspricht. Unsere UI nennt die Aktion daher „Gerätenamen ändern“ und schreibt
  weiterhin die am Evolve 75 bereits erfolgreich getestete Eigenschaft
  `bluetoothName`. Die Property-Definition kennt zusätzlich `deviceName`;
  daraus allein folgt kein Bedarf für eine zweite Namensaktion. Die Änderung
  durch den neuen App-Dialog bleibt auf echter Hardware zu testen.
- Die Definition enthält unter anderem lesbare Eigenschaften `skuId`,
  `audioName` und `mobileDevice1` sowie les-/schreibbare Eigenschaften
  `softphoneIntegrationEnabled`, `sidetoneEnabled` und
  `prioritizedComputerAudioEnabled`. Das sind **Kandidaten**, keine bestätigten
  Fähigkeiten der hier gezeigten Geräte. Der SDK-Zugriff muss pro Gerät und
  Firmware auf Les- und Schreibbarkeit geprüft werden.
- Softphone- und USB-Audio-Verhalten hängt zusätzlich von Linux-Audiostack
  und Anrufintegration ab. Ein Property-Schalter allein belegt keine nutzbare
  Ende-zu-Ende-Funktion mit allen Anwendungen.
- Die Bilder zeigen für das Evolve-Gerät Firmware `1.1.0` und eine Teilenummer.
  Das zuvor lokal geprüfte Evolve 75 meldete Firmware `2.38.0`; daher die
  sichtbaren Optionen nicht pauschal dem vorhandenen Evolve 75 zuordnen.

## Sinnvolle Reihenfolge

1. Die Gerätenamensänderung im App-Dialog am vorhandenen Gerät prüfen.
   Danach lesbare Produktdaten ergänzen (Teilenummer, Audiogerätename,
   Zertifizierung, verbundenes Telefon) und pro Modell sauber als
   „nicht verfügbar“ kennzeichnen.
2. Ein gerätespezifisches Einstellungsmodell mit Capability-Abfrage,
   Originalwert, ausstehenden Änderungen, Speichern/Abbrechen und klarer
   Fehlerbehandlung schaffen. Jede Einstellung einzeln auf echter Hardware
   verifizieren, beginnend mit risikoarmen Optionen wie Sidetone/Tastentönen.
3. Audio-/Softphone-Integration und ihre Linux-Wirkung getrennt testen; erst
   danach die entsprechenden Schalter anbieten. Dongle-Funkreichweite und
   Schutzfunktionen mit vorsichtigen gerätespezifischen Grenzen behandeln.

Das ist ein Vergleich und eine Prüfreihenfolge, noch keine Zusage, dass alle
Jabra-Direct-Optionen über das Linux-SDK für jedes Gerät schreibbar sind.
