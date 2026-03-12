using System.Text.Json.Serialization;

namespace AinSoph.NPC;

/// <summary>
/// What the NPC's LLM returns on each think tick.
/// The engine reads this and drives the NPC's state accordingly.
/// </summary>
public class NpcDecision
{
    /// <summary>The state the NPC is moving into.</summary>
    [JsonPropertyName("state")]
    public string State { get; set; } = "idle";

    /// <summary>Natural language — what the NPC says aloud if talking, or internal if not.</summary>
    [JsonPropertyName("speech")]
    public string Speech { get; set; } = string.Empty;

    /// <summary>Target entity id if talking, moving toward, or attacking.</summary>
    [JsonPropertyName("target_id")]
    public string TargetId { get; set; } = string.Empty;

    /// <summary>Target cell if moving.</summary>
    [JsonPropertyName("target_cell")]
    public string TargetCell { get; set; } = string.Empty;

    /// <summary>Item id to eat if eating.</summary>
    [JsonPropertyName("eat_item_id")]
    public string EatItemId { get; set; } = string.Empty;

    /// <summary>If creating — what kind: "skill" | "item" | "rule".</summary>
    [JsonPropertyName("creation_type")]
    public string CreationType { get; set; } = string.Empty;

    /// <summary>Brief description of the creation the NPC has in mind.</summary>
    [JsonPropertyName("creation_intent")]
    public string CreationIntent { get; set; } = string.Empty;

    /// <summary>What the NPC chooses to write into their memory slots this tick. Null = no change.</summary>
    [JsonPropertyName("memory_updates")]
    public MemoryUpdates? MemoryUpdates { get; set; }

    public NpcState ParsedState => State.ToLowerInvariant() switch
    {
        "moving"   => NpcState.Moving,
        "eating"   => NpcState.Eating,
        "sleeping" => NpcState.Sleeping,
        "creating" => NpcState.Creating,
        "talking"  => NpcState.Talking,
        "praying"  => NpcState.Praying,
        _          => NpcState.Idle
    };
}

public class MemoryUpdates
{
    [JsonPropertyName("will")]    public string? Will    { get; set; }
    [JsonPropertyName("thought")] public string? Thought { get; set; }
    [JsonPropertyName("feeling")] public string? Feeling { get; set; }
    [JsonPropertyName("action")]  public string? Action  { get; set; }
}
