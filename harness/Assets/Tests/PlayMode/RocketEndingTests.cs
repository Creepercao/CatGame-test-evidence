using System.Collections;
using CatGame.Idle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CatGame.Tests
{
    public class RocketEndingTests : CatGameTestBase
    {
        [UnityTest] public IEnumerator Rocket_cancel_insufficient_balance_confirm_and_continue_preserve_progress()
        {
            global::CatGame.CatGame game;
            var world=NewWorld(out game); world.EnableStoryEvents=true;
            yield return null;
            var state=world.Economy.State; state.introCompleted=true; state.catHair=EndingRules.RocketPrice+10;
            Assert.IsTrue(world.Hud.Rocket.Open()); world.Hud.Rocket.Cancel();
            Assert.AreEqual(EndingRules.RocketPrice+10,state.catHair); Assert.IsFalse(state.rocketPurchased);
            Assert.IsTrue(world.Hud.Rocket.Open()); state.catHair=EndingRules.RocketPrice-1;
            world.Hud.Rocket.Confirm(); Assert.IsFalse(state.rocketPurchased);
            state.catHair=EndingRules.RocketPrice+10; state.totalEarned=state.catHair;
            Assert.IsTrue(world.Hud.Rocket.Open()); Assert.IsFalse(world.Economy.BuyRocket(),"other callers cannot bypass confirmation ownership");
            world.Hud.Rocket.Confirm(); world.Hud.Rocket.Confirm();
            Assert.AreEqual(10,state.catHair); Assert.IsTrue(state.rocketPurchased);
            yield return null;
            Assert.AreEqual(StoryPlayer.EndingId,world.Hud.Stories.CurrentId,"ending precedes queued ordinary CGs");
            Assert.AreEqual(0,Time.timeScale); Assert.IsFalse(state.endingCompleted);
            world.Hud.Stories.Complete(); Assert.IsTrue(world.Hud.Stories.ShowingStatistics);
            Assert.IsFalse(state.endingCompleted,"the CG skip must still lead to thanks and choices");
            yield return null;
            Assert.IsNotNull(world.Hud.transform.Find("EndingStatistics/Card/Thanks"));
            world.Hud.transform.Find("EndingStatistics/Card/Btn_continue_ending").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(state.endingCompleted); Assert.AreEqual(1,Time.timeScale);
            Assert.IsFalse(world.Economy.BuyRocket());
            yield return null;
            Assert.AreEqual("first_tuft",world.Hud.Stories.CurrentId,"all unplayed ordinary memories survive ending in catalog order");
            foreach(var story in StoryCatalog.All)
                if(state.pendingStoryEvents.Contains(story.id))
                {
                    Assert.AreEqual(story.id,world.Hud.Stories.CurrentId);
                    world.Hud.Stories.Complete(); yield return null;
                }
            Assert.IsFalse(world.Hud.Stories.IsOpen); Assert.IsFalse(world.Hud.Rocket.Open());
        }

        [UnityTest] public IEnumerator Interrupted_ending_recovers_from_both_screens_without_paying_again_and_menu_marks_done()
        {
            global::CatGame.CatGame game;
            var world=NewWorld(out game); world.EnableStoryEvents=true;
            yield return null;
            var state=world.Economy.State; state.introCompleted=true; state.catHair=EndingRules.RocketPrice; state.totalEarned=0;
            Assert.IsTrue(world.Economy.BuyRocket()); yield return null;
            for(int interruption=0;interruption<2;interruption++)
            {
                var old=world.Hud.GetComponent<StoryPlayer>();
                if(interruption==1) { old.Complete(); Assert.IsTrue(old.ShowingStatistics); }
                Object.Destroy(old); yield return null;
                Assert.IsFalse(state.endingCompleted);
                Assert.AreEqual(state.totalEarned,state.catHair,.001,"only normal production during the released frame may change the zero post-purchase balance");
                int menuCalls=0;
                var restored=world.Hud.gameObject.AddComponent<StoryPlayer>();
                restored.Initialize((RectTransform)world.Hud.transform,state,world.Hud.Modals,()=>menuCalls++);
                yield return null;
                Assert.AreEqual(StoryPlayer.EndingId,restored.CurrentId); Assert.IsFalse(restored.ShowingStatistics);
                Assert.IsFalse(world.Economy.BuyRocket());
                if(interruption==1)
                {
                    restored.Complete(); restored.FinishEnding(true); restored.FinishEnding(true);
                    Assert.AreEqual(1,menuCalls); Assert.IsTrue(state.endingCompleted); Assert.AreEqual(1,Time.timeScale);
                }
            }
        }

        [UnityTest] public IEnumerator Rocket_shop_is_same_scroll_pixel_icon_and_fifty_confirmation_cancels_do_not_leak_modals()
        {
            global::CatGame.CatGame game;
            var world=NewWorld(out game); yield return null;
            world.Economy.State.catHair=EndingRules.RocketPrice;
            var panel=world.Hud.transform.Find("PurchasePanel");
            panel.Find("Btn_shop_rocket").GetComponent<Button>().onClick.Invoke(); yield return null;
            var row=panel.Find("ShopViewport/Content/Rocket");
            Assert.AreEqual(1,row.GetComponent<CanvasGroup>().alpha);
            var sprite=row.Find("Icon").GetComponent<Image>().sprite;
            Assert.IsNotNull(sprite); Assert.AreEqual(64,sprite.rect.width); Assert.AreEqual(FilterMode.Point,sprite.texture.filterMode);
            for(int i=0;i<50;i++)
            {
                double before=world.Economy.State.catHair;
                Assert.IsTrue(world.Hud.Rocket.Open()); Assert.IsFalse(world.Hud.Rocket.Open());
                Assert.IsFalse(world.Hud.GetComponent<PausePanel>().Open());
                world.Hud.Rocket.Cancel();
                Assert.AreEqual(before,world.Economy.State.catHair,"cancel must not charge; subsequent unpaused production is legitimate");
                yield return null;
                Assert.IsNull(world.Hud.transform.Find("RocketConfirmation")); Assert.IsFalse(world.Hud.Modals.IsBusy);
            }
            Assert.GreaterOrEqual(world.Economy.State.catHair,EndingRules.RocketPrice); Assert.AreEqual(1,Time.timeScale);
        }
    }
}
