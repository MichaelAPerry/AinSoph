namespace AinSoph.World;

/// <summary>
/// A season the gods have sent: a long night, a famine or a time of plenty.
/// It lasts real hours, like everything else in the world, and is saved.
/// </summary>
public static class Season
{
    public static string?   Kind     { get; private set; }
    public static DateTime? UntilUtc { get; private set; }

    public static bool Is(string kind) =>
        Kind == kind && UntilUtc is { } until && DateTime.UtcNow < until;

    public static void Begin(string kind, double hours) => Set(kind, DateTime.UtcNow.AddHours(hours));

    public static void Set(string? kind, DateTime? untilUtc)
    {
        Kind     = kind;
        UntilUtc = untilUtc;
    }
}
