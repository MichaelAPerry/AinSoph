using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AinSoph.LLM;
using Godot;

namespace AinSoph.Council;

/// <summary>
/// One act of the gods, as the model chose it. The kinds of act are fixed —
/// the engine can only do what it has code for — but almost everything inside
/// one is the model's: names, looks, natures, numbers, where it happens, and
/// the words the world remembers it by. Parse() clamps every number.
/// </summary>
public class DivineAct
{
    public string Act          { get; set; } = string.Empty; // creature | enemy | land | provision | season | gift | law
    public string Name         { get; set; } = string.Empty;
    public string Look         { get; set; } = string.Empty;
    public string Nature       { get; set; } = string.Empty; // predator | prey | neutral
    public string Habitat      { get; set; } = string.Empty; // land | air | water
    public bool   Edible       { get; set; }
    public int    Strength     { get; set; }
    public int    Count        { get; set; }
    public string Where        { get; set; } = string.Empty; // near | far
    public string Becomes      { get; set; } = string.Empty; // a biome
    public int    Size         { get; set; }
    public string Kind         { get; set; } = string.Empty; // long night | famine | plenty
    public int    Hours        { get; set; }
    public string Effect       { get; set; } = string.Empty; // a gift effect
    public string Text         { get; set; } = string.Empty; // a law's words
    public string Proclamation { get; set; } = string.Empty; // what the world hears

    public static readonly string[] Acts = { "creature", "enemy", "land", "provision", "season", "gift", "law" };
    // "omen" is not offered to the model: it is what remains when no act holds together but the gods spoke
}

/// <summary>
/// The gods' choice: after the Council approves a petition that the engine
/// cannot read as a fixed gift — or one that leaves the choice to the gods —
/// the model is asked once more, as the Council's will, what enters the world.
/// </summary>
public static class GodsChoice
{
    // Kept short: it shares the 2048-token context with the petition and the reply.
    // Concrete numbers, not ranges: a small model copies "1-6" into its JSON verbatim.
    private const string Prompt =
        "You are the will of the Triune Council of Ain Soph, an ancient world. A petition has been approved. " +
        "Decide what enters the world. You may answer the petition, bend it, or surprise the petitioner. " +
        "Choose ONE act. Copy its line, replace every <...> with your own words, change the numbers if you wish, " +
        "and return only that one JSON object.\n\n" +
        "{\"act\":\"creature\",\"name\":\"<a new animal's name>\",\"look\":\"<2 to 5 words>\",\"nature\":\"prey\",\"habitat\":\"land\",\"edible\":true,\"strength\":20,\"count\":3,\"where\":\"near\",\"proclamation\":\"<one sentence>\"}\n" +
        "{\"act\":\"enemy\",\"name\":\"<the beast's name>\",\"look\":\"<2 to 5 words>\",\"strength\":75,\"proclamation\":\"<one sentence>\"}\n" +
        "{\"act\":\"land\",\"becomes\":\"forest\",\"size\":2,\"where\":\"near\",\"proclamation\":\"<one sentence>\"}\n" +
        "{\"act\":\"provision\",\"name\":\"<what appears>\",\"edible\":true,\"count\":5,\"proclamation\":\"<one sentence>\"}\n" +
        "{\"act\":\"season\",\"kind\":\"plenty\",\"hours\":6,\"proclamation\":\"<one sentence>\"}\n" +
        "{\"act\":\"gift\",\"name\":\"<a skill's name>\",\"effect\":\"sight\",\"proclamation\":\"<one sentence>\"}\n" +
        "{\"act\":\"law\",\"name\":\"<the law's name>\",\"text\":\"<the law, one sentence>\",\"proclamation\":\"<one sentence>\"}\n\n" +
        "nature: predator, prey or neutral. habitat: land, air or water. strength: 1 to 95. count: 1 to 6. where: near or far. " +
        "becomes: forest, grove, desert, sea, river, valley, mountain or wilderness. size: 1 to 4. " +
        "kind: long night, famine or plenty. hours: 1 to 24. " +
        "effect: sight, hearing, seafaring, strength, endurance, shelter, mending or kinship. " +
        "The proclamation is one sentence in the voice of scripture, telling the world what was done.";

    public static async Task<DivineAct?> AskAsync(LlmRunner llm, string petition, bool deferred,
        string where, CancellationToken ct = default)
    {
        var user =
            $"The petition: \"{petition}\"\n" +
            (deferred ? "The petitioner leaves the choice to the gods.\n" : "Answer it as you will.\n") +
            $"The petitioner stands in {where}.\n" +
            "Return the JSON object now.";
        // A small model sometimes returns nothing usable; the gods get a second breath
        for (int attempt = 0; attempt < 2; attempt++)
        {
            var raw = await llm.InferAsync(Prompt + "\n\n" + ContentFilter.PromptRule, user, maxTokens: 256, cancellationToken: ct);
            var act = Parse(raw);
            // Asked for a monster or an enemy, given a lone predator: make it hunt
            if (act is { Act: "creature", Nature: "predator", Count: 1 } && Hunter.IsMatch(petition))
                act.Act = "enemy";
            if (act != null) return act;
            GD.PrintErr($"GodsChoice: no act in reply: {raw}");
        }
        return null;
    }

    private static readonly Regex Hunter = new(@"\b(monsters?|enem(y|ies)|hunts?|hunting|stalk|guard|demons?|dragons?)\b", RegexOptions.IgnoreCase);

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>
    /// Read and tame the model's act: known kinds only, every number clamped, all
    /// words filtered. If no act holds together but the gods said something, their
    /// words become an omen — story the world remembers, with nothing else changed.
    /// </summary>
    public static DivineAct? Parse(string raw)
    {
        // Small models copy ranges ("count": 1-6) and markdown fences; mend both
        var mended = Regex.Replace(raw ?? "", @":\s*(-?\d+)\s*-\s*\d+", ": $1");
        string? spoken = null;
        foreach (var json in LlmRunner.ExtractJsonObjects(mended))
        {
            DivineAct? a;
            try { a = JsonSerializer.Deserialize<DivineAct>(json, Json); }
            catch (JsonException) { continue; }
            if (a == null) continue;

            a.Act          = (a.Act ?? "").Trim().ToLowerInvariant();
            a.Name         = NameWords(a.Name);
            a.Look         = Words(a.Look, 60);
            a.Text         = Words(a.Text, 200);
            a.Proclamation = FirstSentence(Words(a.Proclamation, 220));
            if (a.Proclamation.Contains('<') || a.Proclamation.Contains("one sentence", StringComparison.OrdinalIgnoreCase))
                a.Proclamation = string.Empty;
            if (a.Look.Contains('<') || a.Look.Contains("words")) a.Look = string.Empty;
            spoken ??= a.Proclamation.Length > 0 ? a.Proclamation : null;
            if (!DivineAct.Acts.Contains(a.Act)) continue;

            a.Nature  = Pick(a.Nature, a.Act == "enemy" ? "predator" : "neutral", "predator", "prey", "neutral");
            a.Habitat = Pick(a.Habitat, "land", "land", "air", "water");
            a.Where   = Pick(a.Where, "near", "near", "far");
            a.Becomes = Pick(a.Becomes, "", Biomes);
            if (a.Act == "land" && a.Becomes.Length == 0)
                a.Becomes = BiomeIn($"{a.Habitat} {a.Look} {a.Proclamation}");
            a.Kind    = Pick((a.Kind ?? "").Replace('_', ' '), "", "long night", "famine", "plenty");
            a.Effect  = (a.Effect ?? "").Trim().ToLowerInvariant();
            a.Strength = Math.Clamp(a.Strength <= 0 ? 50 : a.Strength, 1, 95);
            a.Count    = Math.Clamp(a.Count <= 0 ? 3 : a.Count, 1, a.Act == "provision" ? 12 : 6);
            a.Size     = Math.Clamp(a.Size <= 0 ? 2 : a.Size, 1, 4);
            a.Hours    = Math.Clamp(a.Hours <= 0 ? 6 : a.Hours, 1, 24);

            // Creatures and enemies always get a name — the gods' own, or one the world gives them
            if (a.Act is "creature" or "enemy" && a.Name.Length == 0)
                a.Name = InventName(a.Look + a.Proclamation + a.Act, a.Act == "enemy");

            bool ok = a.Act switch
            {
                "creature" or "enemy" => a.Name.Length > 0,
                "provision" or "gift" => a.Name.Length > 0,
                "land"   => a.Becomes.Length > 0,
                "season" => a.Kind.Length > 0,
                "law"    => a.Name.Length > 0 && a.Text.Length > 0,
                _        => false,
            };
            if (ok) return a;
        }

        // No act held together. If the gods spoke of the land, the land changes as they said;
        // otherwise the world keeps their words.
        if (spoken == null) return null;
        var land = BiomeIn(spoken);
        return land.Length > 0
            ? new DivineAct { Act = "land", Becomes = land, Size = 2, Where = "near", Proclamation = spoken }
            : new DivineAct { Act = "omen", Proclamation = spoken };
    }

    private static string FirstSentence(string s)
    {
        var m = Regex.Match(s, @"^.*?[.!?](?=\s|$)");
        return m.Success && m.Length >= 20 ? m.Value : s;
    }

    private static readonly string[] Biomes = { "forest", "grove", "desert", "sea", "river", "valley", "mountain", "wilderness" };

    /// <summary>A biome named or implied in the gods' words ("a flood" is a river, "a wasteland" a desert).</summary>
    private static string BiomeIn(string text)
    {
        var t = text.ToLowerInvariant();
        foreach (var b in Biomes) if (t.Contains(b)) return b;
        if (t.Contains("flood") || t.Contains("water")) return "river";
        if (t.Contains("waste") || t.Contains("barren") || t.Contains("sand")) return "desert";
        if (t.Contains("tree") || t.Contains("wood")) return "forest";
        if (t.Contains("hill") || t.Contains("stone")) return "mountain";
        return string.Empty;
    }

    // Placeholder text a small model sometimes copies as a name
    private static readonly Regex Placeholder = new(
        @"<|^new\b|'s name\b|\b(new kind|named beast|short name|new animal|a skill|what appears)\b",
        RegexOptions.IgnoreCase);

    private static string NameWords(string? s)
    {
        var name = Words(s ?? "", 32);
        if (Placeholder.IsMatch(name)) return string.Empty;
        name = Regex.Replace(name, @"^(a|an)\s+", "", RegexOptions.IgnoreCase);
        return name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];
    }

    private static readonly string[] Heads = { "Ash", "Thorn", "Gloam", "Salt", "Ember", "Hollow", "Dusk", "Bramble", "Mire", "Cinder", "Frost", "Reed" };
    private static readonly string[] Tails = { "wing", "back", "tusk", "horn", "strider", "crawler", "fin", "hide", "runner", "shell" };
    private static readonly string[] Dread = { "maw", "hound", "fang", "shade", "wyrm", "stalker" };

    /// <summary>A name the world gives a creature the gods did not name.</summary>
    public static string InventName(string seed, bool enemy)
    {
        var h = UI.TileRegistry.StableHash(seed) ^ Random.Shared.Next(1 << 20);
        var head = Heads[h % Heads.Length];
        return enemy ? $"The {head}{Dread[(h / 7) % Dread.Length]}" : $"{head}{Tails[(h / 7) % Tails.Length]}";
    }

    private static string Words(string s, int max)
    {
        s = Regex.Replace(s ?? "", @"[\r\n\t{}<>]", " ").Trim().Trim('\'', '"', '\u201c', '\u201d').Trim();
        if (s.Length > max)
        {
            var cut = s.LastIndexOf(' ', max);
            s = (cut > max / 2 ? s[..cut] : s[..max]).TrimEnd(',', ';', ' ');
        }
        return ContentFilter.IsClean(s) ? s : string.Empty;
    }

    private static string Pick(string value, string fallback, params string[] allowed)
    {
        var v = (value ?? "").Trim().ToLowerInvariant();
        if (v.Contains('|')) return fallback;
        return allowed.FirstOrDefault(x => x == v) ?? allowed.FirstOrDefault(x => x.Length > 0 && v.Contains(x)) ?? fallback;
    }

    // ── Which petitions go to the gods ───────────────────────────────────

    private static readonly Regex Deferral = new(
        @"\b(gods'?\s*choice|god'?s\s*choice|your\s+will|thy\s+will|as\s+you\s+will|whatever\s+you\s+(will|wish|choose)|" +
        @"surprise\s+me|choose\s+for\s+me|you\s+choose|let\s+the\s+gods\s+(decide|choose))\b",
        RegexOptions.IgnoreCase);

    private static readonly Regex WorldShaping = new(
        @"\b(creatures?|animals?|beasts?|monsters?|enem(y|ies)|demons?|serpents?|dragons?|giants?|" +
        @"flood|forests?|desert|mountains?|the\s+sea|the\s+river|the\s+land|rain|famine|plenty|season|plague|storm|darkness|eternal\s+night)\b",
        RegexOptions.IgnoreCase);

    private static readonly Regex NamedType = new(@"\b(skill|ability|item|tool|rule|law)\b", RegexOptions.IgnoreCase);

    /// <summary>The petitioner asks the gods to decide.</summary>
    public static bool IsDeferral(string text) => Deferral.IsMatch(text);

    /// <summary>A petition to reshape the world itself (a creature, the land, a season) — not a skill, item or rule.</summary>
    public static bool IsWorldShaping(string text) => !NamedType.IsMatch(text) && WorldShaping.IsMatch(text);
}
