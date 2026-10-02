using Godot;
using System;
using AinSoph.NPC;

namespace AinSoph.UI
{
    /// <summary>
    /// Separate full-screen dialogue layer. Activated when the player uses Talk or Pray.
    ///
    /// Layout (inspired by reference screenshot):
    ///   ┌─────────────────────────────────────────────────────┐
    ///   │ ┌──────────────────────────────────────────────────┐│
    ///   │ │  NPC speech text goes here (top box)              ││
    ///   │ └──────────────────────────────────────────────────┘│
    ///   │                                                       │
    ///   │          [  PORTRAIT / ALTAR IMAGE  ]                │
    ///   │                                                       │
    ///   │ ┌──────────────────────────────────────────────────┐│
    ///   │ │> player input here_                               ││
    ///   │ └──────────────────────────────────────────────────┘│
    ///   └─────────────────────────────────────────────────────┘
    ///
    /// NPC mode:    portrait = composited Kenney character from decan seed
    /// Altar mode:  portrait = altar tile rendered large
    /// Council mode: same as altar — submissions go to TribuneCouncil
    /// </summary>
    public partial class DialogueScreen : CanvasLayer
    {
        public enum Mode { NPC, Altar }

        // ── Nodes ─────────────────────────────────────────────────────────
        private ColorRect    _backdrop;
        private Panel        _speechPanel;
        private RichTextLabel _speechLabel;
        private Label        _speakerName;
        private SubViewportContainer _portraitContainer;
        private SubViewport  _portraitViewport;
        private Panel        _inputPanel;
        private LineEdit     _inputField;
        private Label        _inputPrompt;

        // ── State ─────────────────────────────────────────────────────────
        private Mode    _mode;
        private string  _npcId;
        private int     _decanId;
        private int     _seed;
        private Action<string> _onSubmit;

        // ── Colours / style ───────────────────────────────────────────────
        private static readonly Color BackdropColor = new Color(0, 0, 0, 0.97f);
        private static readonly Color PanelBg       = new Color(0.06f, 0.06f, 0.06f, 1f);
        private static readonly Color BorderColor   = new Color(0.55f, 0.55f, 0.55f, 1f);
        private static readonly Color SpeechColor   = new Color(0.9f, 0.9f, 0.85f, 1f);
        private static readonly Color NameColor     = new Color(0.7f, 0.85f, 0.7f, 1f);
        private static readonly Color InputColor    = new Color(0.85f, 0.85f, 0.85f, 1f);

        public override void _Ready()
        {
            Layer   = 20;
            Visible = false;
            BuildLayout();
        }

        public override void _Input(InputEvent ev)
        {
            if (!Visible) return;

            if (ev is InputEventKey key && key.Pressed)
            {
                if (key.Keycode == Key.Escape)
                {
                    Close();
                    GetViewport().SetInputAsHandled(); // don't also open the Esc menu
                }
                else if (key.Keycode == Key.Enter || key.Keycode == Key.KpEnter)
                    SubmitInput();
            }
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>Open dialogue with an NPC.</summary>
        public void OpenNPC(string npcId, string npcName, int decanId, int seed,
                            string openingLine, Action<string> onPlayerSpeak)
        {
            _mode     = Mode.NPC;
            _npcId    = npcId;
            _decanId  = decanId;
            _seed     = seed;
            _onSubmit = onPlayerSpeak;

            _speakerName.Text = npcName.ToUpper();
            SetSpeech(openingLine);
            BuildPortrait();

            _inputPrompt.Text = "> SPEAK :";
            _inputField.PlaceholderText = "say something...";
            _inputField.Clear();
            Visible = true;
            _inputField.GrabFocus();
        }

        /// <summary>Open the altar / prayer submission screen.</summary>
        public void OpenAltar(Action<string> onSubmit)
        {
            _mode     = Mode.Altar;
            _onSubmit = onSubmit;

            _speakerName.Text = "THE COUNCIL";
            SetSpeech("You stand before the altar. The stone is cold beneath your hands.\nSpeak your petition.");
            BuildAltarPortrait();

            _inputPrompt.Text = "> PETITION :";
            _inputField.PlaceholderText = "speak your petition freely...";
            _inputField.Clear();
            Visible = true;
            _inputField.GrabFocus();
        }

        /// <summary>Update the speech box (NPC reply came back from LLM).</summary>
        public void SetSpeech(string text)
        {
            _speechLabel.Text = text;
        }

        /// <summary>Update the speaker name label (used for Council seat names).</summary>
        public void SetSpeakerName(string name)
        {
            _speakerName.Text = name.ToUpper();
        }

        public string SpeechText => _speechLabel.Text;

        /// <summary>Put text in the input box without submitting (demo tour typing).</summary>
        public void SetInputText(string text) => _inputField.Text = text;

        /// <summary>Submit a line as if the player typed it and pressed Enter.</summary>
        public void SubmitText(string text)
        {
            _inputField.Text = text;
            SubmitInput();
        }

        public void Close()
        {
            Visible = false;
            ClearPortrait();
        }

        // ── Layout ────────────────────────────────────────────────────────

        private void BuildLayout()
        {
            var vp = GetViewport().GetVisibleRect().Size;
            float w = vp.X, h = vp.Y;
            float margin  = 32f;
            float speechH = 170f;
            float inputH  = 64f;
            float portraitH = h - speechH - inputH - margin * 4;

            // Full-screen black backdrop
            _backdrop = new ColorRect();
            _backdrop.Color    = BackdropColor;
            _backdrop.Position = Vector2.Zero;
            _backdrop.Size     = vp;
            AddChild(_backdrop);

            // ── Speaker name ──
            _speakerName = new Label();
            _speakerName.AddThemeColorOverride("font_color", NameColor);
            _speakerName.AddThemeFontSizeOverride("font_size", 13);
            _speakerName.Position = new Vector2(margin, margin - 18);
            AddChild(_speakerName);

            // ── Speech panel (top) ──
            _speechPanel = new Panel();
            _speechPanel.Position = new Vector2(margin, margin);
            _speechPanel.Size     = new Vector2(w - margin * 2, speechH);
            _speechPanel.AddThemeStyleboxOverride("panel", MakeBorderedStyle(PanelBg, BorderColor));
            AddChild(_speechPanel);

            _speechLabel = new RichTextLabel();
            _speechLabel.Position = new Vector2(12, 10);
            _speechLabel.Size     = new Vector2(w - margin * 2 - 24, speechH - 20);
            _speechLabel.AddThemeColorOverride("default_color", SpeechColor);
            _speechLabel.AddThemeFontSizeOverride("normal_font_size", 15);
            _speechLabel.BbcodeEnabled = false;
            _speechLabel.AutowrapMode  = TextServer.AutowrapMode.Word;
            _speechPanel.AddChild(_speechLabel);

            // ── Portrait viewport ──
            _portraitContainer = new SubViewportContainer();
            _portraitContainer.Position = new Vector2(w / 2 - 100, margin + speechH + margin);
            _portraitContainer.Size     = new Vector2(200, portraitH);
            _portraitContainer.Stretch  = true;
            AddChild(_portraitContainer);

            _portraitViewport = new SubViewport();
            _portraitViewport.Size                  = new Vector2I(200, (int)portraitH);
            _portraitViewport.TransparentBg          = true;
            _portraitViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            _portraitContainer.AddChild(_portraitViewport);

            // ── Input panel (bottom) ──
            float inputY   = h - inputH - margin;
            float btnW     = 80f;
            float btnGap   = 6f;
            float fieldW   = w - margin * 2 - 90f - (btnW + btnGap) * 2;

            var inputBg = new Panel();
            inputBg.Position = new Vector2(margin, inputY);
            inputBg.Size     = new Vector2(w - margin * 2, inputH);
            inputBg.AddThemeStyleboxOverride("panel", MakeBorderedStyle(PanelBg, BorderColor));
            AddChild(inputBg);

            _inputPrompt = new Label();
            _inputPrompt.Position = new Vector2(10, 22);
            _inputPrompt.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            _inputPrompt.AddThemeFontSizeOverride("font_size", 13);
            inputBg.AddChild(_inputPrompt);

            _inputField = new LineEdit();
            _inputField.Position = new Vector2(90, 8);
            _inputField.Size     = new Vector2(fieldW, inputH - 16);
            _inputField.AddThemeColorOverride("font_color", InputColor);
            _inputField.AddThemeFontSizeOverride("font_size", 15);
            _inputField.AddThemeStyleboxOverride("normal", MakeInputStyle());
            _inputField.TextSubmitted += OnInputSubmitted;
            inputBg.AddChild(_inputField);

            // SEND button
            float btnX = 90f + fieldW + btnGap;
            var sendBtn = MakeButton("SEND", new Vector2(btnX, 10), new Vector2(btnW, inputH - 20),
                new Color(0.2f, 0.35f, 0.2f));
            sendBtn.Pressed += SubmitInput;
            inputBg.AddChild(sendBtn);

            // LEAVE button
            float leaveX = btnX + btnW + btnGap;
            var leaveBtn = MakeButton("LEAVE", new Vector2(leaveX, 10), new Vector2(btnW, inputH - 20),
                new Color(0.3f, 0.12f, 0.12f));
            leaveBtn.Pressed += Close;
            inputBg.AddChild(leaveBtn);
        }

        private void BuildPortrait()
        {
            ClearPortrait();
            var portrait = CharacterPortraitBuilder.Build(_decanId, _seed);
            var box      = _portraitViewport.Size;
            portrait.Position = new Vector2(box.X / 2f,
                Mathf.Max(0f, (box.Y - CharacterPortraitBuilder.PortraitHeight) / 2f));
            _portraitViewport.AddChild(portrait);
        }

        private void BuildAltarPortrait() =>
            BuildGlyphPortrait(TileRegistry.AltarTile, Colors.White, new Color(0.55f, 0.55f, 1f, 0.25f));

        /// <summary>A tile at 16× scale centred in the portrait, over a soft halo of the same shape.</summary>
        private void BuildGlyphPortrait(int tile, Color tint, Color halo)
        {
            ClearPortrait();
            var centre = new Vector2(_portraitViewport.Size.X / 2f, _portraitViewport.Size.Y / 2f);

            var glow = new Sprite2D();
            glow.Texture  = GD.Load<Texture2D>(TileRegistry.TilePath(tile));
            glow.Material = TileRegistry.CutoutMaterial;
            glow.Scale    = Vector2.One * 22;
            glow.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
            glow.Position = centre;
            glow.Modulate = halo;
            _portraitViewport.AddChild(glow);

            var sprite = new Sprite2D();
            sprite.Texture  = GD.Load<Texture2D>(TileRegistry.TilePath(tile));
            sprite.Material = TileRegistry.CutoutMaterial;
            sprite.Scale    = Vector2.One * 16;
            sprite.Position = centre;
            sprite.Modulate = tint;
            sprite.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
            _portraitViewport.AddChild(sprite);
        }

        /// <summary>A vision: something the Council sent, shown as a glowing sign rather than a face.</summary>
        public void OpenVision(string name, int tile, Color tint, string text, Action<string> onSubmit)
        {
            _mode     = Mode.NPC;
            _npcId    = string.Empty;
            _onSubmit = onSubmit;
            _speakerName.Text = name.ToUpper();
            SetSpeech(text);
            BuildGlyphPortrait(tile, tint, new Color(tint.R, tint.G, tint.B, 0.3f));
            _inputPrompt.Text = "> SPEAK :";
            _inputField.PlaceholderText = "say something...";
            _inputField.Clear();
            Visible = true;
            _inputField.GrabFocus();
        }

        private void ClearPortrait()
        {
            foreach (Node child in _portraitViewport.GetChildren())
                child.QueueFree();
        }

        private void SubmitInput()
        {
            var text = _inputField.Text.Trim();
            if (text.Length == 0) return;

            _inputField.Clear();
            _onSubmit?.Invoke(text);
        }

        private void OnInputSubmitted(string text)
        {
            SubmitInput();
        }

        // ── Style helpers ─────────────────────────────────────────────────

        private static StyleBoxFlat MakeBorderedStyle(Color bg, Color border)
        {
            var s = new StyleBoxFlat();
            s.BgColor = bg;
            s.SetBorderWidthAll(2);
            s.BorderColor = border;
            s.SetCornerRadiusAll(0); // Sharp NES-style corners
            return s;
        }

        private static StyleBoxFlat MakeInputStyle()
        {
            var s = new StyleBoxFlat();
            s.BgColor = new Color(0f, 0f, 0f, 0f); // transparent — inherits panel bg
            s.SetBorderWidthAll(0);
            return s;
        }

        private static Button MakeButton(string text, Vector2 pos, Vector2 size, Color bg)
        {
            var btn = new Button();
            btn.Text     = text;
            btn.Position = pos;
            btn.Size     = size;
            btn.AddThemeFontSizeOverride("font_size", 12);
            btn.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f));

            var normal = new StyleBoxFlat();
            normal.BgColor = bg;
            normal.SetBorderWidthAll(1);
            normal.BorderColor = new Color(0.5f, 0.5f, 0.5f);
            normal.SetCornerRadiusAll(2);
            btn.AddThemeStyleboxOverride("normal", normal);

            var hover = new StyleBoxFlat();
            hover.BgColor = new Color(
                Mathf.Clamp(bg.R + 0.1f, 0, 1),
                Mathf.Clamp(bg.G + 0.1f, 0, 1),
                Mathf.Clamp(bg.B + 0.1f, 0, 1));
            hover.SetBorderWidthAll(1);
            hover.BorderColor = new Color(0.7f, 0.7f, 0.7f);
            hover.SetCornerRadiusAll(2);
            btn.AddThemeStyleboxOverride("hover", hover);

            return btn;
        }
    }
}
