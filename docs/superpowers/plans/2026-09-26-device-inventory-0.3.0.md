# Device Inventory 0.3.0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship Jabra Desktop 0.3.0 with clearer per-device identity and role information for multiple Jabra dongles and headsets, backed by a hardware validation matrix.

**Architecture:** Keep the core model independent of Jabra SDK types. Map the SDK's device type, vendor ID, and product ID in `JabraDesktop.Jabra`, then present role and IDs in the localized UI and diagnostic probe. Preserve `CanPair` as the capability result of successful dongle creation; metadata alone never grants pairing actions.

**Tech Stack:** .NET 10, C#, Avalonia, Jabra.NET.Sdk 4.9.1.1, Jabra.NET.Sdk.DevicePairing 3.1.1.2, xUnit, Arch `makepkg`.

**Spec:** `docs/superpowers/specs/2026-09-26-device-capabilities-roadmap-design.md`

## Global Constraints

- App, PKGBUILD, package, annotated tag, and GitHub release versions must match.
- Existing pairing, scan, connect, disconnect, and unpair behavior must remain intact.
- The core remains SDK-independent; unsupported metadata stays optional and unknown.
- Bluetooth addresses and serial numbers are never added to UI, diagnostics, or committed hardware notes.
- No device is claimed as compatible based only on its display name.
- Properties, button customization, call control, firmware updates, and non-Jabra adapters are out of scope for 0.3.0.
- Arch is validated in practice; Jabra's documented .NET SDK Linux target is Ubuntu 22.04+.

## Review Focus

- SDK device types such as `None`, `NotInit`, or `NotGn` must map to an unknown/general role, not crash or imply support. Test in Task 1.
- Missing or zero vendor/product IDs must remain absent rather than displaying fabricated identifiers. Test in Task 1.
- Identical device names and product IDs still represent separate physical entries and selectable sessions. Test in Task 1 and Task 2.
- A headset may have role `Headset` while `CanPair` is false; pairing controls must remain disabled. Test in Task 2.
- German and English role labels must both render, including after an in-session language change. Test in Task 2.

---

## File Structure

- `src/JabraDesktop.Core/Models.cs`: SDK-independent `DeviceRole` and optional USB identity metadata on `DeviceInfo`.
- `src/JabraDesktop.Jabra/DeviceMapper.cs`: map Jabra SDK device types and ID values into core metadata.
- `src/JabraDesktop.Jabra/JabraBackend.cs`: construct the expanded `DeviceInfo` without exposing serial numbers.
- `src/JabraDesktop.App/Localization/UiText.cs` and `UiTextCatalog.cs`: localized device-role labels.
- `src/JabraDesktop.App/ViewModels/DeviceListItemViewModel.cs`: localized display projection for a physical device and its optional identity.
- `src/JabraDesktop.App/ViewModels/MainViewModel.cs` and `Views/MainWindow.axaml`: preserve device selection while presenting role and vendor/product IDs.
- `src/JabraDesktop.Probe/Program.cs`: include safe role and VID:PID in local diagnostic output.
- `tests/JabraDesktop.Tests/DeviceMapperTests.cs`: SDK-to-core mapping tests.
- `tests/JabraDesktop.Tests/ViewModelTests.cs` and `LocalizationServiceTests.cs`: UI capability, duplicate-device, and localized-role tests.
- `docs/hardware-validation.md`: record Link 370/380 and available headset combinations.
- `docs/releases/v0.3.0.md`, `README.md`, `Directory.Build.props`, `packaging/PKGBUILD`: release notes and synchronized version/package instructions.

## Task 1: Add SDK-independent device identity and role mapping

**Files:**
- Modify: `src/JabraDesktop.Core/Models.cs`
- Modify: `src/JabraDesktop.Jabra/DeviceMapper.cs`
- Modify: `src/JabraDesktop.Jabra/JabraBackend.cs`
- Modify: `tests/JabraDesktop.Tests/JabraDesktop.Tests.csproj`
- Test: `tests/JabraDesktop.Tests/DeviceMapperTests.cs`
- Test: `tests/JabraDesktop.Tests/SessionTests.cs`

**Interfaces:**
- Produces `DeviceRole { Unknown, Headset, Dongle, Other }` in Core.
- Produces `DeviceInfo(string Id, string Name, bool CanPair, int? BatteryPercent = null, string? Firmware = null, DeviceRole Role = DeviceRole.Unknown, int? VendorId = null, int? ProductId = null)`; existing positional call sites remain source-compatible.
- Produces `DeviceMapper.Role(Jabra.NET.Sdk.Core.Types.DeviceType type) -> DeviceRole`.
- Produces `DeviceMapper.OptionalUsbId(int id) -> int?`, returning null for zero or negative IDs.
- The test project gains an explicit `Jabra.NET.Sdk` package reference at the same version as the adapter so mapper tests can use SDK enum values directly.

- [ ] **Step 1: Add failing model compatibility and role mapping tests**

In `DeviceMapperTests`, assert that SDK `Dongle` maps to `DeviceRole.Dongle`, `Headset` maps to `Headset`, `NotInit` and `NotGn` map to `Unknown`, and a known non-headset/non-dongle type maps to `Other`. Assert `OptionalUsbId(2830) == 2830`, while zero and negative IDs return null. Assert the existing three-argument `DeviceInfo` constructor still sets role to `Unknown` and IDs to null.

In `SessionTests`, add two `DeviceInfo` records with identical name, role, and product IDs but distinct opaque IDs; assert both remain in `DeviceSession.Devices` and selecting either ID selects the matching entry.

- [ ] **Step 2: Run the focused tests and verify they fail for the missing API**

Run: `./scripts/dotnet.sh test tests/JabraDesktop.Tests --filter FullyQualifiedName~DeviceMapperTests -m:1`
Expected: FAIL because the role and optional-ID mapping API is not present.

- [ ] **Step 3: Implement the core fields and SDK mapping**

Add `DeviceRole` and the optional fields to `Models.cs`. Map only SDK `DeviceType.Dongle` and `DeviceType.Headset` to specific roles; map `None`/`NotInit`/`NotGn` to `Unknown`, and the remaining defined SDK types to `Other`. Map vendor and product IDs only when positive. In `JabraBackend.DeviceEntry.Info`, populate these values from `Source.Type`, `Source.VendorId`, and `Source.ProductId`; continue to derive `CanPair` exclusively from `Dongle != null`.

- [ ] **Step 4: Run mapping and session tests**

Run: `./scripts/dotnet.sh test tests/JabraDesktop.Tests --filter FullyQualifiedName~DeviceMapperTests -m:1`
Expected: PASS, including all known and unknown type cases.

Run: `./scripts/dotnet.sh test tests/JabraDesktop.Tests --filter FullyQualifiedName~SessionTests -m:1`
Expected: PASS, including duplicate physical-device entries.

- [ ] **Step 5: Commit Task 1**

```bash
git add src/JabraDesktop.Core/Models.cs src/JabraDesktop.Jabra/DeviceMapper.cs src/JabraDesktop.Jabra/JabraBackend.cs tests/JabraDesktop.Tests/DeviceMapperTests.cs tests/JabraDesktop.Tests/SessionTests.cs
git commit -m "feat: expose Jabra device roles and USB identity"
```

## Task 2: Present roles and safe identifiers in the app and probe

**Files:**
- Modify: `src/JabraDesktop.App/Localization/UiText.cs`
- Modify: `src/JabraDesktop.App/Localization/UiTextCatalog.cs`
- Modify: `src/JabraDesktop.App/Views/MainWindow.axaml`
- Modify: `src/JabraDesktop.App/Views/MainWindow.axaml.cs`
- Modify: `src/JabraDesktop.Probe/Program.cs`
- Test: `tests/JabraDesktop.Tests/ViewModelTests.cs`
- Test: `tests/JabraDesktop.Tests/LocalizationServiceTests.cs`

**Interfaces:**
- Consumes `DeviceInfo.Role`, `VendorId`, `ProductId`, and `CanPair` from Task 1.
- Adds localization keys `DeviceRoleUnknown`, `DeviceRoleHeadset`, `DeviceRoleDongle`, and `DeviceRoleOther` with German and English catalog entries.
- Presents each device role using the active `LocalizationService`; presents an identifier as `VID:PID` in four-digit hexadecimal only when both IDs are present.

- [ ] **Step 1: Add failing role-label and capability-display tests**

Add tests asserting role labels are exactly `Bluetooth-Dongle`/`Bluetooth dongle`, `Headset`/`Headset`, `Jabra-Gerät`/`Jabra device`, and `Unbekannter Gerätetyp`/`Unknown device type`; changing language changes `DeviceListItemViewModel.RoleLabel` on the existing row. Assert `IdentifierText` is `VID 0B0E · PID 24C7` for the sample IDs and empty when either ID is absent. Add tests asserting a headset with `CanPair == false` remains non-scannable even if it has IDs, while a dongle with `CanPair == true` is scannable. Add `DuplicateDeviceRowsRemainSelectableWhenNamesAndIdsMatch`, asserting distinct opaque IDs remain separately selectable when names and VID:PID match.

- [ ] **Step 2: Run focused tests and confirm the role presentation is missing**

Run: `./scripts/dotnet.sh test tests/JabraDesktop.Tests --filter FullyQualifiedName~LocalizationServiceTests -m:1`
Expected: FAIL because the role keys are not in the catalogs.

- [ ] **Step 3: Add localized labels and present per-device identity**

Create `DeviceListItemViewModel(DeviceInfo device, LocalizationService texts)` with `Device`, `Name`, `RoleLabel`, `IdentifierText`, and `HasIdentifier` properties plus `NotifyLocalizationChanged()`. `RoleLabel` maps `DeviceRole` to the four catalog keys; `IdentifierText` formats only when both IDs are present, using `VID {VendorId:X4} · PID {ProductId:X4}`. Make `MainViewModel.Devices` an `ObservableCollection<DeviceListItemViewModel>` and its `SelectedDevice` a `DeviceListItemViewModel?`; map selection to `SelectedDevice.Device.Id` when calling `DeviceSession.Select`. Reuse display rows by opaque `Device.Id` during synchronization so duplicate names and matching VID:PID remain independently selectable. Refresh each row's `RoleLabel` after a language change. Bind the sidebar to `Name`, `RoleLabel`, and the conditionally visible `IdentifierText`; include the selected role in `DeviceSubtitle`. Keep serial number and SDK/session `Id` out of presentation. Use `Device.CanPair` unchanged for scan/action visibility. Update the probe to show role and VID:PID for local diagnosis, omitting unavailable identifiers.

- [ ] **Step 4: Run focused UI-model and localization tests**

Run: `./scripts/dotnet.sh test tests/JabraDesktop.Tests --filter FullyQualifiedName~LocalizationServiceTests -m:1`
Expected: PASS for German, English, and dynamic language change.

Run: `./scripts/dotnet.sh test tests/JabraDesktop.Tests --filter FullyQualifiedName~ViewModelTests -m:1`
Expected: PASS, including the rule that role does not grant pairing capability.

- [ ] **Step 5: Commit Task 2**

```bash
git add src/JabraDesktop.App/Localization/UiText.cs src/JabraDesktop.App/Localization/UiTextCatalog.cs src/JabraDesktop.App/Views/MainWindow.axaml src/JabraDesktop.App/Views/MainWindow.axaml.cs src/JabraDesktop.Probe/Program.cs tests/JabraDesktop.Tests/ViewModelTests.cs tests/JabraDesktop.Tests/LocalizationServiceTests.cs
git commit -m "feat: show localized device roles and identifiers"
```

## Task 3: Validate hardware coverage and prepare release 0.3.0

**Files:**
- Modify: `docs/hardware-validation.md`
- Create: `docs/releases/v0.3.0.md`
- Modify: `README.md`
- Modify: `Directory.Build.props`
- Modify: `packaging/PKGBUILD`

**Interfaces:**
- Consumes device role and identity output from Task 2's probe.
- Release version is `0.3.0`; Arch package version is `pkgver=0.3.0`, initially `pkgrel=1`.

- [ ] **Step 1: Capture Link 370 and Link 380 hardware baselines**

Run `./scripts/dotnet.sh run --project src/JabraDesktop.Probe -- list` with Link 370 alone, Link 380 alone, and both connected. Record SDK/Device Connector and OS/kernel versions, reported model/role/VID:PID, and whether the device is manageable. Do not record serial numbers or Bluetooth addresses. For each detected manageable dongle, run `./scripts/dotnet.sh run --project src/JabraDesktop.Probe -- peers <index>` and record whether its list can be read. Run `scan` only with a test headset deliberately in pairing mode. On Link 370, test Pair/Connect/Disconnect only if a headset is available for this explicit compatibility check; record success per operation. Do not run `unpair` in this baseline task.

- [ ] **Step 2: Update the hardware matrix and user-facing release notes**

Update `docs/hardware-validation.md` with Link 370 and Link 380 rows for the combinations actually connected. Mark unavailable combinations `Not tested`. Include the known Evolve 75 SE / Link 380 result already recorded and any other headset models identified in the baseline. Write `docs/releases/v0.3.0.md` with visible device identity/roles, hardware coverage, upgrade command, and known limitations. Update the README's capability explanation and current release installation instructions.

- [ ] **Step 3: Set the synchronized version to 0.3.0**

Change `Directory.Build.props` `<Version>` and `packaging/PKGBUILD` `pkgver` to `0.3.0`; keep `pkgrel=1`. Update version examples that refer to the current package in README and release notes.

- [ ] **Step 4: Run complete verification and build the Arch package**

Run: `./scripts/dotnet.sh test -m:1`
Expected: all tests pass.

Run: `git diff --check`
Expected: no whitespace errors.

Run: `./scripts/package.sh`
Expected: self-contained Linux publish and Arch package build succeed with version `0.3.0-1`.

Inspect the package file list and launch the package-built app on the target CachyOS/KDE session. Verify both Link models and DE/EN labels on available hardware; record anything unavailable as not tested.

- [ ] **Step 5: Commit release preparation**

```bash
git add docs/hardware-validation.md docs/releases/v0.3.0.md README.md Directory.Build.props packaging/PKGBUILD
git commit -m "release: prepare Jabra Desktop 0.3.0"
```

- [ ] **Step 6: Publish after final release review**

Create an annotated `v0.3.0` tag and GitHub Release with the verified Arch package and SHA-256. Confirm the package version, tag, release title, and changelog all say `0.3.0`. Do not describe untested models as supported.

## Final Review Checklist

- [ ] Existing pairing flows and all automated tests pass.
- [ ] Unknown SDK roles and absent IDs display safely.
- [ ] Duplicate device names remain separately selectable.
- [ ] DE/EN device roles update dynamically.
- [ ] No serial number or Bluetooth address is shown or committed.
- [ ] Hardware claims match `docs/hardware-validation.md`.
- [ ] Arch package, changelog, tag, and GitHub Release agree on version `0.3.0`.
