using System.Text.Json;

namespace AinSoph.LLM;

/// <summary>
/// Stand-in for the LLM when no model is loaded (demo mode).
///
/// Recognises the three kinds of prompt the game sends — NPC think ticks,
/// NPC dialogue, and Triune Council seats — and returns a scripted reply in
/// the same shape the real model is asked for. Lets the whole game loop run
/// on machines without the 1.9 GB model, and makes demos reproducible.
/// </summary>
public static class DemoResponder
{
    private static readonly Random Rng = new();
    private static readonly object RngLock = new();

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public static async Task<string> RespondAsync(string systemPrompt, string userMessage,
        CancellationToken ct)
    {
        // A short pause so the world feels like it is thinking
        await Task.Delay(Next(250, 700), ct);

        // The Council's judge: a law is broken if the deed shares its key word
        if (systemPrompt.Contains("{\"broken\":"))
            return Judge(userMessage);

        // The gods' choice lists its acts as {"act": ...} schemas
        if (systemPrompt.Contains("{\"act\":\"creature\""))
            return GodsAct(userMessage);

        // Council seat prompts end with a {"seat": ...} schema; NPC prompts merely mention the Council
        if (systemPrompt.Contains("\"seat\":"))
            return CouncilSeat(systemPrompt, userMessage);

        if (userMessage.Contains("Return plain text"))
            return Dialogue(userMessage);

        return NpcDecision(userMessage);
    }

    // ── The judge ─────────────────────────────────────────────────────────

    private static readonly string[] DeedWords = { "kill", "attack", "wound", "ate", "eat" };

    private static string Judge(string userMessage)
    {
        var parts = userMessage.Split("The deed:");
        if (parts.Length < 2) return "{\"broken\": \"\"}";
        var deed = parts[1].ToLowerInvariant();
        foreach (var line in parts[0].Split('\n').Where(l => l.StartsWith("- ")))
        {
            var law = line[2..];
            var name = law.Split(':')[0].Trim();
            var text = law.ToLowerInvariant();
            if (DeedWords.Any(w => deed.Contains(w) && text.Contains(w)))
                return JsonSerializer.Serialize(new { broken = name });
        }
        return "{\"broken\": \"\"}";
    }

    // ── The gods' choice ──────────────────────────────────────────────────

    private static readonly string[] ScriptedActs =
    {
        "{\"act\":\"creature\",\"name\":\"ashwing\",\"look\":\"grey wings like cinders\",\"nature\":\"prey\",\"habitat\":\"air\",\"edible\":true,\"strength\":12,\"count\":4,\"where\":\"near\",\"proclamation\":\"And from the ashes rose a bird, and it was given to the hungry.\"}",
        "{\"act\":\"enemy\",\"name\":\"The Pale Hound\",\"look\":\"white as bone, eyes like coals\",\"strength\":75,\"proclamation\":\"A hound was loosed upon the plain, and it does not sleep.\"}",
        "{\"act\":\"land\",\"becomes\":\"forest\",\"size\":3,\"where\":\"near\",\"proclamation\":\"The ground remembered the seed, and a forest stood where there was none.\"}",
        "{\"act\":\"provision\",\"name\":\"loaves\",\"edible\":true,\"count\":5,\"proclamation\":\"Bread was laid upon the stones, and none asked who baked it.\"}",
        "{\"act\":\"season\",\"kind\":\"long night\",\"hours\":2,\"proclamation\":\"The sun was held back, and the people learned the value of a fire.\"}",
        "{\"act\":\"gift\",\"name\":\"Night Eyes\",\"effect\":\"sight\",\"proclamation\":\"To the one who asked, the dark was made a little thinner.\"}",
        "{\"act\":\"law\",\"name\":\"The Law of the Stranger\",\"text\":\"A traveller who asks for bread may not be refused.\",\"proclamation\":\"Remember that you, too, crossed from somewhere.\"}",
    };

    private static string GodsAct(string userMessage)
    {
        var p = userMessage.ToLowerInvariant();
        int i = p.Contains("enemy") || p.Contains("monster") ? 1
              : p.Contains("creature") || p.Contains("animal") || p.Contains("beast") ? 0
              : p.Contains("forest") || p.Contains("land") ? 2
              : p.Contains("bread") || p.Contains("food") ? 3
              : p.Contains("night") || p.Contains("dark") ? 4
              : p.Contains("law") ? 6
              : Next(0, ScriptedActs.Length);
        return ScriptedActs[i];
    }

    // ── Council ───────────────────────────────────────────────────────────

    private static readonly string[] YesHomilies =
    {
        "A shepherd found a stone that fit his sling. He did not ask who shaped it. He gave thanks, and the flock was kept.",
        "The vine does not argue with the trellis. It climbs. Let this climb.",
        "There was a well in the desert that no one had dug. The thirsty drank, and named it after the first to share.",
        "A small seed was dropped on good ground. Go, and see what it becomes.",
    };

    private static readonly string[] NoHomilies =
    {
        "A man built his house on the riverbed in the dry season. When the rains came, he learned what the river already knew.",
        "Two masters called the same servant at once. He stood in the doorway until nightfall.",
        "The tower reached high, but every brick asked the one beneath it to carry more than it could.",
        "A merchant sold the same lamp twice. Both buyers sat in darkness.",
    };

    private static string CouncilSeat(string systemPrompt, string userMessage)
    {
        var seat = systemPrompt.Contains("\"seat\": \"skills\"") ? "skills"
                 : systemPrompt.Contains("\"seat\": \"items\"")  ? "items"
                 : "rules";
        int seatIndex = seat switch { "skills" => 0, "items" => 1, _ => 2 };

        // Deterministic per petition so a demo replays the same way: a shapeless
        // petition is refused by all; a well-formed one passes 2–1, one seat dissenting
        var hash = StableHash(userMessage);
        bool unknown = userMessage.Contains("\"type\":\"unknown\"");
        userMessage  = userMessage.Split("\n\nGive your verdict")[0];
        bool yes = !unknown && hash % 3 != seatIndex;
        var pool = yes ? YesHomilies : NoHomilies;

        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["seat"]   = seat,
            ["vote"]   = yes ? "yes" : "no",
            ["homily"] = pool[(hash + seatIndex) % pool.Length],
        }, Json);
    }

    // ── Dialogue ──────────────────────────────────────────────────────────

    private static readonly string[] DialogueLines =
    {
        "You walk loudly for someone so new to the world. Sit. The manna will not run from you.",
        "I have been watching the ridge to the north. Something moves there at dusk.",
        "Ask me again tomorrow. Today I am thinking about caves, and who sleeps in them.",
        "I prayed once. The Council answered with a story about a fig tree. I am still deciding what it meant.",
        "Stay close to the river if you want to live. Stay close to me if you want to learn.",
        "Names are cheap here. Deeds are remembered. What have you done?",
    };

    private static string Dialogue(string userMessage)
    {
        var said = ExtractQuoted(userMessage, "Someone speaks to you: \"");
        var line = DialogueLines[Next(0, DialogueLines.Length)];
        return string.IsNullOrWhiteSpace(said) ? line : $"\"{Truncate(said, 40)}\"... {line}";
    }

    // ── NPC think tick ────────────────────────────────────────────────────

    private static readonly string[] IdleThoughts =
    {
        "The light is moving west. I should be closer to shelter.",
        "Someone new walks among us.",
        "The manna tasted of honey today.",
        "I wonder what the Council would say to a new kind of rope.",
    };

    private static readonly string[] Creations =
    {
        "a sling woven from reed fibre",
        "a skill for reading tracks in sand",
        "a rule that a shared meal binds a truce until dawn",
    };

    private static string NpcDecision(string userMessage)
    {
        int roll = Next(0, 100);
        string state, speech = "", intent = "", type = "";

        if (userMessage.Contains("You have not eaten") && roll < 70)
            state = "eating";
        else if (roll < 35) state = "idle";
        else if (roll < 60) state = "moving";
        else if (roll < 80) { state = "talking"; speech = IdleThoughts[Next(0, IdleThoughts.Length)]; }
        else if (roll < 92)
        {
            state  = "creating";
            intent = Creations[Next(0, Creations.Length)];
            type   = intent.StartsWith("a skill") ? "skill" : intent.StartsWith("a rule") ? "rule" : "item";
        }
        else state = "praying";

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["state"]           = state,
            ["speech"]          = speech,
            ["target_id"]       = "",
            ["target_cell"]     = "",
            ["eat_item_id"]     = "",
            ["creation_type"]   = type,
            ["creation_intent"] = intent,
            ["memory_updates"]  = new Dictionary<string, string?>
            {
                ["will"]    = null,
                ["thought"] = state == "idle" ? IdleThoughts[Next(0, IdleThoughts.Length)] : null,
                ["feeling"] = null,
                ["action"]  = $"I was {state}.",
            },
        }, Json);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static int Next(int min, int max)
    {
        lock (RngLock) return Rng.Next(min, max);
    }

    private static int StableHash(string s)
    {
        unchecked
        {
            int h = 23;
            foreach (var c in s) h = h * 31 + c;
            return h & 0x7fffffff;
        }
    }

    private static string ExtractQuoted(string text, string marker)
    {
        var i = text.IndexOf(marker, StringComparison.Ordinal);
        if (i < 0) return "";
        i += marker.Length;
        var j = text.IndexOf('"', i);
        return j < 0 ? "" : text[i..j];
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";
}
