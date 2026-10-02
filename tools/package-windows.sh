#!/usr/bin/env bash
# Package Ain Soph as a Windows installer: build/AinSoph-Setup-<version>.exe
#
#   GODOT=/path/to/Godot_v4.4.1-stable_mono_linux.x86_64 tools/package-windows.sh
#
# Builds the Steam Windows folder (tools/build-steam.sh), then wraps it with NSIS
# (makensis — `apt install nsis`, or NSIS on Windows). For the exe's own icon and
# version info, set Editor Settings → Export → Windows → rcedit (and wine on Linux).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="$(sed -n 's/^config\/version="\(.*\)"/\1/p' "$ROOT/project.godot")"
VERSION="${VERSION:-0.2.1}"

[[ "${SKIP_BUILD:-}" == 1 ]] || "$ROOT/tools/build-steam.sh"

cd "$ROOT/tools/installer"
makensis -V2 -DVERSION="$VERSION" AinSoph.nsi
echo "Installer: $ROOT/build/AinSoph-Setup-$VERSION.exe ($(du -h "$ROOT/build/AinSoph-Setup-$VERSION.exe" | cut -f1))"
