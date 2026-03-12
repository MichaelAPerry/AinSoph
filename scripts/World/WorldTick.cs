using Godot;

namespace AinSoph.World;

/// <summary>
/// The world tick. Runs on a real-time schedule.
///
/// Morning tick (06:00 local): spawn manna across all loaded cells.
/// Hourly tick: drive NPC think cycles, survival checks.
/// The world does not pause when players log out.
/// </summary>
public partial class WorldTick : Node
{
    private readonly WorldGrid _grid;
    private DateTime _lastMorningSpawn = DateTime.MinValue;
    private DateTime _lastHourlyTick   = DateTime.MinValue;

    public event Action<Dictionary<string, List<(int TileX, int TileY)>>>? OnMorningManna;
    public event Action? OnHourlyTick;

    public WorldTick(WorldGrid grid)
    {
        _grid = grid;
    }

    public override void _Process(double delta)
    {
        var now   = DateTime.Now; // local time — the world clock is the player's clock
        var nowUtc = DateTime.UtcNow;

        // Morning manna — fires once per day at 06:00 local
        if (now.Hour >= 6 && (now.Date != _lastMorningSpawn.Date || _lastMorningSpawn == DateTime.MinValue))
        {
            _lastMorningSpawn = now;
            var spawned = _grid.SpawnMorningManna();
            OnMorningManna?.Invoke(spawned);
            GD.Print($"WorldTick: morning manna spawned across {spawned.Count} cells");
        }

        // Hourly tick
        if ((nowUtc - _lastHourlyTick).TotalHours >= 1.0)
        {
            _lastHourlyTick = nowUtc;
            OnHourlyTick?.Invoke();
        }
    }
}
