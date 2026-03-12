using AinSoph.Council;
using Godot;
using System.Text.RegularExpressions;

namespace AinSoph.Skills;

/// <summary>
/// Parses a player's free-form prayer text into a CouncilSubmission.
/// The format is never shown to the player. The Council never explains it.
/// If the prayer does not map cleanly, returns null — the Council rejects in parable.
///
/// The parser looks for intent, not syntax.
/// A good prayer names what is being created, what it does, and what it costs.
/// A bad prayer is shapeless. The Council hears it. Nothing enters the world.
/// </summary>
public class CouncilSubmissionParser
{
    // Simple keyword signals — enough to find intent without requiring format knowledge
    private static readonly string[] SkillSignals = { "skill", "ability", "power", "do", "can", "learn" };
    private static readonly string[] ItemSignals  = { "item", "thing", "object", "make", "create", "craft", "tool", "weapon" };
    private static readonly string[] RuleSignals  = { "rule", "law", "truth", "world", "always", "never", "all things" };

    public CouncilSubmission? Parse(string prayerText)
    {
        if (string.IsNullOrWhiteSpace(prayerText)) return null;

        var text = prayerText.Trim();

        // Must have at least a name/description of the thing
        // Minimum meaningful prayer: 10 characters, more than 2 words
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 3 || text.Length < 10)
        {
            GD.Print("CouncilSubmissionParser: prayer too sparse");
            return null;
        }

        var type = InferType(text.ToLowerInvariant());
        if (type is null)
        {
            GD.Print("CouncilSubmissionParser: could not infer submission type");
            return null;
        }

        // Extract a name — first noun phrase or first few words
        var name = ExtractName(text);
        if (string.IsNullOrEmpty(name)) return null;

        return new CouncilSubmission
        {
            Type        = type,
            Name        = name,
            Description = text,
            BaseSkills  = new List<string>(),
            Cost        = ParseCost(text)
        };
    }

    private static string? InferType(string lower)
    {
        foreach (var s in SkillSignals)
            if (lower.Contains(s)) return "skill";
        foreach (var s in ItemSignals)
            if (lower.Contains(s)) return "item";
        foreach (var s in RuleSignals)
            if (lower.Contains(s)) return "rule";
        return null;
    }

    private static string ExtractName(string text)
    {
        // Take up to the first clause break or 5 words — whichever is shorter
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var end   = Math.Min(5, words.Length);

        // Stop at common clause starters
        for (var i = 1; i < end; i++)
        {
            var w = words[i].ToLower().TrimEnd(',', '.', ';');
            if (w is "that" or "which" or "so" or "to" or "for" or "it")
            {
                end = i;
                break;
            }
        }

        return string.Join(" ", words[..end]).Trim('.', ',', ' ');
    }

    private static SubmissionCost ParseCost(string text)
    {
        var cost  = new SubmissionCost();
        var lower = text.ToLowerInvariant();

        // Look for time cost signals: "takes X hours", "costs X hours"
        var timeMatch = Regex.Match(lower, @"(\d+)\s*hour");
        if (timeMatch.Success && float.TryParse(timeMatch.Groups[1].Value, out var hours))
            cost.TimeHours = hours;

        return cost;
    }
}
