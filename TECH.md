# TECH.md
## Ain Soph — Technical Decisions

---

### ENGINE

Godot.

---

### LANGUAGE

C#. Godot supports GDScript and C#. C# is the correct choice for Ain Soph — the LLM integrations, procedural world, and continuous NPC logic require the performance and ecosystem that C# provides.

---

### PLATFORMS

Windows and Linux first. Other platforms not yet decided.

---

### HARDWARE TARGET

Low-spec. CPU-only machines are the baseline. No GPU requirement.

The game must run on a potato. Every technical decision is evaluated against this.

Minimum spec: 8GB RAM. This is the floor. Do not design below it.

---

### LLM RUNTIME

llama.cpp. Direct integration — no wrapper.

Ollama is appropriate for development and testing. The shipped game uses llama.cpp directly. Less overhead, more control, no separate process for the player to manage.

Model selection not yet decided — chosen model must run acceptably on an 8GB RAM, CPU-only machine.

---

### MEMORY SLOTS

Each NPC has four memory slots: will, thought, feeling, action.

Each slot is a string with a maximum of 1024 characters.

NPCs decide what gets written into their own slots. The NPC's LLM determines what is worth remembering.

---

### THE GRID

The world is a grid of sovereign cells.

Mental model: Game of Life. Each cell operates independently, connects to neighboring cells as peers. There is no hierarchy between cells — they are equal containers.

Each cell is locally expandable to the limits of the player's hardware.

Maximum theoretical grid scale: 10^9 × 10^9 cells.

Only cells near a player need to exist at any moment. Generation is on-demand.

Cells contain biomes, features, and world complexity. Cell contents emerge from rules. Rules are extensible — players and NPCs can contribute new rules through the council.

---

### THE TRIUNE COUNCIL

The council is an automated governance body. Every piece of content that wants to enter the world goes before it — whether created by a player or an NPC.

The council has three seats:

| Seat | Domain |
|------|--------|
| Skills | Evaluates skill coherence, function, and consistency |
| Items | Evaluates item coherence, function, and consistency |
| Rules | Evaluates rule coherence and world-level implications |

All three seats vote on every submission. Skills make items. Items feed skills. Rules govern everything. No submission is siloed.

**Pass condition: 2 of 3 votes in favor.**

The council runs on llama.cpp instances. Each seat is a separate instance with a domain-specific prompt.

**Council prompt architecture is the next design task. Do not implement until prompts are decided.**

---

### NPC CREATION LOOP

1. NPC autonomously decides to create something (skill, item, biome, rule)
2. Submission goes before the Triune Council
3. Council votes — 2 of 3 passes
4. On pass: content enters the world as equal world content
5. On fail: the NPC moves on. What happens when you pray to god and the world doesn't move? You move on. The NPC's decan determines how they carry it.

The player can observe this process. The NPC displays a sprite and status while creating. The player can interrupt mid-process and initiate dialogue about what the NPC is doing.

---

### MODEL

Qwen 2.5 3B. Apache 2.0 license — fully open, compatible with Godot (MIT) and llama.cpp (MIT). Runs on 8GB RAM, CPU-only. Strong instruction following and structured output at low spec.

---

### BUILD SYSTEM

.NET SDK alongside Godot's export templates. Required for LLamaSharp — the .NET binding for llama.cpp that connects Qwen 2.5 3B to the game. Enables proper dependency management and CI/CD when the time comes.

### LLAMA INTEGRATION

LLamaSharp. The .NET binding for llama.cpp. This is how the game calls Qwen — for NPC dialogue, for the Triune Council's three sequential passes, for all LLM inference. It is a standard .NET package reference.
