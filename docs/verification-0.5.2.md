# 0.5.2 verification — 2026-09-29

## Automated and package validation

- Full .NET suite: 154 passed, zero failed after review fixes. Regression tests
  cover device removal and in-flight pairing-list results that arrive after
  a newer physical property update.
- Arch and Debian packages built with version `0.5.2-1`; package metadata
  reports x86_64 and amd64 respectively. `dpkg-deb` was unavailable, so the
  Debian script used its standard `ar`/`tar` fallback.
- Both package copies of `JabraDesktop.App.dll` matched the published build.
  Final package checksums are in `packaging/SHA256SUMS-0.5.2`.
- A network-enabled `dotnet restore` completed without the NuGet audit
  connectivity warnings seen during sandboxed package builds.
- The Debian package was inspected on CachyOS; it was not run on Ubuntu.

## Hardware and UI validation

- JabraCLI 1.6.48.0 read only `audioName` on Link 370 and Link 380, and
  `skuId` plus `mobileDevice1` on Evolve 75. The phone value was empty.
- The 0.5.2 release build launched under KDE. After a clean restart, it
  displayed both dongles and the connected Speak 710 and Evolve 75. Selecting
  Evolve 75 beneath Link 370 displayed part number `7599-838-109` and “No
  phone connected”, together with its existing firmware and battery values.
- The final rebuilt release build was launched again after the snapshot fixes;
  both dongles were present and the Evolve 75 detail showed the same product
  values.
- The first app launch immediately after the CLI probes reported a closed
  Device Connector. A clean restart recovered normally. The cause was not
  established; there was no SDK write or firmware action during these tests.
- Link 370/380 audio names were independently read from hardware and the
  detail binding was tested, but the values were below the visible window
  area in the screenshot. An Ubuntu runtime test and other device models
  remain untested.
