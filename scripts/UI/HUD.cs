using Godot;
using System.Linq;
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
        private Button        _sleepBtn;
        private Button        _ribBtn;
        private Label         _statusLabel;

        // ── State ────────────────────────────────────────────────────────────
        private List<SkillType> _unlockedSkills = new();
        private SkillType?      _selectedSkill  = null;
        private bool            _isSleeping     = false;

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
            [SkillType.Move]  = TileRegistry.SkillIcon(SkillType.Move),
            [SkillType.See]   = TileRegistry.SkillIcon(SkillType.See),
            [SkillType.Hear]  = TileRegistry.SkillIcon(SkillType.Hear),
            [SkillType.Talk]  = TileRegistry.SkillIcon(SkillType.Talk),
            [SkillType.Reap]  = TileRegistry.SkillIcon(SkillType.Reap),
            [SkillType.Pray]  = TileRegistry.SkillIcon(SkillType.Pray),
        };

        public override void _Ready()
        {
            Layer = 10;
            BuildLayout();
        }

        public override void _Process(double delta)
        {
            UpdateClock();
            UpdateSleepButton();

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

        /// <summary>The Council's gifts, shown after the six primitives.</summary>
        public void SetGifts(IReadOnlyList<Skills.Gift> gifts)
        {
            _gifts = gifts.ToList();
            RebuildSlots();
        }

        private List<Skills.Gift> _gifts = new();
        private const int MaxGiftChips = 4;

        /// <summary>Tell the HUD whether the player is currently sleeping.</summary>
        public void SetSleeping(bool sleeping)
        {
            _isSleeping = sleeping;
            if (_sleepBtn != null)
                _sleepBtn.Text = sleeping ? "WAKE" : "SLEEP";
        }

        /// <summary>Hunger and sleep at a glance — they run on real hours.</summary>
        public void SetStatus(string text, bool urgent)
        {
            if (_statusLabel == null) return;
            _statusLabel.Text = text;
            _statusLabel.AddThemeColorOverride("font_color", urgent ? WarnColor : ClockColor);
        }

        /// <summary>Show the RIB button while the rib is earned but not yet used.</summary>
        public void SetRibAvailable(bool available)
        {
            if (_ribBtn != null) _ribBtn.Visible = available;
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
            _clockLabel.HorizontalAlignment = HorizontalAlignment.Right;
            _clockLabel.Size     = new Vector2(120, 20);
            _clockLabel.Position = new Vector2(w - 132, h - barH - 24);
            AddChild(_clockLabel);

            // ── Survival status (bottom-right, above the clock) ──
            _statusLabel = new Label();
            _statusLabel.AddThemeColorOverride("font_color", ClockColor);
            _statusLabel.AddThemeFontSizeOverride("font_size", 12);
            _statusLabel.HorizontalAlignment = HorizontalAlignment.Right;
            _statusLabel.Size     = new Vector2(360, 18);
            _statusLabel.Position = new Vector2(w - 372, h - barH - 44);
            AddChild(_statusLabel);

            // ── Warning label (centre screen) ──
            _warningLabel = new Label();
            _warningLabel.AddThemeColorOverride("font_color", WarnColor);
            _warningLabel.AddThemeFontSizeOverride("font_size", 20);
            _warningLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _warningLabel.Position = new Vector2(w / 2 - 200, h / 2 - 60);
            _warningLabel.Size     = new Vector2(400, 40);
            _warningLabel.Visible  = false;
            AddChild(_warningLabel);

            // ── SLEEP button — appears after 8h awake, left of ROUTES ──
            _sleepBtn = new Button();
            _sleepBtn.Text     = "SLEEP";
            _sleepBtn.Size     = new Vector2(64, 38);
            _sleepBtn.Position = new Vector2(w - 162, (barH - 38) / 2f);
            _sleepBtn.Visible  = false; // shown only when eligible
            _sleepBtn.AddThemeStyleboxOverride("normal",  MakeFlatStyle(new Color(0.08f, 0.12f, 0.10f)));
            _sleepBtn.AddThemeStyleboxOverride("hover",   MakeFlatStyle(new Color(0.14f, 0.22f, 0.18f)));
            _sleepBtn.AddThemeStyleboxOverride("pressed", MakeFlatStyle(new Color(0.11f, 0.17f, 0.14f)));
            _sleepBtn.AddThemeFontSizeOverride("font_size", 10);
            _sleepBtn.AddThemeColorOverride("font_color", new Color(0.55f, 0.78f, 0.60f));
            _sleepBtn.Pressed += () => { AinSoph.Audio.Sound.Play("click"); EmitSignal(SignalName.SleepRequested); };
            _actionBarPanel.AddChild(_sleepBtn);

            // ── RIB button — appears once the rib is earned, until it is used ──
            _ribBtn = new Button();
            _ribBtn.Text     = "RIB";
            _ribBtn.Size     = new Vector2(64, 38);
            _ribBtn.Position = new Vector2(w - 240, (barH - 38) / 2f);
            _ribBtn.Visible  = false;
            _ribBtn.TooltipText = "Give form to your spouse";
            _ribBtn.AddThemeStyleboxOverride("normal",  MakeFlatStyle(new Color(0.18f, 0.14f, 0.06f)));
            _ribBtn.AddThemeStyleboxOverride("hover",   MakeFlatStyle(new Color(0.30f, 0.24f, 0.10f)));
            _ribBtn.AddThemeStyleboxOverride("pressed", MakeFlatStyle(new Color(0.24f, 0.19f, 0.08f)));
            _ribBtn.AddThemeFontSizeOverride("font_size", 10);
            _ribBtn.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.45f));
            _ribBtn.Pressed += () => { AinSoph.Audio.Sound.Play("click"); EmitSignal(SignalName.RibRequested); };
            _actionBarPanel.AddChild(_ribBtn);

            // ── PACK button — what you carry (also I) ──
            var packBtn = new Button();
            packBtn.Text     = "PACK";
            packBtn.Size     = new Vector2(64, 38);
            packBtn.Position = new Vector2(w - 318, (barH - 38) / 2f);
            packBtn.TooltipText = "What you carry (I)";
            packBtn.AddThemeStyleboxOverride("normal",  MakeFlatStyle(new Color(0.10f, 0.10f, 0.08f)));
            packBtn.AddThemeStyleboxOverride("hover",   MakeFlatStyle(new Color(0.20f, 0.19f, 0.14f)));
            packBtn.AddThemeStyleboxOverride("pressed", MakeFlatStyle(new Color(0.16f, 0.15f, 0.11f)));
            packBtn.AddThemeFontSizeOverride("font_size", 10);
            packBtn.AddThemeColorOverride("font_color", new Color(0.6f, 0.58f, 0.48f));
            packBtn.Pressed += () => { AinSoph.Audio.Sound.Play("click"); EmitSignal(SignalName.PackRequested); };
            _actionBarPanel.AddChild(packBtn);

            // ── MAP button — far right of action bar (also M) ──
            var mapBtn = new Button();
            mapBtn.Text     = "MAP";
            mapBtn.Size     = new Vector2(72, 38);
            mapBtn.Position = new Vector2(w - 84, (barH - 38) / 2f);
            mapBtn.TooltipText = "What you have seen (M)";
            mapBtn.AddThemeStyleboxOverride("normal",  MakeFlatStyle(new Color(0.10f, 0.10f, 0.08f)));
            mapBtn.AddThemeStyleboxOverride("hover",   MakeFlatStyle(new Color(0.20f, 0.19f, 0.14f)));
            mapBtn.AddThemeStyleboxOverride("pressed", MakeFlatStyle(new Color(0.16f, 0.15f, 0.11f)));
            mapBtn.AddThemeFontSizeOverride("font_size", 10);
            mapBtn.AddThemeColorOverride("font_color", new Color(0.6f, 0.58f, 0.48f));
            mapBtn.Pressed += () => { AinSoph.Audio.Sound.Play("click"); EmitSignal(SignalName.MapRequested); };
            _actionBarPanel.AddChild(mapBtn);

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

            // Gifts from the Council — what each one does, at a glance; details on hover
            if (_gifts.Count > 0)
                _slotRow.AddChild(new VSeparator());
            foreach (var gift in _gifts.TakeLast(MaxGiftChips))
                _slotRow.AddChild(BuildGiftChip(gift));
            if (_gifts.Count > MaxGiftChips)
            {
                var more = new Label { Text = $"+{_gifts.Count - MaxGiftChips}",
                    TooltipText = string.Join("\n", _gifts.SkipLast(MaxGiftChips).Select(g => g.Label)),
                    MouseFilter = Control.MouseFilterEnum.Stop, VerticalAlignment = VerticalAlignment.Center };
                more.AddThemeColorOverride("font_color", GiftGold);
                _slotRow.AddChild(more);
            }
        }

        private static readonly Color GiftGold = new(1f, 0.85f, 0.45f);

        private Control BuildGiftChip(Skills.Gift gift)
        {
            var chip = new PanelContainer
            {
                CustomMinimumSize = new Vector2(96, 44),
                TooltipText = $"{gift.Label}\n{(gift.Kind == "item" ? "Carried" : "Learned")} — granted by the Council",
                MouseFilter = Control.MouseFilterEnum.Stop,
            };
            var style = MakeFlatStyle(new Color(0.14f, 0.12f, 0.06f));
            style.BorderColor = new Color(0.55f, 0.45f, 0.2f);
            chip.AddThemeStyleboxOverride("panel", style);

            var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            box.AddThemeConstantOverride("separation", 1);
            chip.AddChild(box);

            var name = new Label { Text = gift.Name.Length > 16 ? gift.Name[..15] + "…" : gift.Name,
                HorizontalAlignment = HorizontalAlignment.Center };
            name.AddThemeFontSizeOverride("font_size", 10);
            name.AddThemeColorOverride("font_color", GiftGold);
            box.AddChild(name);

            var tag = new Label { Text = Skills.Gifts.Tag(gift.Effect), HorizontalAlignment = HorizontalAlignment.Center };
            tag.AddThemeFontSizeOverride("font_size", 8);
            tag.AddThemeColorOverride("font_color", gift.Effect == Skills.GiftEffect.Lore
                ? new Color(0.5f, 0.5f, 0.45f) : new Color(0.75f, 0.72f, 0.6f));
            box.AddChild(tag);
            return chip;
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
                icon.Material           = TileRegistry.CutoutMaterial;
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

        private void UpdateSleepButton()
        {
            if (_sleepBtn == null) return;
            var player = AinSoph.GameRoot.Player;
            if (player == null) { _sleepBtn.Visible = false; return; }

            bool isSleeping = player.Survival.IsSleeping;
            double hoursAwake = (DateTime.UtcNow - player.Survival.LastSleptUtc).TotalHours;

            // Show SLEEP after 8h awake (eligible), always show WAKE if sleeping
            _sleepBtn.Visible = isSleeping || hoursAwake >= 8.0;
            _sleepBtn.Text    = isSleeping ? "WAKE" : "SLEEP";
        }

        // ── Signals ──────────────────────────────────────────────────────────
        [Signal] public delegate void SkillSelectedEventHandler(int skillType);
        [Signal] public delegate void RoutesOpenRequestedEventHandler();
        [Signal] public delegate void SleepRequestedEventHandler();
        [Signal] public delegate void RibRequestedEventHandler();
        [Signal] public delegate void PackRequestedEventHandler();
        [Signal] public delegate void MapRequestedEventHandler();
    }
}
