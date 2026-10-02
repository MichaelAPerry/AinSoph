using AinSoph.LLM;
using AinSoph.NPC;
using AinSoph.World;
using Godot;

namespace AinSoph;

/// <summary>
/// The world moving while you watch. NPCs think every couple of minutes (the model
/// decides), but between thoughts they walk about, and they greet you when you come
/// near. Prey shy away from you. Predators that catch your scent stalk you: first a
/// warning, then they close in, and strike when beside you — unless you are sheltered,
/// or a gift of kinship turns them aside. Run, or fight.
/// </summary>
public partial class GameRoot
{
    /// <summary>How often an NPC asks the model what to do, with the real model.</summary>
    private static readonly TimeSpan LiveThinkInterval = TimeSpan.FromMinutes(2);

    private double _npcWanderTimer;
    private const double NpcWanderSeconds = 3.0;

    private readonly Dictionary<string, DateTime> _greetedUtc = new();
    private static readonly TimeSpan GreetAgain = TimeSpan.FromMinutes(10);
    private bool _greeting;

    private readonly Dictionary<string, DateTime> _stalkWarnedUtc = new();
    private readonly Dictionary<string, DateTime> _lastPredatorStrike = new();
    private static readonly TimeSpan StalkWarning = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan PredatorStrikeInterval = TimeSpan.FromSeconds(30);
    private const int StalkRange = 5;

    /// <summary>Holds NPCs and animals still — the self-test sets it while it clicks on exact tiles.</summary>
    public static bool LifePaused { get; set; }

    /// <summary>Called every frame.</summary>
    private void TickLife(double delta)
    {
        if (LifePaused || Player == null || _worldScene == null || string.IsNullOrEmpty(Player.Name)) return;
        _npcWanderTimer -= delta;
        if (_npcWanderTimer <= 0)
        {
            _npcWanderTimer = NpcWanderSeconds;
            WanderNpcs();
        }
        GreetIfNear();
    }

    // ── NPCs ─────────────────────────────────────────────────────────────

    private void WanderNpcs()
    {
        var rng = Random.Shared;
        foreach (var npc in LiveNpcs)
        {
            // Asleep, at work on a creation, or at prayer: they stay put. A "talking" decision does not
            // freeze them (it used to, for minutes) — only someone you are actually talking to waits.
            if (npc.BrokenMove || npc.State is NpcState.Sleeping or NpcState.Creating or NpcState.Praying) continue;
            if (_worldScene?.DialogueOpen == true && Dist(npc.TileX, npc.TileY) <= 2) continue;
            if (Dist(npc.TileX, npc.TileY) > 30 || rng.NextDouble() > 0.4) continue;
            int x = npc.TileX + rng.Next(-1, 2), y = npc.TileY + rng.Next(-1, 2);
            if ((x, y) == (npc.TileX, npc.TileY) || !IsPassableTile(x, y) || IsOccupied(x, y) || AnimalAt(x, y) != null) continue;
            if (Player != null && (x, y) == (Player.TileX, Player.TileY)) continue;
            npc.SetTile(x, y);
            _worldScene?.MoveNpc(npc.NpcId, x, y);
        }
    }

    /// <summary>The first time in a while you come within two paces of someone, they speak to you.</summary>
    private async void GreetIfNear()
    {
        if (_greeting || Player == null || _worldScene == null || _worldScene.DialogueOpen) return;
        var now = DateTime.UtcNow;
        var npc = LiveNpcs.FirstOrDefault(n => Dist(n.TileX, n.TileY) <= 2 && !n.BrokenTalk &&
                                               n.State != NpcState.Sleeping &&
                                               (!_greetedUtc.TryGetValue(n.NpcId, out var t) || now - t > GreetAgain));
        if (npc == null) return;
        _greetedUtc[npc.NpcId] = now;
        _greeting = true;
        try
        {
            _worldScene.ShowNpcSpeech(npc.NpcId, "…");
            var line = await npc.RespondToDialogueAsync(
                "(You notice someone approaching you. Greet them, or warn them off, in one short sentence, as your nature would.)",
                BuildNpcSituation(npc), _cts.Token, Player?.Gifts.Summary() ?? "", PlayerStanding());
            line = line.Trim().Trim('"');
            if (line.Length > 140) line = line[..line.LastIndexOf(' ', 140)] + "…";
            if (line.Length > 1 && LiveNpcs.Contains(npc))
            {
                _worldScene?.ShowNpcSpeech(npc.NpcId, line);
                AinSoph.Audio.Sound.Play("speech");
            }
        }
        catch (OperationCanceledException) { }
        finally { _greeting = false; }
    }

    // ── Animals ──────────────────────────────────────────────────────────

    /// <summary>Whether a predator has caught the player's scent (it warned, and now stalks).</summary>
    public bool IsStalking(string animalId) => _stalkWarnedUtc.ContainsKey(animalId);

    /// <summary>Prey shy from you; predators stalk you, warn, close in and strike.</summary>
    private void StalkAndFlee()
    {
        if (LifePaused || Player == null || _worldScene == null || string.IsNullOrEmpty(Player.Name)) return;
        var now = DateTime.UtcNow;
        var safe = Player.Survival.IsSleeping && (Player.Survival.IsInCave || Player.Gifts.Has(Skills.GiftEffect.Shelter));

        foreach (var a in LiveAnimals.ToList())
        {
            if (!LiveAnimals.Contains(a) || a.Survival.IsSleeping || a.Species is not { } sp || sp.Hunts) continue;
            var d = Dist(a.TileX, a.TileY);

            if (a.AnimalType is AnimalType.Prey && d <= 2)
            {
                // Step away from the player
                int fx = a.TileX + Math.Sign(a.TileX - Player.TileX), fy = a.TileY + Math.Sign(a.TileY - Player.TileY);
                if (AnimalCanStand(sp, fx, fy) && !IsOccupied(fx, fy) && AnimalAt(fx, fy) == null) MoveAnimal(a, fx, fy);
                continue;
            }

            if (a.AnimalType != AnimalType.Predator || safe) continue;
            if (d > StalkRange) { _stalkWarnedUtc.Remove(a.AnimalId); continue; }

            if (!_stalkWarnedUtc.TryGetValue(a.AnimalId, out var warned))
            {
                _stalkWarnedUtc[a.AnimalId] = now;
                _worldScene.ShowNpcSpeech(a.AnimalId, "!");
                _worldScene.ShowWorldText($"A {a.Name} has caught your scent. Get away, or stand and fight.");
                AinSoph.Audio.Sound.Play("warning");
                continue;
            }
            if (now - warned < StalkWarning) continue;

            if (d <= 1)
            {
                if (_lastPredatorStrike.TryGetValue(a.AnimalId, out var last) && now - last < PredatorStrikeInterval) continue;
                if (BuildAnimalSituation(a).NearbyEntityId != Player.Id) continue; // kinship, shelter
                _lastPredatorStrike[a.AnimalId] = now;
                OnAnimalAttack(a, Player.Id);
                if (Player == null) return; // it killed you
                continue;
            }

            if (Random.Shared.NextDouble() > 0.6) continue;
            int sx = Math.Sign(Player.TileX - a.TileX), sy = Math.Sign(Player.TileY - a.TileY);
            // Straight at you if it can, else around whatever is in the way
            foreach (var (dx, dy) in new[] { (sx, sy), (sx, 0), (0, sy), (sx, -sy), (-sx, sy), (sy, sx), (-sy, -sx) })
            {
                int nx = a.TileX + dx, ny = a.TileY + dy;
                if ((dx, dy) == (0, 0) || (nx, ny) == (Player.TileX, Player.TileY)) continue;
                if (Math.Max(Math.Abs(nx - Player.TileX), Math.Abs(ny - Player.TileY)) > d) continue; // never back away
                if (AnimalCanStand(sp, nx, ny) && !IsOccupied(nx, ny) && AnimalAt(nx, ny) == null) { MoveAnimal(a, nx, ny); break; }
            }
        }
    }
}
