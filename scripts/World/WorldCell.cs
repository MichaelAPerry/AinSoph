namespace AinSoph.World;

/// <summary>
/// A sovereign cell in the world grid.
/// 8×8 tiles. Each tile is 32×32 pixels.
/// Generated on demand. Only cells near a player need to exist.
/// </summary>
public class WorldCell
{
    public const int TilesPerSide = 8;
    public const int TilePixels   = 32;
    public const int CellPixels   = TilesPerSide * TilePixels; // 256

    public int       GridX   { get; set; }
    public int       GridY   { get; set; }
    public BiomeType Biome   { get; set; }
    public bool      Generated { get; set; }

    // 8×8 tile array — index as Tiles[x, y]
    public Tile[,] Tiles { get; set; } = new Tile[TilesPerSide, TilesPerSide];

    // Cave id if this cell has one, null otherwise
    public string? CaveId { get; set; }

    public string CellId => $"{GridX},{GridY}";

    public Tile GetTile(int x, int y) => Tiles[x, y];

    public bool HasCave => CaveId is not null;

    /// <summary>All tiles as a flat enumerable.</summary>
    public IEnumerable<Tile> AllTiles()
    {
        for (var x = 0; x < TilesPerSide; x++)
        for (var y = 0; y < TilesPerSide; y++)
            yield return Tiles[x, y];
    }
}
