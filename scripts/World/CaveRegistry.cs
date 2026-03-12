namespace AinSoph.World;

/// <summary>
/// Tracks cave occupancy across the world.
/// A cave can hold exactly one character at a time.
/// Entering claims it. Leaving frees it immediately.
/// Safe sleep is only possible inside a cave.
/// </summary>
public class CaveRegistry
{
    // Key: cave id. Value: occupant entity id.
    private readonly Dictionary<string, string> _occupants = new();

    /// <summary>
    /// Attempt to enter a cave.
    /// Returns true if the cave was free and is now claimed.
    /// Returns false if the cave is already occupied.
    /// </summary>
    public bool TryEnter(string caveId, string entityId)
    {
        if (_occupants.TryGetValue(caveId, out var current))
        {
            // Already occupied — deny entry unless it's the same entity
            return current == entityId;
        }

        _occupants[caveId] = entityId;
        return true;
    }

    /// <summary>
    /// Leave a cave. Frees it immediately for others.
    /// Only the current occupant can free it.
    /// </summary>
    public void Leave(string caveId, string entityId)
    {
        if (_occupants.TryGetValue(caveId, out var current) && current == entityId)
            _occupants.Remove(caveId);
    }

    public bool IsOccupied(string caveId) => _occupants.ContainsKey(caveId);

    public string? GetOccupant(string caveId) =>
        _occupants.TryGetValue(caveId, out var id) ? id : null;

    public bool IsOccupiedBy(string caveId, string entityId) =>
        _occupants.TryGetValue(caveId, out var id) && id == entityId;
}
