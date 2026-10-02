using System.Text.Json;
using AinSoph.Data;
using AinSoph.LLM;
using Godot;

namespace AinSoph.Council;

/// <summary>
/// Laws bind the player too. A law is free text the engine cannot enforce by
/// itself, so when the player does something that matters (reaps a being,
/// eats), the model is asked, as the Council's judge, whether a law was broken.
/// The engine carries out the sentence (GameRoot): the Council will not hear
/// a lawbreaker for a day, and every NPC is told.
/// </summary>
public static class LawJudge
{
    private const string Prompt =
        "You are the judge of the Triune Council of Ain Soph. You read a deed against the laws of the world. " +
        "Judge only by the laws given, plainly and fairly: a deed breaks a law only if the law clearly forbids it. " +
        "Return only JSON: {\"broken\": \"<the exact name of the broken law>\"} or {\"broken\": \"\"} if no law was broken.";

    private static readonly System.Text.RegularExpressions.Regex Prohibition = new(
        @"\b(no|not|never|none|nor|forbid\w*|banned|must not|shall not|may not|cannot|can't|mustn't|unlawful)\b",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static bool Forbids(LawRecord law) => Prohibition.IsMatch($"{law.Name} {law.Description}");

    // Irregular forms the deeds use
    private static readonly Dictionary<string, string> Stems = new()
    {
        ["ate"] = "eat", ["slew"] = "slay", ["slain"] = "slay", ["people"] = "pers", ["beast"] = "anim", ["creature"] = "anim",
    };

    private static IEnumerable<string> Keys(string text) =>
        System.Text.RegularExpressions.Regex.Split(text.ToLowerInvariant(), "[^a-z]+")
            .Where(w => w.Length >= 3)
            .Select(w => Stems.TryGetValue(w, out var s) ? s : w.Length > 4 ? w[..4] : w)
            .Where(w => w is not ("the" or "and" or "one" or "who" or "any" or "may" or "not" or "shal" or "must" or "never"));

    private static bool Touches(LawRecord law, string deed) =>
        Keys($"{law.Name} {law.Description}").Intersect(Keys(deed)).Any();

    /// <summary>The law the deed breaks, or null.</summary>
    public static async Task<LawRecord?> JudgeAsync(LlmRunner llm, IReadOnlyList<LawRecord> laws, string deed,
        CancellationToken ct = default)
    {
        if (laws.Count == 0) return null;
        // Only a law that forbids something, and touches what was done, can be broken by it.
        // The model judges the rest; this keeps it from finding guilt in every meal.
        var recent = laws.TakeLast(8).Where(l => Forbids(l) && Touches(l, deed)).ToList();
        if (recent.Count == 0) return null;
        var user = "The laws:\n" + string.Join("\n", recent.Select(l => $"- {l.Name}: {l.Description}")) +
                   $"\n\nThe deed: {deed}\n\nReturn the JSON now.";
        var raw = await llm.InferAsync(Prompt, user, maxTokens: 64, cancellationToken: ct);
        foreach (var json in LlmRunner.ExtractJsonObjects(raw))
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("broken", out var b)) continue;
                var name = (b.GetString() ?? "").Trim().Trim('"', '\'');
                if (name.Length == 0) return null;
                // Only a law that exists can be broken
                return recent.FirstOrDefault(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    ?? recent.FirstOrDefault(l => l.Name.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                                                  name.Contains(l.Name, StringComparison.OrdinalIgnoreCase));
            }
            catch (JsonException) { }
        }
        GD.Print($"LawJudge: no judgement in reply: {raw}");
        return null;
    }
}
