#!/usr/bin/env bash
# Upload the Windows and Linux builds to itch.io with butler.
#
#   BUTLER_API_KEY=... ITCH_GAME=youruser/ain-soph tools/upload-itch.sh
#
# Run tools/build-steam.sh first — this pushes build/steam/windows and
# build/steam/linux as the itch channels "windows" and "linux". butler only
# uploads what changed, so later versions are quick. Get an API key at
# https://itch.io/user/settings/api-keys (or run `butler login` once locally).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
: "${ITCH_GAME:?set ITCH_GAME to <itch user>/<game slug>, e.g. michaelaperry/ain-soph}"
VERSION="$(sed -n 's/^config\/version="\(.*\)"/\1/p' "$ROOT/project.godot")"
BUTLER="${BUTLER:-butler}"

if ! command -v "$BUTLER" >/dev/null; then
  curl -sSL -o /tmp/butler.zip https://broth.itch.zone/butler/linux-amd64/LATEST/archive/default
  mkdir -p /tmp/butler && unzip -qo /tmp/butler.zip -d /tmp/butler && chmod +x /tmp/butler/butler
  BUTLER=/tmp/butler/butler
fi

push() { # <folder> <channel> <executable>
  local dir="$ROOT/build/steam/$1"
  [[ -x "$dir/$3" || -f "$dir/$3" ]] || { echo "Missing $dir/$3 — run tools/build-steam.sh"; exit 1; }
  # Tell the itch app what to launch
  printf '[[actions]]\nname = "play"\npath = "%s"\n' "$3" > "$dir/.itch.toml"
  "$BUTLER" push "$dir" "$ITCH_GAME:$2" --userversion "$VERSION"
}

push windows windows AinSoph.exe
push linux   linux   AinSoph.x86_64
echo "Uploaded $VERSION to https://$(cut -d/ -f1 <<<"$ITCH_GAME").itch.io/$(cut -d/ -f2 <<<"$ITCH_GAME")"
