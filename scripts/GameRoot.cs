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

    /// <summary>Rules the Council has approved in this world.</summary>
    public static List<Data.LawRecord> Laws                { get; } = new();

    /// <summary>No model loaded — NPCs and the Council use scripted replies.</summary>
    public static bool                 IsDemo              => !Llm.IsReady;

    // -------------------------------------------------------------------------
    // Config
    // -------------------------------------------------------------------------

    private const string SaveSubPath  = "user://saves/world";
    private const string TourSavePath = "user://saves/demo_tour"; // wiped each tour run
    private const string TestSavePath = "user://saves/selftest";  // wiped each self-test run

    private static bool HasArg(string arg) =>
        OS.GetCmdlineUserArgs().Contains(arg) || OS.GetCmdlineArgs().Contains(arg);

    private CancellationTokenSource _cts = new();

    private int      _worldSeed;
    private DateTime _worldCreatedUtc = DateTime.UtcNow;

    private bool _ribButtonShown;
    private double _statusTimer;

    private void ApplySettings() => AinSoph.UI.GameSettings.Apply();

    private void UpdateSurvivalStatus()
    {
        if (Player == null || _worldScene == null) return;
        var now   = DateTime.UtcNow;
        var ate   = (now - Player.Survival.LastAteUtc).TotalHours;
        var slept = (now - Player.Survival.LastSleptUtc).TotalHours;
        string Ago(double h) => h < 1 ? $"{(int)(h * 60)}m" : $"{h:F0}h";

        var text = Player.Survival.IsSleeping
            ? (Player.Survival.IsInCave ? "Sleeping in a cave" : "Sleeping exposed")
            : $"Ate {Ago(ate)} ago · Slept {Ago(slept)} ago";
        if (Player.Survival.IsInCave && !Player.Survival.IsSleeping) text += " · In a cave";
        _worldScene.SetSurvivalStatus(text, ate >= 20 || slept >= 20);
    }

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
        _instance = this;
        GD.Print($"Ain Soph {AinSoph.UI.GameSettings.Version} — booting");

        // Sound and settings first, so the boot screen already has music
        AddChild(new AinSoph.Audio.Sound());
        AinSoph.UI.GameSettings.Load();
        CallDeferred(MethodName.ApplySettings);

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
        var tour     = HasArg("--demo-tour");
        var selfTest = HasArg("--selftest");
        var saveDir  = ProjectSettings.GlobalizePath(tour ? TourSavePath : selfTest ? TestSavePath : SaveSubPath);
        if ((tour || selfTest) && System.IO.Directory.Exists(saveDir))
            System.IO.Directory.Delete(saveDir, recursive: true);
        Save        = new SaveManager(saveDir);

        var worldData = Save.LoadWorld();
        int worldSeed;

        bool isNewWorld = !(worldData is not null && worldData.WorldSeed != 0);
        if (!isNewWorld)
        {
            worldSeed = worldData!.WorldSeed;
            WorldName = worldData.WorldName;
            Laws.Clear();
            Laws.AddRange(worldData.Laws);
            PublishLaws();
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

        // 4b'. Animals — saved ones, plus starting animals for every new cell
        LoadAnimals(DateTime.UtcNow);

        // 4c. Route manager
        Routes = new Data.RouteManager(Save, LiveNpcs);
        Routes.OnEmigrated += npc =>
        {
            NpcQueue?.Remove(npc.NpcId);
            _worldScene?.RemoveNpc(npc.NpcId);
        };

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
            Player.AccumulatedPlayHours = playerData.AccumulatedPlayHours;
            // Survival continues from the save — time away counts (logout is sleep)
            Player.Survival.Restore(playerData.LastAteUtc, playerData.LastSleptUtc,
                                    playerData.SleepStartUtc, playerData.IsInCave);
            Player.RestoreTribe(playerData.HasRib, playerData.SpouseNpcId, playerData.ProgenyIds);
            foreach (var skill in playerData.SkillIds) Player.SkillIds.Add(skill);
            GD.Print($"GameRoot: player '{Player.Name}' loaded — {Player.AccumulatedPlayHours:F1}h played" +
                     (Player.HasSpouse ? ", has spouse" : Player.HasRib ? ", rib earned" : ""));
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
        if (HasArg("--grant-rib") || tour) Player.GrantRibForTesting(); // testing / demo tour only

        // Load all saved NPCs into the queue
        foreach (var npcData in Save.LoadAllNpcs())
        {
            var decan = DecanRegistry.Get(npcData.DecanId);
            if (decan is null) continue;

            var brain = new NpcBrain(npcData.Id, decan, Llm, nowUtc) { Name = npcData.Name };
            brain.SetTile(npcData.TileX, npcData.TileY);
            brain.Lineage.AddRange(npcData.Lineage);
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

        // A new world is not empty: a few travellers crossed into it before you
        // did. They are foreigners — they live, talk and survive, but cannot
        // create or pray. Your own tribe still comes only from the rib.
        if (isNewWorld && !tour)
            SeedFoundingTravellers(nowUtc);

        // Demo mode and the tour: a few native NPCs (who can create and pray) to show the world living
        if ((IsDemo || tour) && LiveNpcs.Count == 0)
            SeedDemoNpcs(nowUtc);

        // 8. Tribe
        CreateTribe(nowUtc, playerData?.LastProgenyBirthUtc);

        // 9. Council + interaction
        Council      = new TribuneCouncil(Llm);
        _creations   = new NpcCreationPipeline(Council);
        _creations.OnCreationApproved += OnNpcCreationApproved;
        _creations.OnCreationRejected += OnNpcCreationRejected;
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
            ShowAllAnimals();
            scene.RibRequested += OnRibRequested;
            AddChild(new AinSoph.UI.GameMenu());
            if (!tour && !selfTest) AddChild(new AinSoph.UI.Hints()); // the tour has its own captions
            if (IsDemo) scene.ShowWorldText("Demo mode — the voices you hear are scripted.");
            if (tour)     AddChild(new Demo.DemoDirector());
            if (selfTest) AddChild(new Demo.SelfTest());
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

        // Hunger and sleep at a glance
        _statusTimer -= delta;
        if (_statusTimer <= 0 && _worldScene != null)
        {
            _statusTimer = 1.0;
            UpdateSurvivalStatus();
        }

        // The rib — announce it once when earned; the RIB button stays until it is used
        bool ribReady = Player.HasRib && !Player.HasSpouse && !string.IsNullOrEmpty(Player.Name);
        if (ribReady != _ribButtonShown && _worldScene != null)
        {
            _ribButtonShown = ribReady;
            _worldScene.SetRibAvailable(ribReady);
            if (ribReady)
            {
                _worldScene.ShowWorldText("A week has passed in this world. Something stirs at your side — the rib is yours.");
                AinSoph.Audio.Sound.Play("rib");
            }
        }

        // Animals near the player wander
        _animalWanderTimer -= delta;
        if (_animalWanderTimer <= 0)
        {
            _animalWanderTimer = AnimalWanderSeconds;
            WanderAnimals();
        }

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

        // Animals — instinct only: eat, sleep, predators hunt, prey flee
        foreach (var animal in LiveAnimals.ToList())
        {
            if (!LiveAnimals.Contains(animal)) continue; // killed earlier this tick
            var situation = BuildAnimalSituation(animal);
            animal.Tick(situation, nowUtc);
        }

        // Tribe — may birth progeny
        Tribe?.Tick(Llm, nowUtc);

        // Player survival — check warnings
        if (Player != null)
        {
            var result = Player.Survival.Tick(nowUtc);
            if (result.HungerWarning) { _worldScene?.ShowWorldText("⚠ You must eat within the hour. ⚠"); AinSoph.Audio.Sound.Play("warning"); }
            if (result.SleepWarning)  { _worldScene?.ShowWorldText("⚠ You must sleep within the hour. ⚠"); AinSoph.Audio.Sound.Play("warning"); }
            if (result.IsDead)
                KillPlayer(result.DiedOfStarvation ? "starved" : "died of exhaustion");
        }
    }

    /// <summary>The player dies: body stays, cave freed, save cleared, a new character descends.</summary>
    private void KillPlayer(string cause)
    {
        if (Player == null) return;

        // Drop corpse — body persists in world
        Items?.SpawnBody(Player.Name, Player.TileX, Player.TileY);

        // Release any cave claim
        ReleaseCave(Player.TileX, Player.TileY, Player.Id);

        // Delete old player save
        Save?.DeletePlayer();

        GD.Print($"GameRoot: player '{Player.Name}' died ({cause})");
        AinSoph.Audio.Sound.Play("death");
        var fallenName = Player.Name;
        Player = null;

        // New character — show creation screen, spawn near a cave
        _worldScene?.ShowWorldText($"{fallenName} {cause}. A new light descends.");
        _worldScene?.RefreshMap();
        CallDeferred(MethodName.SpawnNewPlayer);
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
        Player.BeginSession(nowUtc);

        // A new character has their own week to earn their own rib
        CreateTribe(nowUtc, null);

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

            var brain = new NpcBrain(data.Id, decan, Llm, nowUtc) { Name = data.Name };
            brain.Lineage.AddRange(data.Lineage);
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
        Items?.SpawnBody(npc.Name, npc.TileX, npc.TileY);

        // Delete NPC save file
        Save?.DeleteNpc(npc.NpcId);
        _worldScene?.RemoveNpc(npc.NpcId);
        _worldScene?.RefreshMap();

        GD.Print($"GameRoot: {npc.Name} died at {npc.CellId()} — body placed");
    }

    // ── Animals ───────────────────────────────────────────────────────────

    // Animals placed in cells once per world; tracked so a regenerated cell
    // doesn't get a second set
    private readonly HashSet<string> _animalCells = new();

    // Replacement (two for one) stops when a cell is this crowded, so deaths
    // by starvation can't double the population without limit
    private const int MaxAnimalsPerCell = 6;

    private double _animalWanderTimer;
    private const double AnimalWanderSeconds = 1.5;

    private void LoadAnimals(DateTime nowUtc)
    {
        var data = Save?.LoadAnimals();
        if (data != null)
        {
            _animalCells.UnionWith(data.PopulatedCells);
            foreach (var a in data.Animals)
            {
                var species = AnimalSpecies.Get(a.Name);
                if (species != null) AddAnimal(species, a.TileX, a.TileY, nowUtc, a.Id);
            }
        }

        // New cells get their starting animals as they are first generated
        Grid!.CellGenerated += cell =>
        {
            if (!_animalCells.Add(cell.CellId)) return;
            foreach (var p in Grid.PlaceAnimals(cell))
            {
                var species = AnimalSpecies.Get(p.Name);
                if (species != null) AddAnimal(species, p.TileX, p.TileY, DateTime.UtcNow);
            }
        };

        // Cells generated before the hook (the start area) still need theirs
        foreach (var cell in Grid.LoadedCells.ToList())
        {
            if (!_animalCells.Add(cell.CellId)) continue;
            foreach (var p in Grid.PlaceAnimals(cell))
            {
                var species = AnimalSpecies.Get(p.Name);
                if (species != null) AddAnimal(species, p.TileX, p.TileY, nowUtc);
            }
        }

        GD.Print($"GameRoot: {LiveAnimals.Count} animals loaded; new cells get their own as they are found");
    }

    private void SaveAnimals() => Save?.SaveAnimals(new Data.AnimalsSaveData
    {
        PopulatedCells = _animalCells.ToList(),
        Animals = LiveAnimals.Select(a => new Data.AnimalSaveData
        {
            Id = a.AnimalId, Name = a.Name, AnimalType = a.AnimalType.ToString(),
            TileX = a.TileX, TileY = a.TileY,
        }).ToList(),
    });

    private AnimalBrain AddAnimal(AnimalSpecies species, int tileX, int tileY, DateTime nowUtc, string? id = null)
    {
        var brain = new AnimalBrain(id ?? $"animal:{Guid.NewGuid():N}", species.Name, species.Type,
                                    CellOf(tileX, tileY), tileX, tileY, nowUtc);
        brain.OnDeath  += OnAnimalDeath;
        brain.OnEat    += OnAnimalEat;
        brain.OnAttack += OnAnimalAttack;
        brain.OnFlee   += OnAnimalFlee;
        LiveAnimals.Add(brain);
        _worldScene?.UpsertAnimal(brain.AnimalId, species.Name, species.Glyph, species.Tint, tileX, tileY);
        return brain;
    }

    /// <summary>Animals visible on the map — called once the scene exists.</summary>
    private void ShowAllAnimals()
    {
        foreach (var a in LiveAnimals)
            if (a.Species is { } sp)
                _worldScene?.UpsertAnimal(a.AnimalId, sp.Name, sp.Glyph, sp.Tint, a.TileX, a.TileY);
    }

    private void OnAnimalDeath(AnimalBrain animal)
    {
        if (!LiveAnimals.Remove(animal)) return;
        _worldScene?.RemoveNpc(animal.AnimalId);

        // The body stays. A clean animal's body is food for a day.
        var edible = animal.Species?.Edible == true;
        Items?.Spawn(
            name:          $"Body of {animal.Name}",
            type:          "body",
            tileX:         animal.TileX,
            tileY:         animal.TileY,
            edible:        edible,
            lifespanHours: edible ? 24f : null,
            description:   edible ? $"A {animal.Name}, dead. Clean meat." : $"A {animal.Name}, dead.");

        SpawnAnimalPair(animal);
        _worldScene?.RefreshMap();
        GD.Print($"GameRoot: {animal.Name} died at {animal.TileX},{animal.TileY}");
    }

    /// <summary>When one animal dies, two of its kind appear beside where it fell (ITEMS.md).</summary>
    private void SpawnAnimalPair(AnimalBrain dead)
    {
        var species = dead.Species;
        if (species == null) return;

        var cell = CellOf(dead.TileX, dead.TileY);
        if (LiveAnimals.Count(a => a.CellId == cell) >= MaxAnimalsPerCell) return;

        int spawned = 0;
        foreach (var (dx, dy) in new[] { (1,0), (-1,0), (0,1), (0,-1), (1,1), (-1,1), (1,-1), (-1,-1) })
        {
            if (spawned >= 2) break;
            int x = dead.TileX + dx, y = dead.TileY + dy;
            if (!AnimalCanStand(species, x, y) || IsOccupied(x, y) || AnimalAt(x, y) != null) continue;
            AddAnimal(species, x, y, DateTime.UtcNow);
            spawned++;
        }
    }

    private void OnAnimalEat(AnimalBrain animal, string itemId)
    {
        Items?.Remove(itemId); // manna is shared — what an animal eats, no one else can
        _worldScene?.RefreshMap();
    }

    /// <summary>A predator attacks whoever is beside it — d100 against d100, ties to the defender.</summary>
    private void OnAnimalAttack(AnimalBrain predator, string targetId)
    {
        if (Player != null && targetId == Player.Id)
        {
            var kill = KillResolver.Resolve(predator.KillNumber, Player.KillNumber);
            _worldScene?.ShowNpcSpeech(predator.AnimalId, "!");
            if (kill.AttackerSucceeds) KillPlayer($"was killed by a {predator.Name}");
            else { _worldScene?.ShowWorldText($"A {predator.Name} attacks! You fight it off."); AinSoph.Audio.Sound.Play("reap"); }
            return;
        }

        var npc = LiveNpcs.Find(n => n.NpcId == targetId);
        if (npc != null)
        {
            if (KillResolver.Resolve(predator.KillNumber, npc.KillNumber).AttackerSucceeds) npc.Kill();
            return;
        }

        var prey = LiveAnimals.Find(a => a.AnimalId == targetId);
        if (prey != null && KillResolver.Resolve(predator.KillNumber, prey.KillNumber).AttackerSucceeds)
            prey.Kill();
    }

    /// <summary>Prey bolts two tiles away from the nearest predator.</summary>
    private void OnAnimalFlee(AnimalBrain prey)
    {
        var threat = LiveAnimals
            .Where(a => a.AnimalType == AnimalType.Predator && a.AnimalId != prey.AnimalId)
            .OrderBy(a => Math.Abs(a.TileX - prey.TileX) + Math.Abs(a.TileY - prey.TileY))
            .FirstOrDefault();
        if (threat == null || prey.Species is not { } sp) return;

        for (int i = 0; i < 2; i++)
        {
            int x = prey.TileX + Math.Sign(prey.TileX - threat.TileX);
            int y = prey.TileY + Math.Sign(prey.TileY - threat.TileY);
            if (!AnimalCanStand(sp, x, y) || IsOccupied(x, y) || AnimalAt(x, y) != null) break;
            MoveAnimal(prey, x, y);
        }
    }

    /// <summary>A few animals near the player take a step each beat, so the world looks alive.</summary>
    private void WanderAnimals()
    {
        if (Player == null) return;
        var rng = Random.Shared;
        foreach (var a in LiveAnimals)
        {
            if (a.Survival.IsSleeping || a.Species is not { } sp) continue;
            if (Math.Abs(a.TileX - Player.TileX) > 24 || Math.Abs(a.TileY - Player.TileY) > 16) continue;
            if (rng.NextDouble() > (sp.Habitat == AnimalHabitat.Bird ? 0.5 : 0.3)) continue;

            int x = a.TileX + rng.Next(-1, 2), y = a.TileY + rng.Next(-1, 2);
            if (AnimalCanStand(sp, x, y) && !IsOccupied(x, y) && AnimalAt(x, y) == null)
                MoveAnimal(a, x, y);
        }
    }

    private void MoveAnimal(AnimalBrain a, int x, int y)
    {
        a.TileX = x; a.TileY = y; a.CellId = CellOf(x, y);
        _worldScene?.MoveNpc(a.AnimalId, x, y);
    }

    private static AnimalBrain? AnimalAt(int x, int y) =>
        LiveAnimals.Find(a => a.TileX == x && a.TileY == y);

    private static bool AnimalCanStand(AnimalSpecies species, int tileX, int tileY)
    {
        if (Grid == null) return false;
        int cx = tileX < 0 ? (tileX - 7) / 8 : tileX / 8;
        int cy = tileY < 0 ? (tileY - 7) / 8 : tileY / 8;
        var tile = Grid.GetOrGenerate(cx, cy).GetTile(tileX - cx * 8, tileY - cy * 8);
        return species.CanStandOn(tile.Surface) && !tile.HasCave;
    }

    private static string CellOf(int tileX, int tileY)
    {
        int cx = tileX < 0 ? (tileX - 7) / 8 : tileX / 8;
        int cy = tileY < 0 ? (tileY - 7) / 8 : tileY / 8;
        return $"{cx},{cy}";
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
                    Name      = other.Name,
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
        static int Dist(int ax, int ay, int bx, int by) => Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));

        // Predators strike only what is right beside them: the player, an NPC, or prey
        string? target = null;
        if (animal.AnimalType == AnimalType.Predator)
        {
            if (Player != null && !string.IsNullOrEmpty(Player.Name) &&
                Dist(Player.TileX, Player.TileY, animal.TileX, animal.TileY) <= 1)
                target = Player.Id;
            target ??= LiveNpcs.FirstOrDefault(n => Dist(n.TileX, n.TileY, animal.TileX, animal.TileY) <= 1)?.NpcId;
            target ??= LiveAnimals.FirstOrDefault(a => a.AnimalType != AnimalType.Predator &&
                                                       Dist(a.TileX, a.TileY, animal.TileX, animal.TileY) <= 1)?.AnimalId;
        }

        // Manna within reach — animals and players compete for the same supply
        var manna = Items?.All.FirstOrDefault(i => i.Type == "manna" &&
                                                   Dist(i.TileX, i.TileY, animal.TileX, animal.TileY) <= 1);

        return new AnimalSituation
        {
            NearbyEntityId        = target,
            NearbyEdibleItemId    = manna?.Id,
            NearbyPredatorPresent = LiveAnimals.Any(a => a.AnimalId != animal.AnimalId &&
                                                         a.AnimalType == AnimalType.Predator &&
                                                         Dist(a.TileX, a.TileY, animal.TileX, animal.TileY) <= 2),
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
            WorldName    = WorldName,
            Laws         = Laws.ToList(),
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
            IsInCave     = Player.Survival.IsInCave,
            SleepStartUtc = Player.Survival.SleepStartUtc,
            SkillIds     = Player.SkillIds.ToList(),

            // Rib — play time must survive restarts or a week of play never adds up
            AccumulatedPlayHours = Player.TotalPlayHours,
            HasRib               = Player.HasRib,
            SpouseNpcId          = Player.SpouseNpcId,
            ProgenyIds           = Player.ProgenyIds.ToList(),
            LastProgenyBirthUtc  = Tribe?.LastProgenyBirthUtc,
        });

        // NPCs
        foreach (var npc in LiveNpcs)
            Save.SaveNpc(ToSaveData(npc));

        SaveAnimals();

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

        // An NPC who sets out to create something takes it to the Council (NPCS.md)
        if (decision.ParsedState == NPC.NpcState.Creating && !npc.IsForeigner &&
            !string.IsNullOrWhiteSpace(decision.CreationIntent))
            _instance?.TryBeginCreation(npc, decision);

        _worldScene?.MoveNpc(npc.NpcId, npc.TileX, npc.TileY);
        _worldScene?.SetNpcState(npc.NpcId, npc.State);
        if (decision.ParsedState == NPC.NpcState.Eating) _worldScene?.RefreshMap();

        if (!string.IsNullOrWhiteSpace(decision.Speech) && !npc.BrokenTalk)
            _worldScene?.ShowNpcSpeech(npc.NpcId, decision.Speech);
        else if (decision.ParsedState == NPC.NpcState.Creating && !string.IsNullOrEmpty(decision.CreationIntent))
            _worldScene?.ShowNpcSpeech(npc.NpcId, $"(working on {decision.CreationIntent})");
    }

    // ── Laws, NPC creations, founding travellers ─────────────────────────

    private static GameRoot? _instance;
    private NpcCreationPipeline? _creations;
    private readonly HashSet<string> _creating = new();
    private DateTime _lastCreationUtc = DateTime.MinValue;

    // Three Council calls per creation; on a small CPU that is a minute or more
    // of inference, so NPC creations are spaced out
    private TimeSpan CreationCooldown => IsDemo ? TimeSpan.FromSeconds(40) : TimeSpan.FromMinutes(20);

    private const int FoundingTravellerCount = 4;

    private static void PublishLaws() =>
        NpcPromptBuilder.WorldLaws = Laws.Select(l => (l.Name, l.Description)).ToList();

    public static void AddLaw(string name, string description, string createdBy)
    {
        Laws.Add(new Data.LawRecord
        {
            Name = name, Description = description, CreatedBy = createdBy, ApprovedUtc = DateTime.UtcNow,
        });
        PublishLaws();
        GD.Print($"GameRoot: law added — {name}");
    }

    private void TryBeginCreation(NpcBrain npc, NpcDecision decision)
    {
        if (_creations == null || _creating.Contains(npc.NpcId)) return;
        if (DateTime.UtcNow - _lastCreationUtc < CreationCooldown) return;

        _lastCreationUtc = DateTime.UtcNow;
        _creating.Add(npc.NpcId);
        _worldScene?.SetNpcState(npc.NpcId, NPC.NpcState.Praying);
        _worldScene?.ShowNpcSpeech(npc.NpcId, $"(praying: {decision.CreationIntent})");
        RunCreation(npc, decision);
    }

    private async void RunCreation(NpcBrain npc, NpcDecision decision)
    {
        try   { await _creations!.RunAsync(npc, decision, _cts.Token); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { GD.PrintErr($"GameRoot: creation by {npc.NpcId} failed: {ex.Message}"); }
        finally { _creating.Remove(npc.NpcId); }
    }

    private void OnNpcCreationApproved(NpcBrain npc, Council.CouncilVerdict verdict)
    {
        var sub = verdict.Submission;
        if (sub == null || !LiveNpcs.Contains(npc)) return;

        switch (sub.Type.ToLowerInvariant())
        {
            case "item":
                var (tx, ty) = FindFreeTileNear(npc.TileX, npc.TileY);
                Items?.Spawn(name: sub.Name, type: "crafted", tileX: tx, tileY: ty, description: sub.Description);
                break;
            case "rule":
                AddLaw(sub.Name, sub.Description, npc.NpcId);
                break;
            default: // skill
                npc.Memory.Write(MemorySlot.Action, $"The Council granted me a new skill: {sub.Name}.");
                break;
        }

        npc.Memory.Write(MemorySlot.Feeling, $"The Council heard me. {sub.Name} is in the world now.");
        _worldScene?.ShowNpcSpeech(npc.NpcId, $"The Council has granted it: {sub.Name}.");
        if (IsNear(npc)) _worldScene?.ShowWorldText($"{npc.Name}'s creation enters the world: {sub.Name}.");
        _worldScene?.RefreshMap();
        SaveAll();
    }

    private void OnNpcCreationRejected(NpcBrain npc, Council.CouncilVerdict verdict)
    {
        if (!LiveNpcs.Contains(npc)) return;
        var parable = verdict.Responses.FirstOrDefault(r => !r.Passed)?.Homily ?? "";
        npc.Memory.Write(MemorySlot.Feeling,
            $"I prayed for {verdict.Submission?.Name}. The Council answered with a story: {parable}");
        _worldScene?.ShowNpcSpeech(npc.NpcId, "(the Council does not move)");
    }

    private bool IsNear(NpcBrain npc) =>
        Player != null && Math.Abs(npc.TileX - Player.TileX) <= 12 && Math.Abs(npc.TileY - Player.TileY) <= 8;

    private void SeedFoundingTravellers(DateTime nowUtc)
    {
        if (Player == null) return;
        var arrivals = new List<Data.NpcSaveData>();
        foreach (var decan in DecanRegistry.All.OrderBy(_ => Random.Shared.Next()).Take(FoundingTravellerCount))
        {
            var (tx, ty) = FindFreeTileNear(Player.TileX + Random.Shared.Next(-6, 7), Player.TileY + Random.Shared.Next(-5, 6));
            arrivals.Add(new Data.NpcSaveData
            {
                Id            = $"traveller:{Guid.NewGuid():N}",
                DecanId       = decan.Id,
                Name          = decan.Name,
                TileX         = tx,
                TileY         = ty,
                MemoryThought = "I crossed into this world before anyone else came. I remember a different sky.",
                LastAteUtc    = nowUtc,
                LastSleptUtc  = nowUtc,
                IsForeigner   = true,
                Lineage       = new List<string> { $"crossed-before:{nowUtc:yyyy-MM-dd}" },
            });
        }
        foreach (var a in arrivals) Save?.SaveNpc(a);
        InstantiateForeigners(arrivals);
        GD.Print($"GameRoot: {arrivals.Count} founding travellers crossed into the new world");
    }

    // ── Tribe: the rib, the spouse, progeny ───────────────────────────────

    private void CreateTribe(DateTime nowUtc, DateTime? lastProgenyBirthUtc)
    {
        if (Player == null) return;
        Tribe = new TribeManager(Player, LiveNpcs, nowUtc);
        if (lastProgenyBirthUtc.HasValue) Tribe.LastProgenyBirthUtc = lastProgenyBirthUtc.Value;

        Tribe.OnSpouseCreated += npc => AdoptTribeNpc(npc, Player.TileX, Player.TileY, priority: true);
        Tribe.OnProgenyBorn   += npc =>
        {
            // Children are born beside their mother or father — the spouse — if they still live
            var spouse = LiveNpcs.Find(n => n.NpcId == Player?.SpouseNpcId);
            AdoptTribeNpc(npc, spouse?.TileX ?? Player!.TileX, spouse?.TileY ?? Player!.TileY, priority: false);
            _worldScene?.ShowWorldText($"A child is born to your tribe: {npc.Name}.");
            AinSoph.Audio.Sound.Play("birth");
        };
    }

    /// <summary>Give a newly made NPC a place in the world, a mind in the queue, and a body on the map.</summary>
    private void AdoptTribeNpc(NpcBrain npc, int nearX, int nearY, bool priority)
    {
        var (tx, ty) = FindFreeTileNear(nearX, nearY);
        npc.SetTile(tx, ty);
        npc.OnDeath    += OnNpcDeath;
        npc.OnDecision += OnNpcDecision;
        NpcQueue?.Enqueue(npc, priority);
        _worldScene?.UpsertNpc(ToSaveData(npc));
        SaveAll();
    }

    private void OnRibRequested()
    {
        if (_worldScene == null || Player == null || Tribe == null || !Player.HasRib || Player.HasSpouse) return;

        var screen = new AinSoph.UI.SpouseCreationScreen();
        AddChild(screen);
        _worldScene.InputLocked = true;
        screen.Show(
            (name, description) =>
            {
                _worldScene.InputLocked = false;
                var spouse = Tribe.CreateSpouse(name, description, Llm, DateTime.UtcNow);
                if (spouse == null) return;
                _worldScene.ShowWorldText($"{spouse.Name} stands beside you. Your tribe has begun.");
                AinSoph.Audio.Sound.Play("rib");
                _worldScene.ShowNpcSpeech(spouse.NpcId, "…");
            },
            () => _worldScene.InputLocked = false);
    }

    /// <summary>The nearest dry, unoccupied tile to a point (spiralling outward).</summary>
    private static (int TileX, int TileY) FindFreeTileNear(int x, int y)
    {
        for (int r = 1; r <= 6; r++)
        for (int dx = -r; dx <= r; dx++)
        for (int dy = -r; dy <= r; dy++)
        {
            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
            if (IsPassableTile(x + dx, y + dy) && !IsOccupied(x + dx, y + dy))
                return (x + dx, y + dy);
        }
        return (x + 1, y);
    }

    // ── NPC helpers ───────────────────────────────────────────────────────

    private static Data.NpcSaveData ToSaveData(NpcBrain npc) => new()
    {
        Id            = npc.NpcId,
        DecanId       = npc.Decan.Id.ToString(),
        Name          = npc.Name,
        CellId        = npc.CellId(),
        Lineage       = npc.Lineage.ToList(),
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

    // ── For the self-test ─────────────────────────────────────────────────
    public static int WorldSeed => _instance?._worldSeed ?? 0;
    public void SaveNow() => SaveAll();
    public string SaveDirectory => Save?.SaveDirectory ?? "";

    /// <summary>Press RIB (demo tour).</summary>
    public void UseRib() => OnRibRequested();

    private async void OnPrimitiveUsed(string targetId, int skillType)
    {
        if (_worldScene == null || Interactions == null || Player == null) return;

        var skill = (Skills.SkillType)skillType;
        var npc    = LiveNpcs.Find(n => n.NpcId == targetId);
        var animal = LiveAnimals.Find(a => a.AnimalId == targetId);
        var item   = Items?.All.FirstOrDefault(i => i.Id == targetId);

        // Build the interaction request
        var req = new Skills.InteractionRequest
        {
            ActorId    = Player.Id,
            Primitive  = skill.ToString().ToLower(),
            TargetId   = targetId,
            TargetName = npc?.Name ?? animal?.Name ?? item?.Name ?? targetId.Replace("tile:", ""),
            TargetType = targetId.StartsWith("tile:")                ? Skills.InteractionTarget.Tile
                       : npc != null                                 ? Skills.InteractionTarget.Npc
                       : LiveAnimals.Exists(a => a.AnimalId == targetId) ? Skills.InteractionTarget.Animal
                       : Skills.InteractionTarget.Item,
        };

        // Reaping needs you next to the target; talking needs you within earshot
        int dist = npc != null    ? Math.Max(Math.Abs(npc.TileX - Player.TileX), Math.Abs(npc.TileY - Player.TileY))
                 : animal != null ? Math.Max(Math.Abs(animal.TileX - Player.TileX), Math.Abs(animal.TileY - Player.TileY))
                 : item != null   ? Math.Max(Math.Abs(item.TileX - Player.TileX), Math.Abs(item.TileY - Player.TileY))
                 : 0;
        if ((skill == Skills.SkillType.Reap && dist > 1) || (skill == Skills.SkillType.Talk && dist > 2))
        {
            _worldScene.ShowWorldText(npc != null || animal != null ? $"The {req.TargetName} is too far away." : "It is out of reach.");
            return;
        }

        // Talk → open dialogue; each line the player speaks gets an in-character reply
        if (skill == Skills.SkillType.Talk && npc != null)
        {
            var decanId = int.TryParse(npc.Decan.Id, out var did) ? did : TileRegistryHash(npc.Decan.Id);
            _worldScene.OpenNpcDialogueFull(
                targetId, npc.Name, decanId, TileRegistryHash(targetId),
                npc.BrokenTalk ? "(They cannot speak. They watch you.)" : $"{npc.Name} turns to you.",
                text => TalkToNpc(npc, text));
            return;
        }

        // Reap on a living being → kill resolution (d100 each side, ties to the defender)
        if (skill == Skills.SkillType.Reap && npc != null)
        {
            var kill = KillResolver.Resolve(Player.KillNumber, npc.KillNumber);
            AinSoph.Audio.Sound.Play("reap");
            GD.Print($"GameRoot: reap {npc.Name} — {kill.AttackerRoll}/{kill.AttackerKillNum} " +
                     $"vs {kill.DefenderRoll}/{kill.DefenderKillNum}");
            if (kill.AttackerSucceeds)
            {
                _worldScene.ShowWorldText($"You reap {npc.Name}. The body remains.");
                npc.Kill();
            }
            else
            {
                _worldScene.ShowWorldText($"{npc.Name} resists. Neither of you falls.");
                _worldScene.ShowNpcSpeech(npc.NpcId, "!");
            }
            return;
        }

        // Reap on an animal → the same resolution, against its Reap number
        if (skill == Skills.SkillType.Reap && animal != null)
        {
            var kill = KillResolver.Resolve(Player.KillNumber, animal.KillNumber);
            AinSoph.Audio.Sound.Play("reap");
            if (kill.AttackerSucceeds)
            {
                _worldScene.ShowWorldText(animal.Species?.Edible == true
                    ? $"You reap the {animal.Name}. Its body is clean — reap it again to eat."
                    : $"You reap the {animal.Name}. Its body is unclean.");
                animal.Kill();
            }
            else
            {
                _worldScene.ShowWorldText($"The {animal.Name} escapes you.");
                // A predator that survives turns on you
                if (animal.AnimalType == AnimalType.Predator) OnAnimalAttack(animal, Player.Id);
            }
            return;
        }

        // Pray → only at the altar does it reach the Council
        if (skill == Skills.SkillType.Pray)
        {
            AinSoph.Audio.Sound.Play("pray");
            if (IsAtAltar())
                _worldScene.OpenAltar(petition => OnAltarPetition(petition));
            else
                _worldScene.ShowWorldText("You pray. The world does not move.");
            return;
        }

        // All other primitives → resolve and show world text
        var ateBefore = Player.Survival.LastAteUtc;
        var result = await Interactions.ResolveAsync(req, _cts.Token);
        if (Player != null && Player.Survival.LastAteUtc > ateBefore) AinSoph.Audio.Sound.Play("eat");
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
            AinSoph.Audio.Sound.Play("speech");
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
        AinSoph.Audio.Sound.Play("council");
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
                    AddLaw(sub.Name, sub.Description, Player.Id);
                    _worldScene.ShowWorldText($"The Council accepts the rule: {sub.Name}");
                    SaveAll();
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
