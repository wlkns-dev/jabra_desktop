# Multi-Dongle Device Hierarchy Design

## Purpose

Make every connected Jabra dongle and its paired endpoint devices visible and manageable in one place. Users should be able to understand which headset or speaker belongs to which dongle, see the information that applies to each device, and reach connection actions without opening pairing search on an endpoint.

## Agreed behavior

- Show every detected dongle as a top-level item in a hierarchical device navigator.
- Show every paired headset or speaker as a child under its dongle, including paired devices that are currently disconnected. If a device is paired with more than one dongle, show it under each relevant dongle.
- Keep directly attached devices that have no dongle relationship as top-level items.
- Selecting a dongle shows dongle status, paired endpoints, refresh, nearby-device search, pairing, and connection management.
- Selecting a headset or speaker shows its connection status and device information, plus the applicable connection action and Unpair. The connection action is Connect while disconnected and Disconnect while connected. Do not show nearby-device search or the generic dongle overview for an endpoint.
- Show per-device information throughout the inventory rather than only for the selected device. Firmware is relevant to dongles and endpoints; battery is relevant to battery-powered headsets and speakers, not dongles.
- Read and display all relevant properties the SDK makes available for each device, not just the two product mappings currently hard-coded. A property that applies but cannot be read is shown as unavailable; semantically irrelevant fields are omitted.
- Keep the interface localized in German and English, and update rows and details when dongles, pairing lists, connection states, or property values change.

## Device inventory and state

The device backend already exposes `GetPeersAsync(dongleId, ...)`, and each peer identity is scoped to the dongle that supplied it. The session currently caches peers only for the selected dongle. The inventory model will retain a pairing snapshot for every active dongle, with each pairing represented by its parent dongle ID, opaque peer ID, display name, and connection state. Actions must carry both IDs so duplicate peers across dongles always target the intended dongle.

Refresh each dongle's pairing list independently and serialize commands per dongle, preserving the existing per-dongle operation lock. Removing a dongle removes its children from the live hierarchy. A paired device remains visible beneath its dongle while disconnected. USB devices without a dongle pairing relationship remain visible as independent top-level devices.

## Information and properties

The property model must distinguish property applicability from property value. It will represent firmware and battery availability independently, rather than one `CanRefreshProperties` flag. The inventory refresh should update each device's values independently so one slow or unsupported property does not hide other values or block connection actions.

The initial implementation must investigate and use the SDK's supported property definitions and child-device facilities. It must not assume that the current USB `IDevice` property path automatically supports remote Bluetooth peers. For each device kind, use SDK-exposed data when available; show a localized unavailable value for an applicable property that cannot currently be read. Omit battery from dongles. Keep the model extensible for other device fields without adding unsupported claims.

Render each device's available summary in its own row and expose its applicable properties in the selected-device detail view. Show a property refresh action only where the SDK provides a refreshable property path. Devices without any applicable properties should still show their connection status and actions without an empty properties panel.

## Navigation and presentation

Use an indented hierarchy in the existing left-side device navigation. Parent rows identify dongles; child rows identify their paired endpoint, current connection status, and available information summary. Selecting a child selects that exact dongle-peer relationship in the detail area.

The dongle detail view keeps the current search and pairing flow. The endpoint detail view contains the endpoint name, connection state, applicable property values, Connect or Disconnect, and Unpair with the existing confirmation. It hides nearby-device search, pairing instructions, and the dongle device-overview/status card.

## Error and update behavior

One dongle failing to refresh must not clear another dongle's inventory. Keep the last known pairing snapshot for a transient refresh failure and surface a per-dongle error; remove children only when a successful refresh confirms that they are no longer paired or when the parent dongle is removed. Property read failures remain isolated per property/device. Connection and unpair commands refresh only the affected dongle's pairing list after completion.

## Validation

- Test inventory refresh with two dongles and distinct peers, including the same peer identity/name paired through both dongles.
- Test disconnected paired devices remain nested under their parent dongles.
- Test Connect/Disconnect/Unpair route to the correct parent dongle and peer, and the selected endpoint hides scan/overview controls.
- Test each property independently: firmware-only, battery-only, both, neither, and a failed read.
- Test that a failed refresh on one dongle leaves the other dongle's children and actions usable.
- Test live updates and German/English labels without recreating the window.
- Build and run the full test suite; manually verify with the user's Link 370 + Speaker 710 and Link 380 + Evolve 75 pairings.

## Scope boundaries

This work does not add firmware update, arbitrary property editing, audio routing, or simultaneous playback. It does not promise that every Jabra product exposes battery or firmware through the current SDK; it promises to display the relevant data the SDK can actually supply and to represent unavailable values clearly.
