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

        // 1. Model extraction (first launch) — boots rest of world in callback
        var bootScreen = new AinSoph.UI.ModelBootScreen();
        AddChild(bootScreen);
        bootScreen.OnReady += BootWithModel;
    }

    private void BootWithModel(string modelPath)
    {
        // 1. LLM
        Llm.Initialize(modelPath);
        GD.Print("GameRoot: LLM ready");

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
            WorldName = worldData.WorldName;
            GD.Print($"GameRoot: loaded world '{WorldName}' (seed {worldSeed})");
        }
        else
        {
            worldSeed = new Random().Next();
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

        // 4. World grid + altar
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

        // Drain NPC queue — one NPC per call, queue runs continuously
        if (NpcQueue is not null)
            _ = NpcQueue.ProcessNextAsync(BuildNpcSituation, _cts.Token);

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

        Player = new PlayerCharacter(nowUtc)
        {
            Id     = $"player:{Guid.NewGuid():N}",
            TileX  = spawnCell.GridX * 8 + 4,
            TileY  = spawnCell.GridY * 8 + 4,
            CellId = $"{spawnCell.GridX},{spawnCell.GridY}"
        };

        var spawnTile = new Vector2I(Player.TileX, Player.TileY);

        // Show naming screen — world keeps running behind it
        var creation = new AinSoph.UI.CharacterCreationScreen();
        AddChild(creation);
        creation.Show((name) =>
        {
            Player!.Name = string.IsNullOrWhiteSpace(name) ? "Unnamed" : name;
            GD.Print($"GameRoot: new player '{Player.Name}' descends");
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

            foreach (var (tx, ty) in tiles)
            {
                var item = Items.SpawnManna(tx, ty);

                // Register on tile so NPCs can find it via SituationContext
                if (cell != null)
                {
                    int lx = tx - cx * 8;
                    int ly = ty - cy * 8;
                    if (lx >= 0 && lx < 8 && ly >= 0 && ly < 8)
                        cell.GetTile(lx, ly).ItemIds.Add(item.Id);
                }
                total++;
            }
        }
        GD.Print($"GameRoot: morning manna — {total} portions spawned");
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
            brain.SetCellId(data.CellId);
            brain.OnDeath += OnNpcDeath;

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
        ParseCellId(npc.CellId(), out var cx, out var cy);
        var cell = Grid?.GetIfLoaded(cx, cy);
        int tileX = cell != null ? cx * 8 : 0;
        int tileY = cell != null ? cy * 8 : 0;
        Items?.SpawnBody(npc.Decan.Name, tileX, tileY);

        // Delete NPC save file
        Save?.DeleteNpc(npc.NpcId);
        _worldScene?.RemoveNpc(npc.NpcId);

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
                DecanId       = npc.Decan.Id.ToString(),
                Name          = npc.Decan.Name,
                CellId        = npc.CellId(),
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
        if (decision.ParsedState != NPC.NpcState.Eating) return;
        if (Items == null) return;

        var saved = Save?.LoadNpc(npc.NpcId);
        int tx = saved?.TileX ?? 0;
        int ty = saved?.TileY ?? 0;

        // Find item by id or nearest edible
        var item = Items.All.FirstOrDefault(i => i.Id == decision.EatItemId)
                ?? Items.NearestEdible(tx, ty);

        if (item != null && item.Edible)
        {
            Items.Remove(item.Id);
            npc.Survival.RecordEat(DateTime.UtcNow);
        }
    }

    // ── WorldScene reference ──────────────────────────────────────────────
    private static WorldScene? _worldScene;

    private static void ConsumeItemFromTile(string itemId, int tileX, int tileY)
    {
        Items?.Remove(itemId);
    }

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

        // Eat: any primitive used on an edible item consumes it and satisfies hunger
        if (req.TargetType == Skills.InteractionTarget.Item && Items != null)
        {
            var item = Items.All.FirstOrDefault(i => i.Id == targetId);
            if (item != null && item.Edible && Player != null)
            {
                Player.Survival.RecordEat(System.DateTime.UtcNow);
                ConsumeItemFromTile(item.Id, Player.TileX, Player.TileY);
                _worldScene?.ShowWorldText("You eat. The hunger recedes.");
                return;
            }
        }

        // Talk → open dialogue screen then let the NPC's next think reply
        if (skill == Skills.SkillType.Talk && req.TargetType == Skills.InteractionTarget.Npc)
        {
            var npc = LiveNpcs.Find(n => n.NpcId == targetId);
            if (npc != null)
            {
                var npcSave = Save?.LoadNpc(targetId);
                var name    = npcSave?.Name ?? "Traveller";
                var decanId = int.TryParse(npc.Decan.Id, out var did) ? did : npc.Decan.Id.GetHashCode();
                _worldScene.OpenNpcDialogueFull(
                    targetId, name, decanId, targetId.GetHashCode(),
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
        if (_worldScene == null || Council == null || Player == null) return;

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
        _worldScene.SetDialogueSpeech(sb.ToString().Trim());

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
