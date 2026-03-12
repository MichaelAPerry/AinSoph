# Ain Soph

**The boundless. The infinite before form.**

Ain Soph is a free, open source, persistent, shared world game. It runs on low-spec hardware. It has no prescribed win condition. It is endlessly customizable. It is easy to run. Easy to learn. Hard to master.

---

## What It Is

A living world on a grid of sovereign cells. Every cell is its own territory. The grid expands without limit. Players and NPCs inhabit the same world simultaneously.

NPCs are not scripted. They are powered by a local LLM running on the player's own machine — no server, no cloud, no subscription. Each NPC has a personality drawn from 72 types and a memory that travels with them across the world.

Players and NPCs can create skills, items, and rules. All created content passes through the Triune Council — three LLM instances that evaluate submissions and respond in parable. The council speaks. The world hears or it doesn't.

---

## What It Is Not

- Not pay to win
- Not pay to play
- Not platform-locked
- Nothing phones home

---

## Stack

| Component | Choice |
|-----------|--------|
| Engine | Godot |
| Language | C# |
| Build | .NET SDK |
| LLM Runtime | llama.cpp via LLamaSharp |
| Model | Qwen 2.5 3B (Apache 2.0) |
| Min Hardware | 8GB RAM, CPU-only |
| Platforms | Windows, Linux |

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
| [ain_soph_72.json](ain_soph_72.json) | The 72 NPC personality seeds |

---

## Status

Early design phase. Documentation is complete. No code exists yet.

---

## License

The game is free. The world can be forked. Forking is not punished. It is designed for.

Named by Michael Perry.
