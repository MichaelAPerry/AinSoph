using Godot;
using System.Collections.Generic;
using AinSoph.Skills;

namespace AinSoph.UI
{
    /// <summary>
    /// The persistent HUD layer.
    ///
    /// Layout (CanvasLayer, always on top):
    ///   ┌────────────────────────────────────────────────────┐
    ///   │                  (game world)                       │
    ///   │                                          UTC 14:23 │  ← clock
    ///   │        ⚠ SLEEP IN 1 HOUR ⚠                        │  ← warning (hour 23 only)
    ///   ├───────────────────────────────────────────────────-┤
    ///   │  [Move] [See] [Hear] [Talk] [Reap] [Pray]  ...     │  ← action bar
    ///   └────────────────────────────────────────────────────┘
    ///
    /// Action bar slots expand as the player gains skills via the Council.
    /// Locked slots render dimmed with no icon.
    /// </summary>
    public partial class HUD : CanvasLayer
    {
        // ── Scene nodes ──────────────────────────────────────────────────────
        private Panel        _actionBarPanel;
        private HBoxContainer _slotRow;
        private Label         _clockLabel;
        private Label         _warningLabel;

        // ── State ────────────────────────────────────────────────────────────
        private List<SkillType> _unlockedSkills = new();
        private SkillType?      _selectedSkill  = null;

        // Warning timer (shows warning for 3 seconds, then fades)
        private float _warningTimer = 0f;

        // Colours
        private static readonly Color PanelBg      = new Color(0.08f, 0.08f, 0.08f, 0.95f);
        private static readonly Color SlotBg       = new Color(0.15f, 0.15f, 0.15f, 1f);
        private static readonly Color SlotSelected = new Color(0.35f, 0.35f, 0.18f, 1f);
        private static readonly Color SlotLocked   = new Color(0.10f, 0.10f, 0.10f, 0.5f);
        private static readonly Color ClockColor   = new Color(0.7f, 0.7f, 0.7f, 1f);
        private static readonly Color WarnColor    = new Color(1f, 0.8f, 0.1f, 1f);

        // Skill icon tile indices from TileRegistry
        private static readonly Dictionary<SkillType, int> SkillIcons = new()
        {
            [SkillType.Move]  = 3,
            [SkillType.See]   = 20,
            [SkillType.Hear]  = 107,
            [SkillType.Talk]  = 95,
            [SkillType.Reap]  = 92,
            [SkillType.Pray]  = 94,
        };

        public override void _Ready()
        {
            Layer = 10;
            BuildLayout();
        }

        public override void _Process(double delta)
        {
            UpdateClock();

            if (_warningTimer > 0f)
            {
                _warningTimer -= (float)delta;
                if (_warningTimer <= 0f)
                    _warningLabel.Visible = false;
            }
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Called by GameRoot when player skills change.</summary>
        public void SetSkills(List<SkillType> unlocked)
        {
            _unlockedSkills = new List<SkillType>(unlocked);
            RebuildSlots();
        }

        /// <summary>Flash a warning in the centre of the screen for 4 seconds.</summary>
        public void ShowWarning(string text)
        {
            _warningLabel.Text    = text;
            _warningLabel.Visible = true;
            _warningTimer         = 4f;
        }

        /// <summary>Returns the currently selected skill (if any).</summary>
        public SkillType? SelectedSkill => _selectedSkill;

        // ── Layout builders ──────────────────────────────────────────────────

        private void BuildLayout()
        {
            var viewport = GetViewport().GetVisibleRect();
            float w = viewport.Size.X;
            float h = viewport.Size.Y;
            float barH = 56f;

            // ── Action bar panel ──
            _actionBarPanel = new Panel();
            _actionBarPanel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
            _actionBarPanel.SetDeferred("position", new Vector2(0, h - barH));
            _actionBarPanel.Size = new Vector2(w, barH);
            _actionBarPanel.AddThemeStyleboxOverride("panel", MakeFlatStyle(PanelBg));
            AddChild(_actionBarPanel);

            _slotRow = new HBoxContainer();
            _slotRow.Position = new Vector2(8, 6);
            _slotRow.Size     = new Vector2(w - 16, barH - 12);
            _slotRow.AddThemeConstantOverride("separation", 6);
            _actionBarPanel.AddChild(_slotRow);

            // ── UTC Clock (bottom-right, above bar) ──
            _clockLabel = new Label();
            _clockLabel.AddThemeColorOverride("font_color", ClockColor);
            _clockLabel.AddThemeFontSizeOverride("font_size", 14);
            _clockLabel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
            _clockLabel.Position = new Vector2(w - 110, h - barH - 24);
            AddChild(_clockLabel);

            // ── Warning label (centre screen) ──
            _warningLabel = new Label();
            _warningLabel.AddThemeColorOverride("font_color", WarnColor);
            _warningLabel.AddThemeFontSizeOverride("font_size", 20);
            _warningLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _warningLabel.SetAnchorsPreset(Control.LayoutPreset.Center);
            _warningLabel.Position = new Vector2(w / 2 - 200, h / 2 - 60);
            _warningLabel.Size     = new Vector2(400, 40);
            _warningLabel.Visible  = false;
            AddChild(_warningLabel);

            // ── ROUTES button — far right of action bar ──
            var routesBtn = new Button();
            routesBtn.Text     = "ROUTES";
            routesBtn.Size     = new Vector2(72, 38);
            routesBtn.Position = new Vector2(w - 84, (barH - 38) / 2f);
            routesBtn.AddThemeStyleboxOverride("normal",  MakeFlatStyle(new Color(0.10f, 0.10f, 0.08f)));
            routesBtn.AddThemeStyleboxOverride("hover",   MakeFlatStyle(new Color(0.20f, 0.19f, 0.14f)));
            routesBtn.AddThemeStyleboxOverride("pressed", MakeFlatStyle(new Color(0.16f, 0.15f, 0.11f)));
            routesBtn.AddThemeFontSizeOverride("font_size", 10);
            routesBtn.AddThemeColorOverride("font_color", new Color(0.6f, 0.58f, 0.48f));
            routesBtn.Pressed += () => EmitSignal(SignalName.RoutesOpenRequested);
            _actionBarPanel.AddChild(routesBtn);

            // Build initial slots with the 6 primitives
            var primitives = new List<SkillType>
                { SkillType.Move, SkillType.See, SkillType.Hear, SkillType.Talk, SkillType.Reap, SkillType.Pray };
            SetSkills(primitives);
        }

        private void RebuildSlots()
        {
            foreach (Node child in _slotRow.GetChildren())
                child.QueueFree();

            // All 6 primitives always visible; additional Council skills append
            var allSkills = new List<SkillType>
                { SkillType.Move, SkillType.See, SkillType.Hear, SkillType.Talk, SkillType.Reap, SkillType.Pray };

            foreach (var skill in _unlockedSkills)
                if (!allSkills.Contains(skill))
                    allSkills.Add(skill);

            foreach (var skill in allSkills)
            {
                bool unlocked = _unlockedSkills.Contains(skill);
                var slot = BuildSlot(skill, unlocked);
                _slotRow.AddChild(slot);
            }
        }

        private Control BuildSlot(SkillType skill, bool unlocked)
        {
            var container = new PanelContainer();
            container.CustomMinimumSize = new Vector2(44, 44);
            container.AddThemeStyleboxOverride("panel",
                MakeFlatStyle(skill == _selectedSkill ? SlotSelected : (unlocked ? SlotBg : SlotLocked)));

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 2);
            container.AddChild(vbox);

            // Icon tile
            if (unlocked && SkillIcons.TryGetValue(skill, out int tileIdx))
            {
                var icon = new TextureRect();
                icon.Texture            = GD.Load<Texture2D>(TileRegistry.TilePath(tileIdx));
                icon.ExpandMode         = TextureRect.ExpandModeEnum.IgnoreSize;
                icon.StretchMode        = TextureRect.StretchModeEnum.KeepAspectCentered;
                icon.CustomMinimumSize  = new Vector2(24, 24);
                icon.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
                vbox.AddChild(icon);
            }

            // Skill label
            var label = new Label();
            label.Text = skill.ToString().ToUpper();
            label.AddThemeFontSizeOverride("font_size", 9);
            label.AddThemeColorOverride("font_color",
                unlocked ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.4f, 0.4f, 0.4f));
            label.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(label);

            // Click to select
            if (unlocked)
            {
                container.GuiInput += (ev) =>
                {
                    if (ev is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
                    {
                        _selectedSkill = skill;
                        RebuildSlots();
                        EmitSignal(SignalName.SkillSelected, (int)skill);
                    }
                };
            }

            return container;
        }

        private void UpdateClock()
        {
            var now = System.DateTime.UtcNow;
            _clockLabel.Text = $"UTC {now:HH:mm}";
        }

        private static StyleBoxFlat MakeFlatStyle(Color bg)
        {
            var s = new StyleBoxFlat();
            s.BgColor = bg;
            s.SetBorderWidthAll(1);
            s.BorderColor = new Color(0.3f, 0.3f, 0.3f);
            s.SetCornerRadiusAll(3);
            return s;
        }

        // ── Signals ──────────────────────────────────────────────────────────
        [Signal] public delegate void SkillSelectedEventHandler(int skillType);
        [Signal] public delegate void RoutesOpenRequestedEventHandler();
    }
}
