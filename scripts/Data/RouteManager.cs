using AinSoph.NPC;
using Godot;

namespace AinSoph.Data;

/// <summary>
/// Manages routes between player worlds.
/// A route is a mutual agreement — both players must consent.
/// When open, up to 1/10th of the border population migrates
/// automatically in each direction.
///
/// Migration packets are JSON files the players exchange.
/// The handshake is: Player A exports -> sends to Player B -> Player B imports.
/// Both directions run independently.
/// </summary>
public class RouteManager
{
    private readonly SaveManager _save;
    private readonly List<NpcBrain> _worldNpcs; // reference to the live NPC list

    public RouteManager(SaveManager save, List<NpcBrain> worldNpcs)
    {
        _save      = save;
        _worldNpcs = worldNpcs;
    }

    // -------------------------------------------------------------------------
    // Export — this player opens their side of the route
    // -------------------------------------------------------------------------

    /// <summary>
    /// Select up to 1/10th of the NPC population near the border at random
    /// and export them to a migration packet file.
    /// The packet file is then sent to the other player by any means
    /// (file share, USB, network — the game does not prescribe how).
    /// </summary>
    public string ExportMigrants(string outputPath)
    {
        if (_worldNpcs.Count == 0)
        {
            GD.Print("RouteManager: no NPCs to migrate");
            return outputPath;
        }

        var maxMigrants = Math.Max(1, _worldNpcs.Count / 10);
        var rng         = new Random();

        // Shuffle and take up to the cap
        var pool = _worldNpcs.OrderBy(_ => rng.Next()).Take(maxMigrants).ToList();

        var saveData = pool.Select(npc => new NpcSaveData
        {
            Id            = npc.NpcId,
            DecanId       = npc.Decan.Id,
            State         = npc.State.ToString().ToLower(),
            MemoryWill    = npc.Memory.Will,
            MemoryThought = npc.Memory.Thought,
            MemoryFeeling = npc.Memory.Feeling,
            MemoryAction  = npc.Memory.Action,
            LastAteUtc    = npc.Survival.LastAteUtc,
            LastSleptUtc  = npc.Survival.LastSleptUtc,
            Lineage       = new List<string>() // lineage appended by the receiving world
        }).ToList();

        _save.ExportMigrationPacket(saveData, outputPath);

        // Remove migrants from the local world
        foreach (var npc in pool)
            _worldNpcs.Remove(npc);

        GD.Print($"RouteManager: {pool.Count} NPCs exported. " +
                 $"{_worldNpcs.Count} remain in this world.");

        return outputPath;
    }

    // -------------------------------------------------------------------------
    // Import — receive migrants from another world
    // -------------------------------------------------------------------------

    /// <summary>
    /// Import migrants from a packet file produced by the other player's ExportMigrants.
    /// Decan and all four memory slots arrive intact.
    /// Lineage is appended with the origin world marker.
    /// </summary>
    public List<NpcSaveData> ImportMigrants(string packetPath, string originWorldName)
    {
        var packet = _save.ImportMigrationPacket(packetPath);
        if (packet is null) return new List<NpcSaveData>();

        foreach (var npc in packet.Npcs)
        {
            // Append origin to lineage — read only and append only from here
            npc.Lineage.Add($"migrated-from:{originWorldName}:{packet.ExportedUtc:yyyy-MM-dd}");

            // Save each incoming NPC into this world
            _save.SaveNpc(npc);
        }

        GD.Print($"RouteManager: {packet.Npcs.Count} NPCs arrived from {originWorldName}");
        return packet.Npcs;
    }
}
