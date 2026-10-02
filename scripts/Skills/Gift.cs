using System.Text.Json.Serialization;

namespace AinSoph.Skills;

/// <summary>
/// What a Council grant does in the engine. The model cannot write engine code,
/// so every approved skill or item is read as one of these — or as lore, which
/// is remembered but changes nothing that can be measured.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GiftEffect
{
    Lore,       // no mechanical effect
    Sight,      // extends See: +1 cell of sight, day and night
    Hearing,    // extends Hear: warned when a predator comes near
    Seafaring,  // extends Move: the sea can be crossed
    Strength,   // extends Reap: +15 to the kill number
    Endurance,  // survival: 36 hours between meals instead of 24
    Shelter,    // survival: sleeping in the open is as safe as a cave
    Mending,    // survival: a lost fight wounds instead of kills, once a day
    Kinship,    // extends Talk to beasts: predators pass by half the time
}

/// <summary>A skill or item the Council granted, held by one being.</summary>
public class Gift
{
    public string     Name        { get; set; } = string.Empty;
    public string     Description { get; set; } = string.Empty;
    public string     Kind        { get; set; } = "skill"; // "skill" | "item"
    public GiftEffect Effect      { get; set; }
    public DateTime   GrantedUtc  { get; set; }
    /// <summary>Set when the gift is a carried item: dropping or giving it away takes the gift with it.</summary>
    public string?    ItemId      { get; set; }

    /// <summary>"Fire Making — sleep safely in the open"</summary>
    public string Label => Effect == GiftEffect.Lore ? $"{Name} — lore" : $"{Name} — {Gifts.Describe(Effect)}";
}

/// <summary>Everything one being holds from the Council, with the rules each effect changes.</summary>
public class GiftSet
{
    public List<Gift> All { get; } = new();

    public bool Has(GiftEffect effect) => effect != GiftEffect.Lore && All.Any(g => g.Effect == effect);

    /// <summary>When Mending last turned a death into a wound.</summary>
    public DateTime? LastMendedUtc { get; set; }

    public void Add(Gift gift) => All.Add(gift);

    public void Restore(IEnumerable<Gift>? gifts, DateTime? lastMendedUtc)
    {
        All.Clear();
        if (gifts != null) All.AddRange(gifts);
        LastMendedUtc = lastMendedUtc;
    }

    // ── What the engine asks ─────────────────────────────────────────────

    public int SightBonusCells  => Has(GiftEffect.Sight) ? 1 : 0;
    public int KillBonus        => Has(GiftEffect.Strength) ? Gifts.StrengthBonus : 0;
    public double HungerHours   => Has(GiftEffect.Endurance) ? Gifts.EnduranceHours : World.SurvivalTracker.DayHours;

    /// <summary>Mending is ready if it has not saved this being in the last day.</summary>
    public bool CanMend(DateTime nowUtc) =>
        Has(GiftEffect.Mending) && (LastMendedUtc is null || (nowUtc - LastMendedUtc.Value).TotalHours >= 24);

    /// <summary>Short list for prompts: "Fire Making (sleep safely in the open); Star Reading (…)"</summary>
    public string Summary(int max = 4) =>
        string.Join("; ", All.TakeLast(max).Select(g =>
            g.Effect == GiftEffect.Lore ? g.Name : $"{g.Name} ({Gifts.Describe(g.Effect)})"));
}

/// <summary>Reading a petition as an effect, and describing effects to players and NPCs.</summary>
public static class Gifts
{
    public const int    StrengthBonus  = 15;
    public const double EnduranceHours = 36.0;

    // Ordered: on a tie the earlier effect wins. A word ending in * matches as a
    // prefix ("hunt*" catches hunting, hunter); others match whole words or plurals.
    private static readonly (GiftEffect Effect, string[] Words)[] Signals =
    {
        (GiftEffect.Shelter,   new[] { "fire*", "shelter*", "tent", "hut", "camp*", "warm*", "roof", "blanket", "house", "build*", "hearth" }),
        (GiftEffect.Mending,   new[] { "heal*", "mend*", "cure", "medicine*", "herb*", "salve", "bandage*", "remedy", "wound*" }),
        (GiftEffect.Strength,  new[] { "hunt*", "fight*", "spear", "bow", "arrow", "sword", "weapon*", "strength*", "strong", "trap*", "sling", "club", "knife", "battle", "shield", "kill*" }),
        (GiftEffect.Seafaring, new[] { "swim*", "boat*", "raft", "sail*", "sea", "ship", "canoe", "ferry", "oar" }),
        (GiftEffect.Sight,     new[] { "see", "sight*", "vision", "eye", "star", "telescope", "lamp", "lantern", "torch", "light", "watch*", "scout*", "glass", "navigat*" }),
        (GiftEffect.Hearing,   new[] { "hear*", "listen*", "ear", "sound", "echo*", "horn", "warn*" }),
        (GiftEffect.Endurance, new[] { "endur*", "hunger", "fasting", "forag*", "gather*", "preserv*", "cook*", "bake*", "bread", "farm*", "grow*", "plant*", "harvest*", "food" }),
        (GiftEffect.Kinship,   new[] { "tame*", "beast", "animal", "shepherd*", "calm*", "whisper*", "befriend*", "herd*" }),
    };

    private static bool Matches(string word, string signal) =>
        signal.EndsWith('*') ? word.StartsWith(signal[..^1]) : word == signal || word == signal + "s" || word == signal + "es";

    /// <summary>The effect a petition asks for — the one whose words it uses most.</summary>
    public static GiftEffect Classify(string text)
    {
        var words = System.Text.RegularExpressions.Regex.Split(text.ToLowerInvariant(), "[^a-z]+")
            .Where(w => w.Length > 1).ToArray();
        var best = GiftEffect.Lore;
        var bestHits = 0;
        foreach (var (effect, signals) in Signals)
        {
            var hits = words.Count(w => signals.Any(s => Matches(w, s)));
            if (hits > bestHits) { best = effect; bestHits = hits; }
        }
        return best;
    }

    public static string Describe(GiftEffect effect) => effect switch
    {
        GiftEffect.Sight     => "see one cell further",
        GiftEffect.Hearing   => "hear predators before they reach you",
        GiftEffect.Seafaring => "cross the sea",
        GiftEffect.Strength  => $"+{StrengthBonus} when you reap or are attacked",
        GiftEffect.Endurance => $"go {EnduranceHours:0} hours between meals",
        GiftEffect.Shelter   => "sleep safely in the open",
        GiftEffect.Mending   => "survive a lost fight, once a day",
        GiftEffect.Kinship   => "predators often pass you by",
        _                    => "remembered by the world; it changes nothing you can measure",
    };

    /// <summary>A few letters for the HUD chip.</summary>
    public static string Tag(GiftEffect effect) => effect switch
    {
        GiftEffect.Sight     => "+1 SIGHT",
        GiftEffect.Hearing   => "HEARING",
        GiftEffect.Seafaring => "CROSS SEA",
        GiftEffect.Strength  => $"+{StrengthBonus} REAP",
        GiftEffect.Endurance => $"{EnduranceHours:0}H FAST",
        GiftEffect.Shelter   => "SAFE SLEEP",
        GiftEffect.Mending   => "MENDING",
        GiftEffect.Kinship   => "BEASTS",
        _                    => "LORE",
    };

    /// <summary>The primitive an effect builds on, and how (SKILLS.md).</summary>
    public static (SkillType Base, SkillKind Kind) Basis(GiftEffect effect) => effect switch
    {
        GiftEffect.Sight     => (SkillType.See,  SkillKind.Extension),
        GiftEffect.Hearing   => (SkillType.Hear, SkillKind.Extension),
        GiftEffect.Seafaring => (SkillType.Move, SkillKind.Extension),
        GiftEffect.Strength  => (SkillType.Reap, SkillKind.Extension),
        GiftEffect.Kinship   => (SkillType.Talk, SkillKind.Extension),
        GiftEffect.Mending   => (SkillType.Reap, SkillKind.Substitute),
        _                    => (SkillType.Reap, SkillKind.Composite), // Endurance, Shelter: eating and sleeping
    };

    /// <summary>What a world-text announcement says after a grant.</summary>
    public static string Announce(Gift gift, bool alreadyHeld) =>
        gift.Effect == GiftEffect.Lore
            ? $"The Council grants {gift.Name}. It is lore: remembered, but it changes nothing you can measure."
            : $"The Council grants {gift.Name} — {Describe(gift.Effect)}." +
              (alreadyHeld ? " (Another gift already does this.)" : "");
}
