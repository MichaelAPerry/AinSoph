using AinSoph.Council;
using AinSoph.Data;
using AinSoph.LLM;
using AinSoph.NPC;
using AinSoph.Player;
using AinSoph.Skills;
using AinSoph.World;
using Godot;

namespace AinSoph;

/// <summary>
/// Autoloaded singleton. The single entry point for all engine systems.
/// Add this node to Godot's AutoLoad list as "AinSoph".
///
/// Boot order:
///   1. LLM model
///   2. Decan registry
///   3. Save manager + load or new world
///   4. World grid + altar
///   5. NPC queue
///   6. World tick
///   7. Player character + tribe
///   8. Interaction resolver
/// </summary>
public partial class GameRoot : Node
{
    // -------------------------------------------------------------------------
    // Public surface — everything the scene layer needs
    // -------------------------------------------------------------------------

    public static LlmRunner            Llm                 { get; private set; } = new();
    public static WorldGrid?           Grid                { get; private set; }
    public static Altar?               Altar               { get; private set; }
    public static SaveManager?         Save                { get; private set; }
    public static NpcTickQueue?        NpcQueue            { get; private set; }
    public static WorldTick?           WorldTick           { get; private set; }
    public static PlayerCharacter?     Player              { get; private set; }
    public static TribeManager?        Tribe               { get; private set; }
    public static TribuneCouncil?      Council             { get; private set; }
    public static InteractionResolver? Interactions        { get; private set; }

    public static List<NpcBrain>       LiveNpcs            { get; } = new();
    public static List<AnimalBrain>    LiveAnimals         { get; } = new();

    public static bool                 IsReady             { get; private set; }

    // -------------------------------------------------------------------------
    // Config
    // -------------------------------------------------------------------------

    private const string ModelSubPath = "user://models/qwen2.5-3b.gguf";
    private const string SaveSubPath  = "user://saves/world";

    private CancellationTokenSource _cts = new();

    // -------------------------------------------------------------------------
    // Boot
    // -------------------------------------------------------------------------

    public override void _Ready()
    {
        GD.Print("Ain Soph — booting");

        // 1. LLM
        var modelPath = ProjectSettings.GlobalizePath(ModelSubPath);
        if (System.IO.File.Exists(modelPath))
        {
            Llm.Initialize(modelPath);
            GD.Print("GameRoot: LLM ready");
        }
        else
        {
            GD.PrintErr($"GameRoot: model not found at {ModelSubPath}. " +
                        $"Place qwen2.5-3b.gguf there before launching.");
        }

        // 2. Decans
        DecanRegistry.Load("res://data/ain_soph_72.json");

        // 3. Save manager — load existing world or create new
        var saveDir = ProjectSettings.GlobalizePath(SaveSubPath);
        Save        = new SaveManager(saveDir);

        var worldData = Save.LoadWorld();
        int worldSeed;

        if (worldData is not null)
        {
            worldSeed = worldData.WorldSeed;
            GD.Print($"GameRoot: loaded world '{worldData.WorldName}' " +
                     $"(seed {worldSeed})");
        }
        else
        {
            worldSeed = new Random().Next();
            var newWorld = new Data.WorldSaveData
            {
                WorldSeed  = worldSeed,
                CreatedUtc = DateTime.UtcNow,
                WorldName  = "New World"
            };
            Save.SaveWorld(newWorld);
            GD.Print($"GameRoot: new world created (seed {worldSeed})");
        }

        // 4. World grid + altar
        Grid  = new WorldGrid(worldSeed);
        Altar = Altar.Place(Grid, worldSeed);
        GD.Print($"GameRoot: altar placed at {Altar.CellId} " +
                 $"[{Altar.TileX},{Altar.TileY}] in {Altar.Biome}");

        // 5. NPC queue
        NpcQueue = new NpcTickQueue();

        // 6. World tick — add as child so Godot calls _Process on it
        WorldTick = new WorldTick(Grid);
        WorldTick.OnHourlyTick  += OnHourlyTick;
        WorldTick.OnMorningManna += OnMorningManna;
        AddChild(WorldTick);

        // 7. Player
        var playerData = Save.LoadPlayer();
        var nowUtc     = DateTime.UtcNow;
        bool isNewPlayer = playerData is null;

        if (playerData is not null)
        {
            Player = new PlayerCharacter(nowUtc)
            {
                Id   = playerData.Id,
                Name = playerData.Name,
                CellId = playerData.CellId,
                TileX  = playerData.TileX,
                TileY  = playerData.TileY,
            };
            Player.AccumulatedPlayHours = 0; // restored from save data below
            GD.Print($"GameRoot: player '{Player.Name}' loaded");
        }
        else
        {
            var (startX, startY) = Grid.FindPlayerStart();
            var startCell        = Grid.GetOrGenerate(startX, startY);
            Player = new PlayerCharacter(nowUtc)
            {
                CellId = startCell.CellId,
                TileX  = 0,
                TileY  = 0
            };
            GD.Print($"GameRoot: new player — awaiting name, starting at {startCell.CellId}");
        }

        Player.BeginSession(nowUtc);

        // Load all saved NPCs into the queue
        foreach (var npcData in Save.LoadAllNpcs())
        {
            var decan = DecanRegistry.Get(npcData.DecanId);
            if (decan is null) continue;

            var brain = new NpcBrain(npcData.Id, decan, Llm, nowUtc);
            brain.Memory.Write(NPC.MemorySlot.Will,    npcData.MemoryWill);
            brain.Memory.Write(NPC.MemorySlot.Thought, npcData.MemoryThought);
            brain.Memory.Write(NPC.MemorySlot.Feeling, npcData.MemoryFeeling);
            brain.Memory.Write(NPC.MemorySlot.Action,  npcData.MemoryAction);
            brain.OnDeath += OnNpcDeath;

            LiveNpcs.Add(brain);
            NpcQueue.Enqueue(brain);
        }

        GD.Print($"GameRoot: {LiveNpcs.Count} NPCs loaded into queue");

        // 8. Tribe
        Tribe = new TribeManager(Player, LiveNpcs, nowUtc);
        Tribe.OnSpouseCreated += npc => NpcQueue.Enqueue(npc, priority: true);
        Tribe.OnProgenyBorn   += npc => NpcQueue.Enqueue(npc);

        // 9. Council + interaction
        Council      = new TribuneCouncil(Llm);
        var parser   = new CouncilSubmissionParser();
        Interactions = new InteractionResolver(Grid, Altar, parser);

        // Ensure cells around the player are loaded
        ParseCellId(Player.CellId, out var px, out var py);
        Grid.EnsureLoaded(px, py);
        NpcQueue.Prioritize(
            LiveNpcs
                .Where(n => n.CellId() == Player.CellId)
                .Select(n => n.NpcId));

        IsReady = true;
        GD.Print("Ain Soph — ready");

        // 10. Scene layer — load WorldScene and wire all systems
        var sceneRes = GD.Load<PackedScene>("res://scenes/world/WorldScene.tscn");
        if (sceneRes != null)
        {
            var scene = sceneRes.Instantiate<WorldScene>();
            scene.Grid        = Grid;
            scene.Clock       = new WorldClock();
            scene.Player      = Player;
            scene.SaveMgr     = Save;
            scene.AltarCellId = Altar?.CellId ?? "";

            scene.PrimitiveUsed  += OnPrimitiveUsed;
            scene.AltarPetition  += OnAltarPetition;

            AddChild(scene);
            scene.InitSystems();

            // Spawn initial NPCs visible to player
            ParseCellId(Player.CellId, out var cpx, out var cpy);
            foreach (var npc in LiveNpcs)
            {
                var ncell = npc.CellId();
                if (!string.IsNullOrEmpty(ncell))
                {
                    ParseCellId(ncell, out var nx, out var ny);
                    if (System.Math.Abs(nx - cpx) <= 3 && System.Math.Abs(ny - cpy) <= 3)
                    {
                        var nd = Save?.LoadNpc(npc.NpcId);
                        if (nd != null) scene.UpsertNpc(nd);
                    }
                }
            }

            _worldScene = scene;
            GD.Print("GameRoot: WorldScene ready");

            // New player — show naming screen on top of the world
            if (isNewPlayer)
            {
                var creation = new AinSoph.UI.CharacterCreationScreen();
                AddChild(creation);
                creation.Show((name) =>
                {
                    Player!.Name = string.IsNullOrWhiteSpace(name) ? "Unnamed" : name;
                    GD.Print($"GameRoot: player named '{Player.Name}'");
                    SaveAll();
                });
            }
        }
        else
        {
            GD.PrintErr("GameRoot: could not load WorldScene.tscn");
        }
    }

    // -------------------------------------------------------------------------
    // Process — save tick
    // -------------------------------------------------------------------------

    public override void _Process(double delta)
    {
        if (!IsReady || Save is null || Player is null) return;

        Player.TickPlayTime(DateTime.UtcNow);

        if (Save.ShouldSave())
            SaveAll();
    }

    // -------------------------------------------------------------------------
    // Hourly tick — NPC queue + tribe + survival
    // -------------------------------------------------------------------------

    private void OnHourlyTick()
    {
        if (!IsReady) return;

        var nowUtc = DateTime.UtcNow;

        // Advance animal survival
        foreach (var animal in LiveAnimals)
        {
            var situation = BuildAnimalSituation(animal);
            animal.Tick(situation, nowUtc);
        }

        // Tribe — may birth progeny
        Tribe?.Tick(Llm, nowUtc);

        // Drain NPC queue — one NPC per call, queue runs continuously
        if (NpcQueue is not null)
            _ = NpcQueue.ProcessNextAsync(BuildNpcSituation, _cts.Token);
    }

    // -------------------------------------------------------------------------
    // Morning manna
    // -------------------------------------------------------------------------

    private void OnMorningManna(
        Dictionary<string, List<(int TileX, int TileY)>> spawned)
    {
        if (Grid is null) return;

        foreach (var (cellKey, tiles) in spawned)
        {
            ParseCellId(cellKey, out var gx, out var gy);
            var cell = Grid.GetIfLoaded(gx, gy);
            if (cell is null) continue;

            foreach (var (tx, ty) in tiles)
            {
                var mannaId = $"manna:{cellKey}:{tx},{ty}:{DateTime.UtcNow.Ticks}";
                cell.Tiles[tx, ty].ItemIds.Add(mannaId);
            }
        }
    }

    // -------------------------------------------------------------------------
    // NPC death
    // -------------------------------------------------------------------------

    private void OnNpcDeath(NpcBrain npc)
    {
        LiveNpcs.Remove(npc);
        NpcQueue?.Remove(npc.NpcId);
        GD.Print($"GameRoot: NPC {npc.NpcId} died — body remains in {npc.CellId()}");
        // Body-as-item placement handled by scene layer
    }

    // -------------------------------------------------------------------------
    // Situation builders
    // -------------------------------------------------------------------------

    private SituationContext BuildNpcSituation(NpcBrain npc)
    {
        var nowUtc  = DateTime.UtcNow;
        var isNight = WorldClock.IsNight();
        var range   = WorldClock.VisionRange();

        ParseCellId(npc.CellId(), out var cx, out var cy);

        var entities = new List<NearbyEntity>();
        var items    = new List<NearbyItem>();

        for (var dx = -range; dx <= range; dx++)
        for (var dy = -range; dy <= range; dy++)
        {
            var cell = Grid?.GetIfLoaded(cx + dx, cy + dy);
            if (cell is null) continue;

            // Other NPCs
            foreach (var other in LiveNpcs)
            {
                if (other.NpcId == npc.NpcId) continue;
                if (other.CellId() != cell.CellId) continue;
                entities.Add(new NearbyEntity
                {
                    Id        = other.NpcId,
                    Type      = "npc",
                    Name      = other.Decan.Name,
                    CellId    = cell.CellId,
                    IsSleeping = other.State == NpcState.Sleeping
                });
            }

            // Animals
            foreach (var animal in LiveAnimals)
            {
                if (animal.CellId != cell.CellId) continue;
                entities.Add(new NearbyEntity
                {
                    Id        = animal.AnimalId,
                    Type      = "animal",
                    Name      = animal.Name,
                    CellId    = cell.CellId,
                    IsSleeping = animal.Survival.IsSleeping
                });
            }

            // Player
            if (Player?.CellId == cell.CellId)
                entities.Add(new NearbyEntity
                {
                    Id     = Player.Id,
                    Type   = "pc",
                    Name   = Player.Name,
                    CellId = cell.CellId
                });

            // Items
            foreach (var tile in cell.AllTiles())
            foreach (var itemId in tile.ItemIds)
                items.Add(new NearbyItem
                {
                    Id     = itemId,
                    Name   = itemId.StartsWith("manna") ? "manna" : itemId,
                    Edible = itemId.StartsWith("manna"),
                    CellId = cell.CellId
                });
        }

        return new SituationContext
        {
            LocalTime        = DateTime.Now,
            IsNight          = isNight,
            CurrentCell      = npc.CellId(),
            HoursSinceAte    = (nowUtc - npc.Survival.LastAteUtc).TotalHours,
            HoursSinceSlept  = (nowUtc - npc.Survival.LastSleptUtc).TotalHours,
            IsHungry         = (nowUtc - npc.Survival.LastAteUtc).TotalHours >= SurvivalTracker.WarningHours,
            IsExhausted      = (nowUtc - npc.Survival.LastSleptUtc).TotalHours >= SurvivalTracker.WarningHours,
            IsInCave         = npc.Survival.IsInCave,
            VisibleEntities  = entities,
            VisibleItems     = items
        };
    }

    private AnimalSituation BuildAnimalSituation(AnimalBrain animal)
    {
        ParseCellId(animal.CellId, out var cx, out var cy);

        string? nearestEntity   = null;
        string? nearestEdible   = null;
        bool    predatorNearby  = false;

        var cell = Grid?.GetIfLoaded(cx, cy);
        if (cell is not null)
        {
            // Nearest entity for predators
            nearestEntity = LiveNpcs.FirstOrDefault(n => n.CellId() == animal.CellId)?.NpcId
                         ?? (Player?.CellId == animal.CellId ? Player.Id : null);

            // Manna
            foreach (var tile in cell.AllTiles())
            {
                if (tile.ItemIds.Any(i => i.StartsWith("manna")))
                {
                    nearestEdible = tile.ItemIds.First(i => i.StartsWith("manna"));
                    break;
                }
            }

            // Predator nearby for prey
            predatorNearby = LiveAnimals.Any(a =>
                a.AnimalId != animal.AnimalId &&
                a.AnimalType == AnimalType.Predator &&
                a.CellId == animal.CellId);
        }

        return new AnimalSituation
        {
            NearbyEntityId        = nearestEntity,
            NearbyEdibleItemId    = nearestEdible,
            NearbyPredatorPresent = predatorNearby
        };
    }

    // -------------------------------------------------------------------------
    // Save
    // -------------------------------------------------------------------------

    private void SaveAll()
    {
        if (Save is null || Player is null || Grid is null) return;

        Save.RecordSave();

        // World metadata
        Save.SaveWorld(new Data.WorldSaveData
        {
            WorldSeed    = 0, // loaded from existing, not regenerated here
            CreatedUtc   = DateTime.UtcNow,
            LastSavedUtc = DateTime.UtcNow,
            WorldName    = "World"
        });

        // Player
        Save.SavePlayer(new Data.PlayerSaveData
        {
            Id           = Player.Id,
            Name         = Player.Name,
            CellId       = Player.CellId,
            TileX        = Player.TileX,
            TileY        = Player.TileY,
            LastAteUtc   = Player.Survival.LastAteUtc,
            LastSleptUtc = Player.Survival.LastSleptUtc,
            IsInCave     = Player.Survival.IsInCave
        });

        // NPCs
        foreach (var npc in LiveNpcs)
        {
            Save.SaveNpc(new Data.NpcSaveData
            {
                Id            = npc.NpcId,
                DecanId       = npc.Decan.Id,
                CellId        = npc.CellId(),
                State         = npc.State.ToString().ToLower(),
                MemoryWill    = npc.Memory.Will,
                MemoryThought = npc.Memory.Thought,
                MemoryFeeling = npc.Memory.Feeling,
                MemoryAction  = npc.Memory.Action,
                LastAteUtc    = npc.Survival.LastAteUtc,
                LastSleptUtc  = npc.Survival.LastSleptUtc
            });
        }

        // Cells
        foreach (var cell in Grid.LoadedCells)
        {
            Save.SaveCell(new Data.CellSaveData
            {
                GridX  = cell.GridX,
                GridY  = cell.GridY,
                Biome  = cell.Biome.ToString(),
                CaveId = cell.CaveId,
                Tiles  = cell.AllTiles().Select(t => new Data.TileSaveData
                {
                    TileX   = t.TileX,
                    TileY   = t.TileY,
                    Surface = t.Surface.ToString(),
                    HasCave = t.HasCave
                }).ToList()
            });
        }
    }

    // -------------------------------------------------------------------------
    // Shutdown
    // -------------------------------------------------------------------------

    public override void _ExitTree()
    {
        _cts.Cancel();
        Player?.EndSession(DateTime.UtcNow);
        SaveAll();
        Llm.Dispose();
        GD.Print("Ain Soph — shutdown complete");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    // ── WorldScene reference ──────────────────────────────────────────────
    private WorldScene? _worldScene;

    private async void OnPrimitiveUsed(string targetId, int skillType)
    {
        if (_worldScene == null || Interactions == null || Player == null) return;

        var skill = (Skills.SkillType)skillType;

        // Build the interaction request
        var req = new Skills.InteractionRequest
        {
            ActorId    = Player.Id,
            Primitive  = skill.ToString().ToLower(),
            TargetId   = targetId,
            TargetName = targetId,
        };

        // Determine target type
        if (targetId.StartsWith("tile:"))
        {
            req.TargetType = Skills.InteractionTarget.Tile;
        }
        else if (LiveNpcs.Exists(n => n.NpcId == targetId))
        {
            req.TargetType = Skills.InteractionTarget.Npc;
        }
        else if (LiveAnimals.Exists(a => a.AnimalId == targetId))
        {
            req.TargetType = Skills.InteractionTarget.Animal;
        }
        else
        {
            req.TargetType = Skills.InteractionTarget.Item;
        }

        // Talk → open dialogue screen then let the NPC's next think reply
        if (skill == Skills.SkillType.Talk && req.TargetType == Skills.InteractionTarget.Npc)
        {
            var npc = LiveNpcs.Find(n => n.NpcId == targetId);
            if (npc != null)
            {
                var npcSave = Save?.LoadNpc(targetId);
                var name    = npcSave?.Name ?? "Traveller";
                _worldScene.OpenNpcDialogueFull(
                    targetId, name, npc.Decan.Id, targetId.GetHashCode(),
                    "...",
                    (text) => OnPrimitiveUsed(targetId, (int)Skills.SkillType.Talk)
                );
                // Subscribe to NPC's next decision for the reply
                void Handler(NpcBrain brain, NpcDecision decision)
                {
                    npc.OnDecision -= Handler;
                    var reply = string.IsNullOrEmpty(decision.Speech) ? "..." : decision.Speech;
                    _worldScene.SetDialogueSpeech(reply);
                    _worldScene.SetNpcState(targetId, npc.State);
                }
                npc.OnDecision += Handler;
                NpcQueue?.Prioritize(new[] { targetId });
            }
            return;
        }

        // Pray at altar → open altar screen
        if (skill == Skills.SkillType.Pray && Altar != null)
        {
            _worldScene.OpenAltar((petition) => OnAltarPetition(petition));
            return;
        }

        // All other primitives → resolve and show world text
        var result = await Interactions.ResolveAsync(req, _cts.Token);
        if (!string.IsNullOrEmpty(result.WorldText))
            _worldScene.ShowWorldText(result.WorldText);
    }

    private async void OnAltarPetition(string petition)
    {
        if (_worldScene == null || Council == null) return;
        var verdict = await Council.HearPetitionAsync(petition, Player?.Skills, _cts.Token);
        var sb = new System.Text.StringBuilder();
        foreach (var response in verdict.SeatResponses)
            sb.AppendLine(response.Homily);
        _worldScene.SetDialogueSpeech(sb.ToString().Trim());
        if (verdict.Approved)
            GD.Print($"GameRoot: Council approved — {verdict.GrantSummary}");
    }

    private static void ParseCellId(string cellId, out int x, out int y)
    {
        x = 0; y = 0;
        var parts = cellId.Split(',');
        if (parts.Length == 2)
        {
            int.TryParse(parts[0], out x);
            int.TryParse(parts[1], out y);
        }
    }
}
