using System.Text.RegularExpressions;
using Godot;

namespace AinSoph.LLM;

/// <summary>
/// Last line of defence on AI-generated text before a player sees it or an NPC
/// remembers it. The prompts already forbid this content; this catches what a
/// small model lets through. The word list is data/blocklist.txt.
///
/// Matching text is never partially shown — the whole line is withheld.
/// </summary>
public static class ContentFilter
{
    public const string Withheld = "…";

    private static Regex? _pattern;
    private static readonly object Lock = new();

    /// <summary>The line every NPC and Council prompt carries.</summary>
    public const string PromptRule =
        "Never describe sexual content, never use slurs or hateful language about real groups of people, " +
        "and never encourage self-harm. Violence in this world is plain and brief, never graphic.";

    public static bool IsClean(string? text)
    {
        if (string.IsNullOrEmpty(text)) return true;
        return !Pattern().IsMatch(text);
    }

    /// <summary>The text, or "…" if any part of it is blocked.</summary>
    public static string Clean(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
        if (IsClean(text)) return text;
        GD.Print("ContentFilter: withheld a line of generated text");
        return Withheld;
    }

    private static Regex Pattern()
    {
        lock (Lock)
        {
            if (_pattern != null) return _pattern;

            var entries = new List<string>();
            var text = Godot.FileAccess.FileExists("res://data/blocklist.txt")
                ? Godot.FileAccess.GetFileAsString("res://data/blocklist.txt")
                : string.Empty;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                var prefix = line.EndsWith('*');
                var core = Regex.Escape(prefix ? line[..^1] : line).Replace("\\ ", "\\s+");
                entries.Add(prefix ? $@"\b{core}\w*" : $@"\b{core}\b");
            }

            _pattern = entries.Count == 0
                ? new Regex("(?!)") // matches nothing
                : new Regex(string.Join("|", entries), RegexOptions.IgnoreCase | RegexOptions.Compiled);
            return _pattern;
        }
    }
}
