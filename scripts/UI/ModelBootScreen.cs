using Godot;
using System.Threading.Tasks;

namespace AinSoph.UI
{
    /// <summary>
    /// Shown on first launch (or if the model file is missing from user://).
    /// Extracts the bundled GGUF from the PCK to user://models/ so LLamaSharp
    /// can open it as a real filesystem path.
    ///
    /// Extraction is a one-time operation. ~1.9GB copy.
    /// Subsequent launches skip straight to boot.
    /// </summary>
    public partial class ModelBootScreen : CanvasLayer
    {
        private const string BundledPath  = "res://models/qwen2.5-3b.gguf";
        private const string ExtractedDir = "user://models/";
        private const string ExtractedPath = "user://models/qwen2.5-3b.gguf";

        private const int ChunkSize = 1024 * 1024; // 1MB per frame

        private Label      _titleLabel;
        private Label      _statusLabel;
        private ProgressBar _bar;

        private bool   _done    = false;
        private bool   _started = false;

        // Called by GameRoot before any other boot step.
        // Returns the real filesystem path once extraction is complete.
        public delegate void ReadyCallback(string realPath);
        public event ReadyCallback? OnReady;

        public override void _Ready()
        {
            Layer = 99;
            BuildLayout();
        }

        public override void _Process(double delta)
        {
            if (!_started)
            {
                _started = true;
                CheckAndExtract();
            }
        }

        // ── Layout ────────────────────────────────────────────────────────────

        private void BuildLayout()
        {
            var bg = new ColorRect();
            bg.Color = new Color(0.04f, 0.04f, 0.04f, 1f);
            bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            AddChild(bg);

            var vp = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1280, 720);

            _titleLabel = new Label();
            _titleLabel.Text = "AIN SOPH";
            _titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _titleLabel.AddThemeFontSizeOverride("font_size", 32);
            _titleLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.68f, 0.55f));
            _titleLabel.Position = new Vector2(0, vp.Y * 0.35f);
            _titleLabel.Size     = new Vector2(vp.X, 48);
            AddChild(_titleLabel);

            _statusLabel = new Label();
            _statusLabel.Text = "Preparing the world...";
            _statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _statusLabel.AddThemeFontSizeOverride("font_size", 13);
            _statusLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.48f, 0.42f));
            _statusLabel.Position = new Vector2(0, vp.Y * 0.50f);
            _statusLabel.Size     = new Vector2(vp.X, 24);
            AddChild(_statusLabel);

            _bar = new ProgressBar();
            _bar.MinValue  = 0;
            _bar.MaxValue  = 100;
            _bar.Value     = 0;
            _bar.ShowPercentage = false;
            _bar.Position  = new Vector2(vp.X * 0.25f, vp.Y * 0.56f);
            _bar.Size      = new Vector2(vp.X * 0.5f, 8);
            _bar.Visible   = false;
            AddChild(_bar);
        }

        // ── Extraction logic ──────────────────────────────────────────────────

        private async void CheckAndExtract()
        {
            var realPath = ProjectSettings.GlobalizePath(ExtractedPath);

            // Already extracted — proceed immediately
            if (System.IO.File.Exists(realPath))
            {
                _statusLabel.Text = "Loading...";
                await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
                Finish(realPath);
                return;
            }

            // Check the bundled file exists in the PCK
            if (!FileAccess.FileExists(BundledPath))
            {
                _statusLabel.Text =
                    "Model file not found.\n" +
                    "Place qwen2.5-3b.gguf in res://models/ and re-export.";
                _statusLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.3f, 0.3f));
                GD.PrintErr("ModelBootScreen: bundled model not found at " + BundledPath);
                return;
            }

            // Extract
            _statusLabel.Text = "First launch — extracting AI model (~1.9 GB)...";
            _bar.Visible      = true;

            DirAccess.MakeDirRecursiveAbsolute(
                ProjectSettings.GlobalizePath(ExtractedDir));

            await Task.Run(() => ExtractSync(realPath));

            _statusLabel.Text = "Done.";
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
            Finish(realPath);
        }

        private void ExtractSync(string destPath)
        {
            using var src  = FileAccess.Open(BundledPath, FileAccess.ModeFlags.Read);
            using var dest = System.IO.File.OpenWrite(destPath);

            long total   = (long)src.GetLength();
            long written = 0;

            while (written < total)
            {
                int chunk  = (int)System.Math.Min(ChunkSize, total - written);
                var buffer = src.GetBuffer(chunk);
                dest.Write(buffer, 0, buffer.Length);
                written += buffer.Length;

                float pct = (float)written / total * 100f;
                CallDeferred(MethodName.UpdateProgress, pct,
                    $"Extracting... {written / (1024 * 1024)} MB / {total / (1024 * 1024)} MB");
            }
        }

        private void UpdateProgress(float pct, string status)
        {
            _bar.Value        = pct;
            _statusLabel.Text = status;
        }

        private void Finish(string realPath)
        {
            _done = true;
            OnReady?.Invoke(realPath);
            QueueFree();
        }
    }
}
