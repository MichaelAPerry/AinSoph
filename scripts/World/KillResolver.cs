namespace AinSoph.World;

/// <summary>
/// Kill roll resolution per RULES.md.
/// Both attacker and defender roll d100.
/// Attacker must roll ≤ their kill number to succeed.
/// Defender must roll ≤ their kill number to resist or flee.
/// Ties go to the defender.
/// </summary>
public static class KillResolver
{
    private static readonly Random _rng = new();

    public static KillResult Resolve(int attackerKillNumber, int defenderKillNumber)
    {
        var attackRoll  = _rng.Next(1, 101); // 1–100 inclusive
        var defenseRoll = _rng.Next(1, 101);

        var attackerHit = attackRoll  <= attackerKillNumber;
        var defenderHit = defenseRoll <= defenderKillNumber;

        // Ties go to defender
        var attackerKills = attackerHit && !defenderHit;

        return new KillResult
        {
            AttackerRoll     = attackRoll,
            DefenderRoll     = defenseRoll,
            AttackerKillNum  = attackerKillNumber,
            DefenderKillNum  = defenderKillNumber,
            AttackerSucceeds = attackerKills
        };
    }
}

public class KillResult
{
    public int  AttackerRoll     { get; set; }
    public int  DefenderRoll     { get; set; }
    public int  AttackerKillNum  { get; set; }
    public int  DefenderKillNum  { get; set; }
    public bool AttackerSucceeds { get; set; }
}

/// <summary>Base kill numbers per RULES.md.</summary>
public static class BaseKillNumbers
{
    public const int Pc              = 50;
    public const int NpcBase         = 50;  // ± decan modifier applied at runtime
    public const int AnimalPredator  = 80;  // lion, wolf, bear, eagle
    public const int AnimalNeutral   = 20;  // horse, donkey, camel, ox
    public const int AnimalPrey      = 10;  // sheep, deer, rabbit, dove
    public const int AnimalInsect    = 5;   // locust
}
