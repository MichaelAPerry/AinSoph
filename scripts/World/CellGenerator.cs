namespace AinSoph.World;

/// <summary>
/// Generates a WorldCell on demand from its grid coordinates.
/// Biome is determined by a noise-based selector.
/// Tiles, caves, and initial items (manna) are placed at generation time.
/// Animals are placed randomly after generation.
/// </summary>
public class CellGenerator
{
    private readonly Random _rng;

    // Biome noise weights — wilderness is the default, most of the world
    private static readonly (BiomeType Biome, float Weight)[] BiomeWeights =
    {
        (BiomeType.Wilderness, 0.30f),
        (BiomeType.Desert,     0.15f),
        (BiomeType.River,      0.10f),
        (BiomeType.Sea,        0.10f),
        (BiomeType.Forest,     0.10f),
        (BiomeType.Grove,      0.08f),
        (BiomeType.Mountain,   0.10f),
        (BiomeType.Valley,     0.07f),
    };

    public CellGenerator(int worldSeed = 0)
    {
        _rng = worldSeed == 0 ? new Random() : new Random(worldSeed);
    }

    /// <summary>
    /// Generate a cell at the given grid coordinates.
    /// Biome is selected by weighted random — consistent for a given seed + coords.
    /// </summary>
    public WorldCell Generate(int gridX, int gridY)
    {
        // Seed per-cell rng from world seed + coords for determinism
        var cellRng = new Random(HashCoords(gridX, gridY, _rng.Next()));

        var biome = SelectBiome(cellRng);
        var profile = BiomeData.Get(biome);

        var cell = new WorldCell
        {
            GridX     = gridX,
            GridY     = gridY,
            Biome     = biome,
            Generated = true
        };

        // Place tiles
        for (var x = 0; x < WorldCell.TilesPerSide; x++)
        for (var y = 0; y < WorldCell.TilesPerSide; y++)
            cell.Tiles[x, y] = GenerateTile(x, y, biome, cellRng);

        // Place cave
        if (profile.CaveChance > 0 && cellRng.NextDouble() < profile.CaveChance)
        {
            cell.CaveId = $"cave:{gridX},{gridY}";
            // Mark a tile as having a cave — prefer stone/rock tiles in mountain, else random
            var caveTile = FindCaveTile(cell, biome, cellRng);
            caveTile.HasCave = true;
        }

        return cell;
    }

    /// <summary>
    /// Spawn manna across a cell according to its biome density.
    /// Called each morning by the world tick.
    /// </summary>
    public List<(int TileX, int TileY)> SpawnManna(WorldCell cell)
    {
        var profile = BiomeData.Get(cell.Biome);
        if (profile.MannaDensity <= 0) return new List<(int, int)>();

        var spawned = new List<(int, int)>();
        var cellRng = new Random(); // fresh each morning — manna is not deterministic

        for (var x = 0; x < WorldCell.TilesPerSide; x++)
        for (var y = 0; y < WorldCell.TilesPerSide; y++)
        {
            // Skip water tiles — manna doesn't spawn in the sea or mid-river
            var tile = cell.Tiles[x, y];
            if (tile.Surface == TileSurface.Water) continue;

            if (cellRng.NextDouble() < profile.MannaDensity)
                spawned.Add((x, y));
        }

        return spawned;
    }

    /// <summary>
    /// Place animals randomly across a cell.
    /// Distribution is random per the design — not biome-specific.
    /// </summary>
    public List<AnimalPlacement> PlaceAnimals(WorldCell cell)
    {
        var placed  = new List<AnimalPlacement>();
        var cellRng = new Random();

        // Small chance of each animal type appearing per cell
        var animalTypes = new[]
        {
            (Type: "predator", Names: new[]{"lion","wolf","bear","eagle"}, Chance: 0.15f),
            (Type: "neutral",  Names: new[]{"horse","donkey","camel","ox"}, Chance: 0.20f),
            (Type: "prey",     Names: new[]{"sheep","deer","rabbit","dove"}, Chance: 0.30f),
            (Type: "insect",   Names: new[]{"locust"}, Chance: 0.25f),
        };

        if (!BiomeData.Get(cell.Biome).Passable) return placed; // No animals in the sea

        foreach (var (type, names, chance) in animalTypes)
        {
            if (cellRng.NextDouble() > chance) continue;

            var name   = names[cellRng.Next(names.Length)];
            var tileX  = cellRng.Next(WorldCell.TilesPerSide);
            var tileY  = cellRng.Next(WorldCell.TilesPerSide);

            placed.Add(new AnimalPlacement
            {
                AnimalType = type,
                Name       = name,
                TileX      = tileX,
                TileY      = tileY,
                CellId     = cell.CellId
            });
        }

        return placed;
    }

    // -------------------------------------------------------------------------
    // Tile generation per biome
    // -------------------------------------------------------------------------

    private static Tile GenerateTile(int x, int y, BiomeType biome, Random rng)
    {
        return new Tile
        {
            TileX   = x,
            TileY   = y,
            Surface = SelectSurface(biome, rng)
        };
    }

    private static TileSurface SelectSurface(BiomeType biome, Random rng) => biome switch
    {
        BiomeType.Wilderness => rng.NextDouble() < 0.1 ? TileSurface.Rock  : TileSurface.Ground,
        BiomeType.Desert     => rng.NextDouble() < 0.2 ? TileSurface.Rock  : TileSurface.Sand,
        BiomeType.River      => rng.NextDouble() < 0.4 ? TileSurface.Water : TileSurface.Grass,
        BiomeType.Sea        => TileSurface.Water,
        BiomeType.Forest     => rng.NextDouble() < 0.5
                                    ? (rng.NextDouble() < 0.5 ? TileSurface.TreeCedar : TileSurface.TreeOlive)
                                    : TileSurface.Ground,
        BiomeType.Grove      => rng.NextDouble() < 0.4
                                    ? (rng.NextDouble() < 0.5 ? TileSurface.TreeFig : TileSurface.TreePalm)
                                    : TileSurface.Grass,
        BiomeType.Mountain   => rng.NextDouble() < 0.6 ? TileSurface.Stone : TileSurface.Rock,
        BiomeType.Valley     => rng.NextDouble() < 0.1 ? TileSurface.Water : TileSurface.Grass,
        _                    => TileSurface.Ground
    };

    private static Tile FindCaveTile(WorldCell cell, BiomeType biome, Random rng)
    {
        // Prefer stone/rock tiles for caves in mountain biome
        if (biome == BiomeType.Mountain)
        {
            var stoneTiles = new List<Tile>();
            foreach (var t in cell.AllTiles())
                if (t.Surface == TileSurface.Stone || t.Surface == TileSurface.Rock)
                    stoneTiles.Add(t);
            if (stoneTiles.Count > 0)
                return stoneTiles[rng.Next(stoneTiles.Count)];
        }

        // Otherwise pick any non-water tile
        var candidates = new List<Tile>();
        foreach (var t in cell.AllTiles())
            if (t.Surface != TileSurface.Water)
                candidates.Add(t);

        return candidates.Count > 0
            ? candidates[rng.Next(candidates.Count)]
            : cell.Tiles[0, 0];
    }

    // -------------------------------------------------------------------------
    // Biome selection
    // -------------------------------------------------------------------------

    private static BiomeType SelectBiome(Random rng)
    {
        var roll  = rng.NextDouble();
        var cumul = 0f;
        foreach (var (biome, weight) in BiomeWeights)
        {
            cumul += weight;
            if (roll < cumul) return biome;
        }
        return BiomeType.Wilderness;
    }

    private static int HashCoords(int x, int y, int seed) =>
        HashCode.Combine(x, y, seed);
}

public class AnimalPlacement
{
    public string AnimalType { get; set; } = string.Empty;
    public string Name       { get; set; } = string.Empty;
    public int    TileX      { get; set; }
    public int    TileY      { get; set; }
    public string CellId     { get; set; } = string.Empty;
}
