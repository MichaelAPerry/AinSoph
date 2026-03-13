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

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex)
        {
            GD.PrintErr($"DecanRegistry: JSON parse error — {ex.Message}");
            return;
        }

        JsonElement root = doc.RootElement;

        // The file has a wrapper: { "personalities": [ ... ] }
        if (!root.TryGetProperty("personalities", out JsonElement arr) ||
            arr.ValueKind != JsonValueKind.Array)
        {
            GD.PrintErr("DecanRegistry: missing 'personalities' array");
            return;
        }

        _all.Clear();
        _byId.Clear();

        foreach (JsonElement entry in arr)
        {
            var seed = new DecanSeed
            {
                Id                = entry.GetStringProp("id"),
                Name              = entry.GetStringProp("name"),
                Description       = entry.GetStringProp("decan"),
                Drive             = entry.JoinArray("drives"),
                Avoidance         = entry.JoinArray("avoids"),
                ConversationalStyle = entry.GetStringProp("in_conversation"),
                StressResponse    = entry.GetStringProp("under_stress"),
                EconomicBehavior  = entry.GetStringProp("economic_behavior"),
                PoliticalTendency = entry.GetStringProp("political_tendency"),
                TrustDynamic      = entry.GetStringProp("trust_threshold"),
                BetrayalResponse  = entry.GetStringProp("betrayal_response"),
            };

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

    // ── Helpers ─────────────────────────────────────────────────────────

    private static string GetStringProp(this JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? string.Empty
            : string.Empty;

    private static string JoinArray(this JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Array)
            return string.Empty;
        var items = new List<string>();
        foreach (var item in v.EnumerateArray())
            if (item.ValueKind == JsonValueKind.String)
                items.Add(item.GetString() ?? "");
        return string.Join(", ", items);
    }
}
