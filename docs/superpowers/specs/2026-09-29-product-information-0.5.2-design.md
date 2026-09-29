# Product information in 0.5.2

## Intent and scope

Show additional read-only information supplied by the selected device: the
part number (`skuId`) on endpoints, the audio device name (`audioName`) on
dongles, and the connected phone (`mobileDevice1`) on headsets. The user can
distinguish hardware without opening Jabra Direct. These are candidates per
role, not promises for every model. No property is written and firmware update
remains separate.

## Behavior

Use the existing optional Properties reader and per-device refresh. A valid,
nonempty string is shown under a localized label. An empty `mobileDevice1`
string successfully read from a headset means no phone is connected and is
shown as such; an unsupported, failed, or mistyped read is hidden. Whitespace
around returned values is removed. An endpoint's values come only from its
physical child device and appear in its detail panel when selected under a
dongle. A dongle's audio name comes only from that dongle. Switching selection,
refreshing, or changing language updates the displayed values without
recreating the window.

## Implementation boundaries

Add optional values to `DeviceProperties`; the core remains SDK-independent.
Map SDK properties independently, using the existing bounded single-flight
reader. Preserve current firmware/battery behavior and do not delay pairing
operations for optional reads. The UI presents only returned product values,
not placeholder fields. Tests cover role/property selection, accepted and
rejected value types, empty-phone semantics, peer/dongle separation, and
German/English labels. Build both Linux packages as 0.5.2, verify locally,
then publish a tagged GitHub release.

## Validation limits

The existing read-only probe confirmed `audioName` on Link 370 and `skuId` plus
an empty `mobileDevice1` on Evolve 75. Live values on other models must be
documented as tested or untested; a successful read does not imply a setting
can be changed. The Ubuntu package can be inspected on this Arch-based host,
but running it on Ubuntu requires an Ubuntu machine.
