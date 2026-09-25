#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
"${project_root}/scripts/publish.sh"
cd "${project_root}/packaging"
makepkg -f
