using System.Collections;
using CatGame.Idle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CatGame.Tests
{
    public class StoryPlayerTests : CatGameTestBase
    {
        [UnityTest] public IEnumerator First_real_pet_unlocks_its_own_CG_and_returns_to_play_without_repeating()
        {
            global::CatGame.CatGame game;
            var world=NewWorld(out game); world.EnableStoryEvents=true;
            yield return null;
            var state=world.Economy.State; state.introCompleted=true;
            Debug.Log("CG STEP: pet the actual Momo actor once, then await the production achievement tick.");
            world.Cat.OnLick();
            float deadline=Time.realtimeSinceStartup+2;
            while(!world.Hud.Stories.IsOpen && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.AreEqual(1,state.clicks); Assert.Contains("first_click",state.achievements);
            Assert.AreEqual("first_click",world.Hud.Stories.CurrentId); Assert.AreEqual(0,Time.timeScale);
            world.Hud.Stories.Complete(); yield return null;
            Assert.IsFalse(world.Hud.Stories.IsOpen); Assert.Contains("first_click",state.seenStoryEvents);
            Assert.AreEqual(1,Time.timeScale);
            Debug.Log("CG STEP: first pet memory completed once; simulation resumed.");
        }

        [UnityTest] public IEnumerator Simultaneous_memories_play_in_order_once_and_pause_all_gameplay()
        {
            global::CatGame.CatGame game;
            var world=NewWorld(out game); world.EnableStoryEvents=true;
            yield return null;
            var state=world.Economy.State; state.introCompleted=true; state.catHair=1000;
            foreach(var story in StoryCatalog.All) state.achievements.Add(story.id);
            yield return null;
            var player=world.Hud.Stories;
            foreach(var story in StoryCatalog.All)
            {
                Debug.Log("CG STEP: verify pause, art, blocked inputs and completion for "+story.id);
                Assert.IsTrue(player.IsOpen); Assert.AreEqual(story.id,player.CurrentId);
                Assert.AreEqual(0,Time.timeScale); Assert.IsFalse(world.Hud.GetComponent<PausePanel>().Open());
                double money=state.catHair, play=state.playSeconds; int clicks=state.clicks;
                world.Cat.OnLick(); Assert.IsFalse(world.Economy.BuyCat("Milk"));
                Assert.IsFalse(world.Economy.BuyItem("item_1_1"));
                yield return new WaitForSecondsRealtime(.05f);
                Assert.AreEqual(money,state.catHair); Assert.AreEqual(play,state.playSeconds); Assert.AreEqual(clicks,state.clicks);
                var art=world.Hud.transform.Find("StoryCG/Art").GetComponent<Image>();
                Assert.IsNotNull(art.sprite); Assert.IsTrue(art.preserveAspect);
                Assert.AreEqual(FilterMode.Bilinear,art.sprite.texture.filterMode);
                world.Hud.transform.Find("StoryCG/Btn_close_CG").GetComponent<Button>().onClick.Invoke();
                player.Complete(); // A second queued button event cannot consume the next memory.
                yield return null;
            }
            yield return null;
            Assert.IsFalse(player.IsOpen); Assert.AreEqual(1,Time.timeScale);
            Assert.AreEqual(AchievementCatalog.All.Length,state.seenStoryEvents.Count); Assert.IsEmpty(state.pendingStoryEvents);
            for(int i=0;i<1000;i++) Assert.IsFalse(StoryCatalog.EnqueueUnlocked(state));
            yield return null; Assert.IsFalse(player.IsOpen);
        }

        [UnityTest] public IEnumerator Interrupted_memory_remains_pending_and_reopens_before_later_memories()
        {
            global::CatGame.CatGame game;
            var world=NewWorld(out game); world.EnableStoryEvents=true;
            yield return null;
            var state=world.Economy.State; state.introCompleted=true;
            state.achievements.Add("three_breeds"); state.achievements.Add("six_breeds");
            yield return null;
            Assert.AreEqual("three_breeds",world.Hud.Stories.CurrentId);
            Object.Destroy(world.Hud.Stories); yield return null;
            Assert.AreEqual(1,Time.timeScale); Assert.IsEmpty(state.seenStoryEvents);
            CollectionAssert.AreEqual(new[]{"three_breeds","six_breeds"},state.pendingStoryEvents);
            var restarted=world.Hud.gameObject.AddComponent<StoryPlayer>();
            restarted.Initialize((RectTransform)world.Hud.transform,state,world.Hud.Modals);
            yield return null;
            Assert.AreEqual("three_breeds",restarted.CurrentId);
            restarted.Complete(); yield return null;
            Assert.AreEqual("six_breeds",restarted.CurrentId);
        }

        [UnityTest] public IEnumerator Offline_and_intro_ownership_precede_pending_memories()
        {
            global::CatGame.CatGame game;
            var world=NewWorld(out game); world.EnableStoryEvents=true;
            yield return null;
            var state=world.Economy.State; state.achievements.Add("three_breeds");
            Assert.IsTrue(world.Hud.ShowOfflineSummary(new OfflineEarningsResult{catHair=50,elapsedSeconds=100}));
            var intro=world.Hud.gameObject.AddComponent<IntroDialogue>();
            intro.Initialize((RectTransform)world.Hud.transform,state,world.Hud.Modals);
            yield return null;
            Assert.IsFalse(world.Hud.Stories.IsOpen); Assert.IsFalse(intro.IsOpen);
            world.Hud.transform.Find("OfflineEarningsOverlay/Summary/Btn_Collect").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.IsTrue(intro.IsOpen); Assert.IsFalse(world.Hud.Stories.IsOpen);
            intro.Skip(); yield return null;
            Assert.IsTrue(world.Hud.Stories.IsOpen); Assert.AreEqual(0,Time.timeScale);
        }

        [UnityTest] public IEnumerator Historical_seen_milestones_do_not_replay_but_new_one_still_opens()
        {
            global::CatGame.CatGame game;
            var world=NewWorld(out game); world.EnableStoryEvents=true;
            yield return null;
            var state=world.Economy.State; state.introCompleted=true;
            state.achievements.Add("three_breeds"); state.seenStoryEvents.Add("three_breeds");
            yield return null; Assert.IsFalse(world.Hud.Stories.IsOpen);
            state.achievements.Add("six_breeds"); yield return null;
            Assert.AreEqual("six_breeds",world.Hud.Stories.CurrentId);
        }
    }
}
