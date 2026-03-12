using AinSoph.NPC;
using AinSoph.World;

namespace AinSoph.Data;

/// <summary>
/// Serializable snapshot of a WorldCell.
/// One file per cell on disk: cells/{x},{y}.json
/// </summary>
public class CellSaveData
{
    public int       GridX   { get; set; }
    public int       GridY   { get; set; }
    public string    Biome   { get; set; } = string.Empty;
    public string?   CaveId  { get; set; }
    public string?   CaveOccupant { get; set; } // entity id holding the cave, null if empty
    public List<TileSaveData>   Tiles   { get; set; } = new();
    public List<ItemSaveData>   Items   { get; set; } = new();
    public List<AnimalSaveData> Animals { get; set; } = new();
}

public class TileSaveData
{
    public int    TileX   { get; set; }
    public int    TileY   { get; set; }
    public string Surface { get; set; } = string.Empty;
    public bool   HasCave { get; set; }
}

public class ItemSaveData
{
    public string Id            { get; set; } = string.Empty;
    public string Name          { get; set; } = string.Empty;
    public string Type          { get; set; } = string.Empty;
    public string Description   { get; set; } = string.Empty;
    public bool   Edible        { get; set; }
    public float? LifespanHours { get; set; }
    public float? AgeHours      { get; set; } // how long it's been in the world
    public int    TileX         { get; set; }
    public int    TileY         { get; set; }
}

public class AnimalSaveData
{
    public string Id         { get; set; } = string.Empty;
    public string AnimalType { get; set; } = string.Empty;
    public string Name       { get; set; } = string.Empty;
    public int    TileX      { get; set; }
    public int    TileY      { get; set; }

    // Survival state
    public double HoursSinceAte   { get; set; }
    public double HoursSinceSlept { get; set; }
}

/// <summary>
/// Serializable snapshot of one NPC.
/// Stored inside the cell file that the NPC currently occupies.
/// </summary>
public class NpcSaveData
{
    public string Id      { get; set; } = string.Empty;
    public string DecanId { get; set; } = string.Empty;
    public string Name    { get; set; } = string.Empty;
    public string CellId  { get; set; } = string.Empty;
    public int    TileX   { get; set; }
    public int    TileY   { get; set; }
    public string State   { get; set; } = "idle";

    // The four memory slots — the only persistent NPC state
    public string MemoryWill    { get; set; } = string.Empty;
    public string MemoryThought { get; set; } = string.Empty;
    public string MemoryFeeling { get; set; } = string.Empty;
    public string MemoryAction  { get; set; } = string.Empty;

    // Survival state
    public DateTime LastAteUtc   { get; set; }
    public DateTime LastSleptUtc { get; set; }

    // Birth impairment — true = broken at birth (rolled once, never changes)
    public bool BrokenMove  { get; set; }
    public bool BrokenSee   { get; set; }
    public bool BrokenHear  { get; set; }
    public bool BrokenTalk  { get; set; }

    // Foreigner — arrived via route, permanent
    public bool IsForeigner { get; set; }

    // Lineage — append only, never edited
    public List<string> Lineage { get; set; } = new();
}

/// <summary>
/// Serializable snapshot of the player character.
/// One file: player.json
/// </summary>
public class PlayerSaveData
{
    public string Id      { get; set; } = string.Empty;
    public string Name    { get; set; } = string.Empty;
    public string CellId  { get; set; } = string.Empty;
    public int    TileX   { get; set; }
    public int    TileY   { get; set; }

    // Survival state
    public DateTime LastAteUtc   { get; set; }
    public DateTime LastSleptUtc { get; set; }
    public bool     IsInCave     { get; set; }

    // Play time — for rib tracking
    public double AccumulatedPlayHours { get; set; }

    // Inventory — item ids
    public List<string> InventoryItemIds { get; set; } = new();

    // Skills the player has acquired
    public List<string> SkillIds { get; set; } = new();
}

/// <summary>
/// World-level metadata.
/// One file: world.json
/// </summary>
public class WorldSaveData
{
    public int      WorldSeed    { get; set; }
    public DateTime CreatedUtc   { get; set; }
    public DateTime LastSavedUtc { get; set; }
    public string   WorldName    { get; set; } = string.Empty;
}
