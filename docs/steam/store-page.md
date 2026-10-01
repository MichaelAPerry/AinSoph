# Ain Soph — Steam store page (draft)

Everything Steamworks asks for on the store page, ready to paste. Character limits are Steam's.
Placeholder capsule art is in `docs/steam/` (see the end of this file).

> **Before submitting:** Steam's AI disclosure covers store text players read. This draft was written with AI assistance —
> either rewrite it in your own words or declare it under *Pre-Generated* content in the survey.

---

## Basics

| Field | Value |
|---|---|
| Name | Ain Soph |
| Developer / Publisher | Michael Perry |
| Price | **Free** (the README promises a free game; note the $100 Steam Direct fee is only refunded after $1,000 of sales, so for a free game it is a sunk cost) |
| Release | Early Access recommended — real-time survival and a small local model are things players should know are evolving |
| Languages | English (interface and AI) |
| Platforms | Windows, Linux (SteamOS untested — don't claim Steam Deck support yet) |

---

## Short description (max 300 characters)

> A persistent world whose people — and whose gods — run on your own computer. Survive in real time, speak with NPCs who remember you, and pray at a hidden altar where a three-seat Council answers in parable. No cloud, no GPU, nothing phones home.

*(245 characters)*

---

## About this game

**The boundless. The infinite before form.**

Ain Soph is a living world on an endless grid. Its people are not scripted: each one is voiced by an AI that runs entirely on your own computer — no internet, no account, no graphics card needed. Every NPC has one of 72 natures, remembers what happens to them, and decides for themselves what to do each hour.

**The world does not pause.**
Hunger and sleep run on real hours. Eat once a day or die. Sleep eight hours or die. Only a cave is safe to sleep in, and a cave holds one. When you log out, your character sleeps where they stand — so choose where you leave them.

**Six primitives. Nothing else at the start.**
Move. See. Hear. Talk. Reap. Pray. Reap is killing and eating — the world makes no distinction. Everything beyond the six must be brought into the world.

**The Triune Council.**
Somewhere lies one altar, unmarked. Stand beside it and pray for a new skill, a new thing, a new law. Three seats — Skills, Items, Rules — vote, and every one answers in parable. Two of three, and it enters the world. NPCs pray too. Approved laws bind everyone, and the people of the world live by them.

**The rib.**
After a week of play you earn a rib — one, ever. Name your spouse and describe them yourself. Each week after, children are born, carrying your lineage. They wander. They may cross into other players' worlds.

**Routes between worlds.**
Send a handful of your people to a friend's world as a file. Receive theirs. Travellers keep their memories and their nature — and remain foreigners forever.

**A world of beasts.**
Thirty species from Deuteronomy and Leviticus. The clean ones are food. Lions, bears, wolves and eagles hunt — you, too.

**Free, open source, and yours.**
No microtransactions. No subscription. Nothing phones home. The whole game is MIT-licensed — fork it. Forking is not punished. It is designed for.

---

## Tags (Steam allows up to 20; first ones weigh most)

Survival · Sandbox · Open World · Simulation · Pixel Graphics · Procedural Generation · Life Sim · Indie · Free to Play · Singleplayer · Exploration · Crafting · Atmospheric · Philosophical · Story Rich · 2D · Top-Down · Early Access · Experimental · Religious

---

## System requirements

| | Minimum | Recommended |
|---|---|---|
| OS | Windows 10 64-bit / Ubuntu 22.04 or equivalent | Windows 11 64-bit |
| Processor | 64-bit x86, 4 cores | 6+ cores (AI replies come faster) |
| Memory | 8 GB RAM | 16 GB RAM |
| Graphics | Any GPU with OpenGL 3.3 (integrated is fine) | — |
| Storage | 1.5 GB available space | SSD |
| Additional notes | **No dedicated graphics card needed: the AI runs on the CPU.** Fully offline. | |

---

## Content Survey — AI disclosure

**Does the game use AI-generated content?** Yes.

**Pre-Generated:** None in the game itself. Art is from Kenney (CC0); music and sound effects were synthesised from code (`tools/make-sounds.py`); the 72 personalities were hand-written. AI coding assistants were used in development (exempt). *If the store text above is kept as drafted, declare it here.*

**Live-Generated — paste this:**

> Ain Soph runs a small language model (Qwen 2.5 1.5B Instruct, Apache 2.0) locally on the player's own computer through llama.cpp. During play it generates: what NPCs say, what they decide to do each hour, what they choose to remember, and the votes and short parables of the "Triune Council" that judges what players and NPCs ask to bring into the world.
>
> Guardrails:
> 1. Every prompt instructs the model to stay in character and never to produce sexual content, slurs or hateful language about real groups of people, or encouragement of self-harm.
> 2. Every line the model generates passes a word-list filter before any player sees it or any NPC stores it in memory. A matching line is withheld in full and replaced with "…".
> 3. Output is never shared with other players or sent anywhere. The game makes no network connections; the model runs offline.
> 4. The model can only change the world through fixed game actions (move, eat, sleep, talk, create, pray). It cannot write files or run code.
>
> Players may report problematic output through the Steam community hub.

**Adult content:** No sexual content; the guardrails above exist so live generation stays that way.

---

## Mature content descriptors (IARC / Steam)

- **Violence:** yes — characters and animals can be killed with the "Reap" action (a d100 roll); no gore, no blood, a body is left behind as a marker.
- Frequent death of the player character from starvation, exhaustion or predators.
- **Religious themes:** biblical names, an altar, prayer, a Council that speaks in parable.
- No sexual content, no drug use, no gambling, no real-money transactions.
- **User-generated content:** players type free text to NPCs and to the Council; responses are AI-generated and filtered (see above).

---

## Screenshots (5 minimum, 1280×720 or larger)

From `docs/demo/`, in this order:
1. `03-npcs.png` — the living world
2. `05-dialogue.png` or `qwen-1.5b-dialogue.png` — an NPC answering in character (use a real-model shot for the store)
3. `12-council.png` or `qwen-1.5b-council.png` — the Council's parables
4. `07-animals.png` — animals and terrain
5. `09-rib.png` — naming your spouse
6. `04-primitives.png` — the six primitives
7. `13-menu.png` — optional

Trailer: `docs/demo/tour.mp4` is a 68-second captioned rough cut (1280×720). Steam prefers 1920×1080; re-record with a larger window, or upscale.

---

## Capsule art

Placeholder drafts at the right sizes are in `docs/steam/` — built from game screenshots and the title, good enough for a "Coming Soon" page while proper key art is made:

| File | Size | Steam slot |
|---|---|---|
| `header_capsule.png` | 920×430 | Header capsule |
| `small_capsule.png` | 462×174 | Small capsule |
| `main_capsule.png` | 1232×706 | Main capsule |
| `vertical_capsule.png` | 748×896 | Vertical capsule |
| `library_capsule.png` | 600×900 | Library capsule |
| `library_hero.png` | 3840×1240 | Library hero (no text, per Steam's rules) |
| `library_logo.png` | 1280×720 | Library logo (transparent) |

Regenerate with `python3 tools/make-capsules.py`.
