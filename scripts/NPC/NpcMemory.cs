namespace AinSoph.NPC;

/// <summary>
/// The four memory slots carried by every NPC.
/// Slots are strings, max 1024 characters each.
/// The NPC's LLM decides what is worth writing into them.
/// Memory travels with the NPC across spheres.
/// </summary>
public class NpcMemory
{
    public const int MaxSlotLength = 1024;

    /// <summary>Instinct, survival, what the NPC wants at the body level.</summary>
    public string Will { get; set; } = string.Empty;

    /// <summary>What the NPC knows, believes, has concluded.</summary>
    public string Thought { get; set; } = string.Empty;

    /// <summary>Emotional state, relationships, what the NPC cares about.</summary>
    public string Feeling { get; set; } = string.Empty;

    /// <summary>What the NPC has done — their history of deeds.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Write a value into a slot. Truncates to MaxSlotLength.
    /// </summary>
    public void Write(MemorySlot slot, string value)
    {
        var truncated = value.Length > MaxSlotLength
            ? value[..MaxSlotLength]
            : value;

        switch (slot)
        {
            case MemorySlot.Will:    Will    = truncated; break;
            case MemorySlot.Thought: Thought = truncated; break;
            case MemorySlot.Feeling: Feeling = truncated; break;
            case MemorySlot.Action:  Action  = truncated; break;
        }
    }

    public string Read(MemorySlot slot) => slot switch
    {
        MemorySlot.Will    => Will,
        MemorySlot.Thought => Thought,
        MemorySlot.Feeling => Feeling,
        MemorySlot.Action  => Action,
        _ => string.Empty
    };
}

public enum MemorySlot
{
    Will,
    Thought,
    Feeling,
    Action
}
