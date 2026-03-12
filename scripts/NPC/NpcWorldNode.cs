using Godot;
using AinSoph.NPC;

namespace AinSoph.UI
{
    /// <summary>
    /// The on-map visual for an NPC (or animal) in the world.
    ///
    /// Structure:
    ///   NpcWorldNode (Node2D)
    ///   ├── Sprite2D       — body tile (small 32×32 character dot on the map)
    ///   ├── StateIcon      — floating tile shown above the entity
    ///   │     └── Sprite2D — state tile (e.g. a blue comm tile when Talking)
    ///   └── NameLabel      — shows name on hover/nearby (hidden by default)
    ///
    /// The body tile is a simple Kenney character sprite at 32×32.
    /// The state icon floats 12px above the body tile, scaled at 2×.
    ///
    /// Clicking this node triggers interaction (handled by WorldScene → InteractionResolver).
    /// </summary>
    public partial class NpcWorldNode : Node2D
    {
        // ── Data ──────────────────────────────────────────────────────────
        public string   NpcId      { get; private set; }
        public string   NpcName    { get; private set; } = string.Empty;
        public NpcState State      { get; private set; } = NpcState.Idle;
        public bool     IsAnimal   { get; private set; }

        // ── Nodes ─────────────────────────────────────────────────────────
        private Sprite2D    _bodySprite;
        private Node2D      _stateIconRoot;
        private Sprite2D    _stateIconSprite;
        private Label       _nameLabel;
        private Area2D      _clickArea;

        // ── Constants ─────────────────────────────────────────────────────
        private const int TileSize      = 32;
        private const int StateIconScale = 2;
        private const float StateIconY  = -14f;  // pixels above the tile centre

        public override void _Ready()
        {
            // Body sprite
            _bodySprite          = new Sprite2D();
            _bodySprite.Centered = true;
            _bodySprite.Position = new Vector2(TileSize / 2f, TileSize / 2f);
            AddChild(_bodySprite);

            // State icon (floats above body)
            _stateIconRoot = new Node2D();
            _stateIconRoot.Position = new Vector2(TileSize / 2f, StateIconY);
            AddChild(_stateIconRoot);

            _stateIconSprite          = new Sprite2D();
            _stateIconSprite.Centered = true;
            _stateIconSprite.Scale    = Vector2.One * StateIconScale;
            _stateIconRoot.AddChild(_stateIconSprite);

            // Name label (hidden until near/clicked)
            _nameLabel = new Label();
            _nameLabel.Position = new Vector2(-20, -28);
            _nameLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.9f, 0.8f));
            _nameLabel.AddThemeFontSizeOverride("font_size", 10);
            _nameLabel.Visible = false;
            AddChild(_nameLabel);

            // Clickable area
            _clickArea = new Area2D();
            var col    = new CollisionShape2D();
            var shape  = new RectangleShape2D();
            shape.Size = new Vector2(TileSize, TileSize);
            col.Shape  = shape;
            col.Position = new Vector2(TileSize / 2f, TileSize / 2f);
            _clickArea.AddChild(col);
            _clickArea.InputEvent += OnAreaInput;
            AddChild(_clickArea);
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>Initialise this node for a given NPC or animal.</summary>
        public void Setup(string npcId, string name, int seed, bool isAnimal, NpcState initialState)
        {
            NpcId    = npcId;
            NpcName  = name;
            IsAnimal = isAnimal;
            _nameLabel.Text = name;

            SetBodyTile(seed, isAnimal);
            SetState(initialState);
        }

        /// <summary>Update the floating state icon when the NPC's state changes.</summary>
        public void SetState(NpcState state)
        {
            State = state;

            int tileIdx = TileRegistry.StateIconFor(state);
            if (ResourceLoader.Exists(TileRegistry.TilePath(tileIdx)))
                _stateIconSprite.Texture = GD.Load<Texture2D>(TileRegistry.TilePath(tileIdx));

            // Hide state icon when idle to reduce visual noise
            _stateIconRoot.Visible = state != NpcState.Idle;
        }

        /// <summary>Move the node to a tile position in world space.</summary>
        public void SetTilePosition(int tileX, int tileY)
        {
            Position = new Vector2(tileX * TileSize, tileY * TileSize);
        }

        public void ShowName(bool show) => _nameLabel.Visible = show;

        // ── Body tile selection ───────────────────────────────────────────

        private void SetBodyTile(int seed, bool isAnimal)
        {
            // Animals use earthy/natural tile tones; NPCs use character-coloured tiles
            int tileIdx = isAnimal
                ? PickAnimalTile(seed)
                : PickNpcTile(seed);

            if (ResourceLoader.Exists(TileRegistry.TilePath(tileIdx)))
            {
                _bodySprite.Texture = GD.Load<Texture2D>(TileRegistry.TilePath(tileIdx));
                _bodySprite.Scale   = Vector2.One * (TileSize / 8f);  // source tiles 8px → 32px
            }
        }

        private static int PickNpcTile(int seed)
        {
            // Draw from a curated set of human-shaped silhouette tiles
            int[] npcTiles = { 88, 89, 113, 114, 115, 136 };
            return npcTiles[System.Math.Abs(seed) % npcTiles.Length];
        }

        private static int PickAnimalTile(int seed)
        {
            int[] animalTiles = { 20, 24, 26, 84, 85, 7, 11 };
            return animalTiles[System.Math.Abs(seed) % animalTiles.Length];
        }

        // ── Input ─────────────────────────────────────────────────────────

        private void OnAreaInput(Node viewport, InputEvent ev, long shapeIdx)
        {
            if (ev is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
            {
                EmitSignal(SignalName.EntityClicked, NpcId);
            }
        }

        // ── Signals ───────────────────────────────────────────────────────
        [Signal] public delegate void EntityClickedEventHandler(string npcId);
    }
}
