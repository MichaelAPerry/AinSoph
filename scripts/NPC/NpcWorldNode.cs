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
        public Vector2I Tile       { get; private set; }

        // ── Nodes ─────────────────────────────────────────────────────────
        private Sprite2D    _bodySprite;
        private Node2D      _stateIconRoot;
        private Sprite2D    _stateIconSprite;
        private Label       _nameLabel;
        private Area2D      _clickArea;
        private Label       _speechLabel;
        private float       _speechTimer;

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
            _bodySprite.Material = TileRegistry.CutoutMaterial;
            AddChild(_bodySprite);

            // State icon (floats above body)
            _stateIconRoot = new Node2D();
            _stateIconRoot.Position = new Vector2(TileSize / 2f, StateIconY);
            AddChild(_stateIconRoot);

            _stateIconSprite          = new Sprite2D();
            _stateIconSprite.Centered = true;
            _stateIconSprite.Scale    = Vector2.One * StateIconScale;
            _stateIconSprite.Material = TileRegistry.CutoutMaterial;
            _stateIconRoot.AddChild(_stateIconSprite);

            // Name label under the body
            _nameLabel = new Label();
            _nameLabel.Position = new Vector2(-34, TileSize);
            _nameLabel.Size     = new Vector2(TileSize + 68, 14);
            _nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _nameLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.92f, 0.85f));
            _nameLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));
            _nameLabel.AddThemeConstantOverride("outline_size", 4);
            _nameLabel.AddThemeFontSizeOverride("font_size", 10);
            AddChild(_nameLabel);

            // Speech line floats above the state icon for a few seconds
            _speechLabel = new Label();
            _speechLabel.Position = new Vector2(-104, -46);
            _speechLabel.Size     = new Vector2(TileSize + 208, 28);
            _speechLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _speechLabel.VerticalAlignment   = VerticalAlignment.Bottom;
            _speechLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _speechLabel.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.75f));
            _speechLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));
            _speechLabel.AddThemeConstantOverride("outline_size", 4);
            _speechLabel.AddThemeFontSizeOverride("font_size", 11);
            _speechLabel.Visible = false;
            _speechLabel.ZIndex  = 10;
            AddChild(_speechLabel);

            ZIndex = 5;

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
            Tile     = new Vector2I(tileX, tileY);
            Position = new Vector2(tileX * TileSize, tileY * TileSize);
        }

        /// <summary>Show a line of speech above the NPC for a few seconds.</summary>
        public void ShowSpeech(string text, float seconds = 5f)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _speechLabel.Text    = text.Length > 90 ? text[..90] + "…" : text;
            _speechLabel.Visible = true;
            _speechTimer         = seconds;
        }

        public override void _Process(double delta)
        {
            if (_speechTimer <= 0f) return;
            _speechTimer -= (float)delta;
            if (_speechTimer <= 0f) _speechLabel.Visible = false;
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

        private static int PickNpcTile(int seed) =>
            TileRegistry.NpcTiles[System.Math.Abs(seed) % TileRegistry.NpcTiles.Length];

        private static int PickAnimalTile(int seed) =>
            TileRegistry.AnimalTiles[System.Math.Abs(seed) % TileRegistry.AnimalTiles.Length];

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
