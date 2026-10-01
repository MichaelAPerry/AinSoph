#!/usr/bin/env bash
# Record and cut the trailer to docs/demo/trailer.mp4.
#
#   GODOT=/path/to/Godot_v4.4.1-stable_mono_linux.x86_64 tools/record-trailer.sh
#
# 1. Stages the raw footage in-engine with `--trailer` and the real model
#    (models/qwen2.5-1.5b.gguf), at 1920x1080.
# 2. Speaks the narration with Piper TTS (voice: en_GB "cori", public domain).
# 3. Synthesises the score and cuts picture and sound (tools/trailer/).
#
# Needs: Godot 4.4 .NET, the .NET 8 SDK, ffmpeg, Python 3 with numpy, scipy,
# Pillow and piper-tts, and the Cinzel and Cormorant Garamond fonts (OFL,
# github.com/google/fonts) in $FONTS. On a headless machine it uses xvfb-run.
set -euo pipefail

GODOT="${GODOT:-godot}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
WORK="${WORK:-$(mktemp -d)}"
FONTS="${FONTS:?set FONTS to a folder with Cinzel[wght].ttf and CormorantGaramond[wght].ttf}"
VOICE="${VOICE:-$WORK/cori.onnx}"
mkdir -p "$WORK/vo"

cd "$ROOT"
dotnet build -v q

if [[ ! -f "$VOICE" ]]; then
  base=https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_GB/cori/high/en_GB-cori-high
  curl -sSfL -o "$VOICE" "$base.onnx"
  curl -sSfL -o "$VOICE.json" "$base.onnx.json"
fi
python3 tools/trailer/vo.py "$VOICE" "$WORK/vo"

RUN=("$GODOT" --path "$ROOT" --rendering-driver opengl3 --resolution 1920x1080
     --write-movie "$WORK/raw.avi" --fixed-fps 30
     -- --trailer --model="$ROOT/models/qwen2.5-1.5b.gguf")
if [[ -z "${DISPLAY:-}" ]] && command -v xvfb-run >/dev/null; then
  xvfb-run -a -s "-screen 0 1920x1080x24" "${RUN[@]}" | tee "$WORK/raw.log"
else
  "${RUN[@]}" | tee "$WORK/raw.log"
fi

mkdir -p docs/demo
python3 tools/trailer/assemble.py "$WORK/raw.avi" "$WORK/raw.log" "$WORK/vo" "$FONTS" docs/demo/trailer.mp4
echo "Trailer written to docs/demo/trailer.mp4 (work files in $WORK)"
