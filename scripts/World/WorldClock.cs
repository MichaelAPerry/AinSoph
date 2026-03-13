namespace AinSoph.World;

/// <summary>
/// The world clock. Time is real — synced to the player's local machine.
/// 24 real hours = 24 game hours. No compression, no expansion.
/// Skills that cost time cost real time.
/// </summary>
public class WorldClock
{
    /// <summary>Current UTC time. Always the real clock.</summary>
    public static DateTime NowUtc => DateTime.UtcNow;

    /// <summary>
    /// Night is determined by local time.
    /// Vision reduces to 1 grid square at night (vs. 3 during day).
    /// </summary>
    public static bool IsNight()
    {
        var local = DateTime.Now;
        return local.Hour < 6 || local.Hour >= 20;
    }

    /// <summary>Vision range in grid squares based on time of day.</summary>
    public static int VisionRange() => IsNight() ? 1 : 3;
}
