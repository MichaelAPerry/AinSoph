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
                      SkillType.Talk, SkillType.Kill, SkillType.Pray });
        }

        public override void _Process(double delta)
        {
            HandleMovementInput();
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
                var screenCenter = GetViewport().GetVisibleRect().Size / 2;
                var worldPos     = _camera.GlobalPosition + mb.Position - screenCenter;
                var targetTile   = new Vector2I((int)(worldPos.X / 32), (int)(worldPos.Y / 32));
                StepToward(targetTile);
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
            _playerTile = newTile;

            if (Player != null)
            {
                Player.TileX = newTile.X;
                Player.TileY = newTile.Y;
            }

            _renderer.Refresh(newTile);
            _camera.Position = new Vector2(newTile.X * 32, newTile.Y * 32);
        }

        // ── Entity click → interaction ────────────────────────────────────

        private void OnEntityClicked(string entityId)
        {
            var skill = _hud.SelectedSkill;
            if (skill == SkillType.Talk)
                EmitSignal(SignalName.PlayerSpoke, entityId, "[open]");
        }

        /// <summary>Called by GameRoot with the actual NPC data + LLM response.</summary>
        public void OpenNpcDialogueFull(string npcId, string name, int decanId, int seed,
                                        string openingLine, System.Action<string> onSpeak)
        {
            _dialogue.OpenNPC(npcId, name, decanId, seed, openingLine, onSpeak);
        }

        /// <summary>Update the speech box with a new LLM reply.</summary>
        public void SetDialogueSpeech(string text) => _dialogue.SetSpeech(text);

        /// <summary>Open the altar prayer screen.</summary>
        public void OpenAltar(System.Action<string> onSubmit) => _dialogue.OpenAltar(onSubmit);

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

        private Vector2I TileToCell(Vector2I tile) =>
            new Vector2I(
                tile.X < 0 ? (tile.X - 7) / 8 : tile.X / 8,
                tile.Y < 0 ? (tile.Y - 7) / 8 : tile.Y / 8
            );

        // ── Signals ───────────────────────────────────────────────────────
        [Signal] public delegate void PlayerSpokeEventHandler(string npcId, string text);
        [Signal] public delegate void AltarPetitionEventHandler(string petition);
    }
}
