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
        Survival = new SurvivalTracker(nowUtc);
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

    // -------------------------------------------------------------------------
    // Progeny
    // -------------------------------------------------------------------------

    public void AddProgeny(string npcId) => ProgenyIds.Add(npcId);

    // -------------------------------------------------------------------------
    // Kill number — base 50, modified by skills eventually
    // -------------------------------------------------------------------------

    public int KillNumber => BaseKillNumbers.Pc;
}
