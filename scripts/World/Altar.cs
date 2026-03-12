namespace AinSoph.World;

/// <summary>
/// The altar. One per world. Placed at generation in a random biome and cell.
/// The altar is the physical gateway to the Triune Council via the Pray primitive.
/// It is not marked. It must be found.
/// </summary>
public class Altar
{
    public string CellId { get; set; } = string.Empty;
    public int    TileX  { get; set; }
    public int    TileY  { get; set; }
    public string Biome  { get; set; } = string.Empty;

    // -------------------------------------------------------------------------
    // Placement — called once at world generation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Place the altar in a random passable cell at a random non-water tile.
    /// </summary>
    public static Altar Place(WorldGrid grid, int worldSeed)
    {
        var rng = new Random(worldSeed ^ 0xA17A4);

        // Try up to 100 candidate cells — pick any passable biome
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var gx   = rng.Next(-50, 51);
            var gy   = rng.Next(-50, 51);
            var cell = grid.GetOrGenerate(gx, gy);

            if (!BiomeData.Get(cell.Biome).Passable) continue;

            // Pick a random non-water tile
            var candidates = new List<Tile>();
            foreach (var tile in cell.AllTiles())
                if (tile.Surface != TileSurface.Water && !tile.HasCave)
                    candidates.Add(tile);

            if (candidates.Count == 0) continue;

            var chosen = candidates[rng.Next(candidates.Count)];

            return new Altar
            {
                CellId = cell.CellId,
                TileX  = chosen.TileX,
                TileY  = chosen.TileY,
                Biome  = cell.Biome.ToString()
            };
        }

        // Fallback — origin cell
        var origin = grid.GetOrGenerate(0, 0);
        return new Altar { CellId = "0,0", TileX = 0, TileY = 0,
            Biome = origin.Biome.ToString() };
    }
}
