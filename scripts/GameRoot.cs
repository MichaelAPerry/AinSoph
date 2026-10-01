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
    public static WorldItemRegistry?   Items               { get; private set; }
    public static Data.RouteManager?   Routes              { get; private set; }
    public static string               WorldName           { get; private set; } = "World";

    public static List<NpcBrain>       LiveNpcs            { get; } = new();
    public static List<AnimalBrain>    LiveAnimals         { get; } = new();

    public static bool                 IsReady             { get; private set; }

    /// <summary>No model loaded — NPCs and the Council use scripted replies.</summary>
    public static bool                 IsDemo              => !Llm.IsReady;

    // -------------------------------------------------------------------------
    // Config
    // -------------------------------------------------------------------------

    private const string ModelSubPath = "user://models/qwen2.5-3b.gguf";
    private const string SaveSubPath  = "user://saves/world";
    private const string TourSavePath = "user://saves/demo_tour"; // wiped each tour run

    private static bool HasArg(string arg) =>
        OS.GetCmdlineUserArgs().Contains(arg) || OS.GetCmdlineArgs().Contains(arg);

    private CancellationTokenSource _cts = new();

    private int      _worldSeed;
    private DateTime _worldCreatedUtc = DateTime.UtcNow;

    // NPC pump — the queue is drained continuously, one NPC at a time
    private double _npcPumpTimer;
    private const double NpcPumpSeconds = 2.0;

    // Caps on what an NPC prompt lists, so it fits the 2048-token context
    private const int MaxPromptEntities = 8;
    private const int MaxPromptItems    = 6;

    // Demo mode
    private const int DemoNpcCount = 5;
    private static readonly TimeSpan DemoThinkInterval = TimeSpan.FromSeconds(8);

    // -------------------------------------------------------------------------
    // Boot
    // -------------------------------------------------------------------------

    public override void _Ready()
    {
        GD.Print("Ain Soph — booting");

        // 1. Model extraction (first launch) — boots rest of world in callback
        var bootScreen = new AinSoph.UI.ModelBootScreen();
        AddChild(bootScreen);
        bootScreen.OnReady += BootWithModel;
    }

    private async void BootWithModel(string modelPath)
    {
        try
        {
            // 1. LLM — loading a 1.9 GB model takes seconds; keep the main thread responsive
            await Task.Run(() => Llm.Initialize(modelPath));
            GD.Print(IsDemo ? "GameRoot: demo mode — scripted NPCs and Council" : "GameRoot: LLM ready");
            if (IsDemo) NpcBrain.ThinkInterval = DemoThinkInterval;

            // 2. Decans
            GD.Print("GameRoot: loading decans...");
            DecanRegistry.Load("res://data/ain_soph_72.json");

        // 3. Save manager — load existing world or create new
        GD.Print("GameRoot: initializing save manager...");
        // The demo tour always starts from a fresh, throwaway world
        var tour    = HasArg("--demo-tour");
        var saveDir = ProjectSettings.GlobalizePath(tour ? TourSavePath : SaveSubPath);
        if (tour && System.IO.Directory.Exists(saveDir))
            System.IO.Directory.Delete(saveDir, recursive: true);
        Save        = new SaveManager(saveDir);

        var worldData = Save.LoadWorld();
        int worldSeed;

        if (worldData is not null && worldData.WorldSeed != 0)
        {
            worldSeed = worldData.WorldSeed;
            WorldName = worldData.WorldName;
            _worldCreatedUtc = worldData.CreatedUtc;
            GD.Print($"GameRoot: loaded world '{WorldName}' (seed {worldSeed})");
        }
        else
        {
            worldSeed = new Random().Next(1, int.MaxValue);
            WorldName = "New World";
            var newWorld = new Data.WorldSaveData
            {
                WorldSeed  = worldSeed,
                CreatedUtc = DateTime.UtcNow,
                WorldName  = WorldName
            };
            Save.SaveWorld(newWorld);
            GD.Print($"GameRoot: new world created (seed {worldSeed})");
        }
        _worldSeed = worldSeed;

        // 4. World grid + altar
        GD.Print("GameRoot: generating world grid + altar...");
        Grid  = new WorldGrid(worldSeed);
        Altar = Altar.Place(Grid, worldSeed);
        GD.Print($"GameRoot: altar placed at {Altar.CellId} " +
                 $"[{Altar.TileX},{Altar.TileY}] in {Altar.Biome}");

        // 4b. Item registry
        Items = new WorldItemRegistry(Save);
        Items.LoadAll();

        // 4c. Route manager
        Routes = new Data.RouteManager(Save, LiveNpcs);

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

        // A player that quit before naming themselves never really arrived
        if (playerData is not null && string.IsNullOrWhiteSpace(playerData.Name))
            playerData = null;
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
            var (tx, ty)         = FindOpenTile(startCell);
            Player = new PlayerCharacter(nowUtc)
            {
                CellId = startCell.CellId,
                TileX  = tx,
                TileY  = ty
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
            brain.SetTile(npcData.TileX, npcData.TileY);
            brain.Memory.Write(NPC.MemorySlot.Will,    npcData.MemoryWill);
            brain.Memory.Write(NPC.MemorySlot.Thought, npcData.MemoryThought);
            brain.Memory.Write(NPC.MemorySlot.Feeling, npcData.MemoryFeeling);
            brain.Memory.Write(NPC.MemorySlot.Action,  npcData.MemoryAction);
            brain.BrokenMove = npcData.BrokenMove;
            brain.BrokenSee  = npcData.BrokenSee;
            brain.BrokenHear = npcData.BrokenHear;
            brain.BrokenTalk = npcData.BrokenTalk;
            brain.OnDeath     += OnNpcDeath;
            brain.OnDecision  += OnNpcDecision;

            LiveNpcs.Add(brain);
            NpcQueue.Enqueue(brain);
        }

        GD.Print($"GameRoot: {LiveNpcs.Count} NPCs loaded into queue");

        // Demo mode — a new world has no NPCs yet (they come from players), so
        // seed a few around the player to show the world living
        if ((IsDemo || tour) && LiveNpcs.Count == 0)
            SeedDemoNpcs(nowUtc);

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
        GD.Print("GameRoot: loading WorldScene...");
        var sceneRes = GD.Load<PackedScene>("res://scenes/world/WorldScene.tscn");
        if (sceneRes != null)
        {
            var scene = sceneRes.Instantiate<WorldScene>();
            scene.Grid        = Grid;
            scene.Clock       = new WorldClock();
            scene.Player      = Player;
            scene.SaveMgr     = Save;
            scene.AltarCellId = Altar?.CellId ?? "";
            scene.AltarTile   = new Vector2I(Altar?.TileX ?? 0, Altar?.TileY ?? 0);
            scene.Items       = Items;

            scene.PrimitiveUsed  += OnPrimitiveUsed;
            scene.AltarPetition  += OnAltarPetition;

            AddChild(scene);
            scene.InitSystems();

            // NPCs — every live NPC gets a node; fog hides the far ones
            foreach (var npc in LiveNpcs)
                scene.UpsertNpc(ToSaveData(npc));

            _worldScene = scene;
            if (IsDemo) scene.ShowWorldText("Demo mode — the voices you hear are scripted.");
            if (tour)   AddChild(new Demo.DemoDirector());
            GD.Print("GameRoot: WorldScene ready");

            // New player — show naming screen on top of the world
            if (isNewPlayer)
            {
                var creation = new AinSoph.UI.CharacterCreationScreen();
                AddChild(creation);
                scene.InputLocked = true;
                creation.Show((name) =>
                {
                    Player!.Name = string.IsNullOrWhiteSpace(name) ? "Unnamed" : name;
                    GD.Print($"GameRoot: player named '{Player.Name}'");
                    scene.InputLocked = false;
                    SaveAll();
                });
            }
        }
        else
        {
            GD.PrintErr("GameRoot: could not load WorldScene.tscn");
        }

        }
        catch (Exception ex)
        {
            GD.PrintErr($"GameRoot: BOOT FAILED — {ex.GetType().Name}: {ex.Message}");
            GD.PrintErr(ex.StackTrace ?? "");
        }
    }

    // -------------------------------------------------------------------------
    // Process — save tick
    // -------------------------------------------------------------------------

    public override void _Process(double delta)
    {
        if (!IsReady || Save is null || Player is null) return;

        Player.TickPlayTime(DateTime.UtcNow);

        // Keep the NPC queue moving — each NPC decides itself whether it is due to think
        _npcPumpTimer -= delta;
        if (_npcPumpTimer <= 0 && NpcQueue is not null && !NpcQueue.IsBusy)
        {
            _npcPumpTimer = IsDemo ? NpcPumpSeconds / 2 : NpcPumpSeconds;
            _ = NpcQueue.ProcessNextAsync(BuildNpcSituation, _cts.Token);
        }

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

        // Decay items — age all living items by 1 hour
        Items?.TickDecay(1f);

        // Advance animal survival
        foreach (var animal in LiveAnimals.ToList())
        {
            var situation = BuildAnimalSituation(animal);
            animal.Tick(situation, nowUtc);
        }

        // Tribe — may birth progeny
        Tribe?.Tick(Llm, nowUtc);

        // Player survival — check warnings
        if (Player != null)
        {
            var result = Player.Survival.Tick(nowUtc);
            if (result.HungerWarning) _worldScene?.ShowWorldText("⚠ You must eat within the hour. ⚠");
            if (result.SleepWarning)  _worldScene?.ShowWorldText("⚠ You must sleep within the hour. ⚠");
            if (result.IsDead)
            {
                // Drop corpse — body persists in world
                Items?.SpawnBody(Player.Name, Player.TileX, Player.TileY);

                // Release any cave claim
                ReleaseCave(Player.TileX, Player.TileY, Player.Id);

                // Delete old player save
                Save?.DeletePlayer();

                GD.Print($"GameRoot: player '{Player.Name}' died");
                Player = null;

                // New character — show creation screen, spawn near a cave
                _worldScene?.ShowWorldText("You have died. A new light descends.");
                CallDeferred(MethodName.SpawnNewPlayer);
            }
        }
    }

    private void SpawnNewPlayer()
    {
        if (Grid == null || Save == null) return;

        var rng       = new Random();
        var spawnCell = Grid.FindCaveCell(rng, radius: 3) ?? Grid.GetOrGenerate(0, 0);
        var nowUtc    = DateTime.UtcNow;

        var (tx, ty)  = FindOpenTile(spawnCell);
        Player = new PlayerCharacter(nowUtc)
        {
            Id     = $"player:{Guid.NewGuid():N}",
            TileX  = tx,
            TileY  = ty,
            CellId = $"{spawnCell.GridX},{spawnCell.GridY}"
        };
        if (_worldScene != null) _worldScene.Player = Player;

        var spawnTile = new Vector2I(Player.TileX, Player.TileY);

        // Show naming screen — world keeps running behind it
        var creation = new AinSoph.UI.CharacterCreationScreen();
        AddChild(creation);
        if (_worldScene != null) _worldScene.InputLocked = true;
        creation.Show((name) =>
        {
            Player!.Name = string.IsNullOrWhiteSpace(name) ? "Unnamed" : name;
            GD.Print($"GameRoot: new player '{Player.Name}' descends");
            if (_worldScene != null) _worldScene.InputLocked = false;
            _worldScene?.MovePlayerTo(spawnTile);
            SaveAll();
        });
    }

    // -------------------------------------------------------------------------
    // Morning manna
    // -------------------------------------------------------------------------

    private void OnMorningManna(
        Dictionary<string, List<(int TileX, int TileY)>> spawned)
    {
        if (Items is null) return;

        int total = 0;
        foreach (var (cellKey, tiles) in spawned)
        {
            ParseCellId(cellKey, out var cx, out var cy);
            var cell = Grid?.GetIfLoaded(cx, cy);

            // SpawnManna returns tile coords local to the cell (0–7)
            foreach (var (lx, ly) in tiles)
            {
                var item = Items.SpawnManna(cx * 8 + lx, cy * 8 + ly);

                // Register on tile so NPCs can find it via SituationContext
                cell?.GetTile(lx, ly).ItemIds.Add(item.Id);
                total++;
            }
        }
        GD.Print($"GameRoot: morning manna — {total} portions spawned");
        _worldScene?.RefreshMap();
    }

    // -------------------------------------------------------------------------
    // Foreigner arrival — called by RoutesScreen after import
    // -------------------------------------------------------------------------

    public static void InstantiateForeigners(List<Data.NpcSaveData> arrivals)
    {
        if (Llm == null) return;
        var nowUtc = DateTime.UtcNow;

        foreach (var data in arrivals)
        {
            var decan = DecanRegistry.Get(data.DecanId);
            if (decan == null)
            {
                GD.PrintErr($"GameRoot: foreigner {data.Id} has unknown decan '{data.DecanId}' — skipped");
                continue;
            }

            var brain = new NpcBrain(data.Id, decan, Llm, nowUtc);
            brain.Memory.Write(NPC.MemorySlot.Will,    data.MemoryWill);
            brain.Memory.Write(NPC.MemorySlot.Thought, data.MemoryThought);
            brain.Memory.Write(NPC.MemorySlot.Feeling, data.MemoryFeeling);
            brain.Memory.Write(NPC.MemorySlot.Action,  data.MemoryAction);
            brain.BrokenMove  = data.BrokenMove;
            brain.BrokenSee   = data.BrokenSee;
            brain.BrokenHear  = data.BrokenHear;
            brain.BrokenTalk  = data.BrokenTalk;
            brain.IsForeigner = true; // permanent, regardless of what save says
            brain.SetTile(data.TileX, data.TileY);
            brain.OnDeath    += OnNpcDeath;
            brain.OnDecision += OnNpcDecision;

            LiveNpcs.Add(brain);
            NpcQueue?.Enqueue(brain);

            // Tell the scene layer to add a world node
            _worldScene?.UpsertNpc(data);

            GD.Print($"GameRoot: foreigner '{decan.Name}' arrived from route at {data.CellId}");
        }
    }

    private static void OnNpcDeath(NpcBrain npc)
    {
        LiveNpcs.Remove(npc);
        NpcQueue?.Remove(npc.NpcId);

        // Drop body as a world item at last known position
        Items?.SpawnBody(npc.Decan.Name, npc.TileX, npc.TileY);

        // Delete NPC save file
        Save?.DeleteNpc(npc.NpcId);
        _worldScene?.RemoveNpc(npc.NpcId);
        _worldScene?.RefreshMap();

        GD.Print($"GameRoot: {npc.Decan.Name} died at {npc.CellId()} — body placed");
    }

    private void OnAnimalDeath(AnimalBrain animal)
    {
        LiveAnimals.Remove(animal);
        Items?.SpawnBody(animal.Name, animal.TileX, animal.TileY);
        SpawnAnimalPair(animal.AnimalType, animal.TileX, animal.TileY);
        GD.Print($"GameRoot: {animal.Name} died at {animal.TileX},{animal.TileY} — 2 spawned");
    }

    private void SpawnAnimalPair(AnimalType animalType, int originTileX, int originTileY)
    {
        var offsets = new (int dx, int dy)[]
            { (1,0), (-1,0), (0,1), (0,-1), (1,1), (-1,1), (1,-1), (-1,-1) };

        // Derive cell from tile coords
        int cx = originTileX < 0 ? (originTileX - 7) / 8 : originTileX / 8;
        int cy = originTileY < 0 ? (originTileY - 7) / 8 : originTileY / 8;
        string cellId = $"{cx},{cy}";

        int spawned = 0;
        foreach (var (dx, dy) in offsets)
        {
            if (spawned >= 2) break;
            var brain = new AnimalBrain(
                Guid.NewGuid().ToString("N")[..8],
                animalType.ToString(),
                animalType,
                cellId,
                originTileX + dx,
                originTileY + dy,
                DateTime.UtcNow
            );
            brain.OnDeath += OnAnimalDeath;
            LiveAnimals.Add(brain);
            spawned++;
        }
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

            // Items from WorldItemRegistry
            if (Items != null)
            {
                foreach (var worldItem in Items.InCell(cx + (dx), cy + (dy)))
                    items.Add(new NearbyItem
                    {
                        Id     = worldItem.Id,
                        Name   = worldItem.Name,
                        Edible = worldItem.Edible,
                        CellId = cell.CellId
                    });
            }
        }

        // Keep the prompt inside the model's context window: a morning's manna
        // across the whole vision range is hundreds of lines (thousands of tokens)
        int Near(string cellId)
        {
            ParseCellId(cellId, out var ex, out var ey);
            return Math.Abs(ex - cx) + Math.Abs(ey - cy);
        }
        entities = entities.OrderBy(e => Near(e.CellId)).Take(MaxPromptEntities).ToList();
        items    = items.OrderBy(i => Near(i.CellId)).Take(MaxPromptItems).ToList();

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

        // World metadata — the seed is what regenerates the same world next launch
        Save.SaveWorld(new Data.WorldSaveData
        {
            WorldSeed    = _worldSeed,
            CreatedUtc   = _worldCreatedUtc,
            LastSavedUtc = DateTime.UtcNow,
            WorldName    = WorldName
        });

        // Player — not until they have a name (quit during creation = never arrived)
        if (!string.IsNullOrWhiteSpace(Player.Name))
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
            Save.SaveNpc(ToSaveData(npc));

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

        // Logout = sleeping. Character persists in world in sleep state.
        // If in a cave, they're safe. If exposed, they're vulnerable.
        if (Player != null && !Player.Survival.IsSleeping)
        {
            Player.Survival.BeginSleep(DateTime.UtcNow, Player.Survival.IsInCave);
            GD.Print($"GameRoot: player '{Player.Name}' logged out — sleeping " +
                     $"{(Player.Survival.IsInCave ? "in cave (safe)" : "exposed (vulnerable)")}");
        }

        Player?.EndSession(DateTime.UtcNow);
        SaveAll();
        Llm.Dispose();
        GD.Print("Ain Soph — shutdown complete");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    // ── Cave occupancy ────────────────────────────────────────────────────────

    /// <summary>
    /// Try to claim the cave in the cell at these tile coords for an entity.
    /// Returns true if the cave was unclaimed or already owned by this entity.
    /// Writes to disk immediately.
    /// </summary>
    public static bool TryClaimCave(int tileX, int tileY, string entityId)
    {
        if (Save == null || Grid == null) return false;
        int cx = tileX / 8, cy = tileY / 8;
        var cell = Grid.GetOrGenerate(cx, cy);
        if (!cell.HasCave) return false;

        var saved = Save.LoadCell(cx, cy) ?? new Data.CellSaveData
            { GridX = cx, GridY = cy, Biome = cell.Biome.ToString() };

        if (saved.CaveOccupant != null && saved.CaveOccupant != entityId)
            return false; // occupied by someone else

        saved.CaveOccupant = entityId;
        Save.SaveCell(saved);
        return true;
    }

    /// <summary>Release the cave claim at these tile coords if held by this entity.</summary>
    public static void ReleaseCave(int tileX, int tileY, string entityId)
    {
        if (Save == null || Grid == null) return;
        int cx = tileX / 8, cy = tileY / 8;
        var saved = Save.LoadCell(cx, cy);
        if (saved == null || saved.CaveOccupant != entityId) return;
        saved.CaveOccupant = null;
        Save.SaveCell(saved);
    }

    private static void OnNpcDecision(NpcBrain npc, NpcDecision decision)
    {
        var rng = new Random();

        switch (decision.ParsedState)
        {
            case NPC.NpcState.Moving:
                // Walk a few tiles — toward the target cell if one was named, else wander
                int dx = rng.Next(-1, 2), dy = rng.Next(-1, 2);
                if (!string.IsNullOrEmpty(decision.TargetCell) && decision.TargetCell.Contains(','))
                {
                    ParseCellId(decision.TargetCell, out var tcx, out var tcy);
                    dx = Math.Sign(tcx * 8 + 4 - npc.TileX);
                    dy = Math.Sign(tcy * 8 + 4 - npc.TileY);
                }
                if (!npc.BrokenMove)
                {
                    for (int step = rng.Next(1, 4); step > 0; step--)
                    {
                        int nx = npc.TileX + dx, ny = npc.TileY + dy;
                        if (!IsPassableTile(nx, ny) || IsOccupied(nx, ny)) break;
                        npc.SetTile(nx, ny);
                    }
                }
                break;

            case NPC.NpcState.Eating:
                if (Items == null) break;
                var item = Items.All.FirstOrDefault(i => i.Id == decision.EatItemId && i.Edible)
                        ?? Items.NearestEdible(npc.TileX, npc.TileY, radius: 2);
                if (item != null)
                {
                    // Walk to the food (if nobody is standing on it) and eat it
                    if (!IsOccupied(item.TileX, item.TileY))
                        npc.SetTile(item.TileX, item.TileY);
                    Items.Remove(item.Id);
                    npc.Survival.RecordEat(DateTime.UtcNow);
                }
                break;
        }

        _worldScene?.MoveNpc(npc.NpcId, npc.TileX, npc.TileY);
        _worldScene?.SetNpcState(npc.NpcId, npc.State);
        if (decision.ParsedState == NPC.NpcState.Eating) _worldScene?.RefreshMap();

        if (!string.IsNullOrWhiteSpace(decision.Speech) && !npc.BrokenTalk)
            _worldScene?.ShowNpcSpeech(npc.NpcId, decision.Speech);
        else if (decision.ParsedState == NPC.NpcState.Creating && !string.IsNullOrEmpty(decision.CreationIntent))
            _worldScene?.ShowNpcSpeech(npc.NpcId, $"(working on {decision.CreationIntent})");
    }

    // ── NPC helpers ───────────────────────────────────────────────────────

    private static Data.NpcSaveData ToSaveData(NpcBrain npc) => new()
    {
        Id            = npc.NpcId,
        DecanId       = npc.Decan.Id.ToString(),
        Name          = npc.Decan.Name,
        CellId        = npc.CellId(),
        TileX         = npc.TileX,
        TileY         = npc.TileY,
        State         = npc.State.ToString().ToLower(),
        MemoryWill    = npc.Memory.Will,
        MemoryThought = npc.Memory.Thought,
        MemoryFeeling = npc.Memory.Feeling,
        MemoryAction  = npc.Memory.Action,
        LastAteUtc    = npc.Survival.LastAteUtc,
        LastSleptUtc  = npc.Survival.LastSleptUtc,
        BrokenMove    = npc.BrokenMove,
        BrokenSee     = npc.BrokenSee,
        BrokenHear    = npc.BrokenHear,
        BrokenTalk    = npc.BrokenTalk,
        IsForeigner   = npc.IsForeigner,
    };

    /// <summary>Someone — the player or an NPC — is standing on this tile.</summary>
    private static bool IsOccupied(int tileX, int tileY) =>
        (Player != null && Player.TileX == tileX && Player.TileY == tileY) ||
        LiveNpcs.Any(n => n.TileX == tileX && n.TileY == tileY);

    /// <summary>A tile a being can stand on: passable biome, not water.</summary>
    private static bool IsPassableTile(int tileX, int tileY)
    {
        if (Grid == null) return false;
        int cx = tileX < 0 ? (tileX - 7) / 8 : tileX / 8;
        int cy = tileY < 0 ? (tileY - 7) / 8 : tileY / 8;
        var cell = Grid.GetOrGenerate(cx, cy);
        if (!BiomeData.Get(cell.Biome).Passable) return false;
        return cell.GetTile(tileX - cx * 8, tileY - cy * 8).Surface != TileSurface.Water;
    }

    /// <summary>Global coords of a dry, cave-free tile in a cell (centre-most first).</summary>
    private static (int TileX, int TileY) FindOpenTile(WorldCell cell)
    {
        foreach (var (lx, ly) in new[] { (4, 4), (3, 4), (4, 3), (5, 5), (2, 2), (5, 2), (2, 5), (1, 1), (6, 6) })
        {
            var t = cell.GetTile(lx, ly);
            if (t.Surface != TileSurface.Water && !t.HasCave)
                return (cell.GridX * 8 + lx, cell.GridY * 8 + ly);
        }
        return (cell.GridX * 8 + 4, cell.GridY * 8 + 4);
    }

    private void SeedDemoNpcs(DateTime nowUtc)
    {
        if (Player == null) return;
        var rng    = new Random();
        var decans = DecanRegistry.All.OrderBy(_ => rng.Next()).Take(DemoNpcCount).ToList();

        foreach (var decan in decans)
        {
            // A dry tile within a few steps of the player
            int tx = Player.TileX, ty = Player.TileY;
            for (int tries = 0; tries < 40; tries++)
            {
                int cx = Player.TileX + rng.Next(-5, 6), cy = Player.TileY + rng.Next(-4, 5);
                if ((cx, cy) == (Player.TileX, Player.TileY) || !IsPassableTile(cx, cy)) continue;
                if (LiveNpcs.Any(n => n.TileX == cx && n.TileY == cy)) continue;
                (tx, ty) = (cx, cy);
                break;
            }

            var brain = new NpcBrain($"npc:demo:{Guid.NewGuid():N}", decan, Llm, nowUtc);
            brain.SetTile(tx, ty);
            brain.OnDeath    += OnNpcDeath;
            brain.OnDecision += OnNpcDecision;
            LiveNpcs.Add(brain);
            NpcQueue?.Enqueue(brain);
        }

        GD.Print($"GameRoot: demo — seeded {decans.Count} NPCs near the player");
    }

    // ── WorldScene reference ──────────────────────────────────────────────
    private static WorldScene? _worldScene;

    private static void ConsumeItemFromTile(string itemId, int tileX, int tileY)
    {
        Items?.Remove(itemId);
    }

    /// <summary>Use a primitive on a target as if chosen from the menu (demo tour).</summary>
    public void UsePrimitive(string targetId, Skills.SkillType skill) =>
        OnPrimitiveUsed(targetId, (int)skill);

    public static WorldScene? Scene => _worldScene;

    private async void OnPrimitiveUsed(string targetId, int skillType)
    {
        if (_worldScene == null || Interactions == null || Player == null) return;

        var skill = (Skills.SkillType)skillType;
        var npc   = LiveNpcs.Find(n => n.NpcId == targetId);
        var item  = Items?.All.FirstOrDefault(i => i.Id == targetId);

        // Build the interaction request
        var req = new Skills.InteractionRequest
        {
            ActorId    = Player.Id,
            Primitive  = skill.ToString().ToLower(),
            TargetId   = targetId,
            TargetName = npc?.Decan.Name ?? item?.Name ?? targetId.Replace("tile:", ""),
            TargetType = targetId.StartsWith("tile:")                ? Skills.InteractionTarget.Tile
                       : npc != null                                 ? Skills.InteractionTarget.Npc
                       : LiveAnimals.Exists(a => a.AnimalId == targetId) ? Skills.InteractionTarget.Animal
                       : Skills.InteractionTarget.Item,
        };

        // Reaping needs you next to the target; talking needs you within earshot
        int dist = npc != null  ? Math.Max(Math.Abs(npc.TileX - Player.TileX), Math.Abs(npc.TileY - Player.TileY))
                 : item != null ? Math.Max(Math.Abs(item.TileX - Player.TileX), Math.Abs(item.TileY - Player.TileY))
                 : 0;
        if ((skill == Skills.SkillType.Reap && dist > 1) || (skill == Skills.SkillType.Talk && dist > 2))
        {
            _worldScene.ShowWorldText(npc != null ? $"{req.TargetName} is too far away." : "It is out of reach.");
            return;
        }

        // Talk → open dialogue; each line the player speaks gets an in-character reply
        if (skill == Skills.SkillType.Talk && npc != null)
        {
            var decanId = int.TryParse(npc.Decan.Id, out var did) ? did : TileRegistryHash(npc.Decan.Id);
            _worldScene.OpenNpcDialogueFull(
                targetId, npc.Decan.Name, decanId, TileRegistryHash(targetId),
                npc.BrokenTalk ? "(They cannot speak. They watch you.)" : $"{npc.Decan.Name} turns to you.",
                text => TalkToNpc(npc, text));
            return;
        }

        // Reap on a living being → kill resolution (d100 each side, ties to the defender)
        if (skill == Skills.SkillType.Reap && npc != null)
        {
            var kill = KillResolver.Resolve(Player.KillNumber, npc.KillNumber);
            GD.Print($"GameRoot: reap {npc.Decan.Name} — {kill.AttackerRoll}/{kill.AttackerKillNum} " +
                     $"vs {kill.DefenderRoll}/{kill.DefenderKillNum}");
            if (kill.AttackerSucceeds)
            {
                _worldScene.ShowWorldText($"You reap {npc.Decan.Name}. The body remains.");
                npc.Kill();
            }
            else
            {
                _worldScene.ShowWorldText($"{npc.Decan.Name} resists. Neither of you falls.");
                _worldScene.ShowNpcSpeech(npc.NpcId, "!");
            }
            return;
        }

        // Pray → only at the altar does it reach the Council
        if (skill == Skills.SkillType.Pray)
        {
            if (IsAtAltar())
                _worldScene.OpenAltar(petition => OnAltarPetition(petition));
            else
                _worldScene.ShowWorldText("You pray. The world does not move.");
            return;
        }

        // All other primitives → resolve and show world text
        var result = await Interactions.ResolveAsync(req, _cts.Token);
        if (!string.IsNullOrEmpty(result.WorldText))
            _worldScene.ShowWorldText(result.WorldText);
        if (skill == Skills.SkillType.Reap && item != null)
            _worldScene.RefreshMap();
    }

    private async void TalkToNpc(NpcBrain npc, string text)
    {
        if (_worldScene == null) return;
        if (npc.BrokenTalk)
        {
            _worldScene.SetDialogueSpeech("(They cannot answer. Something passes across their face.)");
            return;
        }

        _worldScene.SetDialogueSpeech("…");
        try
        {
            var reply = await npc.RespondToDialogueAsync(text, BuildNpcSituation(npc), _cts.Token);
            reply = reply.Trim();
            if (reply.Length > 1 && reply[0] == '"' && reply[^1] == '"') reply = reply[1..^1];
            if (reply.Length == 0) reply = "…";
            _worldScene.SetDialogueSpeech(reply);
            _worldScene.ShowNpcSpeech(npc.NpcId, reply);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            GD.PrintErr($"GameRoot: dialogue with {npc.NpcId} failed: {ex.Message}");
            _worldScene.SetDialogueSpeech("(They look past you, lost in thought.)");
        }
    }

    /// <summary>Standing on or next to the altar tile.</summary>
    private static bool IsAtAltar()
    {
        if (Altar == null || Player == null) return false;
        ParseCellId(Altar.CellId, out var ax, out var ay);
        int atx = ax * 8 + Altar.TileX, aty = ay * 8 + Altar.TileY;
        return Math.Abs(Player.TileX - atx) <= 1 && Math.Abs(Player.TileY - aty) <= 1;
    }

    private static int TileRegistryHash(string s) => AinSoph.UI.TileRegistry.StableHash(s);

    private async void OnAltarPetition(string petition)
    {
        if (_worldScene == null || Council == null || Player == null) return;

        _worldScene.SetDialogueSpeech("The Council deliberates…");

        // Parse free-form prayer into a structured submission
        var parser     = new Skills.CouncilSubmissionParser();
        var submission = parser.Parse(petition);

        CouncilVerdict verdict;
        if (submission != null)
        {
            submission.CreatedBy = Player.Id;
            verdict = await Council.SubmitAsync(submission, _cts.Token);
        }
        else
        {
            // Unparseable prayer — Council hears it; all three seats speak; nothing enters the world
            var oblique = new Council.CouncilSubmission
            {
                Type        = "unknown",
                Name        = petition[..Math.Min(40, petition.Length)],
                Description = petition,
                CreatedBy   = Player.Id,
            };
            verdict = await Council.SubmitAsync(oblique, _cts.Token);
        }

        // Deliver all three homiilies to the player regardless of outcome
        var sb = new System.Text.StringBuilder();
        foreach (var response in verdict.Responses)
        {
            sb.AppendLine($"[ {response.Seat.ToUpper()} — {response.Vote.ToUpper()} ]");
            sb.AppendLine(response.Homily);
            sb.AppendLine();
        }
        _worldScene.SetDialogueSpeech(verdict.Responses.Count > 0
            ? sb.ToString().Trim()
            : "The Council is silent. Nothing enters the world.");

        // Apply approved content to the world
        if (verdict.Approved && verdict.Submission != null)
        {
            var sub = verdict.Submission;
            GD.Print($"GameRoot: Council approved '{sub.Name}' ({sub.Type}) for {Player.Name}");

            switch (sub.Type.ToLower())
            {
                case "skill":
                    Player.SkillIds.Add(sub.Name.ToLower().Replace(" ", "_"));
                    // Refresh HUD — custom skills show as unlocked slots
                    // For now, primitive SkillType slots are fixed; custom skills append
                    _worldScene.ShowWorldText($"The Council grants: {sub.Name}");
                    SaveAll();
                    break;

                case "item":
                    // Spawn the item near the player at the altar tile
                    Items?.Spawn(
                        name:        sub.Name,
                        type:        "crafted",
                        tileX:       Player.TileX,
                        tileY:       Player.TileY,
                        edible:      sub.Properties.ContainsKey("edible"),
                        description: sub.Description
                    );
                    _worldScene.ShowWorldText($"The Council grants: {sub.Name}");
                    SaveAll();
                    break;

                case "rule":
                    // Rules are logged — full rule engine is future scope
                    GD.Print($"GameRoot: rule '{sub.Name}' approved — '{sub.Description}'");
                    _worldScene.ShowWorldText($"The Council accepts the rule: {sub.Name}");
                    break;
            }
        }
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
