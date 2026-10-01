namespace AinSoph.World;

/// <summary>
/// Generates a WorldCell on demand from its grid coordinates.
/// Biome is determined by a noise-based selector.
/// Tiles, caves, and initial items (manna) are placed at generation time.
/// Animals are placed randomly after generation.
/// </summary>
public class CellGenerator
{
    private readonly int _worldSeed;

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
        _worldSeed = worldSeed == 0 ? new Random().Next() : worldSeed;
    }

    /// <summary>
    /// Generate a cell at the given grid coordinates.
    /// Biome is selected by weighted random — consistent for a given seed + coords.
    /// </summary>
    public WorldCell Generate(int gridX, int gridY)
    {
        // Seed per-cell rng from world seed + coords only, so a cell is identical
        // no matter when or in what order it is generated
        var cellRng = new Random(HashCoords(gridX, gridY, _worldSeed));

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
    /// Place the starting animals of a cell. Called once per cell per world.
    /// Deterministic for a world seed. Each group of species has its own chance;
    /// predators are rare. Fish only on water tiles; land animals and birds on dry land.
    /// </summary>
    public List<AnimalPlacement> PlaceAnimals(WorldCell cell)
    {
        var placed  = new List<AnimalPlacement>();
        var cellRng = new Random(HashCoords(cell.GridX, cell.GridY, _worldSeed ^ 0x51ED));

        var groups = new (Func<AnimalSpecies, bool> Pick, double Chance)[]
        {
            (s => s.Type == NPC.AnimalType.Predator,                                0.06),
            (s => s.Type == NPC.AnimalType.Neutral,                                 0.20),
            (s => s.Type == NPC.AnimalType.Prey && s.Habitat != AnimalHabitat.Water, 0.30),
            (s => s.Type == NPC.AnimalType.Insect,                                  0.15),
            (s => s.Habitat == AnimalHabitat.Water,                                 0.35),
        };

        foreach (var (pick, chance) in groups)
        {
            if (cellRng.NextDouble() > chance) continue;

            var options = AnimalSpecies.All.Where(pick).ToArray();
            var species = options[cellRng.Next(options.Length)];

            var tiles = cell.AllTiles().Where(t => species.CanStandOn(t.Surface) && !t.HasCave).ToList();
            if (tiles.Count == 0) continue; // no water for fish, no land in the sea
            var tile = tiles[cellRng.Next(tiles.Count)];

            placed.Add(new AnimalPlacement
            {
                AnimalType = species.Type.ToString().ToLowerInvariant(),
                Name       = species.Name,
                TileX      = cell.GridX * WorldCell.TilesPerSide + tile.TileX,
                TileY      = cell.GridY * WorldCell.TilesPerSide + tile.TileY,
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

    // Stable across runs — HashCode.Combine is randomized per process
    private static int HashCoords(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)seed * 0x9E3779B1u;
            h ^= (uint)x * 0x85EBCA77u; h = (h << 13) | (h >> 19);
            h ^= (uint)y * 0xC2B2AE3Du; h = (h << 17) | (h >> 15);
            h *= 0x27D4EB2Fu; h ^= h >> 16;
            return (int)(h & 0x7fffffff);
        }
    }
}

public class AnimalPlacement
{
    // TileX/TileY are world tile coordinates
    public string AnimalType { get; set; } = string.Empty;
    public string Name       { get; set; } = string.Empty;
    public int    TileX      { get; set; }
    public int    TileY      { get; set; }
    public string CellId     { get; set; } = string.Empty;
}
