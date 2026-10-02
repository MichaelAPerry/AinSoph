using AinSoph.Council;
using AinSoph.NPC;
using AinSoph.Skills;
using AinSoph.World;
using Godot;

namespace AinSoph;

/// <summary>
/// The gods' choice, carried out. The model picks one act and fills in its
/// details (GodsChoice.cs); here the engine does it — makes the creature, looses
/// the enemy, changes the land, sends food or a season, grants a gift or writes a
/// law — and saves it with the world. Every act also leaves an omen: the gods'
/// words and the deed, given to every NPC so they can react to what happened.
/// </summary>
public partial class GameRoot
{
    // Land the gods changed, by world tile — applied whenever that cell is generated
    private readonly Dictionary<(int X, int Y), BiomeType> _terrain = new();

    /// <summary>What the gods have done in this world, newest last.</summary>
    public static List<Data.OmenRecord> Omens { get; } = new();
    private const int MaxOmens = 24;

    // NPC petitions reach the gods too, but not so often that the world churns
    private DateTime _lastNpcDivineUtc = DateTime.MinValue;
    private TimeSpan NpcDivineCooldown => IsDemo ? TimeSpan.FromSeconds(60) : TimeSpan.FromMinutes(30);

    // Enemies strike at most this often
    private readonly Dictionary<string, DateTime> _lastEnemyStrike = new();
    private static readonly TimeSpan EnemyStrikeInterval = TimeSpan.FromSeconds(20);

    // ── Load and save ────────────────────────────────────────────────────

    private void LoadDivine(Data.WorldSaveData? world)
    {
        AnimalSpecies.ClearMade();
        _terrain.Clear();
        Omens.Clear();
        Season.Set(null, null);
        if (world != null)
        {
            foreach (var r in world.Species) AnimalSpecies.Register(FromRecord(r));
            foreach (var t in world.Terrain)
                if (Enum.TryParse<BiomeType>(t.Biome, out var b)) _terrain[(t.X, t.Y)] = b;
            Omens.AddRange(world.Omens.TakeLast(MaxOmens));
            Season.Set(world.Season, world.SeasonUntilUtc);
        }
        PublishOmens();
    }

    private void SaveDivine(Data.WorldSaveData world)
    {
        world.Species = AnimalSpecies.Made.Select(ToRecord).ToList();
        world.Terrain = _terrain.Select(kv => new Data.TerrainEdit { X = kv.Key.X, Y = kv.Key.Y, Biome = kv.Value.ToString() }).ToList();
        world.Omens   = Omens.ToList();
        world.Season  = Season.Kind;
        world.SeasonUntilUtc = Season.UntilUtc;
    }

    private void ApplyTerrainEdits(WorldCell cell)
    {
        if (_terrain.Count == 0) return;
        foreach (var tile in cell.AllTiles())
            if (_terrain.TryGetValue((cell.GridX * 8 + tile.TileX, cell.GridY * 8 + tile.TileY), out var biome))
                SetTileBiome(tile, biome);
    }

    private static void PublishOmens() =>
        NpcPromptBuilder.WorldOmens = Omens.TakeLast(3)
            .Select(o => string.IsNullOrEmpty(o.Proclamation) ? o.Deed : $"{o.Deed} (\"{o.Proclamation}\")").ToList();

    private static void RecordOmen(string proclamation, string deed)
    {
        Omens.Add(new Data.OmenRecord { Proclamation = proclamation, Deed = deed, Utc = DateTime.UtcNow });
        if (Omens.Count > MaxOmens) Omens.RemoveAt(0);
        PublishOmens();
    }

    // ── The act ──────────────────────────────────────────────────────────

    /// <summary>Carry out one act of the gods for a petitioner at (x, y). Returns the deed, in plain words.</summary>
    public string ApplyDivineAct(DivineAct act, string petitionerId, GiftSet petitionerGifts, int x, int y)
    {
        var deed = act.Act switch
        {
            "creature"  => MakeCreature(act, x, y),
            "enemy"     => LooseEnemy(act, x, y),
            "land"      => ChangeLand(act, x, y),
            "provision" => SendProvision(act, x, y),
            "season"    => SendSeason(act, x, y),
            "gift"      => GrantDivineGift(act, petitionerGifts),
            "law"       => WriteLaw(act, petitionerId),
            "omen"      => "The gods have spoken. Nothing else changes, but the world remembers their words.",
            _           => string.Empty,
        };
        if (string.IsNullOrEmpty(deed)) deed = "The gods spoke, and the world remembers it.";
        GD.Print($"GameRoot: the gods' choice — {act.Act}: {deed}");
        RecordOmen(act.Proclamation, deed);
        AinSoph.Audio.Sound.Play("council");
        _worldScene?.RefreshMap();
        SaveAll();
        return deed;
    }

    private string MakeCreature(DivineAct a, int px, int py)
    {
        var name = UniqueSpeciesName(a.Name);
        var type = a.Nature switch { "predator" => AnimalType.Predator, "prey" => AnimalType.Prey, _ => AnimalType.Neutral };
        var habitat = a.Habitat switch { "air" => AnimalHabitat.Bird, "water" => AnimalHabitat.Water, _ => AnimalHabitat.Land };
        var strength = type switch
        {
            AnimalType.Predator => Math.Clamp(a.Strength, 50, 95),
            AnimalType.Prey     => Math.Clamp(a.Strength, 5, 40),
            _                   => Math.Clamp(a.Strength, 10, 60),
        };
        var species = new AnimalSpecies(name, type, habitat, a.Edible && type != AnimalType.Predator,
                                        GlyphFor(name, habitat, type, enemy: false), TintFor(name, type, enemy: false))
        {
            Strength = strength, Look = a.Look, Divine = true,
        };
        AnimalSpecies.Register(species);

        var far = a.Where == "far";
        var placed = PlaceAnimals(species, a.Count, px, py, far ? 16 : 2, far ? 24 : 6);
        var look = a.Look.Length > 0 ? $" ({a.Look})" : "";
        return placed == 0
            ? $"A new creature, the {name}{look}, is made — it will be found in new lands."
            : $"A new creature, the {name}{look}, now lives in the world: {placed} {(far ? "far off" : "near you")}.";
    }

    private string LooseEnemy(DivineAct a, int px, int py)
    {
        var name = UniqueSpeciesName(a.Name);
        var species = new AnimalSpecies(name, AnimalType.Predator, AnimalHabitat.Land, false,
                                        GlyphFor(name, AnimalHabitat.Land, AnimalType.Predator, enemy: true),
                                        TintFor(name, AnimalType.Predator, enemy: true))
        {
            Strength = Math.Clamp(a.Strength, 40, 95), Hunts = true, Unique = true, Look = a.Look, Divine = true,
        };
        AnimalSpecies.Register(species);
        var placed = PlaceAnimals(species, 1, px, py, 9, 13);
        var look = a.Look.Length > 0 ? $", {a.Look}" : "";
        return placed == 0 ? $"An enemy is named — {name}{look} — but finds no ground to stand on."
                           : $"An enemy is loosed: {name}{look}. It hunts.";
    }

    private string ChangeLand(DivineAct a, int px, int py)
    {
        if (!Enum.TryParse<BiomeType>(a.Becomes, ignoreCase: true, out var biome)) return string.Empty;
        var rng = Random.Shared;
        var far = a.Where == "far";
        var angle = rng.NextDouble() * Math.PI * 2;
        var dist  = far ? rng.Next(16, 25) : rng.Next(3, 6);
        int cx = px + (int)Math.Round(Math.Cos(angle) * dist), cy = py + (int)Math.Round(Math.Sin(angle) * dist);
        bool watery = biome is BiomeType.Sea or BiomeType.River;

        int changed = 0;
        for (int dx = -a.Size; dx <= a.Size; dx++)
        for (int dy = -a.Size; dy <= a.Size; dy++)
        {
            if (dx * dx + dy * dy > a.Size * a.Size + a.Size) continue; // a rough disc
            int tx = cx + dx, ty = cy + dy;
            if (IsAltarTile(tx, ty)) continue;
            // Never drown a living being where it stands
            if (watery && ((tx, ty) == (px, py) || IsOccupied(tx, ty) || AnimalAt(tx, ty) != null)) continue;
            var tile = TileAtWorld(tx, ty);
            if (tile.HasCave) continue;
            SetTileBiome(tile, biome);
            _terrain[(tx, ty)] = biome;
            changed++;
        }
        _worldScene?.RefreshTerrain();
        var what = BiomeData.Get(biome).Name.ToLowerInvariant();
        return changed == 0 ? string.Empty : $"The land {(far ? "far off" : "near you")} becomes {what}.";
    }

    private string SendProvision(DivineAct a, int px, int py)
    {
        int placed = 0;
        var rng = Random.Shared;
        for (int tries = 0; tries < 80 && placed < a.Count; tries++)
        {
            int x = px + rng.Next(-3, 4), y = py + rng.Next(-3, 4);
            if ((x, y) == (px, py) || !IsPassableTile(x, y)) continue;
            if (Items!.All.Any(i => i.TileX == x && i.TileY == y)) continue;
            Items.Spawn(a.Name, a.Edible ? "provision" : "relic", x, y, edible: a.Edible,
                        lifespanHours: a.Edible ? 24f : null, description: a.Look);
            placed++;
        }
        return placed == 0 ? string.Empty
            : $"{placed} {a.Name} {(placed == 1 ? "appears" : "appear")} around you{(a.Edible ? " — food for a day" : "")}.";
    }

    private string SendSeason(DivineAct a, int px, int py)
    {
        Season.Begin(a.Kind, a.Hours);
        switch (a.Kind)
        {
            case "long night":
                return $"A long night falls on the world for {Hours(a.Hours)}.";
            case "famine":
                // The manna on the ground spoils, and none falls until it ends
                foreach (var m in Items!.All.Where(i => i.Type == "manna").ToList()) Items.Remove(m.Id);
                return $"Famine: the manna spoils, and none will fall for {Hours(a.Hours)}.";
            default: // plenty
                for (int i = 0; i < 6; i++)
                {
                    int x = px + Random.Shared.Next(-4, 5), y = py + Random.Shared.Next(-4, 5);
                    if ((x, y) != (px, py) && IsPassableTile(x, y)) Items!.SpawnManna(x, y);
                }
                return $"Plenty: manna falls around you, and twice over each morning for {Hours(a.Hours)}.";
        }
    }

    private string GrantDivineGift(DivineAct a, GiftSet gifts)
    {
        if (!Enum.TryParse<GiftEffect>(a.Effect, ignoreCase: true, out var effect)) effect = GiftEffect.Lore;
        var already = gifts.Has(effect);
        var gift = new Gift { Name = a.Name, Description = a.Proclamation, Kind = "skill", Effect = effect, GrantedUtc = DateTime.UtcNow };
        gifts.Add(gift);
        if (ReferenceEquals(gifts, Player?.Gifts)) _worldScene?.SetGifts(Player!.Gifts.All);
        return Gifts.Announce(gift, already).Replace("The Council grants", "The gods grant");
    }

    private string WriteLaw(DivineAct a, string petitionerId)
    {
        AddLaw(a.Name, a.Text, petitionerId);
        return $"A new law: {a.Name} — {a.Text}";
    }

    private static string Hours(int h) => h == 1 ? "an hour" : $"{h} hours";

    // ── NPC petitions ────────────────────────────────────────────────────

    /// <summary>An NPC's approved petition that no fixed gift fits goes to the gods — now and then.</summary>
    private async void LetTheGodsAnswer(NpcBrain npc, CouncilSubmission sub)
    {
        if (DateTime.UtcNow - _lastNpcDivineUtc < NpcDivineCooldown) return;
        _lastNpcDivineUtc = DateTime.UtcNow;
        var act = await GodsChoice.AskAsync(Llm, sub.Description, GodsChoice.IsDeferral(sub.Description),
                                            PlaceName(npc.TileX, npc.TileY), _cts.Token);
        if (act == null || !LiveNpcs.Contains(npc)) return;
        var deed = ApplyDivineAct(act, npc.NpcId, npc.Gifts, npc.TileX, npc.TileY);
        npc.Memory.Write(MemorySlot.Feeling, $"I prayed, and the gods answered: {deed}");
        if (IsNear(npc)) _worldScene?.ShowWorldText($"{npc.Name} prayed. {deed}");
    }

    // ── Enemies ──────────────────────────────────────────────────────────

    /// <summary>Each enemy stalks the nearest being within reach, and strikes when beside it.</summary>
    public void HuntWithEnemies()
    {
        foreach (var enemy in LiveAnimals.Where(a => a.Species?.Hunts == true).ToList())
        {
            if (!LiveAnimals.Contains(enemy)) continue;
            var (tx, ty, targetId) = NearestPrey(enemy);
            if (targetId == null) continue;

            if (Math.Max(Math.Abs(tx - enemy.TileX), Math.Abs(ty - enemy.TileY)) <= 1)
            {
                var now = DateTime.UtcNow;
                if (_lastEnemyStrike.TryGetValue(enemy.AnimalId, out var last) && now - last < EnemyStrikeInterval) continue;
                // Shelter and kinship still hold against an enemy
                if (BuildAnimalSituation(enemy).NearbyEntityId != targetId) continue;
                _lastEnemyStrike[enemy.AnimalId] = now;
                OnAnimalAttack(enemy, targetId);
                continue;
            }

            int sx = Math.Sign(tx - enemy.TileX), sy = Math.Sign(ty - enemy.TileY);
            foreach (var (dx, dy) in new[] { (sx, sy), (sx, 0), (0, sy) })
            {
                int nx = enemy.TileX + dx, ny = enemy.TileY + dy;
                if ((dx, dy) == (0, 0) || enemy.Species is not { } sp) continue;
                if (AnimalCanStand(sp, nx, ny) && !IsOccupied(nx, ny) && AnimalAt(nx, ny) == null &&
                    (Player == null || (nx, ny) != (Player.TileX, Player.TileY)))
                {
                    MoveAnimal(enemy, nx, ny);
                    break;
                }
            }
        }
    }

    private (int X, int Y, string? Id) NearestPrey(AnimalBrain enemy)
    {
        const int Reach = 20;
        static int D(int ax, int ay, int bx, int by) => Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));
        if (Player != null && !string.IsNullOrEmpty(Player.Name) && D(Player.TileX, Player.TileY, enemy.TileX, enemy.TileY) <= Reach)
            return (Player.TileX, Player.TileY, Player.Id);
        var npc = LiveNpcs.Where(n => D(n.TileX, n.TileY, enemy.TileX, enemy.TileY) <= Reach)
                          .OrderBy(n => D(n.TileX, n.TileY, enemy.TileX, enemy.TileY)).FirstOrDefault();
        return npc == null ? (0, 0, null) : (npc.TileX, npc.TileY, npc.NpcId);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private int PlaceAnimals(AnimalSpecies species, int count, int px, int py, int minDist, int maxDist)
    {
        var rng = Random.Shared;
        int placed = 0;
        for (int tries = 0; tries < 200 && placed < count; tries++)
        {
            var angle = rng.NextDouble() * Math.PI * 2;
            var dist  = rng.Next(minDist, maxDist + 1);
            int x = px + (int)Math.Round(Math.Cos(angle) * dist), y = py + (int)Math.Round(Math.Sin(angle) * dist);
            if (!AnimalCanStand(species, x, y) || IsOccupied(x, y) || AnimalAt(x, y) != null) continue;
            if (Player != null && (x, y) == (Player.TileX, Player.TileY)) continue;
            AddAnimal(species, x, y, DateTime.UtcNow);
            placed++;
        }
        return placed;
    }

    private static string UniqueSpeciesName(string name)
    {
        name = name.Trim();
        if (name.StartsWith("the ", StringComparison.OrdinalIgnoreCase)) name = name[4..];
        if (AnimalSpecies.Get(name) is { Divine: false }) name = $"{name} of the gods";
        // Two of the gods' creatures never share a name
        var unique = name;
        for (int n = 2; AnimalSpecies.Get(unique) != null; n++) unique = $"{name} {n}";
        return unique;
    }

    private static readonly int[] LandBeasts = { 20, 21, 22, 23, 27, 28 };
    private static readonly int[] Monsters   = { 9, 10, 12, 13, 24 };

    private static int GlyphFor(string name, AnimalHabitat habitat, AnimalType type, bool enemy)
    {
        var h = UI.TileRegistry.StableHash(name);
        if (enemy) return Monsters[h % Monsters.Length];
        return habitat switch
        {
            AnimalHabitat.Bird  => 25,
            AnimalHabitat.Water => 26,
            _ => type == AnimalType.Predator ? new[] { 12, 13, 23, 20 }[h % 4] : LandBeasts[h % LandBeasts.Length],
        };
    }

    private static Color TintFor(string name, AnimalType type, bool enemy)
    {
        var h = UI.TileRegistry.StableHash(name);
        if (enemy) return Color.FromHsv(0.95f + (h % 10) / 100f, 0.75f, 0.85f);
        var hue = type == AnimalType.Predator ? (h % 60) / 1000f : (h % 1000) / 1000f;
        return Color.FromHsv(hue, type == AnimalType.Predator ? 0.6f : 0.35f, 0.95f);
    }

    private static Data.SpeciesRecord ToRecord(AnimalSpecies s) => new()
    {
        Name = s.Name, Type = s.Type.ToString(), Habitat = s.Habitat.ToString(), Edible = s.Edible,
        Glyph = s.Glyph, Tint = s.Tint.ToHtml(false), Strength = s.Strength ?? 0,
        Hunts = s.Hunts, Unique = s.Unique, Look = s.Look,
    };

    private static AnimalSpecies FromRecord(Data.SpeciesRecord r) =>
        new(r.Name,
            Enum.TryParse<AnimalType>(r.Type, out var t) ? t : AnimalType.Neutral,
            Enum.TryParse<AnimalHabitat>(r.Habitat, out var hb) ? hb : AnimalHabitat.Land,
            r.Edible, r.Glyph, Color.FromHtml(r.Tint))
        {
            Strength = r.Strength > 0 ? r.Strength : null, Hunts = r.Hunts, Unique = r.Unique, Look = r.Look, Divine = true,
        };

    private static void SetTileBiome(Tile tile, BiomeType biome)
    {
        tile.Biome = biome;
        var v = (tile.TileX * 7 + tile.TileY * 13) % 5;
        tile.Surface = biome switch
        {
            BiomeType.Sea or BiomeType.River => TileSurface.Water,
            BiomeType.Forest   => v < 3 ? (v % 2 == 0 ? TileSurface.TreeCedar : TileSurface.TreeOlive) : TileSurface.Ground,
            BiomeType.Grove    => v < 2 ? (v == 0 ? TileSurface.TreeFig : TileSurface.TreePalm) : TileSurface.Grass,
            BiomeType.Desert   => v == 0 ? TileSurface.Rock : TileSurface.Sand,
            BiomeType.Mountain => v < 2 ? TileSurface.Rock : TileSurface.Stone,
            BiomeType.Valley   => v < 3 ? TileSurface.Grass : TileSurface.Ground,
            _                  => v == 0 ? TileSurface.Grass : TileSurface.Ground,
        };
    }

    private static Tile TileAtWorld(int tx, int ty)
    {
        int cx = tx < 0 ? (tx - 7) / 8 : tx / 8;
        int cy = ty < 0 ? (ty - 7) / 8 : ty / 8;
        return Grid!.GetOrGenerate(cx, cy).GetTile(tx - cx * 8, ty - cy * 8);
    }

    private static bool IsAltarTile(int tx, int ty)
    {
        if (Altar == null) return false;
        ParseCellId(Altar.CellId, out var ax, out var ay);
        return Math.Abs(ax * 8 + Altar.TileX - tx) <= 1 && Math.Abs(ay * 8 + Altar.TileY - ty) <= 1;
    }

    /// <summary>"the forest" — where a petitioner stands, for the gods' prompt.</summary>
    private static string PlaceName(int tx, int ty) =>
        $"the {BiomeData.Get(TileAtWorld(tx, ty).Biome).Name.ToLowerInvariant()}";
}
