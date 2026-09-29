# 0.5.1 verification — 2026-09-29

## Automated and package validation

- Full .NET suite on locally merged `main`: 144 passed, zero failed. The
  release version is 0.5.1 in `Directory.Build.props` and `packaging/PKGBUILD`.
- Self-contained Linux x86_64 publish, Arch `makepkg -f` and Debian package
  build succeeded. `dpkg-deb` was unavailable, so the Debian script used its
  standard `ar`/`tar` packaging path.
- Arch metadata reports `0.5.1-1` for x86_64; Debian control reports
  `0.5.1-1` for amd64. The application DLL in both packages has the same
  SHA-256 as the published build. Both package hashes are recorded in
  `packaging/SHA256SUMS-0.5.1`.
- The Debian package was inspected but not executed on Ubuntu. Existing
  CA1416 warnings refer to calls unsupported on Windows/macOS; this release
  targets Linux.

## Hardware and UI validation

- On CachyOS/KDE, the 0.5.1 release build launched and showed a connected
  Evolve 75 named “Christoph” beneath Link 370 and a connected Speak 710
  beneath Link 380. Saved disconnected devices remained in the selected
  dongle's pairing list. Both dongles and endpoints stayed in their own
  groups; the current firmware and battery values were displayed.
- The release-source SDK path located the connected Evolve 75 under Link 370,
  reported the rename capability, wrote its **unchanged** name through the
  Properties SDK, and returned success. A separate JabraCLI read again returned
  “Christoph”. This checked the property-write path without changing the
  user's chosen name.
- An initial temporary smoke-run did not discover a rename-capable peer within
  its 25-second polling window during device activity; an immediate repeat
  succeeded. The first run had no detailed state trace, so its cause is not
  established. Automated mouse/keyboard input under KDE/Wayland did not
  reliably activate the dialog; the actual Save-button flow remains a manual
  validation gap. Unit tests cover prompt handoff, targeting, validation,
  unsupported devices and localization.
- A regression test covers an SDK write whose readback cannot confirm the new
  name. The app reports uncertainty and refreshes peer state instead of
  claiming success. The error text is localized in German and English.
- Neither disconnect/unpair nor a firmware update was performed for this
  release. A separate dongle may be used later for firmware-update work.

## Scope

The release changes the sidebar and real device-name action only. The broader
settings comparison is in `docs/jabra-direct-screenshot-gap-2026-09-29.md`.
