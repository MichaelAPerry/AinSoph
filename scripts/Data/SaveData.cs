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

    // What the Council has granted this NPC
    public List<AinSoph.Skills.Gift> Gifts { get; set; } = new();
    public DateTime? LastMendedUtc { get; set; }
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
    public DateTime? SleepStartUtc { get; set; } // asleep when they logged out — logout is sleep

    // Play time — for rib tracking
    public double AccumulatedPlayHours { get; set; }

    // Tribe — the rib, once earned, and what it became
    public bool          HasRib              { get; set; }
    public string?       SpouseNpcId         { get; set; }
    public List<string>  ProgenyIds          { get; set; } = new();
    public DateTime?     LastProgenyBirthUtc { get; set; }

    // Inventory — item ids
    public List<string> InventoryItemIds { get; set; } = new();

    // Skills the player has acquired
    public List<string> SkillIds { get; set; } = new();

    // What the Council has granted — each one changes a rule (Gift.cs)
    public List<AinSoph.Skills.Gift> Gifts { get; set; } = new();
    public DateTime? LastMendedUtc { get; set; }

    // What the player carries, any law they broke, and whether the Council has come to them
    public List<CarriedItem> Carried { get; set; } = new();
    public string?   LawBroken     { get; set; }
    public DateTime? LawBrokenUtc  { get; set; }
    public bool      EncounterDone { get; set; }
}

/// <summary>A thing carried: the item as it lay in the world, and when it was picked up (food still spoils).</summary>
public class CarriedItem
{
    public ItemSaveData Item        { get; set; } = new();
    public DateTime     PickedUpUtc { get; set; }

    public bool Spoiled(DateTime nowUtc) =>
        Item.LifespanHours is { } life && (Item.AgeHours ?? 0) + (nowUtc - PickedUpUtc).TotalHours >= life;
}

/// <summary>
/// Animals — one file (animals.json): every living animal, and which cells
/// have already received their starting animals (so they are only placed once).
/// </summary>
public class AnimalsSaveData
{
    public List<AnimalSaveData> Animals        { get; set; } = new();
    public List<string>         PopulatedCells { get; set; } = new();
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

    // Rules the Council has approved — world physics added by the people in it (RULES.md)
    public List<LawRecord> Laws  { get; set; } = new();

    // What the gods have done to this world (GameRoot.Divine.cs)
    public List<SpeciesRecord> Species { get; set; } = new();
    public List<TerrainEdit>   Terrain { get; set; } = new();
    public List<OmenRecord>    Omens   { get; set; } = new();
    public string?   Season         { get; set; }
    public DateTime? SeasonUntilUtc { get; set; }
}

/// <summary>A species the gods made.</summary>
public class SpeciesRecord
{
    public string Name     { get; set; } = string.Empty;
    public string Type     { get; set; } = "Neutral";
    public string Habitat  { get; set; } = "Land";
    public bool   Edible   { get; set; }
    public int    Glyph    { get; set; }
    public string Tint     { get; set; } = "ffffff";
    public int    Strength { get; set; }
    public bool   Hunts    { get; set; }
    public bool   Unique   { get; set; }
    public string Look     { get; set; } = string.Empty;
}

/// <summary>One tile of land the gods changed.</summary>
public class TerrainEdit
{
    public int    X     { get; set; }
    public int    Y     { get; set; }
    public string Biome { get; set; } = string.Empty;
}

/// <summary>What the gods did, in the words the world remembers — given to every NPC.</summary>
public class OmenRecord
{
    public string   Proclamation { get; set; } = string.Empty;
    public string   Deed         { get; set; } = string.Empty;
    public DateTime Utc          { get; set; }
}

public class LawRecord
{
    public string   Name        { get; set; } = string.Empty;
    public string   Description { get; set; } = string.Empty;
    public string   CreatedBy   { get; set; } = string.Empty;
    public DateTime ApprovedUtc { get; set; }
}
