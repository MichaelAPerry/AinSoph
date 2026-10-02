using System.Linq;
using System.Threading.Tasks;
using AinSoph.Skills;
using AinSoph.UI;
using AinSoph.World;
using Godot;

namespace AinSoph.Demo
{
    /// <summary>
    /// Stages the raw footage for the trailer, started with `--trailer`.
    ///
    /// Plays a hands-free run with no captions and no HUD — night, dawn, a
    /// walk, an NPC, the Council, the rib, a birth, nightfall and a lion — and
    /// prints `TRAILER-MARK name frame` at each beat so tools/trailer/ can cut
    /// it. Staged: night is forced, the lion is placed and its kill is certain.
    /// Everything else is the game running (with --model=, the real AI).
    ///
    ///   godot --path . --resolution 1920x1080 --write-movie raw.avi --fixed-fps 30 -- --trailer
    /// </summary>
    public partial class TrailerDirector : Node
    {
        public override void _Ready()
        {
            Audio.Sound.SetBusVolume("Music", 0f);    // the trailer has its own score
            Audio.Sound.SetBusVolume("Ambience", 0f);
            RunSafely();
        }

        private async void RunSafely()
        {
            try { await Run(); }
            catch (System.Exception ex)
            {
                GD.PrintErr($"TrailerDirector: failed — {ex}");
                GetTree().Quit(1);
            }
        }

        private async Task Run()
        {
            await Wait(0.5);
            var scene = GameRoot.Scene;
            var root  = GetParent<GameRoot>();
            if (scene == null || GameRoot.Player == null) { GetTree().Quit(1); return; }

            var creation = root.GetChildren().OfType<CharacterCreationScreen>().FirstOrDefault();
            creation?.SubmitName("Miriam");
            await Wait(1.0);

            scene.SetHudVisible(false);
            var cam = scene.Camera;
            cam.PositionSmoothingEnabled = true;
            cam.PositionSmoothingSpeed   = 3.5f;

            // Night — a small circle of sight in the dark
            WorldClock.ForceNight = true;
            scene.RefreshMap();
            Zoom(0.75f); // pulled back so the circle of sight reads against the dark
            Mark("night");
            await Walk(Vector2I.Right, 6, 0.6);
            await Wait(1.0);

            // Dawn — the world opens out
            WorldClock.ForceNight = false;
            scene.RefreshMap();
            Mark("dawn");
            await Wait(2.5);
            Zoom(1.0f);
            Mark("walk");
            await Walk(Vector2I.Right, 5, 0.32);
            await Walk(Vector2I.Down, 4, 0.32);
            await Walk(Vector2I.Right, 5, 0.32);
            Zoom(0.75f);
            Mark("wide");
            await Wait(4.0);
            Zoom(1.0f);

            // An NPC, close
            var npc = GameRoot.LiveNpcs.OrderBy(n => Dist(n.TileX, n.TileY)).FirstOrDefault();
            if (npc != null)
            {
                await WalkNextTo(() => new Vector2I(npc.TileX, npc.TileY));
                Zoom(1.5f);
                Mark("npc");
                await Wait(3.0);

                scene.OpenMenuOn(npc.NpcId);
                Mark("menu");
                await Wait(2.5);
                scene.CloseMenu();

                await WalkNextTo(() => new Vector2I(npc.TileX, npc.TileY));
                root.UsePrimitive(npc.NpcId, SkillType.Talk);
                await Wait(0.8);
                Mark("talk");
                // A few questions; the edit quotes whichever answer reads best
                string[] questions = { "What do you pray for?", "What happens to us when we die?", "Do you remember me?" };
                for (int q = 0; q < questions.Length; q++)
                {
                    await Say(scene, questions[q]);
                    await WaitForReply(scene, "…");
                    Mark(q == 0 ? "reply" : $"reply{q + 1}");
                    await Wait(5.0);
                }
                scene.CloseDialogue();
                await Wait(0.5);
            }

            // Eat, then reap a beast
            var manna = GameRoot.Items?.All.Where(i => i.Edible).OrderBy(i => Dist(i.TileX, i.TileY)).FirstOrDefault();
            if (manna != null && Dist(manna.TileX, manna.TileY) <= 14)
            {
                await WalkNextTo(() => new Vector2I(manna.TileX, manna.TileY));
                Mark("eat");
                root.UsePrimitive(manna.Id, SkillType.Reap);
                await Wait(2.5);
            }
            var beast = GameRoot.LiveAnimals
                .Where(a => a.AnimalType != NPC.AnimalType.Predator && a.Species?.Habitat != AnimalHabitat.Water)
                .OrderBy(a => Dist(a.TileX, a.TileY)).FirstOrDefault();
            if (beast != null && Dist(beast.TileX, beast.TileY) <= 14)
            {
                await WalkNextTo(() => new Vector2I(beast.TileX, beast.TileY));
                Mark("reap");
                if (Dist(beast.TileX, beast.TileY) <= 1) root.UsePrimitive(beast.AnimalId, SkillType.Reap);
                await Wait(2.5);
            }

            // The altar and the Council
            if (GameRoot.Altar is { } altar)
            {
                ParseCell(altar.CellId, out var ax, out var ay);
                cam.PositionSmoothingEnabled = false;
                scene.MovePlayerTo(new Vector2I(ax * 8 + altar.TileX, ay * 8 + altar.TileY + 1));
                await Wait(0.1);
                cam.PositionSmoothingEnabled = true;
                Zoom(1.5f);
                Mark("altar");
                await Wait(3.5);
                root.UsePrimitive("altar", SkillType.Pray);
                await Wait(0.8);
                Mark("pray");
                // Ask again (up to three petitions) until two seats say yes
                string[] petitions =
                {
                    "Grant me a skill: Fire Making - to keep my family warm through the night.",
                    "Grant me a skill: Star Reading - to find my way home by the night sky.",
                    "Grant me a skill: Healing - to mend the wounds of those I love.",
                };
                foreach (var petition in petitions)
                {
                    await Say(scene, petition);
                    await WaitForReply(scene, "The Council deliberates…");
                    Mark("verdict");
                    await Wait(8.0);
                    var yes = scene.DialogueSpeech.Split("YES").Length - 1;
                    GD.Print($"TrailerDirector: Council said yes {yes} times");
                    scene.CloseDialogue();
                    if (yes >= 2) break;
                    await Wait(0.5);
                    root.UsePrimitive("altar", SkillType.Pray);
                    await Wait(0.8);
                    Mark("pray");
                }
                Mark("granted");
                await Wait(3.0);
            }

            // The rib, the spouse, a child
            if (GameRoot.Player is { HasRib: true, HasSpouse: false })
            {
                root.UseRib();
                await Wait(0.8);
                var rib = root.GetChildren().OfType<SpouseCreationScreen>().FirstOrDefault();
                if (rib != null)
                {
                    Mark("rib");
                    const string name = "Hava";
                    const string desc = "Quick to laugh, slow to forgive. Keeps the fire. Knows every star by name.";
                    for (int i = 1; i <= name.Length; i++) { rib.Type(name[..i], ""); await Wait(0.12); }
                    for (int i = 1; i <= desc.Length; i++) { rib.Type(name, desc[..i]); await Wait(0.03); }
                    await Wait(1.5);
                    rib.Submit(name, desc);
                    Mark("spouse");
                    await Wait(3.5);
                    root.StageBirth();
                    Mark("birth");
                    await Wait(4.0);
                }
            }

            // Nightfall, hunger, and the lion
            Zoom(0.75f);
            WorldClock.ForceNight = true;
            scene.RefreshMap();
            Mark("nightfall");
            await Wait(2.5);
            scene.ShowWorldText("⚠ You must eat within the hour. ⚠");
            Audio.Sound.Play("warning");
            Mark("hunger");
            await Wait(3.0);

            Zoom(1.5f);
            var p = scene.PlayerTile;
            var lion = root.StageAnimal("lion", p.X + 4, p.Y);
            if (lion != null)
            {
                Mark("lion");
                await Wait(1.2);
                for (int i = 0; i < 6 && Dist(lion.TileX, lion.TileY) > 1; i++)
                {
                    lion.TileX -= System.Math.Sign(lion.TileX - scene.PlayerTile.X);
                    scene.MoveNpc(lion.AnimalId, lion.TileX, lion.TileY);
                    await Wait(0.7);
                }
                scene.ShowNpcSpeech(lion.AnimalId, "!");
                Audio.Sound.Play("reap");
                Mark("attack");
                await Wait(0.8);
                root.StagePredatorKill(lion);
                Mark("death");
                await Wait(5.0);
            }

            WorldClock.ForceNight = null;
            Mark("end");
            GD.Print("TrailerDirector: done");
            GetTree().Quit();
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static void Mark(string name)
        {
            GD.Print($"TRAILER-MARK {name} {Engine.GetFramesDrawn()}");
            if (GameRoot.Scene is { DialogueOpen: true } scene && (name.StartsWith("reply") || name == "verdict"))
                GD.Print($"TRAILER-TEXT {name} {scene.DialogueSpeech.Replace("\n", " | ")}");
        }

        private static void Zoom(float z) => GameRoot.Scene!.Camera.Zoom = new Vector2(z, z);

        private async Task Walk(Vector2I dir, int steps, double pause)
        {
            for (int i = 0; i < steps; i++) { GameRoot.Scene!.Step(dir); await Wait(pause); }
        }

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
                await Wait(0.26);
            }
        }

        private async Task Say(WorldScene scene, string text)
        {
            for (int i = 1; i <= text.Length; i++) { scene.TypeDialogue(text[..i]); await Wait(0.035); }
            await Wait(0.4);
            scene.SubmitDialogue(text);
        }

        private async Task WaitForReply(WorldScene scene, string placeholder, double timeout = 240)
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

        private static void ParseCell(string id, out int x, out int y)
        {
            x = 0; y = 0;
            var parts = id.Split(',');
            if (parts.Length == 2) { int.TryParse(parts[0], out x); int.TryParse(parts[1], out y); }
        }
    }
}
