using AinSoph.NPC;
using Godot;

namespace AinSoph.Data;

/// <summary>
/// Manages routes between player worlds.
/// A route is a mutual agreement — both players must consent.
/// When open, up to 1/10th of the NPC population migrates.
///
/// Migration packets are JSON files the players exchange by any means.
/// Export → send to other player → they Import.
/// Imported NPCs are permanently marked as foreigners.
/// </summary>
public class RouteManager
{
    private readonly SaveManager    _save;
    private readonly List<NpcBrain> _worldNpcs;

    public RouteManager(SaveManager save, List<NpcBrain> worldNpcs)
    {
        _save      = save;
        _worldNpcs = worldNpcs;
    }

    // ── Export ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Select up to 1/10th of the NPC population at random and write them
    /// to a migration packet file. Removes them from the local world.
    /// Returns the number of migrants exported.
    /// </summary>
    public int ExportMigrants(string outputPath)
    {
        if (_worldNpcs.Count == 0)
        {
            GD.Print("RouteManager: no NPCs to export");
            return 0;
        }

        var maxMigrants = Math.Max(1, _worldNpcs.Count / 10);
        var rng         = new Random();
        var pool        = _worldNpcs.OrderBy(_ => rng.Next()).Take(maxMigrants).ToList();

        var saveData = pool.Select(npc => new NpcSaveData
        {
            Id            = npc.NpcId,
            DecanId       = npc.Decan.Id,
            Name          = npc.Decan.Name,
            State         = "idle",
            MemoryWill    = npc.Memory.Will,
            MemoryThought = npc.Memory.Thought,
            MemoryFeeling = npc.Memory.Feeling,
            MemoryAction  = npc.Memory.Action,
            LastAteUtc    = npc.Survival.LastAteUtc,
            LastSleptUtc  = npc.Survival.LastSleptUtc,
            BrokenMove    = npc.BrokenMove,
            BrokenSee     = npc.BrokenSee,
            BrokenHear    = npc.BrokenHear,
            BrokenTalk    = npc.BrokenTalk,
            IsForeigner   = false, // they are native until they cross
            Lineage       = new List<string>()
        }).ToList();

        _save.ExportMigrationPacket(saveData, outputPath);

        foreach (var npc in pool)
            _worldNpcs.Remove(npc);

        GD.Print($"RouteManager: {pool.Count} exported — {_worldNpcs.Count} remain");
        return pool.Count;
    }

    // ── Import ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Import migrants from a packet file.
    /// All imported NPCs are permanently marked as foreigners.
    /// Lineage is appended with origin world and date.
    /// Returns the list of save records for the caller to instantiate as live NPCs.
    /// </summary>
    public List<NpcSaveData> ImportMigrants(string packetPath, string originWorldName,
        int spawnTileX, int spawnTileY)
    {
        var packet = _save.ImportMigrationPacket(packetPath);
        if (packet is null) return new List<NpcSaveData>();

        foreach (var npc in packet.Npcs)
        {
            // Permanent foreigner status — set here, never cleared
            npc.IsForeigner = true;

            // Namespace the id to avoid collision with any existing NPC
            npc.Id = $"foreign.{originWorldName}.{npc.Id}";

            // Append-only lineage
            npc.Lineage.Add($"migrated-from:{originWorldName}:{packet.ExportedUtc:yyyy-MM-dd}");

            // Spawn near the border — caller passes a tile near the edge
            npc.TileX  = spawnTileX + new Random().Next(-2, 3);
            npc.TileY  = spawnTileY + new Random().Next(-2, 3);
            npc.CellId = $"{npc.TileX / 8},{npc.TileY / 8}";

            // Reset survival clock — they arrive hungry and tired after crossing
            npc.LastAteUtc   = DateTime.UtcNow - TimeSpan.FromHours(20);
            npc.LastSleptUtc = DateTime.UtcNow - TimeSpan.FromHours(20);

            _save.SaveNpc(npc);
        }

        GD.Print($"RouteManager: {packet.Npcs.Count} arrived from {originWorldName} as foreigners");
        return packet.Npcs;
    }
}
