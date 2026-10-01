#!/usr/bin/env bash
# Shared CI setup: Godot 4.4.1 .NET (and optionally its export templates).
#   tools/ci-setup.sh            → Godot only
#   tools/ci-setup.sh templates  → Godot + export templates
# Prints nothing useful; sets GODOT in $GITHUB_ENV when run on GitHub Actions.
set -euo pipefail

VER=4.4.1
BASE="https://github.com/godotengine/godot/releases/download/${VER}-stable"
DIR="$HOME/godot"
mkdir -p "$DIR"

if [[ ! -x "$DIR/Godot_v${VER}-stable_mono_linux_x86_64/Godot_v${VER}-stable_mono_linux.x86_64" ]]; then
  curl -sSL -o /tmp/godot.zip "$BASE/Godot_v${VER}-stable_mono_linux_x86_64.zip"
  unzip -q /tmp/godot.zip -d "$DIR" && rm /tmp/godot.zip
fi
GODOT="$DIR/Godot_v${VER}-stable_mono_linux_x86_64/Godot_v${VER}-stable_mono_linux.x86_64"

if [[ "${1:-}" == templates ]]; then
  TPL="$HOME/.local/share/godot/export_templates/${VER}.stable.mono"
  if [[ ! -f "$TPL/version.txt" ]]; then
    curl -sSL -o /tmp/tpl.tpz "$BASE/Godot_v${VER}-stable_mono_export_templates.tpz"
    mkdir -p "$TPL" && unzip -q /tmp/tpl.tpz -d /tmp/tpl && mv /tmp/tpl/templates/* "$TPL/" && rm -rf /tmp/tpl /tmp/tpl.tpz
  fi
fi

[[ -n "${GITHUB_ENV:-}" ]] && echo "GODOT=$GODOT" >> "$GITHUB_ENV"
echo "$GODOT"
