namespace AinSoph.World;

/// <summary>
/// Tracks the eat and sleep state of one character (PC, NPC, or animal).
/// All times are real-world UTC.
///
/// Rules:
/// - Must eat once per 24-hour window. Binary — ate or didn't.
/// - Must sleep 8 continuous real hours per 24-hour window.
/// - Warning issued at hour 23 without eating or sleeping.
/// - Failure at hour 24 = death.
/// </summary>
public class SurvivalTracker
{
    public const double DayHours      = 24.0;
    public const double WarningHours  = 23.0;
    public const double SleepRequired = 8.0;

    // --- Eat ---
    public DateTime LastAteUtc { get; private set; }
    public bool EatWarningFired { get; private set; }

    // --- Sleep ---
    public DateTime? SleepStartUtc { get; private set; }   // null = not sleeping
    public double SleepBankHours   { get; private set; }   // continuous hours accumulated
    public bool SleptThisWindow    { get; private set; }
    public DateTime LastSleptUtc   { get; private set; }
    public bool SleepWarningFired  { get; private set; }

    // --- Location ---
    public bool IsInCave { get; private set; }

    public bool IsSleeping => SleepStartUtc.HasValue;
    public bool IsExposed  => IsSleeping && !IsInCave;

    public SurvivalTracker(DateTime nowUtc)
    {
        LastAteUtc    = nowUtc;
        LastSleptUtc  = nowUtc;
        SleptThisWindow = false;
        SleepBankHours  = 0;
    }

    // -------------------------------------------------------------------------
    // Eat
    // -------------------------------------------------------------------------

    /// <summary>Character consumed an edible item.</summary>
    public void RecordEat(DateTime nowUtc)
    {
        LastAteUtc      = nowUtc;
        EatWarningFired = false;
    }

    // -------------------------------------------------------------------------
    // Sleep
    // -------------------------------------------------------------------------

    /// <summary>Character begins sleeping. Records start time.</summary>
    public void BeginSleep(DateTime nowUtc, bool inCave)
    {
        if (IsSleeping) return;
        SleepStartUtc = nowUtc;
        IsInCave      = inCave;
        SleepBankHours = 0;
    }

    /// <summary>Character wakes up. Accumulates hours — resets if not 8 continuous.</summary>
    public void EndSleep(DateTime nowUtc)
    {
        if (!SleepStartUtc.HasValue) return;

        var hours = (nowUtc - SleepStartUtc.Value).TotalHours;
        SleepStartUtc = null;
        IsInCave      = false;

        if (hours >= SleepRequired)
        {
            SleptThisWindow   = true;
            LastSleptUtc      = nowUtc;
            SleepBankHours    = 0;
            SleepWarningFired = false;
        }
        // Interrupted sleep — bank is reset; must start over
    }

    /// <summary>
    /// Called when a cave is entered. Updates sleep safety if currently sleeping.
    /// </summary>
    public void EnterCave()  => IsInCave = true;

    /// <summary>
    /// Called when a cave is exited.
    /// </summary>
    public void ExitCave()   => IsInCave = false;

    // -------------------------------------------------------------------------
    // Tick — call once per game update to check warnings and death
    // -------------------------------------------------------------------------

    /// <summary>
    /// Evaluates the current survival state.
    /// Returns the result — caller is responsible for acting on it (warnings, death).
    /// </summary>
    public SurvivalTickResult Tick(DateTime nowUtc)
    {
        var result = new SurvivalTickResult();

        // Accumulate in-progress sleep hours for UI display
        if (IsSleeping && SleepStartUtc.HasValue)
        {
            SleepBankHours = (nowUtc - SleepStartUtc.Value).TotalHours;

            // Auto-complete sleep if 8 hours have passed
            if (SleepBankHours >= SleepRequired)
                EndSleep(nowUtc);
        }

        // --- Eat check ---
        var hoursSinceEat = (nowUtc - LastAteUtc).TotalHours;

        if (hoursSinceEat >= DayHours)
        {
            result.DiedOfStarvation = true;
            return result;
        }

        if (hoursSinceEat >= WarningHours && !EatWarningFired)
        {
            EatWarningFired     = true;
            result.HungerWarning = true;
        }

        // --- Sleep check ---
        var hoursSinceSlept = (nowUtc - LastSleptUtc).TotalHours;

        if (!SleptThisWindow && hoursSinceSlept >= DayHours)
        {
            result.DiedOfExhaustion = true;
            return result;
        }

        if (!SleptThisWindow && hoursSinceSlept >= WarningHours && !SleepWarningFired)
        {
            SleepWarningFired   = true;
            result.SleepWarning  = true;
        }

        // Reset window if the character slept successfully
        if (SleptThisWindow && hoursSinceSlept >= DayHours)
        {
            SleptThisWindow   = false;
            SleepWarningFired = false;
        }

        return result;
    }
}

public class SurvivalTickResult
{
    public bool HungerWarning      { get; set; }
    public bool SleepWarning       { get; set; }
    public bool DiedOfStarvation   { get; set; }
    public bool DiedOfExhaustion   { get; set; }

    public bool IsDead => DiedOfStarvation || DiedOfExhaustion;
}
