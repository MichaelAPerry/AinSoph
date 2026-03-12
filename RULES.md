# RULES.md
## Ain Soph — World Rules

---

### WHAT A RULE IS

A rule is world physics. It governs what is true for everything in the world — PCs, NPCs, animals, items, cells.

Rules are not hardcoded forever. Players and NPCs can propose new rules through the Triune Council. Approved rules enter the world as equal world content.

The rules in this document are the founding physics. Everything begins here.

---

### SURVIVAL

**Eat.** Every living character — PC, NPC, animal — must eat once per day. This is binary. You ate or you didn't. Failure to eat results in death. A day is 24 real hours, tracked against the player's local clock.

**Sleep.** Every living character must sleep 8 continuous real hours within each 24-hour window. Failure to sleep results in death.

**Warnings.** At hour 23 without eating, a hunger warning is issued. At hour 23 without sleeping, a sleep warning is issued.

**Safe sleep.** A cave is the only safe location. A character inside a cave is sleeping safely. A cave can be occupied by one character at a time — entering claims it, leaving frees it immediately.

**Exposed sleep.** Any sleep outside a cave is exposed. An exposed sleeping character is vulnerable to Reap rolls from any entity that decides to initiate one.

**Logout.** When a player logs out, their character remains in the world. If they are not inside a cave, they are considered sleeping exposed. They can be killed while offline. The world does not pause.

---

### DEATH

When a character or animal dies, their body remains in the world as an item.

The body follows item rules. It is physical. It can be interacted with. What happens to it is up to other PCs and NPCs.

**Player death.** The player's body stays in the world. The player creates a new character with no continuity — no knowledge of the old character's location, possessions, or history.

---

### KILL

Reap is the fifth primitive skill. Every living thing — PC, NPC, animal — is born with it.

Reap covers both killing and consuming. Against a living target it initiates Reap resolution. Against an edible item it satisfies the daily eat requirement. Same act. The world makes no distinction.

**Resolution:** Both attacker and defender roll d100. The attacker must roll equal to or under their Reap number. The defender must roll equal to or under their Reap number to resist or flee. Ties go to the defender.

**Base Reap numbers:**

| Entity | Base Reap number |
|--------|-----------------|
| PC | 50 |
| NPC | 50 ± decan modifier |
| Animal — predator (lion, wolf, bear, eagle) | 80 |
| Animal — neutral (horse, donkey, camel, ox) | 20 |
| Animal — prey (sheep, deer, rabbit, dove) | 10 |
| Animal — insect (locust) | 5 |

**NPC decan modifiers (applied to base 50):**

| Decan | Modifier |
|-------|----------|
| Chontare | +5 |
| Sitlacer | 0 |
| Subtus | +5 |
| Tepisatras | -5 |
| Archatapias | +5 |
| Sentacer | 0 |
| Luchala | -5 |
| Sothis | 0 |
| Thumis | -5 |
| Ahureuchnos | -5 |
| Cheironin | +5 |
| Raubel | -5 |
| Tekarath | 0 |
| Senacher | -5 |
| Aterechinis | 0 |
| Horieus | -5 |
| Darumiath | -5 |
| Arabas | -5 |
| Aphruimis | +5 |
| Sothis-Khneme | 0 |
| Eregbuo | -5 |
| Serucath | +5 |
| Tepis | -5 |
| Archir | 0 |
| Sphruimis | +5 |
| Boutath | -5 |
| Astiro | +5 |
| Nofre | -5 |
| Phruimine | 0 |
| Thumis-Ra | 0 |
| Sothis-Anath | 0 |
| Erekhisina | -5 |
| Chnoumis | -5 |
| Sothis-Thoth | -5 |
| Sentacher-Min | 0 |
| Aphruimis-Thoth | -5 |
| Zubene | -5 |
| Chara | 0 |
| Asterion | -5 |
| Spica | 0 |
| Vindemiator | +5 |
| Gienah | +5 |
| Isidis | +5 |
| Seket | 0 |
| Antares | 0 |
| Graffias | -5 |
| Lesath | +5 |
| Shaula | 0 |
| Kaus | 0 |
| Nunki | 0 |
| Ascella | 0 |
| Rukbat | -5 |
| Media | -5 |
| Pelagus | 0 |
| Algedi | 0 |
| Dabih | 0 |
| Oculus | 0 |
| Armus | 0 |
| Castra | 0 |
| Nashira | 0 |
| Sadalsuud | 0 |
| Sadalmelik | -5 |
| Albali | -5 |
| Sadachbia | 0 |
| Skat | -5 |
| Situla | +5 |
| Desherith | -5 |
| Revati | 0 |
| Linteum | -5 |
| Achernar | 0 |
| Baten Kaitos | 0 |
| Ain Soph Aour | 0 |

Skills can modify Reap rolls in future. That extension is built by players and NPCs through the council.

---

### TIME

Time is real. The world clock syncs to the player's local clock.

24 hours in the real world is 24 hours in the world.

Skills that cost time cost real time. A skill that takes 2 hours takes 2 real hours.

A player can actively perform an action or set a character to perform it and walk away. Both are valid. The world does not judge.

---

### MOVEMENT

Travel time between locations is based on real walking speed: 1 kilometer per 12 real minutes (5 km/h).

A grid cell is 256 × 256 units. Each unit is approximately 1 meter. Crossing a full cell on foot takes approximately 38 real minutes.

---

### VISION

During daylight a character can see across 3 grid squares.

At night vision reduces to 1 grid square.

Night is determined by the local clock. The world knows when the sun goes down.

Darkness does not break the See primitive — it reduces its effective range.

---

### LINEAGE

Lineage is append-only and read-only. It can only grow. It cannot be edited or removed.

This applies to progeny lineage, skill lineage, and item lineage.

---

### CONTENT EQUALITY

Player-created content and NPC-created content are equal. Neither is secondary. Neither is modded. It is world content.

All created content — skills, items, rules, biomes — passes through the Triune Council before entering the world.

---

### MIGRATION

When an NPC migrates to another world via a route, their decan and all four memory slots travel with them intact.

The original player has no knowledge of or control over an NPC that has left their sphere.

A migrated NPC is a foreigner in the new world. Permanently. There is no path to full standing.

Foreigners run on a sandboxed LLM instruction set — minimal context, no creation pipeline access. Regardless of what skills the NPC held in their origin world, on arrival all prior skills are suspended and do not recover. The engine enforces this at the decision-application layer, not just in the prompt.

Foreigners can Move, See, Hear, Talk, and Reap edible items. They cannot kill. They cannot pray to the Council. They still require food and sleep under the same rules as all living things.

Foreigner status is permanent. It is the condition of having crossed.

---

### FORKING

The entire world can be forked. A forked world is a legitimate world. It runs independently.

Forking is not punished. It is designed for.

---

### EXTINCTION EVENTS

If any content — player-created or NPC-created — becomes too powerful or breaks the world, it can be removed.

This is called an extinction event. It is real world history. Players will mythologize it.

The Triune Council is the automated first line of defense. Extinction events are the manual override.

---

 
