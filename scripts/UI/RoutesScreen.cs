using Godot;
using System;

namespace AinSoph.UI
{
    /// <summary>
    /// The Routes screen. Opened via the HUD ROUTES button.
    ///
    /// Two actions:
    ///   Export — select up to 1/10th of your NPCs, write a packet file,
    ///            share it with the other player by any means.
    ///   Import — load a packet file another player sent you.
    ///            All arriving NPCs are permanently foreigners.
    ///
    /// The screen explains what will happen before the player confirms.
    /// File dialogs use the OS file picker.
    /// </summary>
    public partial class RoutesScreen : CanvasLayer
    {
        public event Action? OnClose;

        private Panel         _panel;
        private Label         _titleLabel;
        private Label         _bodyLabel;
        private Button        _exportBtn;
        private Button        _importBtn;
        private Button        _closeBtn;
        private FileDialog    _fileDialog;
        private Label         _statusLabel;

        private static readonly Color BgColor      = new Color(0.05f, 0.05f, 0.05f, 0.97f);
        private static readonly Color BorderColor   = new Color(0.4f,  0.38f, 0.32f);
        private static readonly Color TextColor     = new Color(0.78f, 0.76f, 0.68f);
        private static readonly Color DimColor      = new Color(0.5f,  0.48f, 0.42f);
        private static readonly Color AccentColor   = new Color(0.55f, 0.52f, 0.38f);
        private static readonly Color StatusOk      = new Color(0.5f,  0.72f, 0.45f);
        private static readonly Color StatusErr     = new Color(0.8f,  0.4f,  0.35f);

        private const float PanelW = 520f;
        private const float PanelH = 360f;

        private enum PendingAction { None, Export, Import }
        private PendingAction _pending = PendingAction.None;

        public override void _Ready()
        {
            Layer   = 25;
            Visible = false;
            BuildLayout();
        }

        public override void _Input(InputEvent ev)
        {
            if (!Visible) return;
            if (ev is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
                Close();
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void Open()
        {
            RefreshBody();
            _statusLabel.Text    = string.Empty;
            _statusLabel.Visible = false;
            CentrePanel();
            Visible = true;
        }

        public void Close()
        {
            Visible = false;
            OnClose?.Invoke();
        }

        // ── Layout ────────────────────────────────────────────────────────────

        private void BuildLayout()
        {
            // Dim overlay
            var overlay = new ColorRect();
            overlay.Color              = new Color(0, 0, 0, 0.55f);
            overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            AddChild(overlay);

            _panel = new Panel();
            _panel.Size = new Vector2(PanelW, PanelH);
            _panel.AddThemeStyleboxOverride("panel", MakePanelStyle());
            AddChild(_panel);

            // Title
            _titleLabel = new Label();
            _titleLabel.Text = "ROUTES";
            _titleLabel.Position = new Vector2(24, 20);
            _titleLabel.AddThemeFontSizeOverride("font_size", 16);
            _titleLabel.AddThemeColorOverride("font_color", AccentColor);
            _panel.AddChild(_titleLabel);

            // Separator
            var sep = new ColorRect();
            sep.Color    = BorderColor;
            sep.Position = new Vector2(24, 46);
            sep.Size     = new Vector2(PanelW - 48, 1);
            _panel.AddChild(sep);

            // Body text
            _bodyLabel = new Label();
            _bodyLabel.Position                = new Vector2(24, 58);
            _bodyLabel.Size                    = new Vector2(PanelW - 48, 190);
            _bodyLabel.AutowrapMode            = TextServer.AutowrapMode.WordSmart;
            _bodyLabel.AddThemeFontSizeOverride("font_size", 12);
            _bodyLabel.AddThemeColorOverride("font_color", TextColor);
            _panel.AddChild(_bodyLabel);

            // Status line
            _statusLabel = new Label();
            _statusLabel.Position   = new Vector2(24, 255);
            _statusLabel.Size       = new Vector2(PanelW - 48, 22);
            _statusLabel.AddThemeFontSizeOverride("font_size", 11);
            _statusLabel.Visible    = false;
            _panel.AddChild(_statusLabel);

            // Buttons
            float btnY = 290;
            _exportBtn = MakeButton("OPEN A ROUTE  ( export )", 24, btnY, 220);
            _exportBtn.Pressed += OnExportPressed;
            _panel.AddChild(_exportBtn);

            _importBtn = MakeButton("RECEIVE TRAVELLERS  ( import )", 254, btnY, 220);
            _importBtn.Pressed += OnImportPressed;
            _panel.AddChild(_importBtn);

            _closeBtn = MakeButton("CLOSE", 24, 325, 80);
            _closeBtn.Pressed += Close;
            _panel.AddChild(_closeBtn);

            // File dialog
            _fileDialog = new FileDialog();
            _fileDialog.UseNativeDialog = true;
            _fileDialog.FileMode        = FileDialog.FileModeEnum.SaveFile;
            _fileDialog.Filters         = new[] { "*.json ; Migration Packet" };
            _fileDialog.FileSelected   += OnFileSelected;
            _fileDialog.Canceled       += () => { _pending = PendingAction.None; };
            AddChild(_fileDialog);
        }

        private void CentrePanel()
        {
            var vp = GetViewport().GetVisibleRect().Size;
            _panel.Position = new Vector2(
                (vp.X - PanelW) / 2f,
                (vp.Y - PanelH) / 2f
            );
        }

        private void RefreshBody()
        {
            int npcCount     = AinSoph.GameRoot.LiveNpcs.Count;
            int maxMigrants  = Math.Max(1, npcCount / 10);
            string worldName = AinSoph.GameRoot.WorldName;

            _bodyLabel.Text =
                $"A route connects two worlds. NPCs cross. They arrive as foreigners — permanently.\n\n" +
                $"OPEN A ROUTE — exports up to {maxMigrants} of your {npcCount} NPCs to a packet file.\n" +
                $"Share the file with another player. They import it. Those NPCs leave your world.\n\n" +
                $"RECEIVE TRAVELLERS — load a packet file from another player.\n" +
                $"Their NPCs enter your world as foreigners. They cannot create, kill, or pray.\n" +
                $"They can move, speak, and survive. Nothing more.\n\n" +
                $"Your world: {worldName}";
        }

        // ── Handlers ──────────────────────────────────────────────────────────

        private void OnExportPressed()
        {
            _pending = PendingAction.Export;
            _fileDialog.FileMode = FileDialog.FileModeEnum.SaveFile;
            _fileDialog.Title    = "Export migration packet";
            _fileDialog.CurrentFile = $"route_{AinSoph.GameRoot.WorldName}_{DateTime.UtcNow:yyyyMMdd}.json"
                .Replace(" ", "_");
            _fileDialog.Popup();
        }

        private void OnImportPressed()
        {
            _pending = PendingAction.Import;
            _fileDialog.FileMode = FileDialog.FileModeEnum.OpenFile;
            _fileDialog.Title    = "Import migration packet";
            _fileDialog.CurrentFile = string.Empty;
            _fileDialog.Popup();
        }

        private void OnFileSelected(string path)
        {
            switch (_pending)
            {
                case PendingAction.Export:
                    DoExport(path);
                    break;
                case PendingAction.Import:
                    DoImport(path);
                    break;
            }
            _pending = PendingAction.None;
        }

        private void DoExport(string path)
        {
            var routes = AinSoph.GameRoot.Routes;
            if (routes == null)
            {
                ShowStatus("Route system not ready.", error: true);
                return;
            }

            int count = routes.ExportMigrants(path);
            if (count == 0)
            {
                ShowStatus("No NPCs available to migrate.", error: true);
                return;
            }

            ShowStatus($"{count} NPC{(count == 1 ? "" : "s")} written to packet. Share the file.", error: false);
            RefreshBody(); // update the count shown
        }

        private void DoImport(string path)
        {
            var routes = AinSoph.GameRoot.Routes;
            if (routes == null)
            {
                ShowStatus("Route system not ready.", error: true);
                return;
            }

            // Parse the origin world name from the filename — best effort
            var filename   = System.IO.Path.GetFileNameWithoutExtension(path);
            var originName = filename.StartsWith("route_") ? filename[6..] : filename;
            // Strip date suffix if present (e.g. "route_WorldName_20260312")
            var parts = originName.Split('_');
            originName = parts.Length > 1 && int.TryParse(parts[^1], out _)
                ? string.Join("_", parts[..^1])
                : originName;

            // Spawn near the player's current position
            int spawnX = AinSoph.GameRoot.Player?.TileX ?? 0;
            int spawnY = AinSoph.GameRoot.Player?.TileY ?? 0;

            var immigrants = routes.ImportMigrants(path, originName, spawnX, spawnY);
            if (immigrants.Count == 0)
            {
                ShowStatus("No travellers found in packet.", error: true);
                return;
            }

            // Instantiate live NpcBrains and add to the world
            AinSoph.GameRoot.InstantiateForeigners(immigrants);

            ShowStatus($"{immigrants.Count} traveller{(immigrants.Count == 1 ? "" : "s")} arrived from {originName}.", error: false);
            RefreshBody();
        }

        private void ShowStatus(string text, bool error)
        {
            _statusLabel.Text    = text;
            _statusLabel.AddThemeColorOverride("font_color", error ? StatusErr : StatusOk);
            _statusLabel.Visible = true;
        }

        // ── Style helpers ─────────────────────────────────────────────────────

        private Button MakeButton(string text, float x, float y, float w)
        {
            var btn = new Button();
            btn.Text     = text;
            btn.Position = new Vector2(x, y);
            btn.Size     = new Vector2(w, 28);
            btn.AddThemeStyleboxOverride("normal",  MakeBtnStyle(new Color(0.12f, 0.12f, 0.10f)));
            btn.AddThemeStyleboxOverride("hover",   MakeBtnStyle(new Color(0.22f, 0.21f, 0.16f)));
            btn.AddThemeStyleboxOverride("pressed", MakeBtnStyle(new Color(0.18f, 0.17f, 0.13f)));
            btn.AddThemeFontSizeOverride("font_size", 11);
            btn.AddThemeColorOverride("font_color", TextColor);
            return btn;
        }

        private static StyleBoxFlat MakePanelStyle()
        {
            var s = new StyleBoxFlat();
            s.BgColor = BgColor;
            s.SetBorderWidthAll(1);
            s.BorderColor = BorderColor;
            s.ShadowColor = new Color(0, 0, 0, 0.7f);
            s.ShadowSize  = 8;
            return s;
        }

        private static StyleBoxFlat MakeBtnStyle(Color bg)
        {
            var s = new StyleBoxFlat();
            s.BgColor = bg;
            s.SetBorderWidthAll(1);
            s.BorderColor = new Color(0.35f, 0.33f, 0.28f);
            s.SetCornerRadiusAll(1);
            return s;
        }
    }
}
