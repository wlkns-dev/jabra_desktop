# Jabra Desktop 0.4.0: Read-only device status properties

## Goal

Show verified firmware and battery status for the specific Jabra devices supported by the SDK Properties module, while keeping device discovery and Bluetooth pairing independent of optional property support.

## Users and success criteria

- Users can see a Link 380 firmware version when its property is supported and readable.
- Users can see battery percentage for the known Evolve 75 SE device when its property is supported and readable.
- Unsupported properties, invalid values, timeouts, and device removal leave the corresponding status unavailable and do not interrupt discovery, pairing, or connection actions.
- Status reads are read-only, bounded, and triggered at discovery/startup or by an explicit refresh; the app does not continuously poll.
- Tests cover SDK-independent capability mapping, parsing, errors, timeouts, refresh, and device removal.
- Hardware results distinguish documentation-backed capability from verified model/firmware combinations.

## Existing system

`DeviceInfo` already carries optional `BatteryPercent` and `Firmware` values, and the UI already presents localized unavailable labels. `JabraBackend` owns the Jabra SDK lifecycle, maps SDK devices into stable session-local entries, and publishes device snapshots. Device discovery and Bluetooth pairing use the core SDK and DevicePairing module. The installed versions are `Jabra.NET.Sdk` 4.9.1.1 and `Jabra.NET.Sdk.DevicePairing` 3.1.1.2.

Jabra's Properties module is an optional package. It creates a property factory from the initialized SDK and property maps for explicitly selected property names. Jabra's public sample documents `firmwareVersion` for Link 380/390 and `batteryLevel` for wireless Evolve, Evolve2, Engage, and Speak devices. The properties API does not itself determine which properties a device supports; the application must maintain that capability information. Jabra lists package `Jabra.NET.Sdk.Properties` 2.5.1.1, whose declared minimum Core SDK dependency is 4.9.1.1. The Link 380 and Evolve 75 SE were detected in the 0.3.0 hardware inventory, but neither property has yet been read successfully on this host.

## Design

### Capability allowlist

The Jabra adapter owns an explicit capability table keyed by stable hardware metadata already available in `DeviceInfo` (vendor ID, product ID, and SDK role). The first candidates are:

| Device | Identity | Property | Result |
|---|---|---|---|
| Jabra Link 380 | VID `0B0E`, PID `24C7`, role Dongle | `firmwareVersion` | Firmware text |
| Jabra Evolve 75 SE | VID `0B0E`, PID `2502`, role Headset | `batteryLevel` | Battery percentage, integer 0–100 |

The table is deliberately limited to these exact known identities. The sample documents family-level support for the battery property, but the 75 SE combination remains unverified until read on hardware. The Link 370 is not enabled by this release because its Properties support is not established. Product display names alone never enable a property. Future devices require an explicit capability entry backed by Jabra documentation and recorded validation.

### Adapter boundary and data flow

The Core layer remains free of Jabra SDK types. It defines an SDK-independent, asynchronous status refresh contract on `IDeviceBackend`; the Jabra adapter implements it using `Jabra.NET.Sdk.Properties`. Existing optional `DeviceInfo.BatteryPercent` and `DeviceInfo.Firmware` carry successfully read values, so the UI can continue to consume the existing model fields. SDK-only property names, `PropertyValue` types, and conversion remain inside `JabraDesktop.Jabra`.

The adapter initializes one Properties module and factory against the already running Core SDK. For each supported device entry, it creates a map containing only the allowlisted read property and gets that property. Discovery publishes the device immediately, before any property operation. Initial reads happen asynchronously after SDK startup/device discovery; a refresh action requests another read for the selected device. Reads are bounded and serialized per device. There is no periodic polling, telemetry subscription, property write, or blocking work on the UI thread.

Firmware is accepted only as a non-empty string. Battery is accepted only as an integer in the inclusive range 0–100. Missing, wrong-typed, out-of-range, unsupported, timed-out, or failed results remain `null`, which the current UI presents as unavailable. A read failure is isolated to that property/device and does not mark the device itself as unavailable. Device removal prevents stale results from being published. No property values are logged.

The Properties package is pinned to a version compatible with the existing Core SDK. Before shipping it, the package's license and all new runtime/native assets are checked and reflected in third-party notices and the Arch package. Failure to initialize the optional module degrades to unavailable status values while preserving the existing manager.

### User interface

The existing battery and firmware fields show the latest available values for the selected physical device. A localized refresh affordance requests a one-shot update for that device and displays a bounded busy state without making the window flicker or blocking other device actions. Unknown and unsupported values use the existing unavailable labels. The UI does not expose properties that have not been admitted by the adapter capability table.

### Testing and hardware validation

Fake backends verify the core refresh contract and UI state transitions. Adapter tests verify the two exact capability entries, property type/range conversion, absence for unknown IDs/roles including Link 370, and isolation of SDK errors, timeout, or removal. Existing pairing and discovery tests remain required.

Hardware validation tries Link 380 firmware and Evolve 75 SE battery reads separately, records device identity, OS/kernel, SDK/Properties/Connector versions, and only the returned property value. Link 370 and other variants remain `Not tested` unless actually connected and checked. No serial number or Bluetooth address is collected. If the hardware path is unavailable during implementation, automated work continues and the release explicitly records that the property is documented but not hardware-verified.

## Release scope

Version 0.4.0 adds read-only firmware and battery status for the exact allowlisted identities only. It does not add arbitrary device settings, property writes, telemetry watches/polling, firmware updates, Button Customization, Easy Call Control, Link 370 Properties support, or non-Jabra adapters.

The release is acceptable when all automated tests pass, discovery and existing pairing flows remain intact, the UI remains responsive during slow/failing reads, package contents and notices are correct, and `docs/hardware-validation.md` clearly separates verified results from documentation-only capabilities. The application version, Arch package version, annotated tag, and GitHub Release must agree.

## References

- Jabra .NET Properties module: https://developer.jabra.com/sdks-and-tools/dotnet/properties
- Jabra Properties sample, including the Link 380 and wireless headset property lists: https://github.com/gnaudio/jabra-dotnet-device-properties-sample
- Jabra Properties package metadata: https://www.nuget.org/packages/Jabra.NET.Sdk.Properties
- Jabra device property explorer: https://developer.jabra.com/sdks-and-tools/device-properties
