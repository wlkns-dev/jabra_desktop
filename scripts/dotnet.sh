#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
export DOTNET_ROOT="$project_root/.tools/dotnet"
export DOTNET_CLI_HOME="$project_root/.tools/cli"
export NUGET_PACKAGES="$project_root/.tools/nuget"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
exec "$DOTNET_ROOT/dotnet" "$@"
