# NPCS.md
## Ain Soph — NPCs

---

### SOURCE

NPCs come from players. Not from the server.

The server does not generate NPCs independently.

---

### THE LOCAL LLM

The model is Qwen 2.5 3B running on llama.cpp. Apache 2.0 license. Runs on 8GB RAM, CPU-only.

The local LLM powers all NPC behavior in that player's sphere — including NPC-driven content creation.

NPC computation is distributed across player machines. There is no central NPC server.

When a progeny NPC arrives in a player's sphere it runs on that player's local LLM.

---

### THE 72 DECANS

Every NPC is assigned one of 72 personality types at creation.

The 72 are documented in `ain_soph_72.json`.

The decan is the NPC's nature. It governs drives, avoidances, conversational style, stress responses, economic behavior, political tendency, trust dynamics, and betrayal response.

The decan does not change over the NPC's life.

---

### MEMORY

Each NPC has four memory slots.

| Slot | Name | What it holds |
|------|------|---------------|
| will | Will | Instinct, survival, what the NPC wants at the body level |
| thought | Thought | What the NPC knows, believes, has concluded |
| feeling | Feeling | Emotional state, relationships, what the NPC cares about |
| action | Action | What the NPC has done — their history of deeds |

Memory slots are strings. Maximum 1024 characters per slot.

Slots begin empty at NPC creation.

The NPC decides what is worth writing into their own slots. The NPC's LLM determines what gets remembered.

When a progeny moves to another sphere, their memory travels with them intact.

---

### HOW THE LLM USES AN NPC

The LLM receives:
1. The NPC's decan seed from `ain_soph_72.json`
2. The NPC's four memory slots
3. The current situation

It responds in character. It does not break character.

---

### NPC AS WORLD CREATOR

NPCs are not passive. They can autonomously create content — skills, items, biomes, rules — exactly as players can.

An NPC's creation is driven by their decan and memory. A Chontare creates differently than an Arabas. The decan shapes what the NPC wants to make and how they make it.

**The creation loop:**
1. NPC decides autonomously to create something
2. The creation goes before the Triune Council (see TECH.md)
3. Council votes — 2 of 3 passes
4. On pass: the content enters the world as equal world content
5. On fail: the NPC moves on. What happens when you pray to god and the world doesn't move? You move on. The NPC's decan determines how they carry it.

**Player visibility:**
- The NPC displays a sprite and status while creating
- The player can observe from any distance
- The player can interrupt mid-process and initiate dialogue
- The NPC will respond in character about what they are doing and why

---

### READING AN NPC'S MEMORY

Players do not get x-ray vision into NPC memory slots.

A player can talk to an NPC like a normal person. What the NPC reveals is what the NPC would say. The decan and memory slots inform the response — they are not exposed directly.

---

 
