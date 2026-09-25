#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
if [[ $EUID -ne 0 ]]; then
  echo 'Bitte mit sudo oder pkexec ausführen.' >&2
  exit 1
fi
install -m 0644 "$project_root/packaging/70-jabra-desktop.rules" /etc/udev/rules.d/70-jabra-desktop.rules
udevadm control --reload-rules
# Trigger only Jabra HID nodes; idVendor lives on a USB ancestor, not the HID node.
for node in /sys/class/usbmisc/hiddev* /sys/class/hidraw/hidraw*; do
  [[ -e "$node" ]] || continue
  parent="$(readlink -f "$node")"
  while [[ "$parent" != / && -n "$parent" ]]; do
    if [[ -r "$parent/idVendor" ]]; then
      if [[ "$(cat "$parent/idVendor")" == 0b0e ]]; then
        udevadm trigger --action=change "$node"
      fi
      break
    fi
    parent="$(dirname "$parent")"
  done
done
# Replugging the dongle applies the rule even where parent-attribute filtering differs.
echo 'Regel installiert. Jabra-Dongle bei Bedarf abziehen und wieder einstecken.'
