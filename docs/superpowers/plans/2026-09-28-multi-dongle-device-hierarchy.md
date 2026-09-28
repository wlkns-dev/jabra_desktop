# Multi-Dongle Device Hierarchy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show every dongle and its paired devices in an indented live inventory, with the right information and actions for each device.

**Architecture:** Keep one pairing snapshot per dongle in `DeviceSession`, refresh dongles independently, and route endpoint actions with both dongle ID and peer ID. Model property applicability separately from values so each device can show its own firmware and battery data without showing an inapplicable battery field on a dongle. Build the tree and selection-specific detail view in the existing Avalonia MVVM layer.

**Tech Stack:** C# / .NET 10, Avalonia 11, CommunityToolkit.Mvvm, Jabra.NET SDK 4.9.1.1, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-28-multi-dongle-device-hierarchy-design.md`

## Global Constraints

- Show each paired headset or speaker beneath every dongle with which it is paired, including while disconnected.
- Actions on an endpoint must target its exact parent dongle and peer.
- Firmware applies to dongles and endpoints; battery applies to battery-powered headsets and speakers, not dongles.
- One dongle's failed refresh must not clear another dongle's inventory.
- Coalesce overlapping automatic refresh requests per dongle so periodic ticks do not build an unbounded command queue.
- Keep German and English localization; never log Bluetooth addresses or serial numbers.
- Do not claim property support the SDK does not expose; show unavailable values for applicable properties that cannot be read.
- Do not add firmware update, arbitrary property editing, audio routing, or simultaneous playback.

## Review Focus

- **Duplicate peers across dongles:** two identical names or peer identifiers remain separate child rows and target separate dongles. Test in Task 2 and Task 3.
- **Transient refresh error:** a failed read preserves that dongle's last successful peers and leaves the other dongle usable. Test in Task 2.
- **Slow or unsupported telemetry:** a failed battery read does not suppress firmware, and property reads do not hold the Bluetooth action lock. Test in Task 1.
- **Dongle removal during refresh:** remove only that dongle and its children; ignore late results while retaining other groups. Test in Task 2.
- **Selection changes while an action is pending:** the action stays routed to the original dongle-peer pair and does not update another group's state. Test in Task 3.

---

### Task 1: Per-device telemetry and SDK peer properties

**Files:**
- Modify: `src/JabraDesktop.Core/Models.cs`
- Modify: `src/JabraDesktop.Jabra/DevicePropertyCapabilities.cs`
- Modify: `src/JabraDesktop.Jabra/DevicePropertiesReader.cs`
- Modify: `src/JabraDesktop.Jabra/DevicePropertyReadRunner.cs`
- Modify: `src/JabraDesktop.Jabra/JabraBackend.cs`
- Modify: `tests/JabraDesktop.Tests/FakeBackend.cs`
- Test: `tests/JabraDesktop.Tests/DevicePropertyTests.cs`

**Interfaces:**
- Produce `DeviceProperties(bool BatteryApplicable, int? BatteryPercent, bool FirmwareApplicable, string? Firmware, bool CanRefresh)`.
- Add an optional `DeviceProperties` value to both `DeviceInfo` and `PeerInfo`; migrate existing property fields and tests to this shared value.
- Peer telemetry is returned with its `PeerInfo` from `GetPeersAsync`. Read a peer's properties only through an SDK-exposed child `IDevice`; otherwise report applicable values as unavailable.

- [ ] **Step 1: Write failing property-model tests.** Add `DevicePropertiesAreIndependentByDeviceKind` for firmware-only Link 380, battery-capable headset, speaker with both applicable properties, dongle without battery, and a device with neither property. Add `FailedBatteryReadDoesNotSuppressFirmware` and `ApplicableButUnreadPropertyStaysUnavailable`; assert the snapshot retains firmware when battery retrieval fails and marks an applicable missing value unavailable.
- [ ] **Step 2: Run `./scripts/dotnet.sh test tests/JabraDesktop.Tests --filter FullyQualifiedName~DevicePropertyTests -m:1`.** Confirm the new tests fail because the shared property model and independent reads do not exist.
- [ ] **Step 3: Implement the shared property snapshot and independent reads.** Use `firmwareVersion` for dongles and endpoints, and probe `batteryLevel` only for endpoint roles; rely on SDK property reads to determine model support instead of limiting reads to the current Link 380 and Evolve 75 SE product IDs. Isolate and time-bound each property read. Resolve a paired endpoint through `BluetoothModule.TryCreateBluetoothChildDevice`; if it cannot be exposed as an SDK `IDevice`, leave its applicable values unavailable without failing the pairing list. Cache endpoint properties independently from the four-second connection-state refresh, refresh them no more often than every 30 seconds, and let an explicit property refresh bypass that cache.
- [ ] **Step 4: Re-run the focused property tests, then the full suite with `./scripts/dotnet.sh test -m:1`.** Confirm per-property failures and unavailable peer telemetry do not block connection operations.
- [ ] **Step 5: Commit** `feat: model per-device telemetry`.

### Task 2: Retain and refresh pairing inventories for every dongle

**Files:**
- Modify: `src/JabraDesktop.Core/DeviceSession.cs`
- Modify: `tests/JabraDesktop.Tests/FakeBackend.cs`
- Test: `tests/JabraDesktop.Tests/SessionTests.cs`

**Interfaces:**
- Produce `DonglePeerSnapshot(string DongleId, IReadOnlyList<PeerInfo> Peers, string? Error = null)`.
- Add `IReadOnlyList<DonglePeerSnapshot> PeerSnapshots` and `Task RefreshAllAsync(CancellationToken token = default)` to `DeviceSession`.
- Add `Task RefreshAsync(string dongleId, CancellationToken token = default)`; keep parameterless `RefreshAsync` delegating to the selected dongle.
- Add `Task RunAsync(string dongleId, string peerId, DeviceAction action, CancellationToken token = default)`; keep the selected-dongle overload delegating to it.

- [ ] **Step 1: Write failing session tests.** Add `RefreshAllRetainsDistinctPeerGroupsForTwoDongles` (each peer remains under its own dongle), `SamePeerOnTwoDonglesRemainsDistinct`, `ConcurrentRefreshAllCoalescesPerDongle`, `FailedDongleRefreshKeepsLastSnapshotAndOtherDongleUsable`, `RemovedDongleDropsOnlyItsSnapshot`, and `PeerActionTargetsExplicitParentDongle`.
- [ ] **Step 2: Run `./scripts/dotnet.sh test tests/JabraDesktop.Tests --filter FullyQualifiedName~SessionTests -m:1`.** Confirm the inventory and explicit-target tests fail against selected-only state.
- [ ] **Step 3: Implement per-dongle snapshots and refresh.** Refresh all active dongles independently using the existing backend `GetPeersAsync`; coalesce overlapping refreshes for the same dongle, keep each last successful peer list on failure, and attach its error to only that dongle snapshot. Preserve disconnected peers returned in the pairing list. On dongle removal, discard only that dongle's snapshot. Use the existing per-dongle semaphore for explicit-target actions, and refresh only the affected dongle after an action.
- [ ] **Step 4: Re-run focused session tests and `./scripts/dotnet.sh test -m:1`.** Confirm two different dongles can refresh independently and action routing remains correct after selection changes.
- [ ] **Step 5: Commit** `feat: retain peer inventory per dongle`.

### Task 3: Build selectable dongle and endpoint navigation models

**Files:**
- Create: `src/JabraDesktop.App/ViewModels/DongleTreeItemViewModel.cs`
- Modify: `src/JabraDesktop.App/ViewModels/DeviceListItemViewModel.cs`
- Modify: `src/JabraDesktop.App/ViewModels/MainViewModel.cs`
- Test: `tests/JabraDesktop.Tests/ViewModelTests.cs`

**Interfaces:**
- Produce `DongleTreeItemViewModel` with one dongle item and an observable `ObservableCollection<PeerRow> Peers`.
- Add `ObservableCollection<DongleTreeItemViewModel> Dongles`, `ObservableCollection<DeviceListItemViewModel> StandaloneDevices`, and `PeerRow? SelectedPeer` to `MainViewModel`.
- Each `PeerRow` carries `DongleId` and routes actions through the explicit-target session method from Task 2.

- [ ] **Step 1: Write failing view-model tests.** Add `DongleTreeContainsOnlyItsOwnPeers`, `SelectingPeerSelectsItsParentDongle`, `EndpointHidesPairingSearchAndOverview`, `EndpointShowsStatusAndApplicableProperties`, `PeerPropertyRefreshTargetsItsParentDongle`, and `DisconnectedEndpointOffersConnect`.
- [ ] **Step 2: Run `./scripts/dotnet.sh test tests/JabraDesktop.Tests --filter FullyQualifiedName~ViewModelTests -m:1`.** Confirm group projection and endpoint-specific selection do not exist.
- [ ] **Step 3: Implement grouped rows and selection state.** Project every `PeerSnapshot` into its own parent group; keep non-dongle USB devices top-level. Selecting a child retains its parent dongle as the action target. Compute search visibility from selected item type and property visibility from `DeviceProperties` applicability. Show Connect while disconnected, Disconnect while connected, and the existing confirmation-backed Unpair action.
- [ ] **Step 4: Re-run focused view-model tests and `./scripts/dotnet.sh test -m:1`.** Confirm localized status and action labels update when language changes without recreating the view model.
- [ ] **Step 5: Commit** `feat: model hierarchical Jabra device navigation`.

### Task 4: Render the hierarchy and selection-specific details

**Files:**
- Modify: `src/JabraDesktop.App/Views/MainWindow.axaml`
- Modify: `src/JabraDesktop.App/Localization/UiText.cs`
- Modify: `src/JabraDesktop.App/Localization/UiTextCatalog.cs`
- Test: `tests/JabraDesktop.Tests/LocalizationServiceTests.cs`
- Test: `tests/JabraDesktop.Tests/ViewModelTests.cs`

**Interfaces:**
- Consume `MainViewModel.Dongles`, `StandaloneDevices`, and `SelectedPeer` from Task 3.
- Add German and English strings for hierarchy labels, unavailable properties, endpoint actions, and per-dongle refresh errors.

- [ ] **Step 1: Add failing localization and visibility assertions.** Verify equivalent German/English labels for endpoint status/actions and assert unsupported fields are hidden while applicable-but-unread values show the localized unavailable text.
- [ ] **Step 2: Run `./scripts/dotnet.sh test tests/JabraDesktop.Tests --filter FullyQualifiedName~LocalizationServiceTests -m:1` and the focused `ViewModelTests` filter.** Confirm missing keys or visibility flags fail.
- [ ] **Step 3: Implement the nested left-side layout and details panels.** Indent peer rows below each dongle and show each row's current state and available telemetry. Keep search and pairing controls on the dongle detail panel. On endpoint selection, show its state, available properties, Connect/Disconnect, and Unpair; hide Nearby devices, pairing instructions, and the dongle overview card. Omit the battery field for dongles.
- [ ] **Step 4: Run both focused test filters and `./scripts/dotnet.sh test -m:1`.** Confirm language switching updates all newly added row and detail labels.
- [ ] **Step 5: Commit** `feat: render per-dongle device hierarchy`.

### Task 5: Verify multi-dongle behavior and prepare the next package version

**Files:**
- Modify: `src/JabraDesktop.App/App.axaml.cs`
- Modify: `src/JabraDesktop.App/ViewModels/MainViewModel.cs`
- Modify: `Directory.Build.props`
- Modify: `README.md`
- Modify: `packaging/PKGBUILD`
- Test: `tests/JabraDesktop.Tests/SessionTests.cs`
- Test: `tests/JabraDesktop.Tests/ViewModelTests.cs`

**Interfaces:**
- Startup and the existing periodic refresh call `RefreshAllAsync`; detail actions refresh their target group.
- Set the application version to `0.5.0` after feature verification.

- [ ] **Step 1: Write failing lifecycle tests.** Add `StartupAndPeriodicRefreshLoadAllDongleGroups`, `TransientFailureDoesNotRemoveOtherDongleChildren`, and `PendingEndpointActionDoesNotOverwriteAnotherGroup`.
- [ ] **Step 2: Run the focused session and view-model tests and verify each new test fails for its intended missing behavior.**
- [ ] **Step 3: Wire startup and periodic inventory refresh.** Refresh pairing lists independently without coupling the timer to whichever dongle is selected; keep UI refresh work off the UI thread and update snapshots through the existing dispatcher path. Bump the application/package version to `0.5.0` and update README package instructions.
- [ ] **Step 4: Run `./scripts/dotnet.sh test -m:1`, `git diff --check`, and `./scripts/publish.sh`.** Build the Arch package with `makepkg -f` in `packaging/` and the Ubuntu package with `./scripts/package-deb.sh`. Manually verify with Link 370 + Speaker 710 and Link 380 + Evolve 75: nested rows, statuses, available data, Connect/Disconnect, Unpair, and continued usability when one dongle is unplugged or returns an error.
- [ ] **Step 5: Commit** `feat: refresh all dongle inventories`.
- [ ] **Step 6: Stop before publishing.** Report package paths and hashes for review; create a remote release only after the user approves the verified build.
