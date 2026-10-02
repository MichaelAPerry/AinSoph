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

        // Leaving it to the gods, or asking for the world itself to change: the gods choose what enters
        if (GodsChoice.IsDeferral(text) || GodsChoice.IsWorldShaping(text))
            return new CouncilSubmission
            {
                Type        = "choice",
                Name        = GodsChoice.IsDeferral(text) ? "The Gods' Choice" : NameOf(text),
                Description = text,
                Effect      = "the gods decide what enters the world: a creature, an enemy, the land, food, a season, a gift or a law",
            };

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
        var name = NameOf(text);
        if (string.IsNullOrEmpty(name)) return null;

        return new CouncilSubmission
        {
            Type        = type,
            Name        = name,
            Description = text,
            BaseSkills  = new List<string>(),
            Cost        = ParseCost(text),
            Effect      = type == "rule" ? string.Empty : Gifts.Describe(Gifts.Classify(text)),
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

    // "Grant me a skill: …", "I want to make a …", "Teach us the art of …"
    private static readonly Regex RequestPreamble = new(
        @"^\s*(please\s+)?" +
        @"((grant|give|teach|bless|show|let)\s+(me|us)\s+|i\s+(ask|pray|want|wish|seek|need)(\s+(for|to))?\s+|we\s+(ask|pray|want|wish|seek|need)(\s+(for|to))?\s+)?" +
        @"((make|craft|build|learn|know|have|create)\s+)?" +
        @"(with\s+)?((a|an|the)\s+)?" +
        @"((new\s+)?(skill|ability|power|item|thing|tool|object|rule|law|gift|art)(\s+(of|called|named))?\s*[:\-–—,]?\s*)?" +
        @"((for|of|to)\s+)?",
        RegexOptions.IgnoreCase);

    /// <summary>
    /// The name of what is asked for: "Grant me a skill: Fire Making - to keep warm" → "Fire Making".
    /// At most five words, cut at the first clause break.
    /// </summary>
    public static string NameOf(string text)
    {
        var rest = RequestPreamble.Replace(text.Trim(), "", 1);
        if (string.IsNullOrWhiteSpace(rest)) rest = text.Trim();

        // Cut at punctuation that ends the name
        var cut = Regex.Match(rest, @"\s[-–—]\s|[:;,.!?(]");
        if (cut.Success && cut.Index > 0) rest = rest[..cut.Index];

        var words = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var end   = Math.Min(5, words.Length);
        for (var i = 1; i < end; i++)
        {
            var w = words[i].ToLowerInvariant();
            if (w is "that" or "which" or "so" or "to" or "for" or "it" or "and" or "with")
            {
                end = i;
                break;
            }
        }

        var name = string.Join(" ", words[..end]).Trim('.', ',', ' ', '"', '\'');
        return name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];
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
