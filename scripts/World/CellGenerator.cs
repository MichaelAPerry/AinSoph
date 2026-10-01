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
    /// The cell's biome is the biome at its centre in a continuous region map
    /// (see BiomeAt); each tile takes the biome at its own, slightly warped,
    /// position — so neighbouring cells share regions and borders are organic,
    /// not a grid of squares. Consistent for a given seed + coords.
    /// </summary>
    public WorldCell Generate(int gridX, int gridY)
    {
        // Seed per-cell rng from world seed + coords only, so a cell is identical
        // no matter when or in what order it is generated
        var cellRng = new Random(HashCoords(gridX, gridY, _worldSeed));

        var biome = BiomeAt(gridX + 0.5, gridY + 0.5);
        var profile = BiomeData.Get(biome);

        var cell = new WorldCell
        {
            GridX     = gridX,
            GridY     = gridY,
            Biome     = biome,
            Generated = true
        };

        // Place tiles — each tile samples the region map at a jittered position
        for (var x = 0; x < WorldCell.TilesPerSide; x++)
        for (var y = 0; y < WorldCell.TilesPerSide; y++)
        {
            double wx = gridX + (x + 0.5) / WorldCell.TilesPerSide;
            double wy = gridY + (y + 0.5) / WorldCell.TilesPerSide;
            // Fine warp (~3 tiles) roughens borders at the tile scale
            wx += (ValueNoise(wx * 1.6, wy * 1.6, 11) - 0.5) * 0.7;
            wy += (ValueNoise(wx * 1.6, wy * 1.6, 12) - 0.5) * 0.7;
            cell.Tiles[x, y] = GenerateTile(x, y, BiomeAt(wx, wy), cellRng,
                                            gridX + (x + 0.5) / WorldCell.TilesPerSide,
                                            gridY + (y + 0.5) / WorldCell.TilesPerSide);
        }

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

    private Tile GenerateTile(int x, int y, BiomeType biome, Random rng, double wx, double wy)
    {
        return new Tile
        {
            TileX   = x,
            TileY   = y,
            Biome   = biome,
            Surface = SelectSurface(biome, rng, wx, wy)
        };
    }

    /// <summary>
    /// Surface by biome. Water and trees follow smooth noise over the world
    /// (wx, wy in cell units), so rivers wind, ponds pool and trees stand in
    /// groves — instead of every tile flipping its own coin.
    /// </summary>
    private TileSurface SelectSurface(BiomeType biome, Random rng, double wx, double wy)
    {
        switch (biome)
        {
            case BiomeType.River:
                // A river is a contour line of slow noise: a winding band of water
                var n = ValueNoise(wx * 0.55, wy * 0.55, 41) * 0.75 + ValueNoise(wx * 1.6, wy * 1.6, 42) * 0.25;
                return Math.Abs(n - 0.5) < 0.055 ? TileSurface.Water : TileSurface.Grass;

            case BiomeType.Valley:
                return ValueNoise(wx * 1.4, wy * 1.4, 43) > 0.80 ? TileSurface.Water : TileSurface.Grass;

            case BiomeType.Forest:
                if (ValueNoise(wx * 2.2, wy * 2.2, 44) > 0.38 && rng.NextDouble() < 0.85)
                    return rng.NextDouble() < 0.5 ? TileSurface.TreeCedar : TileSurface.TreeOlive;
                return TileSurface.Ground;

            case BiomeType.Grove:
                if (ValueNoise(wx * 2.6, wy * 2.6, 45) > 0.55 && rng.NextDouble() < 0.75)
                    return rng.NextDouble() < 0.5 ? TileSurface.TreeFig : TileSurface.TreePalm;
                return TileSurface.Grass;

            case BiomeType.Mountain:
                return ValueNoise(wx * 2.0, wy * 2.0, 46) > 0.45 ? TileSurface.Stone : TileSurface.Rock;

            case BiomeType.Desert:
                return ValueNoise(wx * 2.4, wy * 2.4, 47) > 0.78 ? TileSurface.Rock : TileSurface.Sand;

            case BiomeType.Wilderness:
                return ValueNoise(wx * 2.4, wy * 2.4, 48) > 0.80 ? TileSurface.Rock : TileSurface.Ground;

            default:
                return SelectSurface(biome, rng);
        }
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

    // -------------------------------------------------------------------------
    // Region map — biomes as regions, not per-cell coin flips
    // -------------------------------------------------------------------------

    private const double RegionSpacing = 3.0; // cells between region centres

    /// <summary>
    /// The biome at a point, in cell units. Region centres sit on a jittered
    /// lattice (Voronoi); each region's biome is a weighted draw, so the
    /// weights still describe how much of the world each biome covers.
    /// A coarse warp bends the region borders into natural shapes.
    /// </summary>
    public BiomeType BiomeAt(double x, double y)
    {
        // Coarse warp (~1 cell) so borders curve
        double wx = x + (ValueNoise(x * 0.3, y * 0.3, 21) - 0.5) * 2.0;
        double wy = y + (ValueNoise(x * 0.3, y * 0.3, 22) - 0.5) * 2.0;

        int gx = (int)Math.Floor(wx / RegionSpacing), gy = (int)Math.Floor(wy / RegionSpacing);
        double best = double.MaxValue;
        int bx = gx, by = gy;
        for (int i = -1; i <= 1; i++)
        for (int j = -1; j <= 1; j++)
        {
            int cx = gx + i, cy = gy + j;
            double px = (cx + 0.15 + 0.7 * Hash01(cx, cy, 31)) * RegionSpacing;
            double py = (cy + 0.15 + 0.7 * Hash01(cx, cy, 32)) * RegionSpacing;
            double d  = (px - wx) * (px - wx) + (py - wy) * (py - wy);
            if (d < best) { best = d; bx = cx; by = cy; }
        }
        return SelectBiome(Hash01(bx, by, 33));
    }

    /// <summary>Smooth noise in [0,1], deterministic for the world seed.</summary>
    private double ValueNoise(double x, double y, int channel)
    {
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        double fx = x - x0, fy = y - y0;
        double sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
        double a = Hash01(x0, y0, channel),     b = Hash01(x0 + 1, y0, channel);
        double c = Hash01(x0, y0 + 1, channel), d = Hash01(x0 + 1, y0 + 1, channel);
        return (a + (b - a) * sx) + ((c + (d - c) * sx) - (a + (b - a) * sx)) * sy;
    }

    private double Hash01(int x, int y, int channel) =>
        HashCoords(x, y, _worldSeed ^ (channel * 0x632BE5AB)) / (double)int.MaxValue;

    private static BiomeType SelectBiome(double roll)
    {
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
