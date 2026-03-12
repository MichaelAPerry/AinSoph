namespace AinSoph.World;

public enum BiomeType
{
    Wilderness,
    Desert,
    River,
    Sea,
    Forest,
    Grove,
    Mountain,
    Valley
}

/// <summary>
/// Static data for each founding biome.
/// Cave chance: probability (0–1) that a generated cell contains a cave.
/// Manna density: fraction of tiles that receive manna at morning spawn. 0 = none.
/// </summary>
public static class BiomeData
{
    public record BiomeProfile(
        BiomeType Type,
        string    Name,
        float     CaveChance,
        float     MannaDensity,
        bool      Passable      // Sea is impassable on foot
    );

    public static readonly IReadOnlyDictionary<BiomeType, BiomeProfile> Profiles =
        new Dictionary<BiomeType, BiomeProfile>
        {
            [BiomeType.Wilderness] = new(BiomeType.Wilderness, "Wilderness", CaveChance: 0.15f, MannaDensity: 1f/15f, Passable: true),
            [BiomeType.Desert]     = new(BiomeType.Desert,     "Desert",     CaveChance: 0.05f, MannaDensity: 1f/25f, Passable: true),
            [BiomeType.River]      = new(BiomeType.River,      "River",      CaveChance: 0f,    MannaDensity: 1f/10f, Passable: true),
            [BiomeType.Sea]        = new(BiomeType.Sea,        "Sea",        CaveChance: 0f,    MannaDensity: 0f,     Passable: false),
            [BiomeType.Forest]     = new(BiomeType.Forest,     "Forest",     CaveChance: 0f,    MannaDensity: 1f/10f, Passable: true),
            [BiomeType.Grove]      = new(BiomeType.Grove,      "Grove",      CaveChance: 0.10f, MannaDensity: 1f/12f, Passable: true),
            [BiomeType.Mountain]   = new(BiomeType.Mountain,   "Mountain",   CaveChance: 0.40f, MannaDensity: 1f/20f, Passable: true),
            [BiomeType.Valley]     = new(BiomeType.Valley,     "Valley",     CaveChance: 0f,    MannaDensity: 1f/8f,  Passable: true),
        };

    public static BiomeProfile Get(BiomeType type) => Profiles[type];
}
