using AinSoph.Skills;
using AinSoph.World;

namespace AinSoph.Player;

/// <summary>
/// The player character.
/// Same survival rules as NPCs — eat, sleep, die.
/// Same kill resolution — d100.
/// Earns the rib after 168 accumulated real hours in-world.
/// One rib, ever. One spouse, ever.
/// </summary>
public class PlayerCharacter
{
    public string Id       { get; set; } = Guid.NewGuid().ToString();
    public string Name     { get; set; } = string.Empty;
    public string CellId   { get; set; } = string.Empty;
    public int    TileX    { get; set; }
    public int    TileY    { get; set; }

    // Survival — same rules as NPCs
    public SurvivalTracker Survival { get; }

    // Skills — all six primitives at birth
    public HashSet<string> SkillIds { get; } = new(PrimitiveSkills.All);

    // What the Council has granted — each gift changes a rule (see Gift.cs)
    public GiftSet Gifts { get; } = new();

    // What you carry (GameRoot.Pack.cs) — at most MaxCarried things
    public List<Data.CarriedItem> Carried { get; } = new();
    public const int MaxCarried = 8;

    // A broken law (GameRoot.Laws.cs): the Council will not hear you until a day has passed
    public string?   LawBroken    { get; set; }
    public DateTime? LawBrokenUtc { get; set; }
    public bool IsBranded(DateTime nowUtc) => LawBrokenUtc is { } t && (nowUtc - t).TotalHours < 24;

    // The Council's first encounter (GameRoot.Encounter.cs) — once per life
    public bool EncounterDone { get; set; }

    // Whether this life has seen the altar (then the status line guides you to it)
    public bool AltarSeen { get; set; }

    // Cells this life has seen, for the map (GameRoot.Map.cs)
    public HashSet<(int X, int Y)> Explored { get; } = new();

    // Inventory
    public List<string> InventoryItemIds { get; } = new();

    // Rib and tribe
    public bool         HasRib       { get; private set; }
    public string?      SpouseNpcId  { get; private set; }
    public List<string> ProgenyIds   { get; } = new();

    // Play time tracking for rib — accumulated real hours
    public double AccumulatedPlayHours { get; set; }
    private DateTime? _sessionStartUtc;

    public const double RibEarnHours = 168.0; // 1 real week

    public PlayerCharacter(DateTime nowUtc)
    {
        Survival = new SurvivalTracker(nowUtc) { HungerHours = () => Gifts.HungerHours };
    }

    // -------------------------------------------------------------------------
    // Play time
    // -------------------------------------------------------------------------

    public void BeginSession(DateTime nowUtc)
    {
        _sessionStartUtc = nowUtc;
    }

    public void EndSession(DateTime nowUtc)
    {
        if (_sessionStartUtc is null) return;
        AccumulatedPlayHours += (nowUtc - _sessionStartUtc.Value).TotalHours;
        _sessionStartUtc = null;

        CheckRibEarned();
    }

    public void TickPlayTime(DateTime nowUtc)
    {
        if (_sessionStartUtc is null) return;
        var sessionHours = (nowUtc - _sessionStartUtc.Value).TotalHours;
        CheckRibEarned(sessionHours);
    }

    private void CheckRibEarned(double sessionHours = 0)
    {
        if (!HasRib && (AccumulatedPlayHours + sessionHours) >= RibEarnHours)
            HasRib = true;
    }

    public double TotalPlayHours =>
        AccumulatedPlayHours +
        (_sessionStartUtc.HasValue
            ? (DateTime.UtcNow - _sessionStartUtc.Value).TotalHours
            : 0);

    // -------------------------------------------------------------------------
    // Rib and spouse
    // -------------------------------------------------------------------------

    /// <summary>
    /// Claim the rib and record the spouse NPC id.
    /// Can only be called once — the rib is spent creating the spouse.
    /// </summary>
    public bool ClaimRib(string spouseNpcId)
    {
        if (!HasRib || SpouseNpcId is not null) return false;
        SpouseNpcId = spouseNpcId;
        return true;
    }

    public bool HasSpouse => SpouseNpcId is not null;

    /// <summary>Restore tribe state from a save. The rib is never taken back once earned.</summary>
    public void RestoreTribe(bool hasRib, string? spouseNpcId, IEnumerable<string> progenyIds)
    {
        HasRib      = hasRib || spouseNpcId is not null;
        SpouseNpcId = spouseNpcId;
        ProgenyIds.Clear();
        ProgenyIds.AddRange(progenyIds);
        CheckRibEarned();
    }

    /// <summary>Grant the rib now — for testing and the demo tour only.</summary>
    public void GrantRibForTesting() => HasRib = true;

    // -------------------------------------------------------------------------
    // Progeny
    // -------------------------------------------------------------------------

    public void AddProgeny(string npcId) => ProgenyIds.Add(npcId);

    // -------------------------------------------------------------------------
    // Kill number — base 50, raised by the Strength gift
    // -------------------------------------------------------------------------

    public int KillNumber => BaseKillNumbers.Pc + Gifts.KillBonus;
}
