namespace AinSoph.World;

/// <summary>
/// The live world grid. Cells are generated on demand as players move.
/// Only cells near a player need to exist at any moment.
/// The grid is theoretically unbounded — no edge.
///
/// Generation radius: cells within 3 grid squares of any player are kept live.
/// Cells outside that range can be serialized and unloaded.
/// </summary>
public class WorldGrid
{
    private readonly Dictionary<string, WorldCell> _cells = new();
    private readonly CellGenerator _generator;

    public const int LoadRadius = 3; // cells in each direction around a player

    public WorldGrid(int worldSeed = 0)
    {
        _generator = new CellGenerator(worldSeed);
    }

    // -------------------------------------------------------------------------
    // Cell access — generates on demand
    // -------------------------------------------------------------------------

    public WorldCell GetOrGenerate(int gridX, int gridY)
    {
        var key = CellKey(gridX, gridY);
        if (_cells.TryGetValue(key, out var existing))
            return existing;

        var cell = _generator.Generate(gridX, gridY);
        _cells[key] = cell;
        return cell;
    }

    public WorldCell? GetIfLoaded(int gridX, int gridY) =>
        _cells.TryGetValue(CellKey(gridX, gridY), out var cell) ? cell : null;

    public bool IsLoaded(int gridX, int gridY) =>
        _cells.ContainsKey(CellKey(gridX, gridY));

    // -------------------------------------------------------------------------
    // Load zone — ensure all cells around a player are generated
    // -------------------------------------------------------------------------

    public void EnsureLoaded(int centerX, int centerY, int radius = LoadRadius)
    {
        for (var dx = -radius; dx <= radius; dx++)
        for (var dy = -radius; dy <= radius; dy++)
            GetOrGenerate(centerX + dx, centerY + dy);
    }

    // -------------------------------------------------------------------------
    // Manna spawn — called each morning by the world tick
    // -------------------------------------------------------------------------

    public Dictionary<string, List<(int TileX, int TileY)>> SpawnMorningManna()
    {
        var result = new Dictionary<string, List<(int, int)>>();
        foreach (var (key, cell) in _cells)
        {
            var spawned = _generator.SpawnManna(cell);
            if (spawned.Count > 0)
                result[key] = spawned;
        }
        return result;
    }

    // -------------------------------------------------------------------------
    // Player start — random cell within 3 of a cave
    // -------------------------------------------------------------------------

    /// <summary>
    /// <summary>Find a cave cell within the given radius. Returns null if none found.</summary>
    public WorldCell? FindCaveCell(Random rng, int radius = 3)
    {
        var candidates = new System.Collections.Generic.List<WorldCell>();
        for (var dx = -radius; dx <= radius; dx++)
        for (var dy = -radius; dy <= radius; dy++)
        {
            var cell = GetOrGenerate(dx, dy);
            if (cell.HasCave) candidates.Add(cell);
        }
        if (candidates.Count == 0) return null;
        return candidates[rng.Next(candidates.Count)];
    }

    /// Find a valid player start position: a random cell within 3 grid squares of a cave.
    /// Generates cells as needed until a valid start is found.
    /// </summary>
    public (int GridX, int GridY) FindPlayerStart()
    {
        // Search outward from origin in expanding rings until we find a cave neighbor
        for (var radius = 0; radius <= 20; radius++)
        for (var dx = -radius; dx <= radius; dx++)
        for (var dy = -radius; dy <= radius; dy++)
        {
            if (Math.Abs(dx) != radius && Math.Abs(dy) != radius) continue; // ring only

            var cell = GetOrGenerate(dx, dy);
            if (!cell.HasCave) continue;

            // Found a cave — place the player within 3 cells of it
            var startX = dx + new Random().Next(-3, 4);
            var startY = dy + new Random().Next(-3, 4);
            GetOrGenerate(startX, startY); // ensure generated
            return (startX, startY);
        }

        // Fallback — should not happen in practice
        return (0, 0);
    }

    // -------------------------------------------------------------------------
    // Neighbours
    // -------------------------------------------------------------------------

    public IEnumerable<WorldCell> GetNeighbours(int gridX, int gridY)
    {
        for (var dx = -1; dx <= 1; dx++)
        for (var dy = -1; dy <= 1; dy++)
        {
            if (dx == 0 && dy == 0) continue;
            var neighbour = GetIfLoaded(gridX + dx, gridY + dy);
            if (neighbour is not null) yield return neighbour;
        }
    }

    public IReadOnlyCollection<WorldCell> LoadedCells => _cells.Values;

    private static string CellKey(int x, int y) => $"{x},{y}";
}
