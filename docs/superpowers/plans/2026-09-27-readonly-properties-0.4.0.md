# Read-only device status properties 0.4.0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show read-only firmware and battery values for the exact Jabra device identities admitted by the Properties capability table.

**Architecture:** Keep the Core contract independent of Jabra SDK types and append an opt-in status-refresh capability to `DeviceInfo`. The Jabra adapter maps exact VID/PID/role combinations to allowlisted properties, reads those values through the optional SDK Properties module, and publishes safe parsed values; the UI triggers an independent manual refresh and shows unavailable states for absent values.

**Tech Stack:** .NET 10, C#, Avalonia, Jabra.NET.Sdk 4.9.1.1, Jabra.NET.Sdk.DevicePairing 3.1.1.2, Jabra.NET.Sdk.Properties 2.5.1.1, xUnit, Arch `makepkg`.

**Spec:** `docs/superpowers/specs/2026-09-27-readonly-properties-0.4.0-design.md`

## Global Constraints

- The Core layer remains free of Jabra SDK types.
- The Properties module is optional; its initialization or read failure must not stop discovery, pairing, or connection actions.
- Only Link 380 VID `0B0E`, PID `24C7`, role Dongle may request `firmwareVersion` in this release.
- Only Evolve 75 SE VID `0B0E`, PID `2502`, role Headset may request `batteryLevel` in this release; the combination remains hardware-unverified until checked.
- Link 370, other device identities, and display-name-only matches must not request properties.
- Firmware values must be non-empty strings; battery values must be integers from 0 through 100 inclusive.
- Read access only; no writes, telemetry watches, periodic status polling, or firmware updates.
- Never log property values, serial numbers, or Bluetooth addresses.
- Reads must not block the UI thread or delay discovery and Bluetooth actions; late results after a timeout or device removal must not be published.
- App, PKGBUILD, Arch package, annotated tag, and GitHub Release versions must match at release time.

## Review Focus

- A known PID with the wrong role must not activate a property; test the exact device capability matrix.
- Unknown IDs, missing IDs, and Link 370 must remain non-refreshable; test null and unsupported identity cases.
- Wrong SDK value types, empty firmware strings, and battery values outside 0–100 must stay unavailable; test every mapping boundary.
- Slow or failed optional reads must not set the device manager's global error or prevent pair actions; test failure, timeout, and concurrent action behavior.
- A device removed while a read is in flight must not reappear or publish a late value; test stale completion handling.

---

### Task 1: Add the SDK-independent status refresh contract

**Files:**
- Modify: `src/JabraDesktop.Core/Models.cs`
- Modify: `src/JabraDesktop.Core/IDeviceBackend.cs`
- Modify: `src/JabraDesktop.Core/DeviceSession.cs`
- Modify: `tests/JabraDesktop.Tests/FakeBackend.cs`
- Test: `tests/JabraDesktop.Tests/SessionTests.cs`

**Interfaces:**
- `DeviceInfo` appends `bool CanRefreshProperties = false` after `ProductId`, preserving existing constructor calls.
- `IDeviceBackend` adds `Task RefreshDevicePropertiesAsync(string deviceId, CancellationToken token)`.
- `DeviceSession` adds `Task RefreshDevicePropertiesAsync(string deviceId, CancellationToken token = default)`, delegating only while that physical device remains in its snapshot and without requiring `CanPair` or using the pairing-operation busy lock.

- [x] **Step 1: Add failing session contract tests**

Add `RefreshPropertiesCanTargetAHeadsetWithoutPairing` and assert a `DeviceInfo` with `CanPair == false` and `CanRefreshProperties == true` is passed to `FakeBackend`. Add `RefreshPropertiesForRemovedDeviceDoesNothing` and assert no backend call occurs after the snapshot no longer contains the ID. Add `PropertyRefreshDoesNotSerializeBluetoothActions` with an incomplete fake property task; assert `RefreshAsync` for a selected dongle can still finish.

- [x] **Step 2: Run the focused tests and confirm they fail for the missing contract**

Run: `./scripts/dotnet.sh test tests/JabraDesktop.Tests --filter FullyQualifiedName~SessionTests -m:1`

Expected: FAIL because `CanRefreshProperties` and `RefreshDevicePropertiesAsync` are not yet implemented.

- [x] **Step 3: Implement the Core status-refresh contract**

Append `CanRefreshProperties = false` to `DeviceInfo`; add the backend method; make `DeviceSession.RefreshDevicePropertiesAsync` validate that the ID is still present, then delegate directly. It must not require a dongle and must not change the existing `IsBusy` state used by Bluetooth operations. Extend `FakeBackend` with `PropertyRefreshCalls` and an optional `PropertyRefreshCompletion` task.

- [x] **Step 4: Run session tests**

Run: `./scripts/dotnet.sh test tests/JabraDesktop.Tests --filter FullyQualifiedName~SessionTests -m:1`

Expected: PASS, including headset refresh, removed-device no-op, and independent Bluetooth operations.

- [x] **Step 5: Commit Task 1**

```bash
git add src/JabraDesktop.Core/Models.cs src/JabraDesktop.Core/IDeviceBackend.cs src/JabraDesktop.Core/DeviceSession.cs tests/JabraDesktop.Tests/FakeBackend.cs tests/JabraDesktop.Tests/SessionTests.cs
git commit -m "feat: add device status refresh contract"
```

### Task 2: Integrate the read-only Jabra Properties adapter

**Files:**
- Modify: `src/JabraDesktop.Jabra/JabraDesktop.Jabra.csproj`
- Modify: `src/JabraDesktop.Jabra/JabraBackend.cs`
- Create: `src/JabraDesktop.Jabra/DevicePropertyCapabilities.cs`
- Create: `src/JabraDesktop.Jabra/PropertyValueMapper.cs`
- Create: `src/JabraDesktop.Jabra/DevicePropertiesReader.cs`
- Create: `src/JabraDesktop.Jabra/DevicePropertyReadRunner.cs`
- Modify: `tests/JabraDesktop.Tests/JabraDesktop.Tests.csproj`
- Test: `tests/JabraDesktop.Tests/DevicePropertyTests.cs`
- Modify: `THIRD-PARTY-NOTICES.md`

**Interfaces:**
- Add package `Jabra.NET.Sdk.Properties` version `2.5.1.1` to the Jabra adapter.
- `DevicePropertyCapabilities.Find(DeviceInfo device) -> DevicePropertyCapability?` returns `firmwareVersion` for Link 380 identity and `batteryLevel` for Evolve 75 SE identity only; `DevicePropertyCapability` records the property name and expected value kind.
- `PropertyValueMapper.Firmware(PropertyValue value) -> string?` accepts only `StringPropertyValue` with non-empty trimmed `AsString()`; `PropertyValueMapper.BatteryPercent(PropertyValue value) -> int?` accepts only `IntegerPropertyValue` in [0,100].
- `IDevicePropertiesReader.InitializeAsync(IApi api, CancellationToken token)` initializes the SDK property factory, and `Task<PropertyValue> GetAsync(IDevice device, string propertyName, CancellationToken token)` reads one named property. `JabraPropertiesReader` creates a `PropertyModule`, one property factory, and one-property maps for reads.
- `DevicePropertyReadRunner.ReadAsync(Func<Task<PropertyValue>> read, CancellationToken token) -> Task<PropertyValue?>` bounds an SDK read to 10 seconds, returns null on failure/timeout/already-in-flight, and keeps tracking an unfinished native read so reads for that device never overlap.
- `JabraBackend.RefreshDevicePropertiesAsync` reads an existing SDK entry without requiring a `BluetoothDongle`, applies parsed values to that entry, and publishes only if the same entry remains attached.

- [x] **Step 1: Add failing capability and parser tests**

In `DevicePropertyTests`, add `FindsFirmwareForLink380`, `FindsBatteryForEvolve75Se`, `RejectsLink370AndUnknownIdentity`, and `RejectsKnownIdsWithWrongRole`. Test firmware with `PropertyValue.FromString("2.1.0")`, whitespace-only firmware, and `PropertyValue.FromInt32(75)`; test battery with integer values 0 and 100, -1 and 101, plus string and number values.

- [x] **Step 2: Run focused tests and verify the missing adapter API fails**

Run: `./scripts/dotnet.sh test tests/JabraDesktop.Tests --filter FullyQualifiedName~DevicePropertyTests -m:1`

Expected: FAIL because the capability table and value mapper do not exist.

- [x] **Step 3: Add exact capability mapping and typed value parsing**

Create `DevicePropertyCapabilities.cs` with the two VID/PID/role entries from the spec and no display-name matching. Implement `PropertyValueMapper` using the SDK's `StringPropertyValue`/`IntegerPropertyValue` types and `AsString()`/`AsInteger()` accessors. Set `DeviceInfo.CanRefreshProperties` true only when the capability table has an entry and the optional Properties factory initialized successfully.

- [x] **Step 4: Add the optional Properties package and a testable SDK reader**

Add `Jabra.NET.Sdk.Properties` version `2.5.1.1` as a package reference to both the adapter and test project. In `DevicePropertiesReader.cs`, define the internal `IDevicePropertiesReader` seam and implement `JabraPropertiesReader` with the public SDK APIs. The reader initializes once after Core SDK startup and reads only the name it is given. Add `InternalsVisibleTo("JabraDesktop.Tests")` so tests can inject a fake reader and test the refresh runner without initializing the physical SDK.

- [x] **Step 5: Implement initial and manual property reads safely**

For each discovered supported entry, begin one asynchronous initial read after SDK startup; also run one initial pass over the final device snapshot to cover devices emitted during startup. `JabraBackend.RefreshDevicePropertiesAsync` must serialize reads per entry, wrap SDK `Get()` in a 10-second timeout, map errors/timeouts to null values, and keep any unfinished native read tracked so another read cannot overlap it. Ignore late results after timeout or removal. SDK module initialization failure leaves `CanRefreshProperties` false and values null without raising the pairing/session `Faulted` event. Do not log values.

- [x] **Step 6: Add adapter lifecycle tests**

Use the fake reader to test successful values, optional-module initialization failure, read exception, timeout, duplicate refresh while an SDK operation is still in flight, and removal before completion. Assert discovery and the existing pairing capability remain unchanged, failures do not publish a global error, and late values are ignored.

- [x] **Step 7: Run adapter tests and full existing tests**

Run: `./scripts/dotnet.sh test tests/JabraDesktop.Tests --filter FullyQualifiedName~DevicePropertyTests -m:1`

Expected: PASS for all capability, parser, failure, timeout, and removal cases.

Run: `./scripts/dotnet.sh test -m:1`

Expected: PASS with all prior pairing, UI, and lifecycle tests unchanged.

- [x] **Step 8: Review package terms and commit Task 2**

Verify the `Jabra.NET.Sdk.Properties` license files and publish/runtime assets from the restored package; update `THIRD-PARTY-NOTICES.md` with its exact license and relevant transitive/runtime components. Confirm there are no native files for unsupported platforms copied into the Linux package.

```bash
git add src/JabraDesktop.Jabra/JabraDesktop.Jabra.csproj src/JabraDesktop.Jabra/JabraBackend.cs src/JabraDesktop.Jabra/DevicePropertyCapabilities.cs src/JabraDesktop.Jabra/PropertyValueMapper.cs src/JabraDesktop.Jabra/DevicePropertiesReader.cs tests/JabraDesktop.Tests/JabraDesktop.Tests.csproj tests/JabraDesktop.Tests/DevicePropertyTests.cs THIRD-PARTY-NOTICES.md
git commit -m "feat: read supported Jabra device properties"
```

### Task 3: Add localized status refresh UI

**Files:**
- Modify: `src/JabraDesktop.App/ViewModels/MainViewModel.cs`
- Modify: `src/JabraDesktop.App/Localization/UiText.cs`
- Modify: `src/JabraDesktop.App/Localization/UiTextCatalog.cs`
- Modify: `src/JabraDesktop.App/Views/MainWindow.axaml`
- Modify: `tests/JabraDesktop.Tests/ViewModelTests.cs`
- Modify: `tests/JabraDesktop.Tests/LocalizationServiceTests.cs`

**Interfaces:**
- Add `UiText.RefreshDeviceStatus` and `UiText.RefreshingDeviceStatus` in German and English.
- `MainViewModel` exposes `bool IsRefreshingProperties`, `bool CanRefreshProperties`, and `IAsyncRelayCommand RefreshPropertiesCommand`.
- The command captures the selected device's opaque session ID, calls `DeviceSession.RefreshDevicePropertiesAsync`, and ends its visual busy state when the bounded adapter call returns. It is visible only when `DeviceInfo.CanRefreshProperties` is true and remains independent of scan/pair busy state.

- [x] **Step 1: Add failing ViewModel and localization tests**

Add `RefreshPropertiesCommandTargetsSelectedDevice` and assert the selected headset ID reaches `FakeBackend`, `IsRefreshingProperties` is true while the fake task is pending, and returns false after completion. Add `UnsupportedDeviceCannotRefreshProperties` asserting a Link 370 row does not expose an enabled refresh command. Add `PropertyRefreshDoesNotBlockScanOrPairCommands` with an incomplete property refresh. Add German/English exact-label assertions and a dynamic language-change assertion for the refresh command.

- [x] **Step 2: Run focused tests and confirm refresh presentation is absent**

Run: `./scripts/dotnet.sh test tests/JabraDesktop.Tests --filter FullyQualifiedName~ViewModelTests -m:1`

Expected: FAIL because the status refresh command and localization values are not present.

- [x] **Step 3: Implement localized command state and bind the view**

Add the localized command labels. Bind a refresh button next to the Battery/Firmware card; show it only when the selected row supports status refresh, disable it while refreshing, and change its localized text for the pending state. Keep property refresh busy state separate from `DeviceSession.IsBusy` and scan state. Notify `CanRefreshProperties`, `IsRefreshingProperties`, and command availability when selection or language changes.

- [x] **Step 4: Run focused UI tests**

Run: `./scripts/dotnet.sh test tests/JabraDesktop.Tests --filter FullyQualifiedName~ViewModelTests -m:1`

Expected: PASS for selected identity, unsupported device, independent actions, busy completion, and dynamic DE/EN updates.

- [ ] **Step 5: Commit Task 3**

```bash
git add src/JabraDesktop.App/ViewModels/MainViewModel.cs src/JabraDesktop.App/Localization/UiText.cs src/JabraDesktop.App/Localization/UiTextCatalog.cs src/JabraDesktop.App/Views/MainWindow.axaml tests/JabraDesktop.Tests/ViewModelTests.cs tests/JabraDesktop.Tests/LocalizationServiceTests.cs
git commit -m "feat: add localized status refresh controls"
```

### Task 4: Validate properties and prepare release 0.4.0

**Files:**
- Modify: `docs/hardware-validation.md`
- Create: `docs/releases/v0.4.0.md`
- Modify: `README.md`
- Modify: `Directory.Build.props`
- Modify: `packaging/PKGBUILD`
- Modify: package notices/consent files only if the restored SDK package requires an additional notice.

**Interfaces:**
- Consumes the allowlisted property behavior from Task 2 and the localized refresh command from Task 3.
- Release version is `0.4.0`; Arch package version is `pkgver=0.4.0`, initially `pkgrel=1`.
- Hardware matrix lists Link 380 firmware and Evolve 75 SE battery reads as separate cases; any unavailable live test remains explicitly `Not tested`.

- [ ] **Step 1: Run the hardware property probe with available devices**

Use the app adapter/probe on Link 380 and Evolve 75 SE independently. Record only model, VID/PID, role, OS/kernel, Core/Properties/Device Connector versions, and returned values. Do not perform writes or scan/pair/unpair operations. If SDK communication is unavailable, record the reason as unverified and continue with automated validation.

- [ ] **Step 2: Update hardware and user documentation**

Add observed property outcomes to `docs/hardware-validation.md`, separating documented support from successful reads. Create `docs/releases/v0.4.0.md` with the read-only behavior, exact device coverage, install/upgrade command, hardware limitations, and test totals. Update README capability and current package instructions. Do not claim Link 370 or general Evolve support.

- [ ] **Step 3: Synchronize versions and verify package contents**

Set `Directory.Build.props` and `packaging/PKGBUILD` to `0.4.0` / `pkgrel=1`. Build the self-contained Arch package and inspect its included Properties assemblies, license files, and runtime/native assets; confirm only Linux-supported runtime assets are shipped.

- [ ] **Step 4: Run the release verification suite**

Run: `./scripts/dotnet.sh test -m:1`

Expected: all tests pass.

Run: `git diff --check`

Expected: no whitespace errors.

Run: `./scripts/package.sh`

Expected: the Linux publish and `jabra-desktop-0.4.0-1-x86_64.pkg.tar.zst` build succeed.

Inspect package files and launch the package-built app. Verify DE/EN labels, tray launch, no UI flicker during property refresh, and continued pairing-list refresh. Report hardware properties exactly as the matrix states.

- [ ] **Step 5: Commit release preparation**

```bash
git add docs/hardware-validation.md docs/releases/v0.4.0.md README.md Directory.Build.props packaging/PKGBUILD
git commit -m "release: prepare Jabra Desktop 0.4.0"
```

- [ ] **Step 6: Publish after final review**

Create annotated tag `v0.4.0` and GitHub Release with the verified Arch package and SHA-256. Confirm package, app, tag, release, and notes all say `0.4.0`. Do not claim unverified device/firmware combinations.

## Final Review Checklist

- [ ] Unsupported devices never request SDK properties.
- [ ] Battery and firmware parsing accepts only the expected SDK types and documented value ranges.
- [ ] Optional SDK failures and timeouts do not affect pairing or block the UI.
- [ ] Removed devices and timed-out reads cannot publish stale values.
- [ ] No property write or telemetry/polling path was added.
- [ ] Package notices and Linux contents include the Properties SDK correctly.
- [ ] Hardware claims match `docs/hardware-validation.md`.
- [ ] Arch package, app version, changelog, tag, and GitHub Release agree on `0.4.0`.
