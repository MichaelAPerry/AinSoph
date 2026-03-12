using Godot;
using System;
using System.Collections.Generic;
using AinSoph.Skills;

namespace AinSoph.UI
{
    /// <summary>
    /// Small popup that appears when the player clicks any entity or tile.
    /// Shows all six primitives as icon tiles in a row.
    /// Player picks one; the menu fires OnPrimitiveChosen and closes.
    ///
    /// Appears at the click position, nudged inward if too close to a screen edge.
    /// Closes on Escape or any click outside.
    /// </summary>
    public partial class PrimitiveMenu : CanvasLayer
    {
        public string   TargetId   { get; private set; } = string.Empty;
        public string   TargetName { get; private set; } = string.Empty;

        public event Action<string, SkillType>? OnPrimitiveChosen; // (targetId, skill)

        private Panel         _panel;
        private Label         _targetLabel;
        private HBoxContainer _buttonRow;
        private Vector2       _spawnPos;

        private const float PanelW  = 320f;
        private const float PanelH  = 78f;
        private const float BtnSize = 44f;

        private static readonly Color PanelBg     = new Color(0.06f, 0.06f, 0.06f, 0.97f);
        private static readonly Color BorderColor  = new Color(0.45f, 0.45f, 0.45f);
        private static readonly Color BtnBg        = new Color(0.14f, 0.14f, 0.14f);
        private static readonly Color BtnHover     = new Color(0.24f, 0.24f, 0.18f);
        private static readonly Color LabelColor   = new Color(0.6f,  0.6f,  0.55f);

        // Ordered list of all six primitives
        private static readonly SkillType[] Primitives =
        {
            SkillType.Move, SkillType.See, SkillType.Hear,
            SkillType.Talk, SkillType.Reap, SkillType.Pray
        };

        public override void _Ready()
        {
            Layer   = 15; // above world, below dialogue
            Visible = false;
            BuildLayout();
        }

        public override void _Input(InputEvent ev)
        {
            if (!Visible) return;

            if (ev is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
            {
                Close();
                return;
            }

            // Click outside panel → close
            if (ev is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
            {
                var panelRect = new Rect2(_panel.GlobalPosition, _panel.Size);
                if (!panelRect.HasPoint(mb.GlobalPosition))
                    Close();
            }
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>Open the menu near a screen position for a given target.</summary>
        public void Open(string targetId, string targetName, Vector2 screenPos)
        {
            TargetId   = targetId;
            TargetName = targetName;

            _targetLabel.Text = targetName.Length > 0 ? targetName.ToUpper() : "?";

            // Position panel near click, nudge away from edges
            var vp   = GetViewport().GetVisibleRect().Size;
            float px = Mathf.Clamp(screenPos.X - PanelW / 2f, 4f, vp.X - PanelW - 4f);
            float py = Mathf.Clamp(screenPos.Y - PanelH - 12f, 4f, vp.Y - PanelH - 4f);
            _panel.Position = new Vector2(px, py);

            Visible = true;
        }

        public void Close()
        {
            Visible    = false;
            TargetId   = string.Empty;
            TargetName = string.Empty;
        }

        // ── Layout ────────────────────────────────────────────────────────

        private void BuildLayout()
        {
            _panel = new Panel();
            _panel.Size = new Vector2(PanelW, PanelH);
            _panel.AddThemeStyleboxOverride("panel", MakePanelStyle());
            AddChild(_panel);

            // Target name label (tiny, top of panel)
            _targetLabel = new Label();
            _targetLabel.Position = new Vector2(8, 5);
            _targetLabel.Size     = new Vector2(PanelW - 16, 16);
            _targetLabel.AddThemeFontSizeOverride("font_size", 10);
            _targetLabel.AddThemeColorOverride("font_color", LabelColor);
            _panel.AddChild(_targetLabel);

            // Button row
            _buttonRow = new HBoxContainer();
            _buttonRow.Position = new Vector2(6, 22);
            _buttonRow.Size     = new Vector2(PanelW - 12, BtnSize);
            _buttonRow.AddThemeConstantOverride("separation", 4);
            _panel.AddChild(_buttonRow);

            foreach (var skill in Primitives)
                _buttonRow.AddChild(BuildSkillButton(skill));
        }

        private Control BuildSkillButton(SkillType skill)
        {
            var btn = new Button();
            btn.CustomMinimumSize = new Vector2(BtnSize, BtnSize);
            btn.TooltipText       = skill.ToString();
            btn.AddThemeStyleboxOverride("normal", MakeBtnStyle(BtnBg));
            btn.AddThemeStyleboxOverride("hover",  MakeBtnStyle(BtnHover));
            btn.AddThemeStyleboxOverride("pressed",MakeBtnStyle(BtnHover));

            // Icon
            int tileIdx = TileRegistry.StateIconFor(MapSkillToNpcState(skill));
            var tex     = GD.Load<Texture2D>(TileRegistry.TilePath(tileIdx));
            if (tex != null)
            {
                var icon = new TextureRect();
                icon.Texture   = tex;
                icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
                icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
                icon.CustomMinimumSize = new Vector2(20, 20);
                icon.SetAnchorsPreset(Control.LayoutPreset.Center);
                icon.Position = new Vector2(12, 8);
                btn.AddChild(icon);
            }

            // Label inside button
            var label = new Label();
            label.Text = skill.ToString().ToUpper();
            label.AddThemeFontSizeOverride("font_size", 8);
            label.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.7f));
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.Position = new Vector2(0, BtnSize - 16);
            label.Size     = new Vector2(BtnSize, 14);
            btn.AddChild(label);

            btn.Pressed += () =>
            {
                var chosen = skill;
                Close();
                OnPrimitiveChosen?.Invoke(TargetId == string.Empty ? TargetId : TargetId, chosen);
            };

            return btn;
        }

        private static NPC.NpcState MapSkillToNpcState(SkillType skill) => skill switch
        {
            SkillType.Move  => NPC.NpcState.Moving,
            SkillType.See   => NPC.NpcState.Idle,
            SkillType.Hear  => NPC.NpcState.Idle,
            SkillType.Talk  => NPC.NpcState.Talking,
            SkillType.Reap  => NPC.NpcState.Creating,
            SkillType.Pray  => NPC.NpcState.Praying,
            _               => NPC.NpcState.Idle
        };

        private static StyleBoxFlat MakePanelStyle()
        {
            var s = new StyleBoxFlat();
            s.BgColor = PanelBg;
            s.SetBorderWidthAll(1);
            s.BorderColor = BorderColor;
            s.SetCornerRadiusAll(0);
            s.ShadowColor = new Color(0, 0, 0, 0.6f);
            s.ShadowSize  = 4;
            return s;
        }

        private static StyleBoxFlat MakeBtnStyle(Color bg)
        {
            var s = new StyleBoxFlat();
            s.BgColor = bg;
            s.SetBorderWidthAll(1);
            s.BorderColor = new Color(0.3f, 0.3f, 0.3f);
            s.SetCornerRadiusAll(2);
            return s;
        }
    }
}
