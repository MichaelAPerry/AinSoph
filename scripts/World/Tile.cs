namespace AinSoph.World;

/// <summary>
/// One tile in the 8×8 cell grid. Each tile is 32×32 pixels.
/// A cell is 8 tiles wide and 8 tiles tall — 64 tiles total.
/// </summary>
public class Tile
{
    public int TileX { get; set; } // 0–7 within the cell
    public int TileY { get; set; } // 0–7 within the cell

    public TileSurface Surface  { get; set; }
    public bool         HasCave { get; set; }

    // Items on this tile (item ids)
    public List<string> ItemIds { get; set; } = new();

    // Entities standing on this tile (entity ids)
    public List<string> EntityIds { get; set; } = new();
}

public enum TileSurface
{
    Ground,     // Wilderness, Valley, Grove
    Sand,       // Desert
    Water,      // River, Sea
    Stone,      // Mountain
    Grass,      // Valley floor, River bank
    TreeCedar,  // Forest
    TreeOlive,  // Forest
    TreeFig,    // Grove
    TreePalm,   // Grove
    Rock        // Mountain, Desert outcrops
}
