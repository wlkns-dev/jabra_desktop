#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$project_root"
scripts/dotnet.sh publish src/JabraDesktop.App -c Release -r linux-x64 --self-contained true -o artifacts/linux-x64 -m:1
chmod +x artifacts/linux-x64/JabraDesktop.App
chmod +x artifacts/linux-x64/Jabra.Utilities/DeviceConnector/linux/jabra-device-connector
cp README.md artifacts/linux-x64/README.md
mkdir -p artifacts/linux-x64/licenses
cp .tools/nuget/jabra.net.sdk/4.9.1.1/LICENSE.md artifacts/linux-x64/licenses/Jabra.NET.Sdk-LICENSE.md
cp .tools/nuget/jabra.net.sdk.devicepairing/3.1.1.2/LICENSE.md artifacts/linux-x64/licenses/Jabra.NET.Sdk.DevicePairing-LICENSE.md
cp .tools/nuget/jabra.net.sdk.properties/2.5.1.1/LICENSE.md artifacts/linux-x64/licenses/Jabra.NET.Sdk.Properties-LICENSE.md
cp THIRD-PARTY-NOTICES.md artifacts/linux-x64/licenses/
