# Building Ain Soph

## What you need

| Tool | Where |
|------|-------|
| Godot 4.4 .NET | https://godotengine.org/download |
| Godot export templates 4.4 | Godot → Editor → Manage Export Templates |
| .NET SDK 8.0+ | https://dotnet.microsoft.com/download |
| Model file (see below) | HuggingFace |

---

## Get the model

Download this exact file:

```
https://huggingface.co/Qwen/Qwen2.5-3B-Instruct-GGUF/resolve/main/qwen2.5-3b-instruct-q4_k_m.gguf
```

Rename it to:

```
qwen2.5-3b.gguf
```

Place it at:

```
res://models/qwen2.5-3b.gguf
```

i.e. inside the `models/` folder in the project root, alongside `.gdkeep`.

**This file is in `.gitignore` — do not commit it. It is bundled into the PCK at export time only.**

---

## Export

1. Open the project in Godot 4.4
2. Build C# first: **Build → Build Solution** (or `dotnet build` in project root)
3. Open **Project → Export**
4. Select **Windows Desktop** or **Linux/X11**
5. Click **Export Project** (not Export PCK)
6. Output lands in `build/windows/` or `build/linux/`

The PCK is embedded in the exe (`embed_pck=true`), so the output is a single file.

---

## What happens on first launch

The model (1.9 GB) is stored inside the PCK. On first run, Ain Soph extracts it to the OS user data directory:

| OS | Path |
|----|------|
| Windows | `%APPDATA%\Godot\app_userdata\Ain Soph\models\` |
| Linux | `~/.local/share/godot/app_userdata/Ain Soph/models/` |

This extraction takes 15–30 seconds and is shown on screen. It only happens once. Subsequent launches boot directly.

---

## Distribution

Ship the single exe. Nothing else required. Players double-click and play.

Minimum hardware: 8 GB RAM, any x86_64 CPU (no GPU needed).
