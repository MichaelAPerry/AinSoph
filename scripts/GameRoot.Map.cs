using AinSoph.UI;
using AinSoph.World;
using Godot;

namespace AinSoph;

/// <summary>
/// What this life has seen, for the map (MAP or M). Every cell within sight of the
/// player is remembered; a new life starts with an empty map, as it starts with no
/// knowledge of the old one.
/// </summary>
public partial class GameRoot
{
    private MapScreen? _map;

    /// <summary>Remember the cells now within sight.</summary>
    private void Explore()
    {
        if (Player == null) return;
        int pcx = Player.TileX < 0 ? (Player.TileX - 7) / 8 : Player.TileX / 8;
        int pcy = Player.TileY < 0 ? (Player.TileY - 7) / 8 : Player.TileY / 8;
        int r = WorldClock.VisionRange() + Player.Gifts.SightBonusCells;
        for (int dx = -r; dx <= r; dx++)
        for (int dy = -r; dy <= r; dy++)
            if (dx * dx + dy * dy <= r * r + r) Player.Explored.Add((pcx + dx, pcy + dy));
    }

    private void OpenMap()
    {
        if (Player == null || _worldScene == null || string.IsNullOrEmpty(Player.Name)) return;
        if (_map != null) { CloseMap(); return; }
        Explore();
        _map = new MapScreen(() => Player?.Explored ?? new HashSet<(int, int)>(), () => Grid, MapMarkers,
                             () => (Player?.TileX ?? 0, Player?.TileY ?? 0), CloseMap);
        AddChild(_map);
        _worldScene.InputLocked = true;
    }

    private void CloseMap()
    {
        _map?.QueueFree();
        _map = null;
        if (_worldScene != null) _worldScene.InputLocked = false;
    }

    private List<MapScreen.Marker> MapMarkers()
    {
        var list = new List<MapScreen.Marker>();
        if (Player == null) return list;
        bool Seen(int tx, int ty) =>
            Player!.Explored.Contains((tx < 0 ? (tx - 7) / 8 : tx / 8, ty < 0 ? (ty - 7) / 8 : ty / 8));

        foreach (var n in LiveNpcs.Where(n => Seen(n.TileX, n.TileY)))
        {
            bool mine = n.NpcId == Player.SpouseNpcId || Player.ProgenyIds.Contains(n.NpcId);
            list.Add(new(n.TileX, n.TileY, mine ? new Color(0.45f, 0.9f, 0.5f) : new Color(0.85f, 0.82f, 0.7f), mine ? 3.5f : 2.5f));
        }
        foreach (var a in LiveAnimals.Where(a => (a.Species?.Hunts == true || a.AnimalType == NPC.AnimalType.Predator) &&
                                                 Dist(a.TileX, a.TileY) <= 16))
            list.Add(new(a.TileX, a.TileY, new Color(0.95f, 0.3f, 0.25f), a.Species?.Hunts == true ? 4f : 2.5f));

        if (Altar != null)
        {
            var (ax, ay) = AltarWorldTile();
            if (Player.AltarSeen) list.Add(new(ax, ay, new Color(0.75f, 0.8f, 1f), 5f));
            else if (Player.EncounterDone) list.Add(new(ax, ay, new Color(0.75f, 0.8f, 1f, 0.8f), 14f, Ring: true));
        }
        list.Add(new(Player.TileX, Player.TileY, new Color(1f, 0.85f, 0.4f), 4.5f));
        return list;
    }
}
