using AinSoph.Council;
using Godot;

namespace AinSoph;

/// <summary>
/// Laws bind the player as they bind NPCs. When the player reaps a being or
/// eats, the deed is judged against the world's laws (LawJudge). A broken law
/// brands the player for a day: the Council will not hear their prayers, the
/// status line says so, and every NPC is told.
/// </summary>
public partial class GameRoot
{
    /// <summary>Judge a deed the player did, if the world has laws. Runs in the background.</summary>
    private async void JudgeDeed(string deed)
    {
        if (Player == null || Laws.Count == 0 || Player.IsBranded(DateTime.UtcNow)) return;
        var who = Player;
        var broken = await LawJudge.JudgeAsync(Llm, Laws, $"{who.Name} {deed}", _cts.Token);
        if (broken == null || Player != who) return;
        Brand(broken);
    }

    /// <summary>The player broke a law: the Council turns away for a day, and the world knows.</summary>
    public void Brand(Data.LawRecord law)
    {
        if (Player == null) return;
        Player.LawBroken    = law.Name;
        Player.LawBrokenUtc = DateTime.UtcNow;
        GD.Print($"GameRoot: {Player.Name} broke the law '{law.Name}'");
        _worldScene?.ShowWorldText($"You have broken the law: {law.Name}. For a day the Council will not hear you.");
        AinSoph.Audio.Sound.Play("warning");
        RecordOmen("", $"{Player.Name} broke the law \"{law.Name}\".");
        UpdateSurvivalStatus();
        SaveAll();
    }

    /// <summary>What the Council says instead of hearing a lawbreaker, or null if it will hear them.</summary>
    public string? CouncilRefusal()
    {
        if (Player is not { } p || !p.IsBranded(DateTime.UtcNow)) return null;
        var left = 24 - (DateTime.UtcNow - p.LawBrokenUtc!.Value).TotalHours;
        return $"» The Council does not hear one who broke the law \"{p.LawBroken}\". " +
               $"It will hear you again in {Math.Ceiling(left):0} hours.\n\n" +
               "[ THE COUNCIL ]\nA man came to the well with mud on his hands and asked for clean water. " +
               "The keeper said: first wash, then ask.";
    }

    /// <summary>For NPCs told who they are talking to.</summary>
    private string PlayerStanding() =>
        Player is { } p && p.IsBranded(DateTime.UtcNow) ? $"They broke the law \"{p.LawBroken}\" within the day." : "";
}
