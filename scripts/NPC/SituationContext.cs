namespace AinSoph.NPC;

/// <summary>
/// Everything the NPC can perceive right now.
/// Built by the engine each think tick and handed to the LLM.
/// This is transient — it describes the present moment only.
/// History lives in the NPC's memory slots.
///
/// Vision: 3 cells in daylight, 1 at night.
/// Same information a PC would have.
/// </summary>
public class SituationContext
{
    /// <summary>Real-world local time, for day/night and survival awareness.</summary>
    public DateTime LocalTime { get; set; }

    public bool IsNight { get; set; }

    // --- Survival state ---
    public double HoursSinceAte   { get; set; }
    public double HoursSinceSlept { get; set; }
    public bool   IsHungry        { get; set; } // hour 23+
    public bool   IsExhausted     { get; set; } // hour 23+
    public bool   IsInCave        { get; set; }

    // --- What's nearby ---
    public List<NearbyEntity> VisibleEntities { get; set; } = new();
    public List<NearbyItem>   VisibleItems    { get; set; } = new();

    /// <summary>The NPC's current cell coordinates.</summary>
    public string CurrentCell { get; set; } = string.Empty;
}

public class NearbyEntity
{
    public string Id       { get; set; } = string.Empty;
    public string Type     { get; set; } = string.Empty; // "pc" | "npc" | "animal"
    public string Name     { get; set; } = string.Empty;
    public string CellId   { get; set; } = string.Empty;
    public bool   IsSleeping { get; set; }
}

public class NearbyItem
{
    public string Id     { get; set; } = string.Empty;
    public string Name   { get; set; } = string.Empty;
    public bool   Edible { get; set; }
    public string CellId { get; set; } = string.Empty;
}
