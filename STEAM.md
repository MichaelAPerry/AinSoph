# STEAM.md
## Ain Soph — the road to Steam

Where the game stands for a Steam release, and what is left. Checked items have been done and verified.

---

### BUILD — ready

- [x] Model is **Qwen 2.5 1.5B Instruct** (Apache 2.0) — allowed in a commercial product. The 3B model used earlier is under the Qwen Research License and could not ship.
- [x] Windows and Linux export presets for Steam: **Windows Desktop (Steam)** and **Linux/X11 (Steam)**. `tools/build-steam.sh` builds both into `build/steam/<platform>/` (~1.3 GB each). Upload each folder as its own depot.
- [x] The model ships as a loose file in `models/` beside the executable — one copy on disk, no first-launch extraction.
- [x] llama.cpp native libraries ship for no-AVX, AVX2 and AVX-512 CPUs, so older machines are covered.
- [x] If the model is missing or damaged the game still starts, with scripted voices (demo mode), rather than a dead boot screen.
- [x] App icon (`assets/icon.ico`), version 0.1.0 and publisher info on the Windows exe; window icon in `project.godot`; version shown on the boot screen and in the Esc menu.
- [x] Windows installer: `tools/package-windows.sh` → `build/AinSoph-Setup-<version>.exe` — 64-bit, per-user, shortcuts, uninstaller that keeps saves. Installed under Wine, the installed Windows build ran the full demo tour with real model inference.
- [x] Saves, settings and logs live in their own folder: `%APPDATA%\AinSoph` (Windows), `~/.local/share/AinSoph` (Linux) — ready for Steam Cloud.
- [x] NPC save files use Windows-safe names (ids contain `:`, which on NTFS silently wrote to an alternate data stream instead of a file). Old saves are renamed on load.
- [x] **Self-test** (`--selftest`): 28 checks across the whole game loop, exit code 0/1.
- [x] **CI** (`.github/workflows/ci.yml`): build + self-test on every push and pull request.
- [x] **Release** (`.github/workflows/release.yml`): on a `v*` tag (or by hand), runs the self-test against the real model, builds the installer and the Linux build, and attaches them to a GitHub Release.
- [ ] **Test on a real Windows PC.** Wine is a strong sign, not proof — try Windows 10 and 11, ideally an 8 GB machine.
- [ ] **Code signing.** An unsigned installer triggers Windows SmartScreen ("Windows protected your PC"). Not needed for Steam (Steam installs the folder); worth a certificate if the installer is distributed directly.

---

### STEAMWORKS — needs Michael's account

- [ ] Steam Direct fee ($100 per game) and the Steamworks partner account / tax / bank details.
- [ ] Create the app; note the App ID and the two depot IDs (Windows, Linux).
- [ ] Upload builds with SteamPipe (`steamcmd` + an `app_build` VDF); launch options: `AinSoph.exe` (Windows), `AinSoph.x86_64` (Linux).
- [ ] **Content Survey — AI disclosure.** Declare **Live-Generated** AI content (NPC speech, Council parables, NPC memories) from a model that runs locally. Guardrails to describe, all now in place:
  - every NPC and Council prompt carries an explicit rule against sexual content, slurs/hate and self-harm encouragement, and keeps the model in character;
  - every line the model produces passes a word-list filter (`data/blocklist.txt`) before a player sees it or an NPC remembers it — a blocked line is withheld whole;
  - output is never shared with other players or sent anywhere; the game makes no network calls.
  Before submitting, extend `data/blocklist.txt` with a maintained list (it has a starter set and a note on where to get one).
- [ ] Age rating questionnaire (IARC) — the game has killing (Reap), death and predators.
- [ ] Store page: description, tags, system requirements (Windows 10+ / modern 64-bit Linux, 8 GB RAM, x86-64 CPU, no GPU needed, ~1.5 GB disk). Say plainly that **survival runs on real time and the world does not pause**.
- [ ] Store art: header capsule 920×430, small capsule 462×174, main capsule 1232×706, vertical capsule 748×896, library capsule 600×900, library hero 3840×1240, library logo; at least 5 screenshots (`docs/demo/` has 13 at 1280×720); a trailer (`docs/demo/tour.mp4` is a rough cut).
- [ ] "Coming Soon" page live at least two weeks before release; build and store page review by Valve before launch.
- [ ] Steam Cloud (optional): Auto-Cloud `%APPDATA%\AinSoph\saves` / `~/.local/share/AinSoph/saves`.

---

### THE GAME

- [x] **A new world is not empty.** Four founding travellers have crossed into every new world before the player. They are foreigners (migrants, as the design allows): they live, talk, eat and survive, but cannot create or pray. The player's own tribe still comes only from the rib.
- [x] **Animals** — 30 species from ITEMS.md; wander, eat manna, predators hunt, prey flee, Reap numbers from RULES.md, two replace each that dies, saved.
- [x] **NPC creations** — NPCs who decide to create take it to the Council; approved items appear beside them, skills enter their memory, rules become laws. Spaced out so they don't monopolise the model.
- [x] **Laws** — Council-approved rules are saved with the world and given to every NPC as binding law in its prompt. (A free-text rule can't become engine code; the people of the world live by it.)
- [x] **Routes** — export and import verified end to end by the self-test; exported NPCs leave the map; the spouse never leaves.
- [x] **Sound** — original music, wind ambience and effects (steps, UI, speech, eating, reaping, the Council's bell, the rib, births, warnings, death), generated by `tools/make-sounds.py`.
- [x] **Esc menu** — fullscreen, music / ambience / effects volume, hints on/off, controls, quit. Says that the world does not pause.
- [x] **First-time guidance** — six hints (real-time survival, walking, talking, eating, safe sleep, the altar), each advancing when the player does it.
- [x] **Survival on screen** — "Ate 3h ago · Slept 5h ago" above the clock, gold when it's urgent.
- [x] **Survival persists.** Hunger and sleep are saved; time away counts. Logging out is sleeping where you stand — a player who stays away more than a day without eating comes back to find their character starved (RULES.md). Hint #1, the menu and the store page all say so.
- [ ] Controller / Steam Deck (optional). The D-pad already walks (Godot's default ui_* actions); clicking beings needs the mouse or the Deck's trackpad, and dialogue needs the Steam on-screen keyboard.
- [ ] Play through a full real week with the 1.5B model — rib, spouse, a child — on an 8 GB machine.
