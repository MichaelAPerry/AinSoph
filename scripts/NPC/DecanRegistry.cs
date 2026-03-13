using System.Text.Json;
using Godot;

namespace AinSoph.NPC;

/// <summary>
/// Loads and indexes all 72 decan seeds from data/ain_soph_72.json.
/// Singleton. Call Load() once at startup.
/// </summary>
public static class DecanRegistry
{
    private static readonly Dictionary<string, DecanSeed> _byId = new();
    private static readonly List<DecanSeed> _all = new();
    private static bool _loaded;

    public static void Load(string jsonPath)
    {
        var json = Godot.FileAccess.GetFileAsString(jsonPath);
        if (string.IsNullOrEmpty(json))
        {
            GD.PrintErr($"DecanRegistry: could not read {jsonPath}");
            return;
        }

        var seeds = JsonSerializer.Deserialize<List<DecanSeed>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (seeds is null)
        {
            GD.PrintErr("DecanRegistry: failed to parse ain_soph_72.json");
            return;
        }

        _all.Clear();
        _byId.Clear();

        foreach (var seed in seeds)
        {
            _all.Add(seed);
            _byId[seed.Id] = seed;
        }

        _loaded = true;
        GD.Print($"DecanRegistry: loaded {_all.Count} decans");
    }

    public static DecanSeed? Get(string id) =>
        _byId.TryGetValue(id, out var seed) ? seed : null;

    /// <summary>Draw a random decan from the pool (for progeny assignment).</summary>
    public static DecanSeed DrawRandom()
    {
        if (!_loaded || _all.Count == 0)
            throw new InvalidOperationException("DecanRegistry not loaded");
        return _all[GD.RandRange(0, _all.Count - 1)];
    }

    public static IReadOnlyList<DecanSeed> All => _all;
}
