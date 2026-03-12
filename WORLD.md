# WORLD.md
## Ain Soph — The World

---

### THE GRID

The world is an infinitely vast persistent shared grid of sovereign cells.

Mental model: Game of Life. Each cell is a peer — equal containers connected to neighboring cells. There is no hierarchy between cells.

All players exist on the same grid simultaneously.

Each cell generates on demand. Only cells near a player need to exist at any moment.

There is no edge.

Each cell is locally expandable to the limits of the player's hardware. A player could spend their entire game in a single cell, or expand their own grid to 10^9 × 10^9 cells.

---

### CELLS

Each cell is a sovereign, self-contained unit — enough space that a single player could live an entire game within one.

Cells contain biomes, features, NPCs, items, skills, and world complexity.

Cell contents emerge from rules. Rules are extensible — players and NPCs can contribute new rules, which pass through the Triune Council before entering the world.

Cells are 2D tile grids. Each tile is 32×32 pixels. Each cell is 8×8 tiles.

---

### BIOMES

Eight founding biomes drawn from biblical geography. All world generation begins from these.

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

---

### CAVES

Caves appear in four biomes at varying rates. A cave can hold one occupant at a time.

| Biome | Cave rate |
|-------|-----------|
| Mountain | High |
| Grove | Low |
| Desert | Very low |
| Wilderness | Low |

No caves in River, Sea, Forest, or Valley.

---

### MANNA

Manna spawns each morning based on biome. Animals and players compete for the same supply.

| Biome | Manna density |
|-------|---------------|
| Valley | Generous (1/8) |
| River | Moderate (1/10) |
| Forest | Moderate (1/10) |
| Grove | Moderate (1/12) |
| Wilderness | Sparse (1/15) |
| Mountain | Rare (1/20) |
| Desert | Rare (1/25) |
| Sea | None |

Density is the fraction of tiles that receive one manna item at morning spawn.

---

### PLAYER START

New players spawn in a random cell within 3 cells of a cave. No continuity with any previous character.

---

### SPHERES

Each player instance is a separate sphere.

The grid is what exists between spheres.

Spheres are sovereign. A player's sphere belongs to them.

---

### PLAYER-CREATED CONTENT

Players can create skills and items whole cloth.

NPCs can also create skills, items, biomes, and rules autonomously.

All created content — player or NPC — passes through the Triune Council before entering the world as equal content. It is not secondary or modded — it is world content.

The world has no hardcoded content ceiling. Everything can be extended.

---

### THE TRIUNE COUNCIL

The automated governance body that evaluates all new content.

Three seats: Skills, Items, Rules. All three vote on every submission. Pass condition: 2 of 3.

See TECH.md for full council specification.

---

### PHYSICS

The world has physics constraints designed to play nice with each other.

Constraints are not exhaustive. Gaps exist.

Gaps are handled by iteration — bugs are fixed as found.

Imbalance is not always a bug. Some imbalance becomes lore.

---

### EXTINCTION EVENTS

If a player-created or NPC-created element becomes too powerful or breaks the world, it can be removed.

This is called an extinction event.

The Triune Council is the automated first line of defense. Extinction events are the manual override.

Extinction events are real world history. Players will mythologize them.

The ban hammer is the mechanism. Its use is legitimate and lore-generating.

---

### FORKING

The entire world can be forked.

A forked world is a legitimate world. It runs independently.

Forking is not punished. It is designed for.
