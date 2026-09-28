#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
"${project_root}/scripts/publish.sh"

version="$(sed -n 's/.*<Version>\([^<]*\)<\/Version>.*/\1/p' "${project_root}/Directory.Build.props")"
if [[ -z "${version}" ]]; then
    echo 'Could not read application version from Directory.Build.props.' >&2
    exit 1
fi

work_dir="$(mktemp -d)"
trap 'rm -rf "${work_dir}"' EXIT
package_root="${work_dir}/package"
mkdir -p "${package_root}/DEBIAN" "${package_root}/opt/jabra-desktop"
mkdir -p "${package_root}/usr/bin" "${package_root}/usr/share/applications"
mkdir -p "${package_root}/usr/share/icons/hicolor/scalable/apps"
mkdir -p "${package_root}/usr/share/licenses/jabra-desktop"
mkdir -p "${package_root}/usr/share/doc/jabra-desktop"
mkdir -p "${package_root}/usr/lib/udev/rules.d"

tar -C "${project_root}/artifacts/linux-x64" \
    --exclude='./Jabra.Utilities/DeviceConnector/win32' \
    --exclude='./Jabra.Utilities/DeviceConnector/darwin' \
    -cf - . | tar -C "${package_root}/opt/jabra-desktop" -xf -
install -Dm0755 /dev/stdin "${package_root}/usr/bin/jabra-desktop" <<'LAUNCHER'
#!/bin/sh
exec /opt/jabra-desktop/JabraDesktop.App "$@"
LAUNCHER
install -Dm0644 "${project_root}/packaging/jabra-desktop.desktop" \
    "${package_root}/usr/share/applications/jabra-desktop.desktop"
install -Dm0644 "${project_root}/packaging/jabra-desktop.svg" \
    "${package_root}/usr/share/icons/hicolor/scalable/apps/jabra-desktop.svg"
install -Dm0644 "${project_root}/packaging/70-jabra-desktop.rules" \
    "${package_root}/usr/lib/udev/rules.d/70-jabra-desktop.rules"
cp -a "${project_root}/artifacts/linux-x64/licenses/." \
    "${package_root}/usr/share/licenses/jabra-desktop/"
install -Dm0644 "${project_root}/src/JabraDesktop.App/Resources/Terms.de.md" \
    "${package_root}/usr/share/licenses/jabra-desktop/Terms.de.md"
install -Dm0644 "${project_root}/src/JabraDesktop.App/Resources/Terms.en.md" \
    "${package_root}/usr/share/licenses/jabra-desktop/Terms.en.md"
install -Dm0644 "${project_root}/README.md" \
    "${package_root}/usr/share/doc/jabra-desktop/README.md"

cat > "${package_root}/DEBIAN/control" <<CONTROL
Package: jabra-desktop
Version: ${version}-1
Section: sound
Priority: optional
Architecture: amd64
Maintainer: Jabra Desktop contributors <wlkns-dev@users.noreply.github.com>
Homepage: https://github.com/wlkns-dev/jabra_desktop
Depends: libc6, libgcc-s1, libstdc++6, libudev1, libfontconfig1, libx11-6, libice6, libsm6, libssl3 | libssl3t64, libicu70 | libicu74 | libicu76 | libicu78
Description: Linux desktop app for Jabra dongles and headsets
 Manage Jabra Bluetooth dongles and headsets with device search, pairing,
 connection controls, a system tray menu, and English/German interface text.
CONTROL

cat > "${package_root}/DEBIAN/postinst" <<'POSTINST'
#!/bin/sh
set -e
if command -v udevadm >/dev/null 2>&1; then
    udevadm control --reload-rules
fi
exit 0
POSTINST
cat > "${package_root}/DEBIAN/postrm" <<'POSTRM'
#!/bin/sh
set -e
if command -v udevadm >/dev/null 2>&1; then
    udevadm control --reload-rules
fi
exit 0
POSTRM
chmod 0755 "${package_root}/DEBIAN/postinst" "${package_root}/DEBIAN/postrm"

output="${project_root}/packaging/jabra-desktop_${version}_amd64.deb"
if command -v dpkg-deb >/dev/null 2>&1; then
    dpkg-deb --build --root-owner-group "${package_root}" "${output}"
else
    echo 'dpkg-deb not found; creating the standard ar-based Debian package archive.' >&2
    tar --sort=name --mtime='@0' --owner=0 --group=0 --numeric-owner \
        --format=gnu -C "${package_root}/DEBIAN" -cJf "${work_dir}/control.tar.xz" .
    tar --sort=name --mtime='@0' --owner=0 --group=0 --numeric-owner \
        --format=gnu --exclude='./DEBIAN' -C "${package_root}" \
        -cJf "${work_dir}/data.tar.xz" .
    printf '2.0\n' > "${work_dir}/debian-binary"
    rm -f "${output}"
    (cd "${work_dir}" && ar cr "${output}" debian-binary control.tar.xz data.tar.xz)
fi

echo "Built ${output}"
