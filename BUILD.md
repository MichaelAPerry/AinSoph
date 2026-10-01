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
https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF/resolve/main/qwen2.5-1.5b-instruct-q4_k_m.gguf
```

Rename it to:

```
qwen2.5-1.5b.gguf
```

Place it at:

```
res://models/qwen2.5-1.5b.gguf
```

i.e. inside the `models/` folder in the project root, alongside `.gdkeep`.

**This file is in `.gitignore` — do not commit it.** Depending on the export preset it is either bundled into the PCK or shipped beside the executable (see below).

---

## Export

Install the **Godot 4.4.1 .NET export templates** first (Editor → Manage Export Templates).

There are two kinds of preset:

| Preset | Model goes | Use for |
|--------|-----------|---------|
| **Windows Desktop**, **Linux/X11** | inside the PCK; extracted to user data on first launch | sharing a build as a zip |
| **Windows Desktop (Steam)**, **Linux/X11 (Steam)** | loose, in `models/` next to the executable | Steam — one copy on disk, no extraction |

From the editor: **Build → Build Solution**, then **Project → Export**, pick a preset, **Export Project**.

From the command line, the Steam builds in one go:

```
GODOT=/path/to/Godot_v4.4.1-stable_mono_linux.x86_64 tools/build-steam.sh
```

This writes `build/steam/windows/` and `build/steam/linux/` (about 1.3 GB each), each ready to upload as a depot.

**A C# export is a folder, not a single file.** Next to the executable is a `data_AinSoph_<platform>/` folder holding the .NET runtime, LLamaSharp and llama.cpp's native libraries (no-AVX, AVX2 and AVX-512 builds, so old CPUs work). Ship the whole folder.

---

## What happens on first launch

Steam builds load the model straight from `models/` beside the executable — nothing to extract.

Other builds store the model (~1 GB) inside the PCK. On first run, Ain Soph extracts it to the OS user data directory:

| OS | Path |
|----|------|
| Windows | `%APPDATA%\Godot\app_userdata\Ain Soph\models\` |
| Linux | `~/.local/share/godot/app_userdata/Ain Soph/models/` |

This extraction takes a few seconds to half a minute and is shown on screen. It only happens once. Subsequent launches boot directly.

Extraction writes to a `.part` file and renames it when complete, so an interrupted first launch simply extracts again next time.

If the export has no model in it, the game boots in demo mode (scripted NPC and Council voices) rather than stopping at the boot screen.

---

## Distribution

Ship the whole export folder (executable + `data_AinSoph_<platform>/`, plus `models/` for Steam builds). Players double-click and play.

Minimum hardware: 8 GB RAM, any x86_64 CPU (no GPU needed), about 1.5 GB of disk.

For Steam, see [STEAM.md](STEAM.md).
