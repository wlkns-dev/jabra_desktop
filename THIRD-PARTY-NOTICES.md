# Drittanbieter-Komponenten

Die App ist ein unabhängiges Projekt und kein offizielles Jabra-Produkt.
Jabra ist eine Marke von GN Audio. Hardwarezugriff erfolgt mit deren SDK.

- Jabra.NET.Sdk 4.9.1.1 und DevicePairing 3.1.1.2: Die mitgelieferten LICENSE.md-Dateien enthalten einen Gewährleistungsausschluss. Die Pakete sind öffentlich über NuGet verfügbar.
- Jabra.NET.Sdk.Properties 2.5.1.1: Das NuGet-Paket verlangt die Zustimmung zur mitgelieferten LICENSE.md. Diese enthält einen Gewährleistungsausschluss. Die Abhängigkeit Jabra.Properties.Definition 14.0.3 liefert Properties-Schemadaten; JsonSchema.Net 7.4.0 und JsonPointer.Net 5.3.1 verwenden die MIT-Lizenz. Das Properties-Paket enthält keine nativen Laufzeitdateien.
- Jabra.DeviceConnector.Linux 2.1.5: Proprietärer Geräteprozess von GN Audio. Das NuGet-Paket enthält keinen ausdrücklichen Weitergabelizenztext. Er wird als Teil der Jabra Desktop-Anwendung mitgeliefert und ist ausschließlich zusammen mit einem GN-Audio-Produkt zu verwenden.
- Jabra SDK-Nutzung: Vor der ersten Geräteverwaltung verlangt die App eine aktive Zustimmung zu den lokalisierten Hinweisen unter `Terms.de.md` und `Terms.en.md`. Sie werden zusammen mit diesen Hinweisen im installierten Paket unter `/usr/share/licenses/jabra-desktop/` bereitgestellt. Die offizielle [Jabra Developer License Agreement](https://developer.jabra.com/legal/license-agreement) ist maßgeblich; die Hinweise ersetzen sie nicht.
- Avalonia 11.3.10: MIT, https://github.com/AvaloniaUI/Avalonia/blob/master/licence.md
- CommunityToolkit.Mvvm 8.4.0: MIT, https://github.com/CommunityToolkit/dotnet/blob/main/License.md
- Tmds.DBus.Protocol 0.21.3: MIT, https://github.com/tmds/Tmds.DBus
- .NET 10 Runtime: https://github.com/dotnet/runtime/blob/main/LICENSE.TXT

Transitive Abhängigkeiten sind in den .deps.json-Dateien und NuGet-Projektdateien aufgeführt. Dieses Dokument ersetzt keine individuelle Weitergabelizenz.
