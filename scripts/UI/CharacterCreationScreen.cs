using Godot;
using System;

namespace AinSoph.UI
{
    /// <summary>
    /// Shown once, on first launch, before the world is visible.
    ///
    /// Layout:
    ///   Full black screen.
    ///   Centre: flavour text fades in, then the name prompt beneath it.
    ///   Single LineEdit. Enter or DESCEND button confirms.
    ///   On confirm: screen fades to black, then world loads.
    /// </summary>
    public partial class CharacterCreationScreen : CanvasLayer
    {
        private Label     _flavourLabel;
        private Label     _promptLabel;
        private LineEdit  _nameField;
        private Button    _confirmBtn;
        private ColorRect _backdrop;
        private ColorRect _fadeRect;

        private float _fadeTimer   = 0f;
        private bool  _fadingIn    = true;   // flavour text fading in
        private bool  _fadingOut   = false;  // confirm → world fade
        private bool  _confirmed   = false;

        private Action<string> _onComplete;

        private const float FadeInDuration  = 2.2f;
        private const float FadeOutDuration = 1.4f;

        private static readonly Color TextColor   = new Color(0.82f, 0.82f, 0.75f, 0f); // starts transparent
        private static readonly Color DimText     = new Color(0.5f,  0.5f,  0.45f, 0f);
        private static readonly Color InputColor  = new Color(0.88f, 0.88f, 0.82f, 1f);

        public override void _Ready()
        {
            Layer = 30;
            BuildLayout();
        }

        public override void _Process(double delta)
        {
            if (_fadingIn && !_confirmed)
            {
                _fadeTimer += (float)delta;
                float t = Mathf.Clamp(_fadeTimer / FadeInDuration, 0f, 1f);

                _flavourLabel.Modulate = new Color(0.82f, 0.82f, 0.75f, Mathf.SmoothStep(0f, 1f, t));

                // Prompt and input appear halfway through the flavour fade
                float t2 = Mathf.Clamp((_fadeTimer - FadeInDuration * 0.5f) / (FadeInDuration * 0.5f), 0f, 1f);
                float a2 = Mathf.SmoothStep(0f, 1f, t2);
                _promptLabel.Modulate  = new Color(0.5f, 0.5f, 0.45f, a2);
                _nameField.Modulate    = new Color(1f, 1f, 1f, a2);
                _confirmBtn.Modulate   = new Color(1f, 1f, 1f, a2);

                if (t >= 1f)
                {
                    _fadingIn = false;
                    _nameField.GrabFocus();
                }
            }

            if (_fadingOut)
            {
                _fadeTimer += (float)delta;
                float t = Mathf.Clamp(_fadeTimer / FadeOutDuration, 0f, 1f);
                _fadeRect.Modulate = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, t));

                if (t >= 1f)
                {
                    _fadingOut = false;
                    QueueFree();
                }
            }
        }

        public override void _Input(InputEvent ev)
        {
            if (_confirmed) return;
            if (ev is InputEventKey key && key.Pressed &&
                (key.Keycode == Key.Enter || key.Keycode == Key.KpEnter))
                Confirm();
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>Show the screen. Calls onComplete(name) after the fade-out.</summary>
        public void Show(Action<string> onComplete)
        {
            _onComplete = onComplete;
            Visible     = true;
        }

        // ── Internal ──────────────────────────────────────────────────────

        private void Confirm()
        {
            var name = _nameField.Text.Trim();
            if (name.Length == 0) return;

            _confirmed   = true;
            _fadingOut   = true;
            _fadeTimer   = 0f;

            // Disable input so nothing can fire during fade
            _nameField.Editable   = false;
            _confirmBtn.Disabled  = true;

            // Invoke immediately so GameRoot can set player name while we fade
            _onComplete?.Invoke(name);
        }

        // ── Layout ────────────────────────────────────────────────────────

        private void BuildLayout()
        {
            var vp = GetViewport().GetVisibleRect().Size;
            float cx = vp.X / 2f;
            float cy = vp.Y / 2f;

            // Full black backdrop
            _backdrop        = new ColorRect();
            _backdrop.Color  = new Color(0, 0, 0, 1);
            _backdrop.Size   = vp;
            AddChild(_backdrop);

            // Flavour text — centred, wide, italic feel via spacing
            _flavourLabel = new Label();
            _flavourLabel.Text = "You decided to descend from the light.";
            _flavourLabel.AddThemeFontSizeOverride("font_size", 22);
            _flavourLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.82f, 0.75f));
            _flavourLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _flavourLabel.Size     = new Vector2(600, 40);
            _flavourLabel.Position = new Vector2(cx - 300, cy - 70);
            _flavourLabel.Modulate = new Color(1, 1, 1, 0);
            AddChild(_flavourLabel);

            // Prompt text
            _promptLabel = new Label();
            _promptLabel.Text = "What do you name this emanation?";
            _promptLabel.AddThemeFontSizeOverride("font_size", 15);
            _promptLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.45f));
            _promptLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _promptLabel.Size     = new Vector2(500, 28);
            _promptLabel.Position = new Vector2(cx - 250, cy - 10);
            _promptLabel.Modulate = new Color(1, 1, 1, 0);
            AddChild(_promptLabel);

            // Name input — minimal, centred
            _nameField = new LineEdit();
            _nameField.PlaceholderText = "your name";
            _nameField.MaxLength       = 32;
            _nameField.Size            = new Vector2(280, 38);
            _nameField.Position        = new Vector2(cx - 140, cy + 30);
            _nameField.AddThemeColorOverride("font_color", InputColor);
            _nameField.AddThemeFontSizeOverride("font_size", 18);
            _nameField.AddThemeStyleboxOverride("normal",   MakeInputStyle(false));
            _nameField.AddThemeStyleboxOverride("focus",    MakeInputStyle(true));
            _nameField.Alignment = HorizontalAlignment.Center;
            _nameField.Modulate  = new Color(1, 1, 1, 0);
            AddChild(_nameField);

            // DESCEND confirm button
            _confirmBtn = new Button();
            _confirmBtn.Text     = "DESCEND";
            _confirmBtn.Size     = new Vector2(120, 34);
            _confirmBtn.Position = new Vector2(cx - 60, cy + 82);
            _confirmBtn.AddThemeFontSizeOverride("font_size", 13);
            _confirmBtn.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.65f));
            _confirmBtn.AddThemeStyleboxOverride("normal", MakeButtonStyle(false));
            _confirmBtn.AddThemeStyleboxOverride("hover",  MakeButtonStyle(true));
            _confirmBtn.Modulate = new Color(1, 1, 1, 0);
            _confirmBtn.Pressed += Confirm;
            AddChild(_confirmBtn);

            // Fade-to-black overlay (starts transparent, used for exit fade)
            _fadeRect        = new ColorRect();
            _fadeRect.Color  = new Color(0, 0, 0, 1);
            _fadeRect.Size   = vp;
            _fadeRect.Modulate = new Color(1, 1, 1, 0);
            AddChild(_fadeRect);
        }

        private static StyleBoxFlat MakeInputStyle(bool focused)
        {
            var s = new StyleBoxFlat();
            s.BgColor = new Color(0.06f, 0.06f, 0.06f);
            s.SetBorderWidthAll(1);
            s.BorderColor = focused ? new Color(0.55f, 0.55f, 0.5f) : new Color(0.25f, 0.25f, 0.22f);
            s.SetCornerRadiusAll(0);
            s.ContentMarginLeft   = 8;
            s.ContentMarginRight  = 8;
            s.ContentMarginTop    = 4;
            s.ContentMarginBottom = 4;
            return s;
        }

        private static StyleBoxFlat MakeButtonStyle(bool hover)
        {
            var s = new StyleBoxFlat();
            s.BgColor = hover ? new Color(0.14f, 0.14f, 0.12f) : new Color(0.08f, 0.08f, 0.07f);
            s.SetBorderWidthAll(1);
            s.BorderColor = hover ? new Color(0.55f, 0.55f, 0.5f) : new Color(0.3f, 0.3f, 0.28f);
            s.SetCornerRadiusAll(0);
            return s;
        }
    }
}
