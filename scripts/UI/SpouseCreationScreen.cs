using Godot;
using System;

namespace AinSoph.UI
{
    /// <summary>
    /// Shown when the player uses the rib. The spouse is named and described
    /// by the player consciously — this is a deliberate creation (TRIBES.md).
    ///
    /// Calls onComplete(name, description) on confirm. Esc closes without
    /// spending the rib.
    /// </summary>
    public partial class SpouseCreationScreen : CanvasLayer
    {
        private LineEdit _nameField = null!;
        private TextEdit _descField = null!;
        private Button   _confirmBtn = null!;
        private Action<string, string>? _onComplete;
        private Action? _onCancel;
        private bool _done;

        private static readonly Color TextColor  = new(0.82f, 0.82f, 0.75f);
        private static readonly Color MutedColor = new(0.55f, 0.53f, 0.46f);
        private static readonly Color GoldColor  = new(1f, 0.85f, 0.45f);

        public override void _Ready()
        {
            Layer = 30;
            BuildLayout();
            _nameField.GrabFocus();
        }

        public void Show(Action<string, string> onComplete, Action onCancel)
        {
            _onComplete = onComplete;
            _onCancel   = onCancel;
            Visible     = true;
        }

        /// <summary>Fill in and confirm without the keyboard (demo tour).</summary>
        public void Submit(string name, string description)
        {
            _nameField.Text = name;
            _descField.Text = description;
            Confirm();
        }

        public override void _Input(InputEvent ev)
        {
            if (_done || ev is not InputEventKey { Pressed: true } key) return;
            if (key.Keycode == Key.Escape)
            {
                _done = true;
                GetViewport().SetInputAsHandled();
                _onCancel?.Invoke();
                QueueFree();
            }
        }

        private void Confirm()
        {
            if (_done) return;
            var name = _nameField.Text.Trim();
            if (name.Length == 0) { _nameField.GrabFocus(); return; }

            _done = true;
            _onComplete?.Invoke(name, _descField.Text.Trim());
            QueueFree();
        }

        // ── Layout ────────────────────────────────────────────────────────

        private void BuildLayout()
        {
            var vp = GetViewport().GetVisibleRect().Size;
            float cx = vp.X / 2f, cy = vp.Y / 2f;

            var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.92f), Size = vp };
            AddChild(backdrop);

            AddLabel("THE RIB", 13, GoldColor, cx, cy - 170, 400);
            AddLabel("Something is taken from your side, and waits to be given form.", 20, TextColor, cx, cy - 135, 760);
            AddLabel("Name them. Describe them. This cannot be undone, and there is only one.", 14, MutedColor, cx, cy - 98, 760);

            _nameField = new LineEdit
            {
                PlaceholderText = "their name",
                MaxLength       = 32,
                Size            = new Vector2(320, 38),
                Position        = new Vector2(cx - 160, cy - 50),
                Alignment       = HorizontalAlignment.Center,
            };
            _nameField.AddThemeFontSizeOverride("font_size", 18);
            _nameField.AddThemeColorOverride("font_color", TextColor);
            _nameField.AddThemeStyleboxOverride("normal", MakeStyle(false));
            _nameField.AddThemeStyleboxOverride("focus",  MakeStyle(true));
            _nameField.TextSubmitted += _ => _descField.GrabFocus();
            AddChild(_nameField);

            _descField = new TextEdit
            {
                PlaceholderText = "who they are — their manner, their face, what they love",
                Size            = new Vector2(520, 110),
                Position        = new Vector2(cx - 260, cy + 2),
                WrapMode        = TextEdit.LineWrappingMode.Boundary,
            };
            _descField.AddThemeFontSizeOverride("font_size", 15);
            _descField.AddThemeColorOverride("font_color", TextColor);
            _descField.AddThemeStyleboxOverride("normal", MakeStyle(false));
            _descField.AddThemeStyleboxOverride("focus",  MakeStyle(true));
            AddChild(_descField);

            _confirmBtn = new Button
            {
                Text     = "GIVE FORM",
                Size     = new Vector2(140, 36),
                Position = new Vector2(cx - 70, cy + 128),
            };
            _confirmBtn.AddThemeFontSizeOverride("font_size", 13);
            _confirmBtn.AddThemeColorOverride("font_color", GoldColor);
            _confirmBtn.AddThemeStyleboxOverride("normal", MakeStyle(false));
            _confirmBtn.AddThemeStyleboxOverride("hover",  MakeStyle(true));
            _confirmBtn.Pressed += Confirm;
            AddChild(_confirmBtn);

            AddLabel("Esc — not yet", 11, MutedColor, cx, cy + 176, 300);
        }

        private void AddLabel(string text, int size, Color color, float cx, float y, float width)
        {
            var label = new Label
            {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Size     = new Vector2(width, size * 2),
                Position = new Vector2(cx - width / 2f, y),
            };
            label.AddThemeFontSizeOverride("font_size", size);
            label.AddThemeColorOverride("font_color", color);
            AddChild(label);
        }

        private static StyleBoxFlat MakeStyle(bool active)
        {
            var s = new StyleBoxFlat
            {
                BgColor = new Color(0.06f, 0.06f, 0.06f),
                BorderColor = active ? new Color(0.7f, 0.6f, 0.35f) : new Color(0.3f, 0.3f, 0.28f),
                ContentMarginLeft = 10, ContentMarginRight = 10,
                ContentMarginTop = 6, ContentMarginBottom = 6,
            };
            s.SetBorderWidthAll(1);
            return s;
        }
    }
}
