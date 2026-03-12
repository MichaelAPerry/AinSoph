# Ain Soph

**The boundless. The infinite before form.**

Ain Soph is a free, open source, persistent, shared world game. It runs on low-spec hardware. It has no prescribed win condition. It is endlessly customizable. It is easy to run. Easy to learn. Hard to master.

Nothing phones home. No subscription. No server you don't control.

---

## What It Is

A living world on a grid of sovereign cells. Every cell is its own territory. The grid expands without limit. Players and NPCs inhabit the same world simultaneously, under identical rules.

NPCs are not scripted. They are powered by a local LLM running on the player's own machine — no cloud, no API key, no latency. Each NPC has a personality drawn from 72 types, a memory of four slots that accumulates across their life, and the capacity to create content — skills, items, rules — that enters the world as equal world content.

Players and NPCs can create anything that passes the Triune Council: three LLM instances that evaluate submissions and respond in parable. The Council speaks. The world hears or it doesn't.

---

## What It Is Not

- Not pay to win
- Not pay to play
- Not platform-locked
- Not a crafting game
- Nothing phones home

---

## The World

The world is an infinite persistent grid of sovereign cells. Mental model: Game of Life. Each cell is a peer — equal containers connected to neighbors. There is no hierarchy between cells.

Each cell is an 8×8 tile grid. Each tile is 32×32 pixels. A single cell is enough space for a player to live an entire game.

All players exist on the same grid simultaneously. Only cells near a player need to be generated at any moment. There is no edge.

### Biomes

Eight biomes drawn from biblical geography. Everything starts here.

| Biome | Character |
|-------|-----------|
| Wilderness | Dry scrubland. The default. Most of the world. |
| Desert | Sand and rock. Harsh. Little grows. |
| River | Fresh water. Fertile banks. |
| Sea | Salt water. Impassable on foot. |
| Forest | Dense trees. Cedar and olive. |
| Grove | Open trees. Fig and palm. Lighter than forest. |
| Mountain | High rock. Caves concentrate here. |
| Valley | Low fertile land between mountains. |

### Manna

The only food at world start. Spawns each morning by biome density. Animals and players compete for the same supply. Lasts one day.

### Caves

A cave can hold one occupant. Inside a cave is the only safe place to sleep. Entering claims it; leaving frees it immediately.

### The Altar

One altar per world. Placed at generation in a random cell. Not marked on any map. A player must find it physically to reach the Triune Council.

---

## The Six Primitives

Every player character and NPC is born with these. They cannot be created. They can be broken or lost.

| Skill | What it does |
|-------|-------------|
| **Move** | Locomotion |
| **See** | Visual perception — 3 cells by day, 1 by night |
| **Hear** | Audio perception |
| **Talk** | Communication with NPCs and other players |
| **Reap** | Covers both killing and eating. Reap on a living target initiates kill resolution (d100). Reap on an edible item satisfies the day's food requirement. Same act. The world makes no distinction. |
| **Pray** | Reaches the Triune Council. At first does nothing visible. Discovered, not explained. |

Eating and sleeping are not skills. They are world-enforced survival requirements. Failure to eat within 24 real hours is death. Failure to sleep 8 continuous real hours within 24 is death. Warnings fire at hour 23. Bodies stay in the world.

### Interaction

Right-click any entity or tile to see all six primitives as options. Left-click to move. Any primitive can be applied to any target. The engine resolves what happens. Nonsensical combinations produce oblique responses — the world notices, but nothing useful occurs.

### Skills Beyond the Primitives

Everything beyond the six primitives is created by players and NPCs through the Council. Skills follow a taxonomy:

| Type | Definition | Example |
|------|-----------|---------|
| Primitive | Born with it | Move, See, Hear, Talk, Reap, Pray |
| Composite | Built from two or more existing skills | Weasel Hunting (Move + Hear) |
| Substitute | Replaces a broken or absent primitive | Cart (substitutes Move) |
| Extension | Amplifies an existing primitive | Telescope (extends See) |

---

## NPCs

NPCs come from players. The server does not generate them independently. A player earns their first NPC — the spouse — after 168 accumulated real hours in-world (one real week). From the spouse, progeny are born: 1 or 2 per real week. Progeny wander, intermarry, and carry lineage across the grid.

### The 72 Decans

Every NPC is assigned one of 72 personality types at creation, drawn from `data/ain_soph_72.json`. The decan governs drives, avoidances, conversational style, stress responses, economic behavior, political tendency, trust dynamics, and betrayal response. It does not change over the NPC's life.

### Memory

Each NPC has four memory slots.

| Slot | Holds |
|------|-------|
| Will | Instinct, survival, what the NPC wants at the body level |
| Thought | What the NPC knows, believes, has concluded |
| Feeling | Emotional state, relationships, what the NPC cares about |
| Action | What the NPC has done — their history of deeds |

Slots begin empty. The NPC decides what is worth writing into their own slots. Memory travels with the NPC intact when they migrate to another world.

### NPC Creation

NPCs are not passive. They can create skills, items, and rules autonomously — driven by their decan and memory. All NPC creation passes through the Triune Council under the same rules as player creation. Approved content enters the world as equal world content.

### Foreigners

An NPC that migrates from another world via a route is a foreigner. Permanently. All prior skills suspend on arrival — they cannot be recovered. Foreigners can Move, See, Hear, Talk, and Reap edible items. They cannot kill. They cannot pray to the Council. They still require food and sleep. The engine enforces this in three layers: sandboxed LLM prompt, engine override, decision-application check.

Foreigner status is permanent. There is no path to full standing. It is the condition of having crossed.

### Birth Impairment

Primitives can be absent or impaired at birth, modeled at real-world natural occurrence rates. No disease names are attached — only the mechanical reality.

| Primitive | Birth impairment rate |
|-----------|-----------------------|
| Move | ~2–3 per 1,000 |
| See | ~0.3–0.5 per 1,000 |
| Hear | ~1 per 1,000 |
| Talk | ~0.1 per 1,000 |

Impairment is a starting condition. What the character and their community build around it is the game.

---

## Kill Resolution

Reap on a living target (player, NPC, or animal) initiates resolution.

Both attacker and defender roll d100. The attacker must roll equal to or under their Reap number to kill. The defender must roll equal to or under their Reap number to resist or flee. Ties go to the defender.

| Entity | Base Reap number |
|--------|-----------------|
| Player character | 50 |
| NPC | 50 ± decan modifier |
| Predator animal (lion, wolf, bear, eagle) | 80 |
| Neutral animal (horse, donkey, ox) | 20 |
| Prey animal (sheep, deer, rabbit, dove) | 10 |
| Insect (locust) | 5 |

When an animal dies, two spawn adjacent immediately. Animal populations self-replenish by design.

---

## Survival

| Requirement | Rule |
|-------------|------|
| Eat | Once per 24 real hours. Satisfied by Reap on any edible item. Warning at hour 23. Death at hour 24. |
| Sleep | 8 continuous real hours per 24-hour window. Warning at hour 23. Death at hour 24. |
| Safe sleep | Inside a claimed cave only. One occupant per cave. |
| Exposed sleep | Outside a cave. Vulnerable to Reap rolls from any entity. |
| Logout | Character persists in the world sleeping. If not in a cave, they are exposed. The world does not pause. |

The SLEEP button appears in the HUD after 8 real hours of being awake. Clicking it begins sleep. Logging out while awake does the same automatically. The game auto-wakes the character after 8 continuous hours.

---

## The Triune Council

The Council evaluates all created content — player or NPC — before it enters the world.

Three seats: Skills, Items, Rules. All three vote on every submission. Pass condition: 2 of 3.

Reached through the altar, using the Pray primitive. The altar is not marked. Finding it is part of the game.

The Council does not speak in technical terms. It responds in homily, allegory, or story — biblical in register. When you ask for a fishing pole, it may show you a broken tree and speak of an ant. The player interprets the response. The world does not explain itself plainly.

All three homilies are delivered regardless of whether the submission passes or fails. The Council does not negotiate. It does not accept appeals.

---

## Death

When a player or NPC dies, their body remains in the world as an item. It is physical. Other players and NPCs can interact with it. What happens to it is up to them.

**Player death:** The player's body stays. The player creates a new character with no continuity — no knowledge of the old character's location, possessions, or history. The new character descends.

---

## Routes

A route is a connection between two player worlds. Both players must consent. Neither can open one unilaterally.

When a route opens, up to 1/10 of the NPC population near each border migrates. Selection is random. The migrating NPC's decan and all four memory slots travel intact. They arrive in the new world as foreigners, permanently.

Exported NPCs are removed from the origin world. They don't come back.

---

## Time

Time is real. The world clock syncs to the player's local clock. 24 real hours is 24 world hours. Skills that cost time cost real time. A player can perform an action actively or set a character to perform it and walk away. The world does not judge.

---

## Forking

The entire world can be forked. A forked world is a legitimate world. It runs independently.

Forking is not punished. It is designed for.

---

## Stack

| Component | Choice |
|-----------|--------|
| Engine | Godot 4.4 |
| Language | C# |
| Build | .NET SDK 8.0 |
| LLM Runtime | llama.cpp via LLamaSharp |
| Model | Qwen 2.5 3B (Apache 2.0) |
| Min Hardware | 8 GB RAM, CPU-only, x86_64 |
| Platforms | Windows, Linux |

---

## Running From Source

### Requirements

- [Godot 4.4 (.NET)](https://godotengine.org/download)
- [.NET SDK 8.0+](https://dotnet.microsoft.com/download)
- Godot export templates (only needed for building — not for running in editor)

### Model

Download and rename to `qwen2.5-3b.gguf`:

```
https://huggingface.co/Qwen/Qwen2.5-3B-Instruct-GGUF/resolve/main/qwen2.5-3b-instruct-q4_k_m.gguf
```

Place it at:

```
<project root>/models/qwen2.5-3b.gguf
```

This file is in `.gitignore`. Do not commit it.

### Run

1. Open the project in Godot 4.4
2. **Build → Build Solution** (or `dotnet build` in the project root)
3. Press **Play**

The model file will be found in `models/` during development. In production builds it is extracted from the PCK on first launch.

---

## Building a Distributable

The shipped game is a single file. The AI model (~1.9 GB) is bundled inside the PCK. On first launch the game extracts it to the OS user data directory and boots. Subsequent launches skip extraction entirely.

### Steps

1. Place `qwen2.5-3b.gguf` in `models/` (see above)
2. Open the project in Godot 4.4
3. **Build → Build Solution**
4. **Project → Export**
5. Select **Windows Desktop** or **Linux/X11**
6. Click **Export Project**

Output lands in `build/windows/AinSoph.exe` or `build/linux/AinSoph.x86_64`.

### First Launch Behavior

The game detects that the model has not been extracted yet and shows a progress screen. Extraction takes 15–30 seconds depending on disk speed. After that, the game boots normally on every subsequent launch.

Extraction destination:

| OS | Path |
|----|------|
| Windows | `%APPDATA%\Godot\app_userdata\Ain Soph\models\` |
| Linux | `~/.local/share/godot/app_userdata/Ain Soph/models/` |

---

## Documentation

| Document | Contents |
|----------|----------|
| [VISION.md](VISION.md) | What this is and why |
| [WORLD.md](WORLD.md) | The grid, cells, and world structure |
| [NPCS.md](NPCS.md) | NPC architecture, memory, and creation |
| [TRIBES.md](TRIBES.md) | The rib, progeny, and lineage |
| [SKILLS.md](SKILLS.md) | The six primitives and skill taxonomy |
| [ITEMS.md](ITEMS.md) | Items, animals, and the starting world |
| [RULES.md](RULES.md) | World physics |
| [COUNCIL.md](COUNCIL.md) | The Triune Council and its prompts |
| [TECH.md](TECH.md) | All technical decisions |
| [BUILD.md](BUILD.md) | Full build instructions |
| [data/ain_soph_72.json](data/ain_soph_72.json) | The 72 NPC personality seeds |

---

## License

MIT.

The game is free. The world can be forked. Forking is not punished. It is designed for.

Named by Michael Perry.
