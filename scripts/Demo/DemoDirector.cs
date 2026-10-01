using System.Linq;
using System.Threading.Tasks;
using AinSoph.Skills;
using AinSoph.UI;
using Godot;

namespace AinSoph.Demo
{
    /// <summary>
    /// Scripted walkthrough of the game, started with `--demo-tour`.
    ///
    /// Plays the core loop hands-free — name a character, walk, talk to an NPC,
    /// eat manna, pray at the altar — with captions, so it can be recorded:
    ///
    ///   godot --path . --write-movie demo.avi --fixed-fps 30 -- --demo-tour
    ///
    /// `--shots=<dir>` also saves a PNG at each step. The tour quits when done.
    /// </summary>
    public partial class DemoDirector : Node
    {
        private CanvasLayer    _captionLayer = null!;
        private PanelContainer _captionPanel = null!;
        private Label          _caption      = null!;
        private string?     _shotDir;
        private int         _shotIndex;

        public override void _Ready()
        {
            var args = OS.GetCmdlineUserArgs().Concat(OS.GetCmdlineArgs());
            var shots = args.FirstOrDefault(a => a.StartsWith("--shots="));
            if (shots != null)
            {
                _shotDir = shots["--shots=".Length..];
                DirAccess.MakeDirRecursiveAbsolute(_shotDir);
            }

            BuildCaption();
            RunTour();
        }

        // ── The tour ──────────────────────────────────────────────────────

        private async void RunTour()
        {
            await Wait(0.5);
            var scene = GameRoot.Scene;
            var root  = GetParent<GameRoot>();
            if (scene == null || GameRoot.Player == null)
            {
                GD.PrintErr("DemoDirector: world not ready — tour aborted");
                GetTree().Quit(1);
                return;
            }

            // 1. Character creation
            var creation = root.GetChildren().OfType<CharacterCreationScreen>().FirstOrDefault();
            if (creation != null)
            {
                Caption("A new soul descends into the world.");
                await Wait(3.2);
                await Shot("creation");
                creation.SubmitName("Miriam");
                await Wait(2.2);
            }

            // 2. Walk
            Caption("Walk with WASD, the arrow keys, or left-click.  Fog lifts as you see.");
            await Wait(0.8);
            foreach (var dir in new[] { Vector2I.Right, Vector2I.Right, Vector2I.Down, Vector2I.Down,
                                        Vector2I.Left, Vector2I.Up })
            {
                scene.Step(dir);
                await Wait(0.28);
            }
            await Shot("world");
            await Wait(1.2);

            // 3. NPCs
            var npc = GameRoot.LiveNpcs
                .OrderBy(n => Dist(n.TileX, n.TileY))
                .FirstOrDefault();
            if (npc != null)
            {
                Caption($"NPCs live their own lives. This one's nature is \"{npc.Decan.Name}\" — one of 72.");
                await WalkNextTo(() => new Vector2I(npc.TileX, npc.TileY));
                await Wait(1.5);
                await Shot("npcs");

                Caption("Click any being to see the six primitives:  Move · See · Hear · Talk · Reap · Pray");
                scene.OpenMenuOn(npc.NpcId);
                await Wait(2.2);
                await Shot("primitives");
                scene.CloseMenu();

                Caption("Talk — every NPC answers in character.");
                await WalkNextTo(() => new Vector2I(npc.TileX, npc.TileY)); // they may have wandered
                root.UsePrimitive(npc.NpcId, SkillType.Talk);
                await Wait(1.0);
                await Say(scene, "Where can I find shelter tonight?");
                await WaitForReply(scene, "…");
                await Wait(2.5);
                await Shot("dialogue");
                await Say(scene, "Will you walk with me?");
                await WaitForReply(scene, "…");
                await Wait(2.5);
                scene.CloseDialogue();
                await Wait(0.6);
            }

            // 4. Eat
            var manna = GameRoot.Items?.All
                .Where(i => i.Edible)
                .OrderBy(i => Dist(i.TileX, i.TileY))
                .FirstOrDefault();
            if (manna != null && Dist(manna.TileX, manna.TileY) <= 14)
            {
                Caption("Manna falls each morning. Eat once a day or die.  Reap is how you eat.");
                await WalkNextTo(manna.TileX, manna.TileY);
                await Wait(0.6);
                root.UsePrimitive(manna.Id, SkillType.Reap);
                await Wait(2.4);
                await Shot("eat");
            }

            // 5. The altar
            var altar = GameRoot.Altar;
            if (altar != null)
            {
                Caption("One altar per world, hidden and unmarked.  (The tour takes a shortcut.)");
                await Wait(2.4);
                ParseCell(altar.CellId, out var ax, out var ay);
                scene.MovePlayerTo(new Vector2I(ax * 8 + altar.TileX, ay * 8 + altar.TileY + 1));
                await Wait(1.6);
                await Shot("altar");

                Caption("Pray at the altar and the Triune Council answers — in parable.");
                root.UsePrimitive("altar", SkillType.Pray);
                await Wait(1.2);
                await Say(scene, "Grant me a skill: Star Reading - to find my way home by the night sky.");
                await WaitForReply(scene, "The Council deliberates…");
                await Wait(4.5);
                await Shot("council");
                scene.CloseDialogue();
            }

            Caption("Ain Soph — the boundless.  Nothing phones home.");
            await Wait(2.5);
            GD.Print("DemoDirector: tour complete");
            GetTree().Quit();
        }

        public override void _Process(double delta)
        {
            // Drop to just above the input box while a dialogue is open
            if (_captionPanel != null)
                _captionPanel.Position = new Vector2(0, GameRoot.Scene?.DialogueOpen == true ? 572 : 0);
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private Task WalkNextTo(int tx, int ty) => WalkNextTo(() => new Vector2I(tx, ty));

        /// <summary>Walk until adjacent to a target that may itself be moving.</summary>
        private async Task WalkNextTo(System.Func<Vector2I> target)
        {
            var scene = GameRoot.Scene!;
            for (int i = 0; i < 40 && Dist(target().X, target().Y) > 1; i++)
            {
                var (tx, ty) = (target().X, target().Y);
                var p  = scene.PlayerTile;
                var dx = System.Math.Sign(tx - p.X);
                var dy = System.Math.Sign(ty - p.Y);
                scene.Step(System.Math.Abs(tx - p.X) >= System.Math.Abs(ty - p.Y) && dx != 0
                    ? new Vector2I(dx, 0) : new Vector2I(0, dy));
                await Wait(0.22);
            }
        }

        private async Task Say(WorldScene scene, string text)
        {
            for (int i = 1; i <= text.Length; i++)
            {
                scene.TypeDialogue(text[..i]);
                await Wait(0.035);
            }
            await Wait(0.4);
            scene.SubmitDialogue(text);
        }

        /// <summary>Wait until the speech box moves past a placeholder (the model is answering).</summary>
        private async Task WaitForReply(WorldScene scene, string placeholder, double timeout = 180)
        {
            await Wait(0.3);
            for (double t = 0; t < timeout && scene.DialogueSpeech == placeholder; t += 0.25)
                await Wait(0.25);
        }

        private static int Dist(int tx, int ty)
        {
            var p = GameRoot.Scene!.PlayerTile;
            return System.Math.Max(System.Math.Abs(tx - p.X), System.Math.Abs(ty - p.Y));
        }

        private async Task Wait(double seconds) =>
            await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

        private async Task Shot(string name)
        {
            if (_shotDir == null) return;
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var image = GetViewport().GetTexture().GetImage();
            image.SavePng($"{_shotDir}/{++_shotIndex:D2}-{name}.png");
        }

        private void Caption(string text)
        {
            _caption.Text = text;
            GD.Print($"DemoDirector: {text}");
        }

        private void BuildCaption()
        {
            _captionLayer = new CanvasLayer { Layer = 50 };
            AddChild(_captionLayer);

            var panel = _captionPanel = new PanelContainer();
            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.03f, 0.03f, 0.03f, 0.82f),
                ContentMarginLeft = 18, ContentMarginRight = 18,
                ContentMarginTop = 10, ContentMarginBottom = 10,
            };
            style.SetBorderWidthAll(0);
            style.BorderWidthBottom = 2;
            style.BorderColor = new Color(0.7f, 0.62f, 0.4f);
            panel.AddThemeStyleboxOverride("panel", style);
            panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
            _captionLayer.AddChild(panel);

            _caption = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            _caption.AddThemeFontSizeOverride("font_size", 17);
            _caption.AddThemeColorOverride("font_color", new Color(0.93f, 0.88f, 0.72f));
            panel.AddChild(_caption);
        }

        private static void ParseCell(string id, out int x, out int y)
        {
            x = 0; y = 0;
            var parts = id.Split(',');
            if (parts.Length == 2) { int.TryParse(parts[0], out x); int.TryParse(parts[1], out y); }
        }
    }
}
