#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$project_root"
if [[ -x artifacts/linux-x64/JabraDesktop.App ]]; then
  exec artifacts/linux-x64/JabraDesktop.App "$@"
fi
exec scripts/dotnet.sh run --project src/JabraDesktop.App -- "$@"
