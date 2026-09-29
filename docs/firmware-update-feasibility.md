# Firmware updates on Linux — feasibility check (2026-09-28)

## Finding

Jabra's current .NET SDK modules cover device detection, properties, and
pairing, but Jabra explicitly identifies **JabraCLI as the current firmware
update route** for third-party software. Its Linux AppImage is beta software;
Jabra reports testing it on Ubuntu 26.04 x64. Our CachyOS installation and
the exact Link 370, Link 380, Evolve 75 SE, and Speak 710 variants have not
yet been fully validated with JabraCLI. Jabra lists the Evolve and Speak families
generally, and limits JabraCLI to USB-connected devices, including headsets
connected through a dongle. Model-level support and available firmware must
be queried from the tool rather than inferred from this family list.

## Documented manual workflow

1. Download the Linux AppImage from Jabra's installation page and run it
   separately from our application.
2. Run `jabracli device list --output-format json` to obtain each attached
   device's product ID (PID). Check the exact model/variant, especially if
   multiple devices share a PID.
3. Run `jabracli firmware list --pid "<pid>"` to see available versions.
4. Run `jabracli firmware download --pid "<pid>" --version "<version>"` to
   obtain an official firmware file.
5. Run `jabracli firmware update --pid "<pid>" --firmware-file "./firmware.zip" --check-only`
   to validate the operation without writing firmware.
6. Only after the user deliberately chooses to proceed, run the same `update`
   command without `--check-only`. Keep the device connected and let the
   updater complete before our app resumes device operations.

The `list` and `download` commands need access to Jabra's firmware catalog;
the update itself uses the previously downloaded local file. JabraCLI reports
anonymized usage telemetry.

## Read-only check on this machine

The official JabraCLI 1.6.48.0 AppImage was downloaded to `/tmp`. The direct
AppImage launch failed because this CachyOS system lacks `libfuse.so.2`;
extracting the AppImage and running its `AppRun` worked without installing it.
The download size and MD5 matched the values served by Jabra.

With our app stopped, `device list --output-format json` found a Link 370
(PID 9310 / `0x245E`, firmware 1.87.0) and an Evolve 75 connected through it
(PID 9318 / `0x2466`, firmware 2.38.0). `firmware list` returned versions
1.87.0, 1.82.0, and 1.50.0 for the Link 370, and 2.38.0, 2.32.0, and 2.10.0
for the Evolve 75. Both installed versions were the newest returned for these
product IDs. No firmware file was downloaded or written. Link 380 and Speak 710
were not attached during this check and remain unverified with JabraCLI.

The same `device list` call stalled for over two minutes while our app and its
Jabra connector were running, but completed quickly when the app was stopped.
This demonstrates that a future integration must coordinate exclusive device
access instead of launching the CLI concurrently with our SDK session.

## Possible integration

An optional future adapter could discover an existing JabraCLI installation,
show available firmware for the selected USB-connected device, download the
official file, run `--check-only`, and request explicit confirmation before
flashing. Device operations in our app would need to be suspended during the
update, with progress, error, and reconnect handling. The CLI command examples
target a PID; safe selection when two identical models are connected must be
tested before exposing an update button. Redistribution terms for bundling the
CLI must be checked separately; requiring the user to install JabraCLI avoids
assuming that permission.

Sources (Jabra primary documentation):

- [Using JabraCLI](https://developer.jabra.com/sdks-and-tools/jabracli/use-cases)
- [JabraCLI reference](https://developer.jabra.com/sdks-and-tools/jabracli/reference)
- [Installation and Linux support](https://developer.jabra.com/sdks-and-tools/jabracli/installation)
- [Supported device families](https://developer.jabra.com/sdks-and-tools/jabracli)
- [.NET SDK modules](https://developer.jabra.com/sdks-and-tools/dotnet)
- [Jabra License Agreement](https://developer.jabra.com/legal/license-agreement)
