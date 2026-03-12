using Godot;
using System.Collections.Generic;
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

        // ── Child nodes ───────────────────────────────────────────────────
        private Camera2D        _camera;
        private WorldRenderer   _renderer;
        private Node2D          _entityLayer;
        private HUD             _hud;
        private DialogueScreen  _dialogue;
        private PrimitiveMenu   _primitiveMenu;
        private Label           _worldTextLabel;
        private float           _worldTextTimer;

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

            // Set initial player position
            if (Player != null)
            {
                _playerTile = new Vector2I(Player.TileX, Player.TileY);
                _renderer.Refresh(_playerTile);
            }

            // Wire HUD skills
            if (Player != null)
                _hud.SetSkills(new List<SkillType>
                    { SkillType.Move, SkillType.See, SkillType.Hear,
                      SkillType.Talk, SkillType.Reap, SkillType.Pray });
        }

        public override void _Process(double delta)
        {
            HandleMovementInput();

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
            node.Setup(data.Id, data.Name, data.Id.GetHashCode(), isAnimal: false, state);
            node.SetTilePosition(data.TileX, data.TileY);
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
            if (_dialogue.Visible) return;

            var dir = Vector2I.Zero;
            if (Input.IsKeyPressed(Key.D) || Input.IsActionPressed("ui_right")) dir.X =  1;
            else if (Input.IsKeyPressed(Key.A) || Input.IsActionPressed("ui_left"))  dir.X = -1;
            if (Input.IsKeyPressed(Key.S) || Input.IsActionPressed("ui_down")) dir.Y =  1;
            else if (Input.IsKeyPressed(Key.W) || Input.IsActionPressed("ui_up"))   dir.Y = -1;

            if (dir != Vector2I.Zero)
                ApplyPlayerMove(_playerTile + dir);
        }

        public override void _UnhandledInput(InputEvent ev)
        {
            if (_dialogue.Visible) return;

            if (ev is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
            {
                var viewport   = GetViewport().GetVisibleRect().Size;
                var camOffset  = _camera.GlobalPosition - viewport / 2f;
                var worldPos   = mb.Position + camOffset;
                var targetTile = new Vector2I((int)(worldPos.X / 32), (int)(worldPos.Y / 32));

                // Check if click landed on an NPC — NpcWorldNode handles its own click signal,
                // so we only open the tile menu here if no entity was under the cursor.
                // A simple way: open tile menu on right-click, move on left-click.
                // Per design: click opens primitive menu on any target including tiles.
                // We use right-click for tile interaction; left-click still moves.
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    StepToward(targetTile);
                }
            }

            if (ev is InputEventMouseButton rbmb && rbmb.ButtonIndex == MouseButton.Right && rbmb.Pressed)
            {
                var viewport  = GetViewport().GetVisibleRect().Size;
                var camOffset = _camera.GlobalPosition - viewport / 2f;
                var worldPos  = rbmb.Position + camOffset;
                var tileX     = (int)(worldPos.X / 32);
                var tileY     = (int)(worldPos.Y / 32);
                var cellCoord = TileToCell(new Vector2I(tileX, tileY));
                var cell      = Grid?.GetOrGenerate(cellCoord.X, cellCoord.Y);
                var biome     = cell?.Biome.ToString() ?? "ground";
                OnTileClicked(rbmb.Position, biome);
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
            _primitiveMenu?.Close(); // moving dismisses the menu
            _playerTile = newTile;

            if (Player != null)
            {
                Player.TileX = newTile.X;
                Player.TileY = newTile.Y;
            }

            _renderer.Refresh(newTile);
            _camera.Position = new Vector2(newTile.X * 32, newTile.Y * 32);
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
            AddChild(_entityLayer);

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

        // ── Signals ───────────────────────────────────────────────────────
        [Signal] public delegate void PrimitiveUsedEventHandler(string targetId, int skillType);
        [Signal] public delegate void AltarPetitionEventHandler(string petition);
    }
}
