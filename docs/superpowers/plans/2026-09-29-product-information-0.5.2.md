# Product Information 0.5.2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show verified read-only part number, audio name, and connected phone in device details and release 0.5.2.

**Architecture:** Extend `DeviceProperties` and the existing optional Property reader. Map each SDK value independently; the view model only exposes populated fields. Keep peer properties tied to the physical endpoint.

**Tech Stack:** .NET 10, Jabra.NET.Sdk.Properties, Avalonia, xUnit, Arch `makepkg`, Debian `ar`/`tar`.

**Spec:** `docs/superpowers/specs/2026-09-29-product-information-0.5.2-design.md`

## Global Constraints

- Read only; never write these three SDK properties or update firmware.
- Only show values supplied by the selected physical device.
- Keep optional property failures isolated from pairing and existing telemetry.
- Localize labels and empty-phone text in German and English.

## Review Focus

- Unsupported property or incorrect SDK value type: hide its field.
- Empty `mobileDevice1`: show a localized no-phone value only after a successful read.
- Two dongles and endpoints: never display another device's product information.
- A late result after USB removal: do not publish stale data.
- Language changes: update labels and empty-phone text without reopening the window.

---

### Task 1: Product property mapping

**Files:** `src/JabraDesktop.Core/Models.cs`, `src/JabraDesktop.Jabra/DevicePropertyCapabilities.cs`, `src/JabraDesktop.Jabra/PropertyValueMapper.cs`, `src/JabraDesktop.Jabra/DevicePropertyValueUpdate.cs`, `src/JabraDesktop.Jabra/JabraBackend.cs`, `tests/JabraDesktop.Tests/DevicePropertyTests.cs`.

**Interfaces:** `DeviceProperties` gains `PartNumber`, `AudioName`, `MobilePhone`, and `MobilePhoneKnown`; `DevicePropertyValueKind` gains the corresponding values. `DevicePropertyCapabilities.For` assigns `audioName` to dongles, `skuId` to endpoints, and `mobileDevice1` to headsets. `DevicePropertyValueUpdate.Apply` maps returned values.

- [ ] Add failing tests for role selection, string trimming, invalid types, empty phone, and preserving unrelated values.
- [ ] Run focused tests and confirm expected RED failures.
- [ ] Implement model, capability, mapper, and backend metadata persistence.
- [ ] Run focused and full tests; confirm GREEN.
- [ ] Commit the tested mapping.

### Task 2: Detail panel and localization

**Files:** `src/JabraDesktop.App/ViewModels/MainViewModel.cs`, `src/JabraDesktop.App/Views/MainWindow.axaml`, `src/JabraDesktop.App/Localization/UiText.cs`, `src/JabraDesktop.App/Localization/UiTextCatalog.cs`, `tests/JabraDesktop.Tests/ViewModelTests.cs`, `tests/JabraDesktop.Tests/LocalizationServiceTests.cs`.

**Interfaces:** The view model exposes `ShowPartNumber`, `PartNumberText`, `ShowAudioName`, `AudioNameText`, `ShowMobilePhone`, and `MobilePhoneText`; XAML binds the detail card to these properties.

- [ ] Add failing tests for selected dongle/peer value separation, hidden unavailable fields, and language switching.
- [ ] Run focused tests and confirm expected RED failures.
- [ ] Add localized fields and bindings; notify changes on selection/property/language events.
- [ ] Run focused and full tests; confirm GREEN.
- [ ] Commit the tested UI.

### Task 3: Hardware and release

**Files:** `Directory.Build.props`, `packaging/PKGBUILD`, `README.md`, `docs/hardware-validation.md`, `docs/releases/v0.5.2.md`, `docs/verification-0.5.2.md`, `packaging/SHA256SUMS-0.5.2`.

- [ ] Probe read-only values on attached hardware without competing app processes; record observed models/versions and limits.
- [ ] Bump 0.5.2 and build Arch and Debian packages.
- [ ] Run full tests, `git diff --check`, metadata/content/hash checks, and a local app smoke test.
- [ ] Commit, merge to `main`, push, tag `v0.5.2`, and publish packages plus checksums on GitHub.
