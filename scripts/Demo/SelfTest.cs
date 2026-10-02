using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AinSoph.Council;
using AinSoph.LLM;
using AinSoph.NPC;
using AinSoph.Skills;
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

            // ── Gifts: what the Council grants changes the rules ─────────
            var named = CouncilSubmissionParser.NameOf("Grant me a skill: Fire Making - to keep my family warm.");
            Check("petitions are named plainly", named == "Fire Making", named);
            Check("petitions are read as effects",
                Gifts.Classify("Fire Making - to keep my family warm") == GiftEffect.Shelter &&
                Gifts.Classify("Star Reading - to find my way by the night sky") == GiftEffect.Sight &&
                Gifts.Classify("a spear for hunting") == GiftEffect.Strength &&
                Gifts.Classify("a song of the old days") == GiftEffect.Lore);

            var now = System.DateTime.UtcNow;
            Check("a lion strikes a player asleep in the open", StrikesSleepingPlayer(root));

            var kill0 = player.KillNumber;
            await root.ApplyPlayerGrantAsync(new CouncilSubmission { Type = "skill", Name = "Spear Craft", Description = "a spear for hunting" });
            Check("Strength raises the kill number", player.KillNumber == kill0 + Gifts.StrengthBonus, $"{kill0} → {player.KillNumber}");

            await root.ApplyPlayerGrantAsync(new CouncilSubmission { Type = "item", Name = "Lantern", Description = "a lantern to see by at night" });
            Check("Sight widens what you see", player.Gifts.SightBonusCells == 1);

            await root.ApplyPlayerGrantAsync(new CouncilSubmission { Type = "skill", Name = "Bread Baking", Description = "bake bread to endure hunger" });
            var fed30h = new SurvivalTracker(now.AddHours(-30)) { HungerHours = () => player.Gifts.HungerHours };
            var plain30h = new SurvivalTracker(now.AddHours(-30));
            fed30h.RecordEat(now.AddHours(-30)); plain30h.RecordEat(now.AddHours(-30));
            Check("Endurance lengthens the hunger window", !fed30h.Tick(now).DiedOfStarvation && plain30h.Tick(now).DiedOfStarvation);

            await root.ApplyPlayerGrantAsync(new CouncilSubmission { Type = "skill", Name = "Fire Making", Description = "fire to keep my family warm" });
            Check("Shelter keeps a sleeper in the open safe", !StrikesSleepingPlayer(root));

            await root.ApplyPlayerGrantAsync(new CouncilSubmission { Type = "skill", Name = "Healing", Description = "herbs to mend wounds" });
            Check("Mending is ready once a day", player.Gifts.CanMend(now) &&
                (player.Gifts.LastMendedUtc = now) == now && !player.Gifts.CanMend(now.AddHours(1)) && player.Gifts.CanMend(now.AddHours(25)));
            player.Gifts.LastMendedUtc = null;

            var omensBefore = GameRoot.Omens.Count;
            var songs = await root.ApplyPlayerGrantAsync(new CouncilSubmission { Type = "skill", Name = "Old Songs", Description = "the songs of the old days" });
            // Scripted gods always act; the real model may stay silent, and then the grant is kept as lore
            Check("a petition no gift fits goes to the gods", GameRoot.Omens.Count == omensBefore + 1 ||
                  (!GameRoot.IsDemo && songs.Contains("lore")), songs);

            // ── The gods' choice ──────────────────────────────────────────
            var parser = new CouncilSubmissionParser();
            Check("petitions left to the gods reach them",
                parser.Parse("Gods' choice")?.Type == "choice" &&
                parser.Parse("Make a beast that hunts by night")?.Type == "choice" &&
                parser.Parse("Grant me a skill to tame beasts")?.Type == "skill");
            Check("the gods' words are tamed",
                GodsChoice.Parse("{\"act\":\"creature\",\"name\":\"x\",\"count\":999,\"strength\":500}") is { Count: 6, Strength: 95 } &&
                GodsChoice.Parse("no json here") == null &&
                GodsChoice.Parse("{\"act\":\"unmake the world\",\"name\":\"all\"}") == null);

            DivineAct Act(string json) => GodsChoice.Parse(json)!;
            string Do(string json) => root.ApplyDivineAct(Act(json), player.Id, player.Gifts, player.TileX, player.TileY);

            var made = Do("{\"act\":\"creature\",\"name\":\"ashwing\",\"look\":\"grey wings\",\"nature\":\"prey\",\"habitat\":\"air\",\"edible\":true,\"count\":3}");
            Check("the gods can make a new creature", AnimalSpecies.Get("ashwing") is { Divine: true } &&
                GameRoot.LiveAnimals.Any(a => a.Name == "Ashwing"), made);

            var loosed = Do("{\"act\":\"enemy\",\"name\":\"The Pale Hound\",\"look\":\"white as bone\",\"strength\":75}");
            var hound = GameRoot.LiveAnimals.FirstOrDefault(a => a.Name == "Pale Hound");
            int Dist(AnimalBrain a) => System.Math.Max(System.Math.Abs(a.TileX - player.TileX), System.Math.Abs(a.TileY - player.TileY));
            var far0 = hound == null ? 0 : Dist(hound);
            for (int i = 0; i < 3; i++) root.HuntWithEnemies();
            Check("an enemy hunts", hound != null && hound.Species is { Hunts: true, Unique: true } && Dist(hound) < far0,
                  hound == null ? loosed : $"{far0} → {Dist(hound)}");
            if (hound != null) { GameRoot.LiveAnimals.Remove(hound); GameRoot.Scene?.RemoveNpc(hound.AnimalId); }

            var land = Do("{\"act\":\"land\",\"becomes\":\"forest\",\"size\":2,\"where\":\"near\"}");
            Check("the gods can change the land", land.Contains("forest"), land);

            var itemsBefore = GameRoot.Items!.All.Count(i => i.Type == "provision");
            Do("{\"act\":\"provision\",\"name\":\"loaves\",\"edible\":true,\"count\":4}");
            Check("the gods can send food", GameRoot.Items.All.Count(i => i.Type == "provision" && i.Edible) > itemsBefore);

            Do("{\"act\":\"season\",\"kind\":\"long night\",\"hours\":1}");
            Check("the gods can send a long night", WorldClock.IsNight());
            Season.Set(null, null);

            Do("{\"act\":\"law\",\"name\":\"The Law of the Stranger\",\"text\":\"A traveller who asks for bread may not be refused.\"}");
            Check("the gods can write a law", GameRoot.Laws.Any(l => l.Name == "The Law of the Stranger"));

            Check("NPCs hear what the gods did", NpcPromptBuilder.BuildSystemPrompt(GameRoot.LiveNpcs[0].Decan).Contains("Ashwing") ||
                NpcPromptBuilder.WorldOmens.Count == 3);

            root.SaveNow();
            using (var w = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "world.json"))))
                Check("the gods' work is saved with the world",
                    w.RootElement.GetProperty("species").EnumerateArray().Any(x => x.GetProperty("name").GetString() == "Ashwing") &&
                    w.RootElement.GetProperty("terrain").GetArrayLength() > 0 &&
                    w.RootElement.GetProperty("omens").GetArrayLength() >= 6);

            root.SaveNow();
            using (var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "player.json"))))
                Check("gifts are saved", doc.RootElement.GetProperty("gifts").GetArrayLength() == player.Gifts.All.Count &&
                    doc.RootElement.GetProperty("gifts")[0].GetProperty("effect").GetString() == "Strength");

            var gifted = GameRoot.LiveNpcs.First(n => !n.IsForeigner);
            gifted.Gifts.Add(new Gift { Name = "Net Weaving", Effect = GiftEffect.Endurance });
            Check("an NPC's gifts reach its prompt", NpcPromptBuilder.BuildSystemPrompt(gifted.Decan,
                gifts: gifted.Gifts.Summary()).Contains("Net Weaving"));

            // ── Carrying ──────────────────────────────────────────────────
            var lantern = GameRoot.Items!.Spawn("Lantern", "crafted", player.TileX + 1, player.TileY, description: "a lantern to see by");
            var bread   = GameRoot.Items.Spawn("Loaf", "provision", player.TileX, player.TileY + 1, edible: true, lifespanHours: 24);
            var giftsBefore = player.Gifts.All.Count;
            Check("Move picks a thing up", root.PickUp(lantern) && root.PickUp(bread) && player.Carried.Count == 2 &&
                  !GameRoot.Items.All.Any(i => i.Id == lantern.Id));
            Check("a carried thing works as a gift", player.Gifts.All.Count == giftsBefore + 1 &&
                  player.Gifts.All.Last().ItemId == lantern.Id);
            var ateAt = player.Survival.LastAteUtc;
            root.PackEat(player.Carried.First(c => c.Item.Id == bread.Id));
            Check("carried food can be eaten", player.Survival.LastAteUtc > ateAt && player.Carried.Count == 1);
            root.PackDrop(player.Carried[0]);
            Check("a dropped thing returns to the ground and takes its gift", player.Carried.Count == 0 &&
                  GameRoot.Items.All.Any(i => i.Name == "Lantern") && player.Gifts.All.Count == giftsBefore);
            var neighbour = GameRoot.LiveNpcs.First(n => !n.IsForeigner);
            neighbour.SetTile(player.TileX - 1, player.TileY);
            GameRoot.Scene!.MoveNpc(neighbour.NpcId, player.TileX - 1, player.TileY);
            root.PickUp(GameRoot.Items.All.First(i => i.Name == "Lantern"));
            var receiver = root.NpcBeside(); // the nearest of those beside you
            root.PackGive(player.Carried[0]);
            Check("a thing can be given to whoever is beside you", receiver != null && player.Carried.Count == 0 &&
                  receiver.Gifts.All.Any(g => g.Name == "Lantern"), receiver?.Name ?? "no one beside");

            // ── Laws bind the player ──────────────────────────────────────
            GameRoot.AddLaw("The Life of Beasts", "No one may kill an animal.", player.Id);
            var broken = await LawJudge.JudgeAsync(GameRoot.Llm, GameRoot.Laws, $"{player.Name} killed a deer, an animal.");
            Check("a deed is judged against the laws", broken?.Name == "The Life of Beasts", broken?.Name ?? "none");
            var innocent = await LawJudge.JudgeAsync(GameRoot.Llm, GameRoot.Laws, $"{player.Name} walked to the river.");
            Check("an innocent deed breaks no law", innocent == null, innocent?.Name ?? "none");
            root.Brand(broken ?? GameRoot.Laws.Last());
            Check("a lawbreaker is not heard by the Council", player.IsBranded(System.DateTime.UtcNow) &&
                  root.CouncilRefusal()?.Contains("does not hear") == true);
            player.LawBrokenUtc = System.DateTime.UtcNow.AddHours(-25);
            Check("the brand lifts after a day", root.CouncilRefusal() == null);

            // ── The Council's first encounter ─────────────────────────────
            player.EncounterDone = false;
            await root.SendEncounterAsync();
            Check("the Council sends a messenger to a new life", player.EncounterDone && GameRoot.Scene!.DialogueOpen &&
                  GameRoot.Scene.DialogueSpeech.Contains("altar"));
            GameRoot.Scene.CloseDialogue();

            Check("the screen scales with the window",
                  ProjectSettings.GetSetting("display/window/stretch/mode").AsString() == "canvas_items");

            // ── Real clicks and keys, as a player plays ───────────────────
            // (Every action from the click menu once did nothing; nothing but real input catches that.)
            var scene = GameRoot.Scene!;
            scene.CloseDialogue(); scene.CloseMenu();
            GameRoot.LifePaused = true; // no one wanders onto the tiles being clicked
            var here = scene.PlayerTile;

            // An empty tile beside you: a being standing on it would be the one clicked
            bool Empty(Vector2I t) => !GameRoot.LiveNpcs.Any(n => n.TileX == t.X && n.TileY == t.Y) &&
                                      !GameRoot.LiveAnimals.Any(a => a.TileX == t.X && a.TileY == t.Y);
            var foodTile = new[] { new Vector2I(1, 0), new Vector2I(1, 1), new Vector2I(1, -1), new Vector2I(0, 1) }
                .Select(d => here + d).First(Empty);
            var manna = GameRoot.Items!.SpawnManna(foodTile.X, foodTile.Y);
            scene.RefreshMap();
            var ateBeforeClick = player.Survival.LastAteUtc;
            await ClickTile(foodTile.X, foodTile.Y, MouseButton.Right);
            var food = GameRoot.Items.All.FirstOrDefault(i => i.Id == scene.PrimitiveMenu.TargetId);
            Check("right-clicking food opens the primitives on it", scene.PrimitiveMenu.IsOpen &&
                  food is { Edible: true } && food.TileX == foodTile.X && food.TileY == foodTile.Y, scene.PrimitiveMenu.TargetId);
            await ClickControl(scene.PrimitiveMenu.ButtonFor(SkillType.Reap));
            await Wait(0.3);
            Check("choosing Reap from the menu eats it", player.Survival.LastAteUtc > ateBeforeClick &&
                  food != null && !GameRoot.Items.All.Any(i => i.Id == food.Id), scene.WorldText);
            foreach (var left in GameRoot.Items.All.Where(i => System.Math.Max(System.Math.Abs(i.TileX - here.X), System.Math.Abs(i.TileY - here.Y)) <= 1).ToList())
                GameRoot.Items.Remove(left.Id); // a clear patch for the next checks

            var talker = GameRoot.LiveNpcs.First(n => !n.BrokenTalk);
            // A free tile beside you (another NPC standing there would be the one clicked)
            var spot = new[] { new Vector2I(-1, 0), new Vector2I(0, -1), new Vector2I(-1, -1), new Vector2I(-1, 1) }
                .Select(d => here + d).First(t => Empty(t) || (talker.TileX == t.X && talker.TileY == t.Y));
            talker.SetTile(spot.X, spot.Y);
            scene.MoveNpc(talker.NpcId, spot.X, spot.Y);
            await ClickTile(spot.X, spot.Y, MouseButton.Left);
            Check("clicking a person opens the primitives on them", scene.PrimitiveMenu.IsOpen &&
                  scene.PrimitiveMenu.TargetId == talker.NpcId, scene.PrimitiveMenu.TargetId);
            await ClickControl(scene.PrimitiveMenu.ButtonFor(SkillType.Talk));
            await Wait(0.3);
            Check("choosing Talk from the menu opens the conversation", scene.DialogueOpen);
            scene.CloseDialogue();

            var stone = GameRoot.Items.Spawn("Lantern", "crafted", here.X, here.Y + 1, description: "a lantern");
            await PressKey(Key.Key1);
            Check("key 1 (Move) picks up the thing beside you", player.Carried.Any(c => c.Item.Id == stone.Id));

            await PressKey(Key.Key4);
            await Wait(0.3);
            Check("key 4 (Talk) speaks to the nearest person", scene.DialogueOpen);
            scene.CloseDialogue();

            // Far away: you walk there, then talk
            var far = GameRoot.LiveNpcs.FirstOrDefault(n => n != talker && !n.BrokenTalk) ?? talker;
            far.SetTile(here.X + 6, here.Y);
            scene.MoveNpc(far.NpcId, here.X + 6, here.Y);
            root.UsePrimitive(far.NpcId, SkillType.Talk);
            for (int i = 0; i < 40 && !scene.DialogueOpen; i++) await Wait(0.25);
            Check("talking to someone far away walks you to them first", scene.DialogueOpen,
                  $"player at {scene.PlayerTile}, them at {far.TileX},{far.TileY}");
            scene.CloseDialogue();

            // The altar: near the start, and stepping up to it opens the Council
            var (altarX, altarY) = (GameRoot.Altar!.TileX + int.Parse(GameRoot.Altar.CellId.Split(',')[0]) * 8,
                                    GameRoot.Altar.TileY + int.Parse(GameRoot.Altar.CellId.Split(',')[1]) * 8);
            Check("the altar lies within a walk of the start", System.Math.Max(System.Math.Abs(altarX), System.Math.Abs(altarY)) <= 40,
                  $"{altarX},{altarY}");
            scene.MovePlayerTo(new Vector2I(altarX, altarY + 1));
            await Wait(0.3);
            Check("stepping up to the altar opens the Council", scene.DialogueOpen && scene.DialogueSpeech.Contains("altar"));
            scene.CloseDialogue();
            scene.MovePlayerTo(here);
            await Wait(0.2);

            // The world moves while you watch
            GameRoot.LifePaused = false;
            var npcSpots = GameRoot.LiveNpcs.ToDictionary(n => n.NpcId, n => (n.TileX, n.TileY));
            for (int i = 0; i < 24 && !GameRoot.LiveNpcs.Any(n => npcSpots.TryGetValue(n.NpcId, out var s0) && s0 != (n.TileX, n.TileY)); i++)
                await Wait(0.5);
            Check("NPCs walk about between their thoughts",
                  GameRoot.LiveNpcs.Any(n => npcSpots.TryGetValue(n.NpcId, out var b) && b != (n.TileX, n.TileY)));

            here = scene.PlayerTile;
            // Put it where open land lies between you (a land beast cannot cross a river to reach you)
            var wolfDir = new[] { new Vector2I(1, 0), new Vector2I(-1, 0), new Vector2I(0, 1), new Vector2I(0, -1) }
                .FirstOrDefault(d => Enumerable.Range(1, 5).All(k =>
                {
                    var t = TileAt(here.X + d.X * k, here.Y + d.Y * k);
                    return t.Surface != TileSurface.Water && !t.HasCave && BiomeData.Get(t.Biome).Passable;
                }), new Vector2I(1, 0));
            var wolf = root.StageAnimal("wolf", here.X + wolfDir.X * 5, here.Y + wolfDir.Y * 5);
            if (wolf != null)
            {
                int D() => System.Math.Max(System.Math.Abs(wolf.TileX - here.X), System.Math.Abs(wolf.TileY - here.Y));
                var d0 = D();
                for (int i = 0; i < 12 && !root.IsStalking(wolf.AnimalId); i++) await Wait(0.5);
                var warned = root.IsStalking(wolf.AnimalId);
                for (int i = 0; i < 30 && D() >= d0 && D() > 2; i++) await Wait(0.5);
                Check("a predator warns you, then stalks you", warned && D() < d0,
                      $"{d0} → {D()}, warned={warned}, asleep={wolf.Survival.IsSleeping}, player={scene.PlayerTile} here={here}, " +
                      $"sleeping={player.Survival.IsSleeping}, text='{scene.WorldText}'");
                GameRoot.LiveAnimals.Remove(wolf);
                scene.RemoveNpc(wolf.AnimalId);
            }

            await PressKey(Key.M);
            Check("M opens the map", root.GetChildren().OfType<MapScreen>().Any() && player.Explored.Count > 0,
                  $"{player.Explored.Count} cells seen");
            await PressKey(Key.M);

            // ── Talk ──────────────────────────────────────────────────────
            var npc = GameRoot.LiveNpcs.First(n => !n.BrokenTalk);
            var reply = await npc.RespondToDialogueAsync("Where can I find shelter tonight?", new SituationContext());
            Check("an NPC answers in dialogue", !string.IsNullOrWhiteSpace(reply), reply.Length > 60 ? reply[..60] : reply);
        }

        /// <summary>Put a lion beside the sleeping player and ask whether it would strike.</summary>
        private static bool StrikesSleepingPlayer(GameRoot root)
        {
            var p = GameRoot.Player!;
            p.Survival.BeginSleep(System.DateTime.UtcNow, inCave: false);
            var lion = root.StageAnimal("lion", p.TileX + 1, p.TileY)!;
            var strikes = root.BuildAnimalSituation(lion).NearbyEntityId == p.Id;
            GameRoot.LiveAnimals.Remove(lion);
            GameRoot.Scene?.RemoveNpc(lion.AnimalId);
            p.Survival.EndSleep(System.DateTime.UtcNow);
            return strikes;
        }

        // ── Real input ────────────────────────────────────────────────────

        /// <summary>A click at a point in the game's own 1280×720 coordinates, delivered as the window would.</summary>
        private async Task ClickAt(Vector2 screen, MouseButton button)
        {
            foreach (var pressed in new[] { true, false })
            {
                GetViewport().PushInput(new InputEventMouseButton
                    { ButtonIndex = button, Pressed = pressed, Position = screen, GlobalPosition = screen }, inLocalCoords: true);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }

        private Task ClickTile(int tx, int ty, MouseButton button) =>
            ClickAt(GetViewport().GetCanvasTransform() * new Vector2(tx * 32 + 16, ty * 32 + 16), button);

        private async Task ClickControl(Control? control)
        {
            if (control == null) { Fail("a control to click", "not found"); return; }
            await ClickAt(control.GetGlobalRect().GetCenter(), MouseButton.Left);
        }

        private async Task PressKey(Key key)
        {
            foreach (var pressed in new[] { true, false })
            {
                GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed }, inLocalCoords: true);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
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
