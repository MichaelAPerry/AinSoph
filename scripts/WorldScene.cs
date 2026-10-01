using Godot;
using System.Collections.Generic;
using System.Linq;
using AinSoph.Data;
using AinSoph.NPC;
using AinSoph.Player;
using AinSoph.Skills;
using AinSoph.UI;
using AinSoph.World;

namespace AinSoph
{
    /// <summary>
    /// Root scene controller. Owns all visual children.
    /// Receives system references from GameRoot and wires signals.
    ///
    /// Scene tree (built at runtime in _Ready):
    ///   WorldScene
    ///   ├── Camera2D
    ///   ├── WorldRenderer     (Node2D)
    ///   ├── EntityLayer       (Node2D) — NPC/player/item sprites
    ///   ├── HUD               (CanvasLayer, layer=10)
    ///   └── DialogueScreen    (CanvasLayer, layer=20)
    /// </summary>
    public partial class WorldScene : Node2D
    {
        // ── System references (injected by GameRoot) ──────────────────────
        public WorldGrid        Grid        { get; set; }
        public WorldClock       Clock       { get; set; }
        public PlayerCharacter  Player      { get; set; }
        public SaveManager      SaveMgr     { get; set; }
        public string           AltarCellId { get; set; }
        public Vector2I         AltarTile   { get; set; }
        public WorldItemRegistry? Items     { get; set; }

        /// <summary>True while a full-screen overlay (e.g. character creation) owns the keyboard.</summary>
        public bool             InputLocked { get; set; }

        // ── Child nodes ───────────────────────────────────────────────────
        private Camera2D        _camera;
        private WorldRenderer   _renderer;
        private Node2D          _entityLayer;
        private HUD             _hud;
        private DialogueScreen  _dialogue;
        private PrimitiveMenu   _primitiveMenu;
        private RoutesScreen    _routesScreen;
        private Label           _worldTextLabel;
        private float           _worldTextTimer;
        private Sprite2D        _playerSprite;
        private Label           _playerLabel;
        private float           _moveCooldown;

        private const float MoveRepeatSeconds = 0.14f;

        // ── NPC tracking ──────────────────────────────────────────────────
        private readonly Dictionary<string, NpcWorldNode> _npcNodes = new();

        // ── Player tile position ──────────────────────────────────────────
        private Vector2I _playerTile = Vector2I.Zero;

        public override void _Ready()
        {
            BuildSceneTree();
        }

        /// <summary>Called by GameRoot after all systems are ready.</summary>
        public void InitSystems()
        {
            _renderer.Grid        = Grid;
            _renderer.Clock       = Clock;
            _renderer.AltarCellId = AltarCellId;
            _renderer.AltarTile   = AltarTile;
            _renderer.Items       = Items;

            // Set initial player position
            if (Player != null)
                MovePlayerTo(new Vector2I(Player.TileX, Player.TileY));

            // Wire HUD skills
            if (Player != null)
                _hud.SetSkills(new List<SkillType>
                    { SkillType.Move, SkillType.See, SkillType.Hear,
                      SkillType.Talk, SkillType.Reap, SkillType.Pray });
        }

        public override void _Process(double delta)
        {
            _moveCooldown -= (float)delta;
            HandleMovementInput();

            if (Player != null && _playerLabel.Text != Player.Name)
                _playerLabel.Text = Player.Name;

            if (_worldTextTimer > 0f)
            {
                _worldTextTimer -= (float)delta;
                float a = Mathf.Clamp(_worldTextTimer / 1.2f, 0f, 1f);
                _worldTextLabel.Modulate = new Color(1, 1, 1, a);
                if (_worldTextTimer <= 0f)
                    _worldTextLabel.Visible = false;
            }
        }

        // ── Entity management ─────────────────────────────────────────────

        /// <summary>Spawn or update an NPC's on-map node.</summary>
        public void UpsertNpc(NpcSaveData data)
        {
            if (!_npcNodes.TryGetValue(data.Id, out var node))
            {
                node = new NpcWorldNode();
                node.EntityClicked += OnEntityClicked;
                _entityLayer.AddChild(node);
                _npcNodes[data.Id] = node;
            }

            var state = System.Enum.TryParse<NpcState>(data.State, true, out var s) ? s : NpcState.Idle;
            node.Setup(data.Id, data.Name, TileRegistry.StableHash(data.Id), isAnimal: false, state);
            node.SetTilePosition(data.TileX, data.TileY);
        }

        /// <summary>Move an NPC's on-map node to a new tile.</summary>
        public void MoveNpc(string npcId, int tileX, int tileY)
        {
            if (_npcNodes.TryGetValue(npcId, out var node))
                node.SetTilePosition(tileX, tileY);
        }

        /// <summary>Float a line of speech above an NPC.</summary>
        public void ShowNpcSpeech(string npcId, string text)
        {
            if (_npcNodes.TryGetValue(npcId, out var node))
                node.ShowSpeech(text);
        }

        /// <summary>Redraw the map around the player (e.g. after manna spawns or an item is eaten).</summary>
        public void RefreshMap() => _renderer.Refresh(_playerTile);

        public Vector2I PlayerTile => _playerTile;

        private NpcWorldNode? NpcAt(Vector2I tile)
        {
            foreach (var node in _npcNodes.Values)
                if (node.Tile == tile) return node;
            return null;
        }

        /// <summary>Remove an NPC from the map.</summary>
        public void RemoveNpc(string npcId)
        {
            if (_npcNodes.TryGetValue(npcId, out var node))
            {
                node.QueueFree();
                _npcNodes.Remove(npcId);
            }
        }

        /// <summary>Update a single NPC's state icon.</summary>
        public void SetNpcState(string npcId, NpcState state)
        {
            if (_npcNodes.TryGetValue(npcId, out var node))
                node.SetState(state);
        }

        // ── Input handling ────────────────────────────────────────────────

        private void HandleMovementInput()
        {
            if (_dialogue.Visible || InputLocked || _moveCooldown > 0f) return;

            var dir = Vector2I.Zero;
            if (Input.IsKeyPressed(Key.D) || Input.IsActionPressed("ui_right")) dir.X =  1;
            else if (Input.IsKeyPressed(Key.A) || Input.IsActionPressed("ui_left"))  dir.X = -1;
            if (Input.IsKeyPressed(Key.S) || Input.IsActionPressed("ui_down")) dir.Y =  1;
            else if (Input.IsKeyPressed(Key.W) || Input.IsActionPressed("ui_up"))   dir.Y = -1;

            if (dir != Vector2I.Zero)
            {
                _moveCooldown = MoveRepeatSeconds;
                ApplyPlayerMove(_playerTile + dir);
            }
        }

        public override void _UnhandledInput(InputEvent ev)
        {
            if (_dialogue.Visible || InputLocked) return;
            if (ev is not InputEventMouseButton mb || !mb.Pressed) return;

            var worldPos   = GetGlobalMousePosition();
            var targetTile = new Vector2I(Mathf.FloorToInt(worldPos.X / 32f), Mathf.FloorToInt(worldPos.Y / 32f));
            var npc        = NpcAt(targetTile);

            if (mb.ButtonIndex == MouseButton.Left)
            {
                // Left-click an NPC opens the primitive menu on them; anywhere else walks
                if (npc != null) OnEntityClicked(npc.NpcId);
                else             StepToward(targetTile);
            }
            else if (mb.ButtonIndex == MouseButton.Right)
            {
                // Right-click: NPC, then item on the tile, then the tile itself
                if (npc != null) { OnEntityClicked(npc.NpcId); return; }

                var item = Items?.NearTile(targetTile.X, targetTile.Y, radius: 0).FirstOrDefault();
                if (item != null)
                {
                    _primitiveMenu.Open(item.Id, item.Name, mb.Position);
                    return;
                }

                var cellCoord = TileToCell(targetTile);
                var cell      = Grid?.GetOrGenerate(cellCoord.X, cellCoord.Y);
                var biome     = cell?.Biome.ToString() ?? "ground";
                OnTileClicked(mb.Position, biome);
            }
        }

        private void StepToward(Vector2I target)
        {
            var diff = target - _playerTile;
            if (diff == Vector2I.Zero) return;
            var step = new Vector2I(
                diff.X == 0 ? 0 : (diff.X > 0 ? 1 : -1),
                diff.Y == 0 ? 0 : (diff.Y > 0 ? 1 : -1)
            );
            ApplyPlayerMove(_playerTile + (step.X != 0 ? new Vector2I(step.X, 0) : new Vector2I(0, step.Y)));
        }

        private void ApplyPlayerMove(Vector2I newTile)
        {
            _primitiveMenu?.Close();

            // The sea is impassable on foot
            var destCellCoord = TileToCell(newTile);
            var destCell      = Grid?.GetOrGenerate(destCellCoord.X, destCellCoord.Y);
            if (destCell != null && !BiomeData.Get(destCell.Biome).Passable)
            {
                ShowWorldText("The sea will not carry you.");
                return;
            }

            // Cave exit — if leaving a cave tile
            if (Player != null && WasOnCaveTile())
            {
                GameRoot.ReleaseCave(_playerTile.X, _playerTile.Y, Player.Id);
                Player.Survival.ExitCave();
            }

            MovePlayerTo(newTile);

            // Cave entry — if landing on a cave tile
            if (Player != null && TileAt(_playerTile)?.HasCave == true)
            {
                bool claimed = GameRoot.TryClaimCave(_playerTile.X, _playerTile.Y, Player.Id);
                if (claimed)
                {
                    Player.Survival.EnterCave();
                    ShowWorldText("You enter the cave. It is yours while you stay.");
                }
                else
                {
                    ShowWorldText("Someone already shelters in this cave.");
                }
            }
        }

        // ── Entity / tile click → primitive menu ─────────────────────────

        private void OnEntityClicked(string entityId)
        {
            if (!_npcNodes.TryGetValue(entityId, out var node)) return;

            var npcName  = node.NpcName;
            var viewport = GetViewport().GetVisibleRect().Size;
            var camOffset = _camera.GlobalPosition - viewport / 2f;
            var screenPos = node.GlobalPosition + new Vector2(16, 0) - camOffset;

            _primitiveMenu.Open(entityId, npcName, screenPos);
        }

        private void OnTileClicked(Vector2 screenPos, string tileDesc)
        {
            _primitiveMenu.Open("tile:" + tileDesc, tileDesc, screenPos);
        }

        private void OnPrimitiveChosen(string targetId, SkillType skill)
        {
            EmitSignal(SignalName.PrimitiveUsed, targetId, (int)skill);
        }

        /// <summary>Called by GameRoot with the actual NPC data + LLM response.</summary>
        public void OpenNpcDialogueFull(string npcId, string name, int decanId, int seed,
                                        string openingLine, System.Action<string> onSpeak)
        {
            _dialogue.OpenNPC(npcId, name, decanId, seed, openingLine, onSpeak);
        }

        /// <summary>Update the HUD skill bar — call after Council grants a new skill.</summary>
        public void RefreshHudSkills(System.Collections.Generic.List<SkillType> skills)
            => _hud.SetSkills(skills);

        /// <summary>Update the speech box with a new LLM reply.</summary>
        public void SetDialogueSpeech(string text) => _dialogue.SetSpeech(text);

        // ── Scripted control (demo tour) ──────────────────────────────────

        public void Step(Vector2I dir) => ApplyPlayerMove(_playerTile + dir);
        public bool DialogueOpen => _dialogue.Visible;
        public string DialogueSpeech => _dialogue.SpeechText;
        public void TypeDialogue(string text)   => _dialogue.SetInputText(text);
        public void SubmitDialogue(string text) => _dialogue.SubmitText(text);
        public void CloseDialogue()             => _dialogue.Close();
        public void OpenMenuOn(string npcId)    => OnEntityClicked(npcId);
        public void CloseMenu()                 => _primitiveMenu.Close();

        /// <summary>Open the altar prayer screen.</summary>
        public void OpenAltar(System.Action<string> onSubmit) => _dialogue.OpenAltar(onSubmit);

        /// <summary>Show a brief environmental/oblique response line above the action bar.</summary>
        public void ShowWorldText(string text)
        {
            _worldTextLabel.Text    = text;
            _worldTextLabel.Visible = true;
            _worldTextTimer         = 3.5f;
            _worldTextLabel.Modulate = new Color(1, 1, 1, 1);
        }

        /// <summary>Reposition the player sprite and camera after death/respawn.</summary>
        public void MovePlayerTo(Vector2I tile)
        {
            _playerTile = tile;

            if (Player != null)
            {
                Player.TileX = tile.X;
                Player.TileY = tile.Y;
                var cell = TileToCell(tile);
                Player.CellId = $"{cell.X},{cell.Y}";
            }

            _playerSprite.Position = new Vector2(tile.X * 32 + 16, tile.Y * 32 + 16);
            _playerLabel.Position  = new Vector2(tile.X * 32 - 34, tile.Y * 32 + 32);
            _renderer.Refresh(tile); // also centres the camera
        }

        // ── Survival events ───────────────────────────────────────────────

        private void OnPlayerDied() => _hud.ShowWarning("YOU HAVE DIED");

        // ── Scene tree builder ────────────────────────────────────────────

        private void BuildSceneTree()
        {
            _camera = new Camera2D();
            _camera.Name = "Camera2D";
            AddChild(_camera);

            _renderer = new WorldRenderer();
            _renderer.Name = "WorldRenderer";
            AddChild(_renderer);

            _entityLayer = new Node2D();
            _entityLayer.Name = "EntityLayer";
            _entityLayer.ZIndex = 5;
            AddChild(_entityLayer);

            // The player — drawn above NPCs, with a warm name label
            _playerSprite = new Sprite2D();
            _playerSprite.Name     = "Player";
            _playerSprite.Texture  = GD.Load<Texture2D>(TileRegistry.TilePath(TileRegistry.PlayerTile));
            _playerSprite.Scale    = Vector2.One * 4f;
            _playerSprite.Material = TileRegistry.CutoutMaterial;
            _playerSprite.ZIndex   = 6;
            AddChild(_playerSprite);

            _playerLabel = new Label();
            _playerLabel.Size     = new Vector2(100, 14);
            _playerLabel.ZIndex   = 6;
            _playerLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _playerLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.4f));
            _playerLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));
            _playerLabel.AddThemeConstantOverride("outline_size", 4);
            _playerLabel.AddThemeFontSizeOverride("font_size", 10);
            AddChild(_playerLabel);

            _hud = new HUD();
            _hud.Name = "HUD";
            _hud.SkillSelected += OnSkillSelected;
            AddChild(_hud);

            _dialogue = new DialogueScreen();
            _dialogue.Name = "DialogueScreen";
            AddChild(_dialogue);

            _primitiveMenu = new PrimitiveMenu();
            _primitiveMenu.Name = "PrimitiveMenu";
            _primitiveMenu.OnPrimitiveChosen += OnPrimitiveChosen;
            AddChild(_primitiveMenu);

            _routesScreen = new RoutesScreen();
            _routesScreen.Name = "RoutesScreen";
            _routesScreen.OnClose += () => { /* nothing special needed */ };
            AddChild(_routesScreen);

            _hud.Connect(HUD.SignalName.RoutesOpenRequested,
                Callable.From(() => _routesScreen.Open()));

            _hud.Connect(HUD.SignalName.SleepRequested,
                Callable.From(OnSleepRequested));

            // World text — oblique/environmental responses, fades out above action bar
            var hudLayer = new CanvasLayer();
            hudLayer.Layer = 9; // just below HUD
            AddChild(hudLayer);

            _worldTextLabel = new Label();
            _worldTextLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.68f));
            _worldTextLabel.AddThemeFontSizeOverride("font_size", 14);
            _worldTextLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _worldTextLabel.Visible = false;
            // Positioned just above the action bar — set after viewport is known
            CallDeferred(MethodName.PositionWorldText);
            hudLayer.AddChild(_worldTextLabel);
        }

        private void OnSkillSelected(int skillType)
        {
            if ((SkillType)skillType == SkillType.Pray && Grid != null)
            {
                var cell = TileToCell(_playerTile);
                var cellId = $"{cell.X},{cell.Y}";
                if (cellId == AltarCellId)
                    OpenAltar((petition) => EmitSignal(SignalName.AltarPetition, petition));
            }
        }

        private void PositionWorldText()
        {
            var vp = GetViewport().GetVisibleRect().Size;
            _worldTextLabel.Size     = new Vector2(vp.X - 80, 28);
            _worldTextLabel.Position = new Vector2(40, vp.Y - 100);
        }

        private Vector2I TileToCell(Vector2I tile) =>
            new Vector2I(
                tile.X < 0 ? (tile.X - 7) / 8 : tile.X / 8,
                tile.Y < 0 ? (tile.Y - 7) / 8 : tile.Y / 8
            );

        private bool WasOnCaveTile() => TileAt(_playerTile)?.HasCave == true;

        /// <summary>The world tile at a global tile position — handles negative coordinates.</summary>
        private Tile? TileAt(Vector2I tile)
        {
            if (Grid == null) return null;
            var c    = TileToCell(tile);
            var cell = Grid.GetOrGenerate(c.X, c.Y);
            return cell.GetTile(tile.X - c.X * 8, tile.Y - c.Y * 8);
        }

        private void OnSleepRequested()
        {
            if (Player == null) return;
            var now = System.DateTime.UtcNow;
            if (Player.Survival.IsSleeping)
            {
                Player.Survival.EndSleep(now);
                ShowWorldText("You awaken.");
            }
            else
            {
                bool inCave = WasOnCaveTile() && Player.Survival.IsInCave;
                Player.Survival.BeginSleep(now, inCave);
                ShowWorldText(inCave ? "You sleep in the shelter of the cave." : "You sleep under the open sky.");
            }
        }

        // ── Signals ───────────────────────────────────────────────────────
        [Signal] public delegate void PrimitiveUsedEventHandler(string targetId, int skillType);
        [Signal] public delegate void AltarPetitionEventHandler(string petition);
    }
}
