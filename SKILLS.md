# SKILLS.md
## Ain Soph — Skills

---

### WHAT A SKILL IS

A skill is anything a character can do.

Skills are not hardcoded beyond the four primitives. Everything else is created by players and NPCs and enters the world through the Triune Council.

PCs and NPCs operate under identical skill constraints.

---

### THE FOUR PRIMITIVES

Every PC and NPC is born with these four skills. They cannot be created. They can be broken or lost.

| Skill | What it does |
|-------|-------------|
| Move | Base locomotion |
| See | Visual perception |
| Hear | Audio perception |
| Talk | Communication |
| Kill | The capacity to end a life. Resolved by d100 roll. See RULES.md. |
| Pray | Reaches the Triune Council. At first does nothing visible. Discovered, not explained. |

When a primitive is broken or lost, the character is limited in that dimension. Limitation drives invention — new skills and items emerge to compensate, substitute, or route around the absence.

A primitive skill can only be absent from birth. Death is the only way to permanently lose a skill after birth. Wounds and degradation are not modeled.

---

### SKILL TAXONOMY

| Type | Definition | Example |
|------|-----------|---------|
| **Primitive** | Born with it. Can be broken or lost. | Move, See, Hear, Talk |
| **Composite** | Built from two or more existing skills. | Weasel Hunting (Move + Hear) |
| **Substitute** | Replaces a broken or absent primitive. | Cart (substitutes Move) |
| **Extension** | Amplifies an existing primitive beyond its base. | Telescope (extends See) |

A substitute covers for absence. An extension amplifies presence. Both reference a base skill. The difference is whether the base skill is broken or intact.

Skills can be created whole cloth with no prerequisites. Prerequisites are not required.

---

### SURVIVAL REQUIREMENTS

Eat and sleep are not skills. They are world-enforced survival requirements.

They apply equally to PCs and NPCs.

**Failure to meet them results in death.** The body is left in the world. What happens to it is up to other PCs and NPCs.

Skills and items that address survival requirements — foraging, cooking, shelter-building, setting a watch — are created by players and NPCs.

---

### TIME

Time is the universal world resource.

The world clock syncs to the player's local clock. 24 hours in a real day is 24 hours in the world.

Skills that take time cost real time. A skill that takes 2 hours takes 2 real hours.

A player can actively perform a skill in-game or set a character to perform it and walk away. Both are valid. The world does not judge.

---

### SKILL COST

Cost is not a universal field. Cost is defined within the skill itself as part of its rules — the council evaluates whether the cost is coherent when the skill is submitted.

Time is always a potential cost. Other costs (items consumed, prerequisites required) are declared in the skill's definition.

---

### THE SKILL SCHEMA

```json
{
  "id": "",
  "name": "",
  "type": "",
  "description": "",
  "base_skills": [],
  "created_by": "",
  "cost": {
    "time_hours": null,
    "items_consumed": [],
    "notes": ""
  },
  "broken": false,
  "lost": false,
  "council_status": "",
  "lineage": ""
}
```

**Field definitions:**

| Field | Type | Notes |
|-------|------|-------|
| id | string | Unique identifier |
| name | string | What the skill is called |
| type | string | primitive / composite / substitute / extension |
| description | string | What the skill does |
| base_skills | array | Skills this builds on or references. Empty for whole-cloth creations. |
| created_by | string | Player ID or NPC ID. Empty for primitives. |
| cost.time_hours | number or null | Real-world hours the skill takes. Null if instantaneous. |
| cost.items_consumed | array | Items used up when skill is performed. Empty if none. |
| cost.notes | string | Any additional cost rules defined by the creator. |
| broken | boolean | True if the skill has been damaged and is non-functional or impaired. |
| lost | boolean | True if the skill has been permanently removed. |
| council_status | string | pending / approved / rejected. Empty for primitives. |
| lineage | string | Append-only record of who created and modified this skill. |

---

### PRIMITIVE SKILL IMPAIRMENT AT BIRTH

Primitives can be absent or impaired at birth. Rates are hardcoded to real-world natural occurrence. No disease names are attached — only the mechanical reality.

Each primitive rolls independently.

| Primitive | Birth impairment rate |
|-----------|----------------------|
| Move | ~2-3 per 1,000 |
| See | ~0.3-0.5 per 1,000 |
| Hear | ~1 per 1,000 |
| Talk | ~0.1 per 1,000 |

Impairment does not define the character. It is a starting condition. What the character and their community build around it is the game.
