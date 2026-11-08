using System;
using System.Collections;
using System.Reflection;
using CatGame.Idle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatGame.Tests
{
    public class PixelMilkIntegrationTests : CatGameTestBase
    {
        [Test]
        public void Milk_preview_and_economy_share_new_four_action_frames()
        {
            var game=NewGame();
            var visual=CatVisualLibrary.Load(Array.Find(CatCatalog.All,row=>row.id=="Milk"));
            Assert.AreSame(visual,game.Current);
            Assert.IsTrue(visual.UseWalkWhileMoving);
            Assert.IsTrue(visual.UseSleepWhileResting);
            foreach (var action in new[]{ActionNames.Idle,ActionNames.Walk,ActionNames.Jump,ActionNames.Sleep})
            {
                var frames=visual.Clip(action);
                Assert.AreEqual(action==ActionNames.Jump?12:8,frames.Length,action);
                foreach (var frame in frames)
                {
                    Assert.AreEqual(new Vector2(64,64),frame.rect.size);
                    Assert.AreEqual(new Vector2(32,32),frame.pivot);
                    Assert.AreEqual(32,frame.pixelsPerUnit);
                    Assert.AreEqual(FilterMode.Point,frame.texture.filterMode);
                }
            }
            Assert.IsFalse(LegacyCalicoBreed().UseWalkWhileMoving);
            Assert.IsNull(LegacyCalicoBreed().Walk);
        }

        static void Set(CatActor actor,string name,object value)
        { typeof(CatActor).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(actor,value); }

        [UnityTest]
        public IEnumerator Milk_world_sleeps_produces_clicks_once_and_resumes_roaming()
        {
            global::CatGame.CatGame game;
            var world=NewWorld(out game);
            yield return null;
            var actor=world.Cat;
            Assert.AreEqual("Milk",actor.Breed.Id);
            Assert.AreSame(game.Current.Idle,actor.Breed.Idle);
            float until=Time.time+3;
            while (!actor.IsWalking && Time.time<until) yield return null;
            Assert.AreEqual(ActionNames.Walk,actor.CurrentAction);
            // The generic arrival/random timer is exercised in PixelCatSleepTests.
            // Here place the real roster actor into Sleep to verify economy/UI event wiring.
            Set(actor,"_moving",false);
            Set(actor,"_resting",true);
            Set(actor,"_pauseUntil",Time.time+10);
            Set(actor,"_sleepAmount",CatActor.SleepDelay+.1f);
            yield return null;
            Assert.AreEqual(ActionNames.Sleep,actor.CurrentAction);
            var state=world.Economy.State;
            double hair=state.catHair;
            yield return new WaitForSeconds(.15f);
            Assert.Greater(state.catHair,hair,"Sleeping must not stop passive production");
            hair=state.catHair;
            int clicks=state.clicks;
            double expected=IdleMath.ClickValue(state);
            actor.OnLick();
            Assert.AreEqual(clicks+1,state.clicks);
            Assert.AreEqual(hair+expected,state.catHair,1e-7);
            Assert.AreEqual(ActionNames.Jump,actor.CurrentAction);
            yield return new WaitForSeconds(.9f);
            Assert.AreEqual(ActionNames.Walk,actor.CurrentAction);
            Assert.IsFalse(actor.IsForced);
        }
    }
}
