## Ain Soph 0.2.1 — the world answers when you touch it

A persistent world whose people (and whose gods) run on your own computer. Nothing phones home.

[![Ain Soph trailer](https://raw.githubusercontent.com/MichaelAPerry/AinSoph/main/docs/demo/trailer-poster.jpg)](https://github.com/MichaelAPerry/AinSoph/blob/main/docs/demo/trailer.mp4)

### Download

| | |
|---|---|
| **Windows** | `AinSoph-Setup-0.2.1.exe` installs for the current user (no administrator prompt), adds Start-menu and desktop shortcuts, and uninstalls from Add/Remove Programs. Windows may show a SmartScreen warning because the installer is not yet code-signed: choose **More info → Run anyway**. |
| **Linux** | `AinSoph-0.2.1-linux-x86_64.tar.gz`: extract it and run `linux/AinSoph.x86_64`. |

Needs a 64-bit x86 CPU, 8 GB of RAM and about 1.5 GB of disk. **No graphics card needed**: the AI runs on the CPU. The AI model (Qwen 2.5 1.5B Instruct, Apache 2.0) is included.

### Know before you play

- **The world does not pause.** Hunger and sleep run on real hours. Logging out is sleeping where you stand.
- NPCs think on your CPU, so a reply takes a few seconds on a modest machine.
- This is an alpha. Worlds from 0.1.0 load, and the new systems start empty in them.

### Fixed in 0.2.1 (from playtesting)

- **Actions did nothing.** Choosing an action from the click menu sent it to no target, and actions that go through the world's rules (eating food from the ground, among others) were filed under the wrong name. Both are fixed. Reap eats, Talk talks, Move picks up.
- **The action bar did nothing.** Its six slots now act on whatever is nearest, and so do keys 1–6. Move picks up, See describes, Hear listens, Talk speaks to the nearest person, Reap eats or hunts, Pray points you to the altar.
- **Out of reach** no longer means "too far": you walk to what you clicked, then act.
- **The world was still.** NPCs now think every couple of minutes instead of every hour, walk about between thoughts, and greet you when you come near. Prey shy away; predators catch your scent, warn you, stalk you and strike.
- **The altar could not be found.** It now lies two to four cells from where lives begin. The messenger's bearing stays on the status line, and you simply walk up to it: the Council opens by itself.
- **A map** (MAP or M) shows what this life has seen: the land, you, the altar, caves, your people, hunters.
- **Travellers** moved from the action bar into the Esc menu.
- The self-test now plays with real clicks and keys, as a player would: 72 checks.

### New in 0.2.0

**What the Council grants now changes the game.**
- Skills and items become *gifts*, each read as one of eight effects: Sight, Hearing, Seafaring, Strength, Endurance, Shelter, Mending or Kinship.
- Gifts show on the action bar and apply to NPCs too.
- The Council's answer opens with one plain line saying what entered the world.

**The gods' choice.**
- Leave the choice to the gods, or ask for the world itself to change, and the AI decides what enters it: a new creature it invents, a named enemy that hunts, land turned to forest or sea, food or relics, a long night, a famine or a time of plenty, a gift or a law.
- The engine checks and caps every act and saves it with the world.
- Every act ends with a proclamation that NPCs hear and react to.

**Laws bind you too.** Your deeds are judged against the world's laws. Break one and the Council will not hear you for a day, and every NPC knows.

**The first encounter.** Early in every life, the Council sends a messenger, its form and words chosen by the AI, to tell you which way the hidden altar lies.

**Carrying.**
- Pick things up with Move. Open the pack (PACK or I) to eat, give to the NPC beside you, or drop.
- A made thing such as a lantern or a bow works as a gift while you carry it.
- Food spoils in the pack, and what you carry falls where you die.

**Sleep matters.** Predators cannot reach a sleeper in a cave (or one sheltered by a gift). Anyone asleep in the open defends at half strength.

**Any window.** The layout scales to fullscreen and large windows.

**A trailer**, narrated, cut from the game itself.

Also: Council seats that answer out of shape get a second try, petitions are named plainly ("Fire Making", not "Grant me a skill: Fire"), and the docs now say what the game is: single-player, with routes between friends' worlds. The self-test has grown from 28 checks to 60.

Free and open source (MIT). Fork it: forking is not punished, it is designed for.
