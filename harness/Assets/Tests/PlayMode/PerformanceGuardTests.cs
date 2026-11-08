using System.Collections;
using CatGame.Idle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace CatGame.Tests
{
    public class PerformanceGuardTests : CatGameTestBase
    {
        [UnityTest]
        public IEnumerator Two_hundred_CG_pointer_closes_with_one_hundred_cats_block_modal_and_same_frame_inputs()
        {
            global::CatGame.CatGame game;
            var world = NewWorld(out game);
            yield return null;
            var state = world.Economy.State;
            state.introCompleted = true;
            state.catHair = state.totalEarned = 1e12;
            foreach (var story in StoryCatalog.All) state.achievements.Add(story.id);
            foreach (var cat in state.cats) cat.count = 40;
            world.Roster.Rebuild(state);
            world.Economy.Initialize(state, world.Roster);
            yield return null; // Rebuild removes excess candidate actors at end-of-frame.
            Assert.AreEqual(100, world.Roster.VisibleCount);
            Assert.AreEqual(440, world.Roster.TotalOwned);
            Assert.AreEqual(100, world.Roster.GetComponentsInChildren<CatActor>().Length);
            Assert.AreEqual(20, StoryCatalog.All.Length);
            int children = world.Hud.transform.childCount, closed = 0;
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };

            for (int cycle = 0; cycle < 10; cycle++)
            {
                var preview = GameStateFactory.CreateNewGame();
                preview.introCompleted = true;
                foreach (var story in StoryCatalog.All) preview.achievements.Add(story.id);
                var player = world.Hud.gameObject.AddComponent<StoryPlayer>();
                player.Initialize((RectTransform)world.Hud.transform, preview, world.Hud.Modals);
                foreach (var story in StoryCatalog.All)
                {
                    float deadline = Time.realtimeSinceStartup + 5;
                    while (!player.IsOpen && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.IsTrue(player.IsOpen, story.id);
                    Assert.AreEqual(story.id, player.CurrentId);
                    Assert.IsTrue(world.Hud.Modals.IsBusy);
                    Assert.AreEqual(0, Time.timeScale);
                    int clicks = state.clicks;
                    double money = state.catHair;
                    world.Cat.OnLick();
                    Assert.IsFalse(world.Economy.BuyItem("item_1_1"), "Purchase while CG owns the modal");
                    Assert.AreEqual(clicks, state.clicks);
                    Assert.AreEqual(money, state.catHair);

                    var overlay = world.Hud.transform.Find("StoryCG").gameObject;
                    Assert.IsTrue(ExecuteEvents.Execute(overlay, pointer, ExecuteEvents.pointerClickHandler));
                    Assert.IsFalse(player.IsOpen);
                    Assert.IsFalse(world.Hud.Modals.IsBusy);
                    Assert.AreEqual(1, Time.timeScale);
                    world.Cat.OnLick();
                    Assert.IsFalse(world.Economy.BuyItem("item_1_1"), "Purchase in the closing pointer frame");
                    Assert.AreEqual(clicks, state.clicks);
                    Assert.AreEqual(money, state.catHair);
                    closed++;
                    Assert.AreEqual(closed - cycle * 20, preview.seenStoryEvents.Count);
                    Debug.Log("PERF GUARD STEP " + closed + ": " + story.id + "; modal and closing-frame click/buy blocked; pointer close once.");
                    yield return null;
                }
                Object.Destroy(player);
                yield return null;
                Assert.IsFalse(world.Hud.Modals.IsBusy);
                Assert.AreEqual(children, world.Hud.transform.childCount, "CG overlay leaked after cycle " + cycle);
            }
            Assert.AreEqual(200, closed);
            Assert.IsTrue(world.Economy.BuyItem("item_1_1"), "Positive control: the same affordable purchase succeeds after the guard expires");
            Debug.Log("PERF GUARD COMPLETE: 200 pointer closes, 400 blocked click/buy pairs, 10 overlay cleanup checks, post-close purchase succeeds.");
        }
    }
}
