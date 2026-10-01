#!/usr/bin/env bash
# Record the scripted demo tour to docs/demo/ (video, GIF and screenshots).
#
#   GODOT=/path/to/Godot_v4.4.1-stable_mono_linux.x86_64 tools/record-demo.sh
#
# Needs: Godot 4.4 .NET, the .NET 8 SDK, ffmpeg. On a headless machine it runs
# under xvfb-run automatically. Pass --model=/path/to/model.gguf to record the
# tour with the real LLM instead of scripted demo voices.
set -euo pipefail

GODOT="${GODOT:-godot}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/docs/demo"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

cd "$ROOT"
dotnet build -v q

RUN=("$GODOT" --path "$ROOT" --rendering-driver opengl3
     --write-movie "$TMP/tour.avi" --fixed-fps 30
     -- --demo-tour --shots="$TMP/shots" "$@")
if [[ -z "${DISPLAY:-}" ]] && command -v xvfb-run >/dev/null; then
  xvfb-run -a -s "-screen 0 1280x720x24" "${RUN[@]}"
else
  "${RUN[@]}"
fi

mkdir -p "$OUT"
ffmpeg -v error -y -i "$TMP/tour.avi" -c:v libx264 -preset slow -crf 26 \
  -pix_fmt yuv420p -movflags +faststart -an "$OUT/tour.mp4"
ffmpeg -v error -y -i "$TMP/tour.avi" \
  -vf "fps=12,scale=800:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=96[p];[b][p]paletteuse=dither=bayer" \
  "$OUT/tour.gif"
cp "$TMP"/shots/*.png "$OUT/"
echo "Demo written to $OUT"
