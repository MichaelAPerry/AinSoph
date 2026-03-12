using System.Text.Json;
using Godot;

namespace AinSoph.Data;

/// <summary>
/// Reads and writes the world to disk as JSON.
/// 
/// Structure on disk:
///   {saveDir}/
///     world.json
///     player.json
///     cells/
///       0,0.json
///       1,0.json
///       ...
///     npcs/
///       {npcId}.json
///
/// Save interval: every 5 real minutes.
/// Files are small — one cell is 8x8 tiles plus whatever's on them.
/// </summary>
public class SaveManager
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented    = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions ReadOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly string _saveDir;
    private readonly string _cellsDir;
    private readonly string _npcsDir;
    private readonly string _itemsDir;

    private DateTime _lastSaveUtc = DateTime.MinValue;
    private static readonly TimeSpan SaveInterval = TimeSpan.FromMinutes(5);

    public SaveManager(string saveDir)
    {
        _saveDir  = saveDir;
        _cellsDir = Path.Combine(saveDir, "cells");
        _npcsDir  = Path.Combine(saveDir, "npcs");
        _itemsDir = Path.Combine(saveDir, "items");

        Directory.CreateDirectory(_saveDir);
        Directory.CreateDirectory(_cellsDir);
        Directory.CreateDirectory(_npcsDir);
        Directory.CreateDirectory(_itemsDir);
    }

    // -------------------------------------------------------------------------
    // Tick — call from WorldTick; saves on interval
    // -------------------------------------------------------------------------

    public bool ShouldSave() =>
        (DateTime.UtcNow - _lastSaveUtc) >= SaveInterval;

    public void RecordSave() => _lastSaveUtc = DateTime.UtcNow;

    // -------------------------------------------------------------------------
    // World metadata
    // -------------------------------------------------------------------------

    public void SaveWorld(WorldSaveData data)
    {
        data.LastSavedUtc = DateTime.UtcNow;
        WriteJson(Path.Combine(_saveDir, "world.json"), data);
    }

    public WorldSaveData? LoadWorld()
    {
        var path = Path.Combine(_saveDir, "world.json");
        return File.Exists(path) ? ReadJson<WorldSaveData>(path) : null;
    }

    // -------------------------------------------------------------------------
    // Player
    // -------------------------------------------------------------------------

    public void SavePlayer(PlayerSaveData data) =>
        WriteJson(Path.Combine(_saveDir, "player.json"), data);

    public void DeletePlayer()
    {
        var path = Path.Combine(_saveDir, "player.json");
        if (File.Exists(path)) File.Delete(path);
    }

    public PlayerSaveData? LoadPlayer()
    {
        var path = Path.Combine(_saveDir, "player.json");
        return File.Exists(path) ? ReadJson<PlayerSaveData>(path) : null;
    }

    // -------------------------------------------------------------------------
    // Cells
    // -------------------------------------------------------------------------

    public void SaveCell(CellSaveData data) =>
        WriteJson(CellPath(data.GridX, data.GridY), data);

    public CellSaveData? LoadCell(int gridX, int gridY)
    {
        var path = CellPath(gridX, gridY);
        return File.Exists(path) ? ReadJson<CellSaveData>(path) : null;
    }

    public bool CellExists(int gridX, int gridY) =>
        File.Exists(CellPath(gridX, gridY));

    // -------------------------------------------------------------------------
    // NPCs
    // -------------------------------------------------------------------------

    public void SaveNpc(NpcSaveData data) =>
        WriteJson(NpcPath(data.Id), data);

    public void DeleteNpc(string npcId)
    {
        var path = NpcPath(npcId);
        if (File.Exists(path)) File.Delete(path);
    }

    public NpcSaveData? LoadNpc(string npcId)
    {
        var path = NpcPath(npcId);
        return File.Exists(path) ? ReadJson<NpcSaveData>(path) : null;
    }

    public IEnumerable<NpcSaveData> LoadAllNpcs()
    {
        foreach (var file in Directory.EnumerateFiles(_npcsDir, "*.json"))
        {
            var data = ReadJson<NpcSaveData>(file);
            if (data is not null) yield return data;
        }
    }

    // -------------------------------------------------------------------------
    // Items
    // -------------------------------------------------------------------------

    public void SaveItem(ItemSaveData data) =>
        WriteJson(ItemPath(data.Id), data);

    public void DeleteItem(string itemId)
    {
        var path = ItemPath(itemId);
        if (File.Exists(path)) File.Delete(path);
    }

    public IEnumerable<ItemSaveData> LoadAllItems()
    {
        foreach (var file in Directory.EnumerateFiles(_itemsDir, "*.json"))
        {
            var data = ReadJson<ItemSaveData>(file);
            if (data is not null) yield return data;
        }
    }

    // -------------------------------------------------------------------------
    // Migration export/import — for route handshakes
    // -------------------------------------------------------------------------

    /// <summary>
    /// Export a set of NPCs to a migration packet file.
    /// The receiving world imports this to bring the NPCs in.
    /// </summary>
    public void ExportMigrationPacket(IEnumerable<NpcSaveData> npcs, string outputPath)
    {
        var packet = new MigrationPacket
        {
            ExportedUtc = DateTime.UtcNow,
            Npcs        = npcs.ToList()
        };
        WriteJson(outputPath, packet);
        GD.Print($"SaveManager: exported {packet.Npcs.Count} NPCs to {outputPath}");
    }

    public MigrationPacket? ImportMigrationPacket(string inputPath)
    {
        if (!File.Exists(inputPath))
        {
            GD.PrintErr($"SaveManager: migration packet not found at {inputPath}");
            return null;
        }
        var packet = ReadJson<MigrationPacket>(inputPath);
        GD.Print($"SaveManager: imported {packet?.Npcs.Count ?? 0} NPCs from {inputPath}");
        return packet;
    }

    // -------------------------------------------------------------------------
    // Internal
    // -------------------------------------------------------------------------

    private string CellPath(int x, int y) =>
        Path.Combine(_cellsDir, $"{x},{y}.json");

    private string NpcPath(string npcId) =>
        Path.Combine(_npcsDir, $"{npcId}.json");

    private string ItemPath(string itemId) =>
        Path.Combine(_itemsDir, $"{itemId.Replace("/", "_")}.json");

    private void WriteJson<T>(string path, T data)
    {
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOpts));
        }
        catch (Exception ex)
        {
            GD.PrintErr($"SaveManager: failed to write {path}: {ex.Message}");
        }
    }

    private T? ReadJson<T>(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), ReadOpts);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"SaveManager: failed to read {path}: {ex.Message}");
            return default;
        }
    }
}

public class MigrationPacket
{
    public DateTime         ExportedUtc { get; set; }
    public List<NpcSaveData> Npcs       { get; set; } = new();
}
