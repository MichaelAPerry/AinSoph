#!/usr/bin/env bash
# Build Steam-ready Windows and Linux folders in build/steam/<platform>/.
#
#   GODOT=/path/to/Godot_v4.4.1-stable_mono_linux.x86_64 tools/build-steam.sh
#
# The model ships as a loose file (models/qwen2.5-1.5b.gguf next to the
# executable) instead of inside the .pck, so players get one copy on disk and
# no extraction on first launch. Upload each platform folder as its own depot.
# Needs the Godot 4.4.1 .NET export templates and models/qwen2.5-1.5b.gguf.
set -euo pipefail

GODOT="${GODOT:-godot}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
MODEL="$ROOT/models/qwen2.5-1.5b.gguf"
[[ -f "$MODEL" ]] || { echo "Missing $MODEL — see README, Model"; exit 1; }

cd "$ROOT"
dotnet build -v q
for preset in "Windows Desktop (Steam)|windows|AinSoph.exe" "Linux/X11 (Steam)|linux|AinSoph.x86_64"; do
  IFS='|' read -r name dir exe <<<"$preset"
  out="$ROOT/build/steam/$dir"
  rm -rf "$out" && mkdir -p "$out/models"
  "$GODOT" --headless --path "$ROOT" --export-release "$name" "$out/$exe"
  cp "$MODEL" "$out/models/"
  echo "$name → $out ($(du -sh "$out" | cut -f1))"
done
