using System.Collections;
using CatGame.Idle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CatGame.Tests
{
    public class IntroDialogueTests : CatGameTestBase
    {
        [UnityTest]
        public IEnumerator Fresh_intro_shows_Momo_teaches_both_incomes_and_blocks_gameplay_until_skip()
        {
            global::CatGame.CatGame game;
            var world=NewWorld(out game); world.EnableStoryIntro=true;
            yield return null;
            var intro=world.Hud.Intro;
            Assert.IsTrue(intro.IsOpen); Assert.IsTrue(world.Hud.Modals.IsBusy);
            Assert.AreEqual(0,Time.timeScale);
            var box=world.Hud.transform.Find("IntroDialogue/DialogueBox");
            Assert.AreEqual("墨墨",box.Find("Speaker").GetComponent<Text>().text);
            var portrait=world.Hud.transform.Find("IntroDialogue/Portrait").GetComponent<Image>();
            Assert.AreSame(CatVisualLibrary.Load(CatCatalog.All[0]).Portrait,portrait.sprite);
            Assert.AreEqual(FilterMode.Point,portrait.sprite.texture.filterMode);
            Assert.NotNull(portrait.GetComponent<PixelDialoguePortrait>());
            yield return null;
            float zoom=portrait.rectTransform.rect.width*Mathf.Abs(portrait.rectTransform.lossyScale.x)/portrait.sprite.rect.width;
            Assert.AreEqual(Mathf.Round(zoom),zoom,.001f,"portrait uses integer screen magnification");
            var portraitCorners=new Vector3[4]; var boxCorners=new Vector3[4];
            portrait.rectTransform.GetWorldCorners(portraitCorners);
            ((RectTransform)box).GetWorldCorners(boxCorners);
            Assert.GreaterOrEqual(portraitCorners[0].y,boxCorners[1].y,"dialogue must not cover the portrait paws");
            world.Economy.State.catHair=100;
            double before=world.Economy.State.catHair, play=world.Economy.State.playSeconds;
            int clicks=world.Economy.State.clicks;
            world.Cat.OnLick();
            Assert.AreEqual(clicks,world.Economy.State.clicks);
            Assert.IsFalse(world.Economy.BuyCat("Milk"));
            Assert.IsFalse(world.Economy.BuyItem("item_1_1"));
            Assert.IsFalse(world.Hud.GetComponent<PausePanel>().Open());
            yield return new WaitForSecondsRealtime(.15f);
            Assert.AreEqual(before,world.Economy.State.catHair);
            Assert.AreEqual(play,world.Economy.State.playSeconds);
            box.Find("Btn_next_intro").GetComponent<Button>().onClick.Invoke();
            StringAssert.Contains("摸摸",box.Find("DialogueText").GetComponent<Text>().text);
            intro.Advance();
            StringAssert.Contains("就算你不摸",box.Find("DialogueText").GetComponent<Text>().text);
            box.Find("Btn_skip_intro").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(world.Economy.State.introCompleted); Assert.IsFalse(intro.IsOpen);
            Assert.IsFalse(world.Hud.Modals.IsBusy); Assert.AreEqual(1,Time.timeScale);
            world.Cat.OnLick(); Assert.AreEqual(clicks+1,world.Economy.State.clicks);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.Greater(world.Economy.State.playSeconds,play);
        }

        [UnityTest]
        public IEnumerator Finished_intro_does_not_repeat_and_interrupted_intro_restarts_without_marking_done()
        {
            global::CatGame.CatGame game;
            var world=NewWorld(out game); world.EnableStoryIntro=true;
            yield return null;
            world.Hud.Intro.Advance();
            Assert.IsFalse(world.Economy.State.introCompleted);
            Object.Destroy(world.Hud.Intro);
            yield return null;
            var restarted=world.Hud.gameObject.AddComponent<IntroDialogue>();
            restarted.Initialize((RectTransform)world.Hud.transform,world.Economy.State,world.Hud.Modals);
            Assert.IsTrue(restarted.IsOpen); Assert.AreEqual(0,restarted.LineIndex);
            for(int i=0;i<5;i++) restarted.Advance();
            Assert.IsFalse(restarted.IsOpen); Assert.IsTrue(world.Economy.State.introCompleted);
            var repeated=world.Hud.gameObject.AddComponent<IntroDialogue>();
            repeated.Initialize((RectTransform)world.Hud.transform,world.Economy.State,world.Hud.Modals);
            yield return null;
            Assert.IsFalse(repeated.IsOpen); Assert.AreEqual(1,Time.timeScale);
        }

        [UnityTest]
        public IEnumerator Offline_summary_keeps_modal_ownership_then_pending_intro_can_open()
        {
            global::CatGame.CatGame game;
            var world=NewWorld(out game);
            yield return null;
            Assert.IsTrue(world.Hud.ShowOfflineSummary(new OfflineEarningsResult{catHair=120,elapsedSeconds=600}));
            var intro=world.Hud.gameObject.AddComponent<IntroDialogue>();
            intro.Initialize((RectTransform)world.Hud.transform,world.Economy.State,world.Hud.Modals);
            Assert.IsFalse(intro.IsOpen);
            world.Hud.Modals.Release(intro); Assert.AreEqual(0,Time.timeScale,"wrong owner cannot unpause");
            world.Hud.transform.Find("OfflineEarningsOverlay/Summary/Btn_Collect").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.IsTrue(intro.IsOpen); Assert.AreEqual(0,Time.timeScale);
            intro.Skip(); Assert.AreEqual(1,Time.timeScale);
        }

        [Test]
        public void Modal_gate_restores_original_speed_only_for_its_owner()
        {
            var gate=Track(new GameObject("Gate.Test")).AddComponent<ModalGate>();
            var owner=new object(); var other=new object();
            Time.timeScale=.35f;
            Assert.IsTrue(gate.TryAcquire(owner)); Assert.IsFalse(gate.TryAcquire(other));
            gate.Release(other); Assert.AreEqual(0,Time.timeScale);
            gate.Release(owner); Assert.AreEqual(.35f,Time.timeScale);
        }
    }
}
