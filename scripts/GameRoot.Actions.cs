using AinSoph.Skills;
using AinSoph.World;
using Godot;

namespace AinSoph;

/// <summary>
/// The six primitives as the player reaches for them: from the action bar (or keys
/// 1–6), each acts on what is nearest — Move picks up, See describes, Hear listens,
/// Talk speaks to the nearest person, Reap eats or strikes a beast, Pray answers at
/// the altar. A target out of reach is walked to first, then acted on. Stepping up to
/// the altar opens the Council by itself.
/// </summary>
public partial class GameRoot
{
    private bool _approaching;
    private bool _besideAltar;
    private Vector2I _lastPlayerTile = new(int.MinValue, int.MinValue);

    /// <summary>Whether the player has seen the altar or been told where it is.</summary>
    private bool AltarKnown => Player is { } p && (p.EncounterDone || p.AltarSeen);

    /// <summary>The action bar: act on whatever is nearest.</summary>
    public void QuickAction(SkillType skill)
    {
        if (Player == null || _worldScene == null || string.IsNullOrEmpty(Player.Name)) return;
        var (px, py) = (Player.TileX, Player.TileY);

        switch (skill)
        {
            case SkillType.Move:
                var near = Items?.All.Where(i => Dist(i.TileX, i.TileY) <= 1).OrderBy(i => Dist(i.TileX, i.TileY)).FirstOrDefault();
                if (near != null) PickUp(near);
                else _worldScene.ShowWorldText("Nothing beside you to pick up. Click the ground, or use WASD, to walk.");
                break;

            case SkillType.See:
                _worldScene.ShowWorldText(Describe());
                break;

            case SkillType.Hear:
                _worldScene.ShowWorldText(Listen());
                break;

            case SkillType.Talk:
                var person = LiveNpcs.Where(n => Dist(n.TileX, n.TileY) <= 8).OrderBy(n => Dist(n.TileX, n.TileY)).FirstOrDefault();
                if (person != null) UsePrimitive(person.NpcId, SkillType.Talk);
                else _worldScene.ShowWorldText("No one is near enough to talk to.");
                break;

            case SkillType.Reap:
                // Food first; then a beast beside you. People are never struck from the bar — click them to do that.
                var food = Items?.All.Where(i => i.Edible && Dist(i.TileX, i.TileY) <= 1).OrderBy(i => Dist(i.TileX, i.TileY)).FirstOrDefault();
                var beast = LiveAnimals.Where(a => Dist(a.TileX, a.TileY) <= 1).OrderBy(a => Dist(a.TileX, a.TileY)).FirstOrDefault();
                if (food != null) UsePrimitive(food.Id, SkillType.Reap);
                else if (beast != null) UsePrimitive(beast.AnimalId, SkillType.Reap);
                else _worldScene.ShowWorldText("Nothing within reach to reap. Stand beside manna to eat it, or beside a beast to hunt it.");
                break;

            case SkillType.Pray:
                UsePrimitive("altar", SkillType.Pray);
                break;
        }
    }

    private static int Dist(int tx, int ty) =>
        Player == null ? int.MaxValue : Math.Max(Math.Abs(tx - Player.TileX), Math.Abs(ty - Player.TileY));

    /// <summary>Walk toward a (possibly moving) target until within reach, then act. False if it could not be reached.</summary>
    private async Task<bool> Approach(Func<Vector2I?> target, int reach)
    {
        if (_worldScene == null || Player == null || _approaching) return false;
        _approaching = true;
        try
        {
            for (int i = 0; i < 40; i++)
            {
                if (Player == null || target() is not { } t) return false;
                if (Dist(t.X, t.Y) <= reach) return true;
                if (_worldScene.DialogueOpen || _worldScene.InputLocked || _worldScene.MenuOpen) return false;
                var before = _worldScene.PlayerTile;
                var dx = Math.Sign(t.X - before.X);
                var dy = Math.Sign(t.Y - before.Y);
                _worldScene.Step(Math.Abs(t.X - before.X) >= Math.Abs(t.Y - before.Y) && dx != 0 ? new Vector2I(dx, 0) : new Vector2I(0, dy));
                if (_worldScene.PlayerTile == before)   // blocked: try the other axis once
                    _worldScene.Step(dx != 0 && dy != 0 ? new Vector2I(0, dy) : new Vector2I(dx == 0 ? 1 : 0, dy == 0 ? 1 : 0));
                await ToSignal(GetTree().CreateTimer(0.16), SceneTreeTimer.SignalName.Timeout);
            }
            return target() is { } last && Dist(last.X, last.Y) <= reach;
        }
        finally { _approaching = false; }
    }

    /// <summary>Called every frame: notice the player arriving somewhere that matters.</summary>
    private void TickPlayerPlace()
    {
        if (Player == null || _worldScene == null) return;
        var here = new Vector2I(Player.TileX, Player.TileY);
        if (here == _lastPlayerTile) return;
        _lastPlayerTile = here;
        Explore();

        // The altar shows itself once it is within sight; from then on it is known
        if (Altar != null && !Player.AltarSeen)
        {
            var (ax, ay) = AltarWorldTile();
            if (_worldScene.TileVisibleToPlayer(ax, ay))
            {
                Player.AltarSeen = true;
                _worldScene.ShowWorldText("You see it: a standing stone, cut in the shape of a cross. The altar.");
                AinSoph.Audio.Sound.Play("council");
            }
        }

        // Stepping up to the altar opens the Council — no other act is needed
        var beside = IsAtAltar();
        if (beside && !_besideAltar && !_worldScene.DialogueOpen && !_worldScene.InputLocked && !string.IsNullOrEmpty(Player.Name))
        {
            Player.AltarSeen = true;
            AinSoph.Audio.Sound.Play("pray");
            _worldScene.OpenAltar(petition => OnAltarPetition(petition));
        }
        _besideAltar = beside;
    }

    private (int X, int Y) AltarWorldTile()
    {
        ParseCellId(Altar!.CellId, out var ax, out var ay);
        return (ax * 8 + Altar.TileX, ay * 8 + Altar.TileY);
    }

    /// <summary>"to the north-east, about 3 cells away" — or null if the altar is unknown or unplaced.</summary>
    public string? AltarDirection()
    {
        if (Altar == null || Player == null) return null;
        var (ax, ay) = AltarWorldTile();
        int dx = ax - Player.TileX, dy = ay - Player.TileY;
        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) <= 1) return "right beside you";
        var ns = dy < -2 ? "north" : dy > 2 ? "south" : "";
        var ew = dx < -2 ? "west" : dx > 2 ? "east" : "";
        var dir = ns.Length > 0 && ew.Length > 0 ? $"{ns}-{ew}" : ns + ew;
        var tiles = (int)Math.Round(Math.Sqrt(dx * dx + dy * dy));
        var how = tiles < 8 ? $"{tiles} paces" : $"about {Math.Max(1, (int)Math.Round(tiles / 8.0))} cell{(tiles < 12 ? "" : "s")}";
        return dir.Length == 0 ? "very near" : $"to the {dir}, {how} away";
    }

    private string Describe()
    {
        var biome = BiomeData.Get(TileAtWorld(Player!.TileX, Player.TileY).Biome).Name.ToLowerInvariant();
        var seen = new List<string>();
        var people = LiveNpcs.Where(n => Dist(n.TileX, n.TileY) <= 8).Select(n => n.Name).Take(3).ToList();
        if (people.Count > 0) seen.Add(string.Join(", ", people));
        var beasts = LiveAnimals.Where(a => Dist(a.TileX, a.TileY) <= 8).GroupBy(a => a.Name)
                                .Select(g => g.Count() == 1 ? $"a {g.Key}" : $"{g.Count()} {g.Key}").Take(3).ToList();
        if (beasts.Count > 0) seen.Add(string.Join(", ", beasts));
        var manna = Items?.All.Count(i => i.Edible && Dist(i.TileX, i.TileY) <= 6) ?? 0;
        if (manna > 0) seen.Add(manna == 1 ? "food on the ground" : $"{manna} portions of food");
        var text = $"You stand in the {biome}." + (seen.Count > 0 ? $" You see {string.Join("; ", seen)}." : " Nothing moves near you.");
        if (AltarKnown && AltarDirection() is { } way) text += $" The altar lies {way}.";
        return text;
    }

    private string Listen()
    {
        var sounds = LiveAnimals.Where(a => Dist(a.TileX, a.TileY) <= 10)
            .OrderBy(a => a.AnimalType == NPC.AnimalType.Predator ? 0 : 1).ThenBy(a => Dist(a.TileX, a.TileY)).Take(3)
            .Select(a => $"a {a.Name} {Bearing(a.TileX, a.TileY)}").ToList();
        var voices = LiveNpcs.Where(n => Dist(n.TileX, n.TileY) <= 10).OrderBy(n => Dist(n.TileX, n.TileY)).Take(2)
            .Select(n => $"{n.Name}'s voice {Bearing(n.TileX, n.TileY)}").ToList();
        var all = voices.Concat(sounds).ToList();
        return all.Count == 0 ? "You listen. Only the wind." : $"You listen. You hear {string.Join(", ", all)}.";
    }

    private static string Bearing(int tx, int ty)
    {
        int dx = tx - Player!.TileX, dy = ty - Player.TileY;
        var ns = dy < -1 ? "north" : dy > 1 ? "south" : "";
        var ew = dx < -1 ? "west" : dx > 1 ? "east" : "";
        var dir = ns.Length > 0 && ew.Length > 0 ? $"{ns}-{ew}" : ns + ew;
        return dir.Length == 0 ? "close by" : $"to the {dir}";
    }
}
