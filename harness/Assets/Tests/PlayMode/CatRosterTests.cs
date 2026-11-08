using System.Collections;
using System.Diagnostics;
using CatGame.Idle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatGameType = global::CatGame.CatGame;

namespace CatGame.Tests
{
    public class CatRosterTests : CatGameTestBase
    {
        [UnityTest]
        public IEnumerator Cat_frames_stay_inside_floor_at_all_four_edges()
        {
            var position = typeof(CatActor).GetField("_pos", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            foreach (var definition in CatCatalog.All)
            {
                var actor = Track(new GameObject("Boundary_" + definition.id)).AddComponent<CatActor>();
                actor.Setup(CatVisualLibrary.Load(definition), Vector2.one * 3);
                foreach (var edge in new[] { new Vector3(-3, -3), new Vector3(-3, 3), new Vector3(3, -3), new Vector3(3, 3) })
                {
                    position.SetValue(actor, edge);
                    actor.OnLick(); // Includes the jump frame after an edge click.
                    yield return null;
                    var bounds = actor.Renderer.bounds;
                    Assert.GreaterOrEqual(bounds.min.x, -3.01f, definition.id);
                    Assert.GreaterOrEqual(bounds.min.y, -3.01f, definition.id);
                    Assert.LessOrEqual(bounds.max.x, 3.01f, definition.id);
                    Assert.LessOrEqual(bounds.max.y, 3.01f, definition.id);
                }
            }
        }

        [UnityTest]
        public IEnumerator Roster_keeps_one_actor_per_owned_breed_and_caps_at_100()
        {
            CatGameType game;
            var world = NewWorld(out game, false);
            yield return null;

            var state = SaveSystem.CreateNewGame();
            foreach (var count in state.cats) count.count = 10;
            world.Roster.Rebuild(state);

            Assert.LessOrEqual(world.Roster.VisibleCount, IdleBalance.MaxVisibleCats);
            Assert.AreEqual(IdleBalance.MaxVisibleCats, world.Roster.VisibleCount);
            Assert.AreEqual(10, world.Roster.StackedCount);
            foreach (var cat in CatCatalog.All)
                Assert.GreaterOrEqual(world.Roster.VisibleCountFor(cat.id), 1, cat.id + " must keep a visible actor");
        }

        [UnityTest]
        public IEnumerator Stress_440_owned_cats_survive_100_roster_rebuilds()
        {
            CatGameType game;
            var world = NewWorld(out game, false);
            yield return null;

            var state = SaveSystem.CreateNewGame();
            foreach (var row in state.cats) row.count = IdleBalance.CatMaxOwned;

            var stopwatch = Stopwatch.StartNew();
            long memoryBefore = System.GC.GetTotalMemory(false);
            for (int i = 0; i < 100; i++)
            {
                world.Roster.Rebuild(state);
                Assert.AreEqual(440, world.Roster.TotalOwned, "持有量不能被渲染上限裁剪");
                Assert.AreEqual(IdleBalance.MaxVisibleCats, world.Roster.VisibleCount);
                Assert.AreEqual(340, world.Roster.StackedCount);
                foreach (var breed in CatCatalog.All)
                    Assert.GreaterOrEqual(world.Roster.VisibleCountFor(breed.id), 1, breed.id);
            }
            stopwatch.Stop();
            long memoryAfter = System.GC.GetTotalMemory(false);
            UnityEngine.Debug.Log(string.Format(
                "STR-01 roster stress: owned={0}, visible={1}, stacked={2}, rebuilds=100, elapsedMs={3}, managedBytesBefore={4}, managedBytesAfter={5}",
                world.Roster.TotalOwned, world.Roster.VisibleCount, world.Roster.StackedCount,
                stopwatch.ElapsedMilliseconds, memoryBefore, memoryAfter));

            yield return null;
        }

        [UnityTest]
        public IEnumerator Stress_ten_thousand_click_and_purchase_requests_apply_once()
        {
            CatGameType game;
            var world = NewWorld(out game, false);
            yield return null;

            var state = world.Economy.State;
            var actor = world.Roster.GetFirstActor();
            double expectedClickTotal = 0;
            double observedClickTotal = 0;
            var stopwatch = Stopwatch.StartNew();
            long memoryBefore = System.GC.GetTotalMemory(false);

            // One simulated pointer action per frame keeps this a sustained burst while allowing
            // the short-lived reward labels to animate and expire instead of accumulating 5,000 at once.
            for (int i = 0; i < 5000; i++)
            {
                double beforeClick = state.catHair;
                double clickValue = world.Economy.CurrentClickValue;
                actor.OnLick();
                expectedClickTotal += clickValue;
                observedClickTotal += state.catHair - beforeClick;
                yield return null;
            }

            Assert.AreEqual(5000, state.clicks);
            Assert.AreEqual(expectedClickTotal, observedClickTotal, 0.01, "click value should be applied exactly once");
            Assert.GreaterOrEqual(state.totalEarned, expectedClickTotal, "automatic production may add earnings during the input run");

            state.totalEarned = IdleBalance.Stage5Threshold;
            state.catHair = 1e15;
            int successfulPurchases = 0;
            var milk = state.cats.Find(row => row.id == "Milk");
            while (milk.count < IdleBalance.CatMaxOwned)
            {
                Assert.IsTrue(world.Economy.BuyCat("Milk"));
                successfulPurchases++;
            }
            for (int level = 0; level < ItemCatalog.LevelsPerItem; level++)
            {
                Assert.IsTrue(world.Economy.BuyItem("item_1_1"));
                successfulPurchases++;
            }

            // 5,000 more requests cover insufficient currency, capped cats/items and an unknown id.
            state.catHair = 0;
            var catCountsBefore = new int[state.cats.Count];
            var itemLevelsBefore = new int[state.items.Count];
            for (int i = 0; i < state.cats.Count; i++) catCountsBefore[i] = state.cats[i].count;
            for (int i = 0; i < state.items.Count; i++) itemLevelsBefore[i] = state.items[i].level;
            double balanceBeforeRejectedRequests = state.catHair;
            int clicksBeforeRejectedRequests = state.clicks;

            for (int i = 0; i < 5000; i++)
            {
                bool bought;
                switch (i % 4)
                {
                    case 0: bought = world.Economy.BuyCat("Tabby"); break; // insufficient funds
                    case 1: bought = world.Economy.BuyCat("Milk"); break; // cat cap
                    case 2: bought = world.Economy.BuyItem("item_1_1"); break; // item cap
                    default: bought = world.Economy.BuyItem("unknown_item"); break;
                }
                Assert.IsFalse(bought, "rejected purchase request " + i + " must not succeed");
            }

            Assert.AreEqual(balanceBeforeRejectedRequests, state.catHair);
            Assert.AreEqual(clicksBeforeRejectedRequests, state.clicks);
            for (int i = 0; i < state.cats.Count; i++) Assert.AreEqual(catCountsBefore[i], state.cats[i].count);
            for (int i = 0; i < state.items.Count; i++) Assert.AreEqual(itemLevelsBefore[i], state.items[i].level);

            stopwatch.Stop();
            long memoryAfter = System.GC.GetTotalMemory(false);
            UnityEngine.Debug.Log(string.Format(
                "STR-04 input stress: requests=10000, clicks=5000, rejectedPurchases=5000, successfulPurchases={0}, elapsedMs={1}, managedBytesBefore={2}, managedBytesAfter={3}",
                successfulPurchases, stopwatch.ElapsedMilliseconds, memoryBefore, memoryAfter));
        }

        [UnityTest]
        public IEnumerator Newly_purchased_breed_gets_a_visible_actor_when_roster_is_full()
        {
            CatGameType game;
            var world = NewWorld(out game, false);
            yield return null;

            var state = SaveSystem.CreateNewGame();
            state.cats[0].count = 40;
            state.cats[1].count = 40;
            state.cats[2].count = 20;
            world.Roster.Rebuild(state);
            Assert.AreEqual(100, world.Roster.VisibleCount);

            state.cats[3].count = 1;
            world.Roster.Rebuild(state, "Calico");

            Assert.AreEqual(100, world.Roster.VisibleCount);
            Assert.AreEqual(1, world.Roster.StackedCount);
            Assert.AreEqual(1, world.Roster.VisibleCountFor("Calico"));
            Assert.GreaterOrEqual(world.Roster.VisibleCountFor("Milk"), 1);
            Assert.GreaterOrEqual(world.Roster.VisibleCountFor("Tabby"), 1);
            Assert.GreaterOrEqual(world.Roster.VisibleCountFor("Snow"), 1);
        }

        [UnityTest]
        public IEnumerator Cat_visuals_scale_32px_art_to_the_same_height_as_64px_art()
        {
            CatGameType game;
            var world = NewWorld(out game, false);
            yield return null;

            var state = SaveSystem.CreateNewGame();
            state.cats[3].count = 1;
            world.Roster.Rebuild(state);

            Assert.AreEqual(1f, world.Roster.FindActor("Milk").VisualScale, 0.001f);
            Assert.AreEqual(1f, world.Roster.FindActor("Calico").VisualScale, 0.001f);
            var legacy=Track(new GameObject("Legacy32.Scale")).AddComponent<CatActor>();
            legacy.Setup(LegacyCalicoBreed(),Vector2.one*3);
            Assert.AreEqual(2f,legacy.VisualScale,.001f);
            Assert.AreEqual(world.Cat.Renderer.bounds.size.y,legacy.Renderer.bounds.size.y,.001f);
        }

        [UnityTest]
        public IEnumerator Licking_a_cat_adds_click_value_and_shows_a_reward()
        {
            CatGameType game;
            var world = NewWorld(out game, false);
            yield return null;

            var state = SaveSystem.CreateNewGame();
            world.Roster.Rebuild(state);
            var cat = world.Roster.FindActor("Milk");
            double expected = IdleMath.ClickValue(state);

            cat.OnLick();

            Assert.AreEqual(expected, state.catHair);
            Assert.AreEqual(expected, state.totalEarned);
            Assert.AreEqual(1, state.clicks);
            Assert.AreEqual(ActionNames.Jump, cat.CurrentAction);
            var reward = Object.FindObjectOfType<TextMesh>();
            Assert.IsNotNull(reward);
            Assert.AreEqual("+" + Hud.FormatNumber(expected), reward.text);

            yield return new WaitForSeconds(0.9f);
            Assert.IsFalse(cat.IsForced, "Jump should play once and return to auto mode");
        }
    }
}
