using AinSoph.World;
using Godot;

namespace AinSoph.NPC;

public enum AnimalType { Predator, Neutral, Prey, Insect }

/// <summary>
/// Animals are NPCs with limited action — no LLM, instinct only.
/// Same survival rules as everyone else: eat or die, sleep or die.
/// Kill rolls use the base numbers from RULES.md.
///
/// Predator: attacks nearby entities on tick.
/// Prey: flees nearby predators.
/// Neutral: ignores others, grazes.
/// Insect: ignores others, grazes.
/// </summary>
public class AnimalBrain
{
    public string     AnimalId   { get; }
    public string     Name       { get; }
    public AnimalType AnimalType { get; }
    public string     CellId     { get; set; }
    public int        TileX      { get; set; }
    public int        TileY      { get; set; }

    public SurvivalTracker Survival { get; }

    public int KillNumber => AnimalType switch
    {
        AnimalType.Predator => BaseKillNumbers.AnimalPredator,
        AnimalType.Neutral  => BaseKillNumbers.AnimalNeutral,
        AnimalType.Prey     => BaseKillNumbers.AnimalPrey,
        AnimalType.Insect   => BaseKillNumbers.AnimalInsect,
        _                   => BaseKillNumbers.AnimalNeutral
    };

    // Events the engine listens to
    public event Action<AnimalBrain, string>? OnAttack; // target entity id
    public event Action<AnimalBrain>?         OnDeath;
    public event Action<AnimalBrain>?         OnFlee;

    private DateTime _lastThinkUtc;
    private static readonly TimeSpan ThinkInterval = TimeSpan.FromHours(1);

    public AnimalBrain(string animalId, string name, AnimalType type,
        string cellId, int tileX, int tileY, DateTime nowUtc)
    {
        AnimalId   = animalId;
        Name       = name;
        AnimalType = type;
        CellId     = cellId;
        TileX      = tileX;
        TileY      = tileY;
        Survival   = new SurvivalTracker(nowUtc);
        _lastThinkUtc = nowUtc;
    }

    // -------------------------------------------------------------------------
    // Tick — hourly, no LLM
    // -------------------------------------------------------------------------

    public void Tick(AnimalSituation situation, DateTime nowUtc)
    {
        // Survival always ticks
        var result = Survival.Tick(nowUtc);
        if (result.IsDead)
        {
            OnDeath?.Invoke(this);
            return;
        }

        // Sleep — animals sleep if no threat nearby and exhausted
        if (Survival.IsSleeping)
        {
            var sleepHours = (nowUtc - (Survival.SleepStartUtc ?? nowUtc)).TotalHours;
            if (sleepHours >= SurvivalTracker.SleepRequired)
                Survival.EndSleep(nowUtc);
            return;
        }

        if (!ShouldThink(nowUtc)) return;
        _lastThinkUtc = nowUtc;

        // Eat if food nearby and hungry
        if (situation.NearbyEdibleItemId is not null &&
            (nowUtc - Survival.LastAteUtc).TotalHours > 12)
        {
            Survival.RecordEat(nowUtc);
            return;
        }

        switch (AnimalType)
        {
            case AnimalType.Predator:
                ActPredator(situation, nowUtc);
                break;
            case AnimalType.Prey:
                ActPrey(situation, nowUtc);
                break;
            case AnimalType.Neutral:
            case AnimalType.Insect:
                ActPassive(nowUtc);
                break;
        }
    }

    private void ActPredator(AnimalSituation situation, DateTime nowUtc)
    {
        if (situation.NearbyEntityId is not null)
        {
            OnAttack?.Invoke(this, situation.NearbyEntityId);
            return;
        }

        // No target — sleep if exhausted
        if ((nowUtc - Survival.LastSleptUtc).TotalHours > 20)
            Survival.BeginSleep(nowUtc, inCave: false);
    }

    private void ActPrey(AnimalSituation situation, DateTime nowUtc)
    {
        if (situation.NearbyPredatorPresent)
        {
            OnFlee?.Invoke(this);
            return;
        }

        if ((nowUtc - Survival.LastSleptUtc).TotalHours > 20)
            Survival.BeginSleep(nowUtc, inCave: false);
    }

    private void ActPassive(DateTime nowUtc)
    {
        if ((nowUtc - Survival.LastSleptUtc).TotalHours > 20)
            Survival.BeginSleep(nowUtc, inCave: false);
    }

    private bool ShouldThink(DateTime nowUtc) =>
        (nowUtc - _lastThinkUtc) >= ThinkInterval;
}

/// <summary>What an animal can perceive on its tick.</summary>
public class AnimalSituation
{
    public string? NearbyEntityId       { get; set; } // closest entity id in range
    public string? NearbyEdibleItemId   { get; set; }
    public bool    NearbyPredatorPresent { get; set; }
}
