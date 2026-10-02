using System.Text.Json;
using AinSoph.LLM;
using AinSoph.World;
using Godot;

namespace AinSoph;

/// <summary>
/// The first encounter. Within five minutes of a new life, the Council sends a
/// messenger — its form and its words chosen by the model — that appears beside
/// the player and tells them what the world will not explain: there is one
/// hidden altar, which way it lies, and that prayer there can change the world.
/// Once per life; it replaces a hint with a meeting.
/// </summary>
public partial class GameRoot
{
    private DateTime? _encounterDueUtc;
    private bool _encountersEnabled = true; // off for the tour, the trailer and the self-test
    private const string ApparitionId = "apparition:council";

    private const string EncounterPrompt =
        "You are the Triune Council of Ain Soph, an ancient world. A new soul has just arrived. " +
        "Send them a messenger. Choose what form it takes and what it says. It should be strange and holy, " +
        "something no one has seen before (for example {0} or {1} — but invent your own). " +
        "Return only one JSON object: {{\"form\":\"<what appears, 2 to 5 words>\"," +
        "\"words\":\"<what it says: two sentences of parable, welcoming the soul and telling it that the Council hears those who pray at its hidden altar>\"}}";

    // Two are shown each time, so even a model that copies an example does not send everyone the same messenger
    private static readonly string[] ExampleForms =
    {
        "a heron made of white fire", "a child with a lamp for eyes", "a ram with a star between its horns",
        "a woman woven from reeds", "a voice inside the wind", "a stag of smoke", "an old man carrying the moon",
        "a swarm of golden bees", "a lion made of rain", "a shepherd with no face",
    };

    private static readonly (string Form, string Words)[] ScriptedMessengers =
    {
        ("a figure of pale light", "A traveller once walked the whole earth looking for a door, and found it in the place he had passed first. There is an altar in this world; those who pray there are heard."),
        ("a white heron", "The river does not ask the stones for permission; it asks the sea. Find the altar, and ask."),
        ("an old woman with no shadow", "I planted a fig and waited seven years, and the waiting was the planting. The Council is listening, at the altar no one marked."),
    };

    /// <summary>Called every frame: schedule the encounter for a new life, and send it when its time comes.</summary>
    private void TickEncounter()
    {
        if (!_encountersEnabled || Player == null || _worldScene == null) return;
        if (Player.EncounterDone || string.IsNullOrEmpty(Player.Name)) { _encounterDueUtc = null; return; }

        _encounterDueUtc ??= DateTime.UtcNow.AddSeconds(Random.Shared.Next(90, 300)); // within five minutes
        if (DateTime.UtcNow < _encounterDueUtc) return;
        // Wait for a quiet moment: not asleep, not mid-conversation, not in a menu
        if (Player.Survival.IsSleeping || _worldScene.DialogueOpen || _worldScene.MenuOpen || _worldScene.InputLocked) return;

        Player.EncounterDone = true; // once per life, even if the model is slow
        _ = SendEncounterAsync();
    }

    /// <summary>The Council's messenger appears beside the player and speaks.</summary>
    public async Task SendEncounterAsync()
    {
        if (Player == null || _worldScene == null) return;
        Player.EncounterDone = true;
        SaveAll();

        var (form, words) = await ChooseMessenger();
        if (Player == null || _worldScene == null) return;

        // It stands two tiles away, on open ground
        var (x, y) = (Player.TileX, Player.TileY);
        foreach (var (dx, dy) in new[] { (2, 0), (-2, 0), (0, 2), (0, -2), (1, 1), (-1, -1), (1, 0) })
            if (IsPassableTile(Player.TileX + dx, Player.TileY + dy)) { (x, y) = (Player.TileX + dx, Player.TileY + dy); break; }
        _worldScene.UpsertAnimal(ApparitionId, form, glyph: 89, tint: new Color(1f, 0.92f, 0.65f), x, y);
        _worldScene.ShowNpcSpeech(ApparitionId, "…");
        _worldScene.ShowWorldText($"Something descends beside you: {form}.");
        AinSoph.Audio.Sound.Play("council");

        var guidance = AltarGuidance();
        _worldScene.OpenVision(Capitalise(form), 89, new Color(1f, 0.92f, 0.65f),
            $"{words}\n\n» {guidance}",
            _ => _worldScene?.SetDialogueSpeech($"It says nothing more. It only turns toward the altar.\n\n» {guidance}"));

        RecordOmen("", $"The Council sent {form} to {Player.Name}.");
        GD.Print($"GameRoot: the Council's encounter — {form}");

        // It does not stay
        GetTree().CreateTimer(60).Timeout += () => _worldScene?.RemoveNpc(ApparitionId);
    }

    private async Task<(string Form, string Words)> ChooseMessenger()
    {
        try
        {
            var picks = ExampleForms.OrderBy(_ => Random.Shared.Next()).Take(2).ToArray();
            var prompt = string.Format(EncounterPrompt, picks[0], picks[1]);
            var raw = await Llm.InferAsync(prompt + "\n\n" + ContentFilter.PromptRule,
                $"The soul is called {Player?.Name}. It stands in {PlaceName(Player!.TileX, Player.TileY)}. Return the JSON now.",
                maxTokens: 160, cancellationToken: _cts.Token);
            foreach (var json in LlmRunner.ExtractJsonObjects(raw))
            {
                using var doc = JsonDocument.Parse(json);
                var form  = doc.RootElement.TryGetProperty("form", out var f) ? (f.GetString() ?? "").Trim() : "";
                var words = doc.RootElement.TryGetProperty("words", out var w) ? (w.GetString() ?? "").Trim() : "";
                // Not the soul's own name, not a bare title
                if (form.Contains(Player!.Name, StringComparison.OrdinalIgnoreCase) || !form.Contains(' ') ||
                    form.Equals("the messenger", StringComparison.OrdinalIgnoreCase)) continue;
                if (form.Length is > 2 and < 48 && words.Length > 20 && !form.Contains('<') && !words.Contains('<') &&
                    ContentFilter.IsClean(form) && ContentFilter.IsClean(words))
                    return (form.TrimEnd('.'), words.Length > 400 ? words[..400] : words);
            }
            GD.Print($"GameRoot: no messenger in reply, sending a scripted one: {raw}");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { }
        return ScriptedMessengers[Random.Shared.Next(ScriptedMessengers.Length)];
    }

    /// <summary>"There is one altar … It lies to the north-east, about 4 cells away …"</summary>
    private string AltarGuidance()
    {
        var where = "somewhere in this world";
        if (Altar != null && Player != null)
        {
            ParseCellId(Altar.CellId, out var ax, out var ay);
            int dx = ax * 8 + Altar.TileX - Player.TileX, dy = ay * 8 + Altar.TileY - Player.TileY;
            var ns = dy < -4 ? "north" : dy > 4 ? "south" : "";
            var ew = dx < -4 ? "west" : dx > 4 ? "east" : "";
            var dir = ns.Length > 0 && ew.Length > 0 ? $"{ns}-{ew}" : ns + ew;
            var cells = Math.Max(1, (int)Math.Round(Math.Sqrt(dx * dx + dy * dy) / 8));
            where = dir.Length == 0 ? "very near you" : $"to the {dir}, about {cells} cell{(cells == 1 ? "" : "s")} away";
        }
        return $"There is one altar in this world, unmarked. It lies {where}. Stand beside it and Pray: ask for a skill, " +
               "a thing or a law, or leave the choice to the gods. The Council answers in parable, but the first line of its " +
               "answer says plainly what entered the world.";
    }

    private static string Capitalise(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
