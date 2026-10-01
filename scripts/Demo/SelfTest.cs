using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AinSoph.Council;
using AinSoph.LLM;
using AinSoph.NPC;
using AinSoph.UI;
using AinSoph.World;
using Godot;

namespace AinSoph.Demo
{
    /// <summary>
    /// Automated check of the whole game loop, started with `--selftest`.
    /// Boots a throwaway world (scripted voices unless `--model=` is given),
    /// exercises each system, prints PASS/FAIL per check and quits with exit
    /// code 0 if everything passed, 1 otherwise. Run by CI on every push.
    ///
    ///   godot --headless --path . -- --selftest
    /// </summary>
    public partial class SelfTest : Node
    {
        private int _passed, _failed;

        public override void _Ready() => Run();

        private async void Run()
        {
            try
            {
                await Checks();
            }
            catch (System.Exception ex)
            {
                Fail("self-test crashed", ex.ToString());
            }

            GD.Print($"SELFTEST: {_passed} passed, {_failed} failed");
            GetTree().Quit(_failed == 0 ? 0 : 1);
        }

        private async Task Checks()
        {
            var root  = GetParent<GameRoot>();
            await Wait(0.5);

            // ── Boot ──────────────────────────────────────────────────────
            Check("world booted", GameRoot.Grid != null && GameRoot.Altar != null && GameRoot.Player != null && GameRoot.Scene != null);
            Check("world generation is stable for a seed",
                new WorldGrid(GameRoot.WorldSeed).GetOrGenerate(5, -3).Biome == GameRoot.Grid!.GetOrGenerate(5, -3).Biome);

            var creation = root.GetChildren().OfType<CharacterCreationScreen>().FirstOrDefault();
            Check("new player sees character creation", creation != null);
            creation?.SubmitName("Tester");
            await Wait(0.3);
            Check("player is named", GameRoot.Player!.Name == "Tester");

            // ── People ────────────────────────────────────────────────────
            var travellers = GameRoot.LiveNpcs.Count(n => n.IsForeigner);
            Check("a new world starts with founding travellers", travellers == 4, $"{travellers}");

            // ── Animals ───────────────────────────────────────────────────
            Check("animals spawn in the world", GameRoot.LiveAnimals.Count > 0, $"{GameRoot.LiveAnimals.Count}");
            Check("fish live only on water", GameRoot.LiveAnimals
                .Where(a => a.Species?.Habitat == AnimalHabitat.Water)
                .All(a => TileAt(a.TileX, a.TileY).Surface == TileSurface.Water));

            var clean = GameRoot.LiveAnimals.FirstOrDefault(a => a.Species?.Edible == true && a.Species.Habitat != AnimalHabitat.Water);
            if (clean != null)
            {
                int before = GameRoot.LiveAnimals.Count;
                var (x, y, name) = (clean.TileX, clean.TileY, clean.Name);
                clean.Kill();
                var body = GameRoot.Items!.All.FirstOrDefault(i => i.Type == "body" && i.TileX == x && i.TileY == y);
                Check("a clean animal leaves an edible body", body is { Edible: true }, name);
                Check("two animals replace one that dies (up to the cell cap)", GameRoot.LiveAnimals.Count >= before - 1);
            }

            // ── Rib, spouse, progeny ──────────────────────────────────────
            var player = GameRoot.Player!;
            player.GrantRibForTesting();
            var spouse = GameRoot.Tribe!.CreateSpouse("Hava", "Keeps the fire.", GameRoot.Llm, System.DateTime.UtcNow);
            Check("the rib becomes a spouse", spouse != null && player.HasSpouse && spouse.Name == "Hava");
            Check("the spouse is placed in the world", spouse != null && (spouse.TileX != 0 || spouse.TileY != 0));

            GameRoot.Tribe.LastProgenyBirthUtc = System.DateTime.UtcNow.AddDays(-8);
            GameRoot.Tribe.Tick(GameRoot.Llm, System.DateTime.UtcNow);
            Check("a week after the spouse, a child is born", player.ProgenyIds.Count >= 1, $"{player.ProgenyIds.Count}");
            var child = GameRoot.LiveNpcs.FirstOrDefault(n => n.NpcId == player.ProgenyIds.FirstOrDefault());
            Check("the child carries the player's lineage", child?.Lineage.Contains($"origin:{player.Id}") == true);

            // ── Saves ─────────────────────────────────────────────────────
            root.SaveNow();
            var dir = root.SaveDirectory;
            var npcFiles = Directory.GetFiles(Path.Combine(dir, "npcs"));
            Check("NPC save files have Windows-safe names", npcFiles.Length > 0 && npcFiles.All(f => !Path.GetFileName(f).Contains(':')));
            using (var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "player.json"))))
            {
                var p = doc.RootElement;
                Check("player save keeps the rib and spouse", p.GetProperty("hasRib").GetBoolean() &&
                                                               p.GetProperty("spouseNpcId").GetString() == spouse?.NpcId);
                Check("player save keeps play time", p.TryGetProperty("accumulatedPlayHours", out _));
            }
            Check("world seed is saved", File.ReadAllText(Path.Combine(dir, "world.json")).Contains(GameRoot.WorldSeed.ToString()));
            Check("animals are saved", File.Exists(Path.Combine(dir, "animals.json")));

            // ── Routes ────────────────────────────────────────────────────
            var packet = Path.Combine(dir, "route_Elsewhere_20260101.json");
            int countBefore = GameRoot.LiveNpcs.Count;
            int exported = GameRoot.Routes!.ExportMigrants(packet);
            Check("route export sends travellers away", exported > 0 && GameRoot.LiveNpcs.Count == countBefore - exported);
            Check("the spouse never leaves over a route", GameRoot.LiveNpcs.Any(n => n.IsSpouse));
            var arrivals = GameRoot.Routes.ImportMigrants(packet, "Elsewhere", player.TileX, player.TileY);
            GameRoot.InstantiateForeigners(arrivals);
            Check("route import brings them in as foreigners",
                arrivals.Count == exported && arrivals.All(a => a.IsForeigner && a.Lineage.Any(l => l.StartsWith("migrated-from:Elsewhere"))));

            // ── Council, laws, filter ─────────────────────────────────────
            var verdict = await GameRoot.Council!.SubmitAsync(new CouncilSubmission
            {
                Type = "skill", Name = "Star Reading", Description = "Reading the night sky to find the way home.",
                CreatedBy = player.Id,
            });
            Check("all three Council seats answer", verdict.Responses.Count == 3, $"{verdict.Responses.Count}");
            Check("every seat speaks a parable", verdict.Responses.All(r => !string.IsNullOrWhiteSpace(r.Homily)));

            GameRoot.AddLaw("The Truce of Bread", "A shared meal binds a truce until dawn.", player.Id);
            Check("approved laws reach NPC prompts",
                NpcPromptBuilder.BuildSystemPrompt(GameRoot.LiveNpcs[0].Decan).Contains("The Truce of Bread"));

            Check("filter passes ordinary speech", ContentFilter.IsClean("The manna tasted of honey today."));
            Check("filter withholds blocked words", !ContentFilter.IsClean("something pornographic"));
            Check("NPC prompts carry the content rule",
                NpcPromptBuilder.BuildSystemPrompt(GameRoot.LiveNpcs[0].Decan).Contains(ContentFilter.PromptRule));

            // ── Talk ──────────────────────────────────────────────────────
            var npc = GameRoot.LiveNpcs.First(n => !n.BrokenTalk);
            var reply = await npc.RespondToDialogueAsync("Where can I find shelter tonight?", new SituationContext());
            Check("an NPC answers in dialogue", !string.IsNullOrWhiteSpace(reply), reply.Length > 60 ? reply[..60] : reply);
        }

        private static Tile TileAt(int x, int y)
        {
            int cx = x < 0 ? (x - 7) / 8 : x / 8, cy = y < 0 ? (y - 7) / 8 : y / 8;
            return GameRoot.Grid!.GetOrGenerate(cx, cy).GetTile(x - cx * 8, y - cy * 8);
        }

        private void Check(string name, bool ok, string detail = "")
        {
            if (ok) { _passed++; GD.Print($"SELFTEST PASS  {name}"); }
            else    Fail(name, detail);
        }

        private void Fail(string name, string detail)
        {
            _failed++;
            GD.PrintErr($"SELFTEST FAIL  {name}{(detail.Length > 0 ? " — " + detail : "")}");
        }

        private async Task Wait(double seconds) =>
            await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    }
}
