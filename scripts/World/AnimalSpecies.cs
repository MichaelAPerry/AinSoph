using AinSoph.NPC;
using Godot;

namespace AinSoph.World;

public enum AnimalHabitat { Land, Bird, Water }

/// <summary>
/// The starting animals, from ITEMS.md (Deuteronomy 14 / Leviticus 11).
/// Clean animals are edible: their body satisfies the day's hunger when reaped.
/// Reap numbers follow RULES.md — predator 80, neutral 20, prey 10, insect 5.
/// </summary>
public record AnimalSpecies(
    string        Name,
    AnimalType    Type,
    AnimalHabitat Habitat,
    bool          Edible,
    int           Glyph,   // tile in the 1-bit pack
    Color         Tint)
{
    private const int Beast = 21, Bird = 25, Fish = 26, Bug = 27;

    private static readonly Color Predator = new(1.0f, 0.55f, 0.50f);
    private static readonly Color Tame     = new(0.85f, 0.75f, 0.60f);
    private static readonly Color Wild     = new(0.95f, 0.95f, 0.90f);

    // ── Made by the gods (see Divine.cs) ─────────────────────────────────

    /// <summary>Kill number, when the gods set one; otherwise it follows the type (RULES.md).</summary>
    public int?   Strength { get; init; }
    /// <summary>An enemy: stalks the nearest being and strikes when beside it.</summary>
    public bool   Hunts    { get; init; }
    /// <summary>One of a kind: not placed in new land, not replaced when it dies.</summary>
    public bool   Unique   { get; init; }
    /// <summary>What it looks like, in the gods' words.</summary>
    public string Look     { get; init; } = string.Empty;
    public bool   Divine   { get; init; }

    private static readonly AnimalSpecies[] Created =
    {
        // Land — edible (clean)
        new("ox",       AnimalType.Neutral,  AnimalHabitat.Land,  true,  Beast, Tame),
        new("sheep",    AnimalType.Prey,     AnimalHabitat.Land,  true,  Beast, Wild),
        new("goat",     AnimalType.Neutral,  AnimalHabitat.Land,  true,  Beast, Tame),
        new("deer",     AnimalType.Prey,     AnimalHabitat.Land,  true,  Beast, Wild),
        new("gazelle",  AnimalType.Prey,     AnimalHabitat.Land,  true,  Beast, Wild),
        new("antelope", AnimalType.Prey,     AnimalHabitat.Land,  true,  Beast, Wild),
        // Land — not edible (unclean)
        new("camel",    AnimalType.Neutral,  AnimalHabitat.Land,  false, Beast, Tame),
        new("hare",     AnimalType.Prey,     AnimalHabitat.Land,  false, Beast, Wild),
        new("pig",      AnimalType.Neutral,  AnimalHabitat.Land,  false, Beast, Tame),
        new("horse",    AnimalType.Neutral,  AnimalHabitat.Land,  false, Beast, Tame),
        new("donkey",   AnimalType.Neutral,  AnimalHabitat.Land,  false, Beast, Tame),
        new("lion",     AnimalType.Predator, AnimalHabitat.Land,  false, Beast, Predator),
        new("bear",     AnimalType.Predator, AnimalHabitat.Land,  false, Beast, Predator),
        new("dog",      AnimalType.Neutral,  AnimalHabitat.Land,  false, Beast, Tame),
        new("wolf",     AnimalType.Predator, AnimalHabitat.Land,  false, Beast, Predator),
        // Birds — edible (clean)
        new("dove",     AnimalType.Prey,     AnimalHabitat.Bird,  true,  Bird,  Wild),
        new("pigeon",   AnimalType.Prey,     AnimalHabitat.Bird,  true,  Bird,  Wild),
        new("fowl",     AnimalType.Prey,     AnimalHabitat.Bird,  true,  Bird,  Wild),
        new("duck",     AnimalType.Prey,     AnimalHabitat.Bird,  true,  Bird,  Wild),
        new("quail",    AnimalType.Prey,     AnimalHabitat.Bird,  true,  Bird,  Wild),
        // Birds — not edible (unclean)
        new("raven",    AnimalType.Neutral,  AnimalHabitat.Bird,  false, Bird,  Tame),
        new("eagle",    AnimalType.Predator, AnimalHabitat.Bird,  false, Bird,  Predator),
        new("vulture",  AnimalType.Neutral,  AnimalHabitat.Bird,  false, Bird,  Tame),
        new("owl",      AnimalType.Neutral,  AnimalHabitat.Bird,  false, Bird,  Tame),
        new("hawk",     AnimalType.Predator, AnimalHabitat.Bird,  false, Bird,  Predator),
        new("stork",    AnimalType.Neutral,  AnimalHabitat.Bird,  false, Bird,  Tame),
        // Water — edible (fins and scales)
        new("salmon",   AnimalType.Prey,     AnimalHabitat.Water, true,  Fish,  Wild),
        new("trout",    AnimalType.Prey,     AnimalHabitat.Water, true,  Fish,  Wild),
        new("perch",    AnimalType.Prey,     AnimalHabitat.Water, true,  Fish,  Wild),
        // Insects — edible (clean)
        new("locust",   AnimalType.Insect,   AnimalHabitat.Land,  true,  Bug,   Wild),
    };

    private static readonly List<AnimalSpecies> _all = new(Created);
    private static readonly Dictionary<string, AnimalSpecies> ByName =
        _all.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>Every species in the world: the thirty from the start, and any the gods have made since.</summary>
    public static IReadOnlyList<AnimalSpecies> All => _all;

    /// <summary>Species the gods made — saved with the world.</summary>
    public static IEnumerable<AnimalSpecies> Made => _all.Where(s => s.Divine);

    public static AnimalSpecies? Get(string name) =>
        ByName.TryGetValue(name, out var s) ? s : null;

    /// <summary>Add (or replace) a species made by the gods.</summary>
    public static void Register(AnimalSpecies species)
    {
        if (ByName.TryGetValue(species.Name, out var old))
        {
            if (!old.Divine) return; // the first thirty are never replaced
            _all.Remove(old);
        }
        _all.Add(species);
        ByName[species.Name] = species;
    }

    /// <summary>Forget the gods' species — a different world is loading.</summary>
    public static void ClearMade()
    {
        foreach (var s in _all.Where(s => s.Divine).ToList()) { _all.Remove(s); ByName.Remove(s.Name); }
    }

    /// <summary>Fish live on water tiles; everything else on dry land.</summary>
    public bool CanStandOn(TileSurface surface) =>
        Habitat == AnimalHabitat.Water ? surface == TileSurface.Water : surface != TileSurface.Water;
}
