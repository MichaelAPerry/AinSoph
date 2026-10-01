using Godot;
using System.Linq;
using System.Threading.Tasks;
using FileAccess = Godot.FileAccess;

namespace AinSoph.UI
{
    /// <summary>
    /// Shown on first launch (or if the model file is missing from user://).
    /// Extracts the bundled GGUF from the PCK to user://models/ so LLamaSharp
    /// can open it as a real filesystem path.
    ///
    /// Extraction is a one-time operation. ~1 GB copy.
    /// Subsequent launches skip straight to boot.
    /// </summary>
    public partial class ModelBootScreen : CanvasLayer
    {
        // Qwen 2.5 1.5B Instruct, Q4_K_M — Apache 2.0, so it can ship in a commercial build
        public  const string ModelFileName = "qwen2.5-1.5b.gguf";
        private const string BundledPath   = "res://models/" + ModelFileName;
        private const string ExtractedDir  = "user://models/";
        private const string ExtractedPath = ExtractedDir + ModelFileName;

        private const int ChunkSize = 1024 * 1024; // 1MB per frame

        private Label      _titleLabel;
        private Label      _statusLabel;
        private ProgressBar _bar;

        private bool   _done    = false;
        private bool   _started = false;

        // Called by GameRoot before any other boot step.
        // Returns the real filesystem path once extraction is complete,
        // or an empty string for demo mode (no model).
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
            _statusLabel.Size     = new Vector2(vp.X, 48);
            AddChild(_statusLabel);

            var version = new Label();
            version.Text = $"v{GameSettings.Version}";
            version.HorizontalAlignment = HorizontalAlignment.Right;
            version.AddThemeFontSizeOverride("font_size", 11);
            version.AddThemeColorOverride("font_color", new Color(0.35f, 0.34f, 0.30f));
            version.Position = new Vector2(vp.X - 216, vp.Y - 28);
            version.Size     = new Vector2(200, 18);
            AddChild(version);

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
            // --demo forces scripted NPCs/Council; --model=<path> points at any .gguf
            var args = OS.GetCmdlineUserArgs().Concat(OS.GetCmdlineArgs()).ToArray();
            // The tour and the self-test use scripted voices unless a model is named
            bool scriptedRun = (args.Contains("--demo-tour") || args.Contains("--selftest")) &&
                               !args.Any(a => a.StartsWith("--model="));
            if (args.Contains("--demo") || scriptedRun)
            {
                await StartDemo("Demo mode — scripted voices, no AI model.");
                return;
            }

            var modelArg = args.FirstOrDefault(a => a.StartsWith("--model="));
            if (modelArg != null)
            {
                var path = modelArg["--model=".Length..];
                if (System.IO.File.Exists(path))
                {
                    _statusLabel.Text = "Loading model...";
                    await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
                    Finish(path);
                    return;
                }
                GD.PrintErr($"ModelBootScreen: --model path not found: {path}");
            }

            // Shipped as a loose file next to the game (e.g. a Steam depot) — use it in place,
            // no extraction and no second copy on disk
            var besideExe = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(OS.GetExecutablePath()) ?? "", "models", ModelFileName);
            if (!OS.HasFeature("editor") && System.IO.File.Exists(besideExe))
            {
                _statusLabel.Text = "Loading...";
                await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
                Finish(besideExe);
                return;
            }

            var realPath = ProjectSettings.GlobalizePath(ExtractedPath);

            // Already extracted — proceed immediately
            if (System.IO.File.Exists(realPath))
            {
                _statusLabel.Text = "Loading...";
                await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
                Finish(realPath);
                return;
            }

            // No bundled model — still boot, with scripted voices instead of the LLM
            if (!FileAccess.FileExists(BundledPath))
            {
                GD.Print("ModelBootScreen: bundled model not found at " + BundledPath + " — starting in demo mode");
                await StartDemo(
                    "No AI model found — starting in demo mode.\n" +
                    $"Place {ModelFileName} in res://models/ for living NPCs.");
                return;
            }

            // Extract
            _statusLabel.Text = "First launch — preparing the AI model (~1 GB)...";
            _bar.Visible      = true;

            DirAccess.MakeDirRecursiveAbsolute(
                ProjectSettings.GlobalizePath(ExtractedDir));

            await Task.Run(() => ExtractSync(realPath));

            _statusLabel.Text = "Done.";
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
            Finish(realPath);
        }

        private async Task StartDemo(string message)
        {
            _statusLabel.Text = message;
            await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
            Finish(string.Empty);
        }

        private void ExtractSync(string destPath)
        {
            // Write to a temp file and rename at the end, so an interrupted
            // extraction is never mistaken for a complete model next launch
            var partPath = destPath + ".part";
            using (var src  = FileAccess.Open(BundledPath, FileAccess.ModeFlags.Read))
            using (var dest = System.IO.File.Create(partPath))
            {
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
            System.IO.File.Move(partPath, destPath, overwrite: true);
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
