# 0.5.0 verification — 2026-09-28

## Automated and build validation

- Full .NET suite: 134 passed, zero failed.
- Linux self-contained publish succeeded; existing CA1416 portability warnings and NU1900 warnings because the NuGet vulnerability feed was unreachable (dependency restore and build succeeded).
- Arch `makepkg -f` and Debian `scripts/package-deb.sh` produce version 0.5.0-1.
- Package metadata, executable permissions and packaged application assemblies
  are checked against the published assemblies.
- Ubuntu runtime execution was not tested on this CachyOS machine.

## Hardware validation

Both dongles were attached simultaneously. Link 370 reported firmware 1.87.0;
its connected Speak 710 reported firmware 1.40.0 and 53% battery. Link 380
reported firmware 1.16.0; its Evolve 75 SE reported firmware 1.1.0 and 94%
battery. Battery values are observations, not fixed expectations.

Each dongle returned four paired devices, including disconnected devices.
Each connected endpoint was disconnected, read back as Disconnected, reconnected,
and read back as Connected. Both original connections were restored. The GUI
was launched under KDE/XWayland; the endpoint layout and separate dongle groups
were inspected by screenshot.

## Independent review

A fresh read-only reviewer inspected bf66c16..e64ae50. Its first attempt was
interrupted by a usage limit; the same reviewer resumed and returned its verdict.
Four findings were corrected in one pass:

1. Saved-list selection now resolves to the canonical hierarchy row, preserving
   endpoint selection across periodic updates.
2. Explicit action failures remain scoped to the dongle; successful retries no
   longer expose a stale global error banner.
3. Top-level rows show available telemetry and notify bindings when values change.
4. Known per-dongle error details are translated together with their heading.

The fourth finding was upgraded from Minor because complete German/English
operation is an explicit user requirement. All four fixes have tests observed
failing before the correction and passing afterwards. No minor findings remain
deferred.

## Validation decision

Real unpair and physical USB removal were not executed during unattended tests,
to avoid deleting saved pairings or interrupting the user's hardware setup.
Automated tests cover inventory removal, late results, action targeting and
confirmation flow. Cost of this decision: actual unplug/unpair remains a manual
validation gap. The reviewer also explicitly declined independent hardware
unplug/unpair verification; this gap is retained here rather than claimed passed.

## Delivery

Artifacts are local to the feature worktree. No remote release or merge was
performed as part of this implementation. Package hashes are stored next to the
packages in `packaging/SHA256SUMS-0.5.0`.
