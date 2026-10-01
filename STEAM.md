# STEAM.md
## Ain Soph — the road to Steam

Where the game stands for a Steam release, and what is left. Checked items have been done and verified.

---

### BUILD — ready

- [x] Model is **Qwen 2.5 1.5B Instruct** (Apache 2.0) — allowed in a commercial product. The 3B model used earlier is under the Qwen Research License and could not ship.
- [x] Windows and Linux export presets for Steam: **Windows Desktop (Steam)** and **Linux/X11 (Steam)**. `tools/build-steam.sh` builds both into `build/steam/<platform>/`.
- [x] The model ships as a loose file in `models/` beside the executable — one copy on disk, no first-launch extraction.
- [x] The Linux Steam build was run from its folder: it found the model, loaded llama.cpp, and booted into the world.
- [x] llama.cpp native libraries ship for no-AVX, AVX2 and AVX-512 CPUs, so older machines are covered.
- [x] If the model is missing or damaged the game still starts, with scripted voices (demo mode), rather than a dead boot screen.
- [ ] **Test the Windows build on a real Windows PC.** It exports cleanly but has not been run.
- [ ] **App icon and version info.** `application/icon` is empty in the export presets. Set an icon, `file_version` / `product_version`, company name and copyright. The Windows exe metadata needs `rcedit` configured in Editor Settings (Export → Windows) — export on Windows, or point Godot at `rcedit.exe` under Wine.
- [ ] **Version number.** Add `config/version` to `project.godot` and show it somewhere (boot screen) so bug reports name a build.

Each Steam build folder is about 1.3 GB. Upload `build/steam/windows/` and `build/steam/linux/` as separate depots.

---

### STEAMWORKS — needs Michael's account

- [ ] Steam Direct fee ($100 per game) and the Steamworks partner account / tax / bank details.
- [ ] Create the app; note the App ID and the two depot IDs (Windows, Linux).
- [ ] Upload builds with SteamPipe (`steamcmd` + an `app_build` VDF); launch options: `AinSoph.exe` (Windows), `AinSoph.x86_64` (Linux).
- [ ] **Content Survey — AI disclosure.** Steam requires games that use AI to say so. Ain Soph generates content *live* (NPC speech, Council parables) from a local model, so it must be declared as **Live-Generated** AI content, with a description of the guardrails. Honest guardrails today: NPC and Council system prompts keep the model in character and in the game's register; output is never shared with other players; it runs locally and is not connected to the internet. Worth adding a simple output filter before launch — players can type anything to an NPC.
- [ ] Age rating questionnaire (IARC) — the game has killing (Reap) and death.
- [ ] Store page: description, tags, system requirements (Windows 10+ / modern 64-bit Linux, 8 GB RAM, x86-64 CPU, no GPU needed, ~1.5 GB disk).
- [ ] Store art: header capsule 920×430, small capsule 462×174, main capsule 1232×706, vertical capsule 748×896, library capsule 600×900, library hero 3840×1240, library logo; at least 5 screenshots (1280×720 or larger — `docs/demo/` has a start); a trailer (`docs/demo/tour.mp4` is a rough cut).
- [ ] "Coming Soon" page live at least two weeks before release; build and store page review by Valve before launch.
- [ ] Steam Cloud (optional): Auto-Cloud the save folder. Consider setting `application/config/use_custom_user_dir` so saves live at a clean path (`%APPDATA%\AinSoph`) rather than under `Godot\app_userdata`.

---

### THE GAME — what Steam players will expect

These are not technical blockers, but reviews will mention them.

- [ ] **A new world is empty for the first week.** By design NPCs come only from the rib (after 168 hours of play) and its children. On Steam, a first session with no one in the world will read as broken. Options: seed founding NPCs at world creation, shorten the first rib, or open in a populated starter area. **Design decision needed.**
- [ ] **Animals** are designed and generated per cell but never spawned.
- [ ] **NPC creations** — NPCs decide to create skills, items and rules but don't yet take them to the Council.
- [ ] **No audio** at all — no music, no ambience, no UI sounds.
- [ ] **No pause / settings menu** — window mode, resolution, volume, quit to desktop. Esc currently only closes dialogue.
- [ ] **First-time guidance.** Nothing tells a new player how to move, click beings, or eat. The demo tour's captions are a starting point.
- [ ] **Survival over real time.** Hunger and sleep run on real hours and the world does not pause. Make this unmistakable on the store page and in game, or players who log off for two days will be surprised.
- [ ] Controller / Steam Deck support (optional; Deck needs gamepad input and text entry).
- [ ] Play through a full real week with the 1.5B model — rib, spouse, a child — on an 8 GB machine.

---

### ALREADY WORKING

World generation (stable per seed), fog of war, movement, caves, manna, eating, hunger/sleep death and respawn, NPCs that think and talk in character, kill resolution, praying at the altar and the three-seat Council, Council-granted skills and items, the rib → spouse → weekly progeny chain with lineage, routes between worlds, saves that survive restarts, demo mode and the recorded demo tour.
