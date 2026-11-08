using System;
using System.Collections;
using System.Reflection;
using CatGame.Idle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatGame.Tests
{
    public class PixelCatSleepTests : CatGameTestBase
    {
        static void Set(CatActor actor,string name,object value)
        { typeof(CatActor).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(actor,value); }
        static float PauseUntil(CatActor actor)
        { return (float)typeof(CatActor).GetField("_pauseUntil",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(actor); }

        CatActor NewActor(bool withSleep=true)
        {
            var breed=CatVisualLibrary.Load(Array.Find(CatCatalog.All,row=>row.id=="Egypt"));
            Assert.AreEqual(8,breed.Sleep.Length);
            if (!withSleep) breed=new CatBreed { Id="MissingSleep",Idle=breed.Idle,Walk=breed.Walk,
                Jump=breed.Jump,UseWalkWhileMoving=true,UseSleepWhileResting=true };
            var actor=Track(new GameObject("SleepEgypt.Test")).AddComponent<CatActor>();
            actor.Setup(breed,new Vector2(6,4));
            return actor;
        }

        // Exercise the actual arrival branch with a reproducible random draw, not a timed sleep mock.
        static void Arrive(CatActor actor,bool longRest)
        {
            var original=UnityEngine.Random.state;
            try {
                int seed=0;
                for (;seed<1000;seed++) {
                    UnityEngine.Random.InitState(seed);
                    if ((UnityEngine.Random.value<.2f)==longRest) break;
                }
                Assert.Less(seed,1000);
                UnityEngine.Random.InitState(seed);
                Set(actor,"_target",actor.Position);
                Set(actor,"_pauseUntil",Time.time-1);
                actor.SendMessage("Update");
            } finally { UnityEngine.Random.state=original; }
        }

        [UnityTest]
        public IEnumerator Egypt_long_rest_enters_Sleep_and_pause_freezes_it()
        {
            var actor=NewActor();
            yield return null;
            Arrive(actor,true);
            Assert.That(PauseUntil(actor)-Time.time,Is.InRange(8f,14f));
            Assert.AreEqual(ActionNames.Idle,actor.CurrentAction);
            yield return new WaitForSeconds(CatActor.SleepDelay+.15f);
            Assert.AreEqual(ActionNames.Sleep,actor.CurrentAction);
            Assert.Contains(actor.Renderer.sprite,actor.Breed.Sleep);
            var position=actor.Position;
            yield return new WaitForSeconds(.5f);
            Assert.AreEqual(ActionNames.Sleep,actor.CurrentAction,"Sleep must not reset its own idle timer every frame");
            Assert.AreEqual(position,actor.Position);
            Time.timeScale=0;
            var sprite=actor.Renderer.sprite;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.AreEqual(ActionNames.Sleep,actor.CurrentAction);
            Assert.AreEqual(sprite,actor.Renderer.sprite);
            Time.timeScale=1;
        }

        [UnityTest]
        public IEnumerator Clicking_sleeping_Egypt_wakes_Jumps_once_and_resumes_Walk()
        {
            var actor=NewActor();
            yield return null;
            Arrive(actor,true);
            yield return new WaitForSeconds(CatActor.SleepDelay+.1f);
            Assert.AreEqual(ActionNames.Sleep,actor.CurrentAction);
            int clicks=0;
            actor.Licked+=ignored=>clicks++;
            actor.OnLick();
            Assert.AreEqual(1,clicks);
            Assert.AreEqual(ActionNames.Jump,actor.CurrentAction);
            var position=actor.Position;
            yield return new WaitForSeconds(.3f);
            Assert.AreEqual(position,actor.Position);
            Assert.AreEqual(ActionNames.Jump,actor.CurrentAction);
            yield return new WaitForSeconds(.6f);
            Assert.IsFalse(actor.IsForced);
            Assert.AreEqual(ActionNames.Walk,actor.CurrentAction);
            Assert.Contains(actor.Renderer.sprite,actor.Breed.Walk);
            Assert.AreEqual(1,clicks);
            Assert.Greater((actor.Position-position).magnitude,.01f);
        }

        [UnityTest]
        public IEnumerator Legacy_forced_Idle_keeps_its_existing_rest_timer()
        {
            var breed=LegacyCalicoBreed();
            var actor=Track(new GameObject("LegacyIdle.Test")).AddComponent<CatActor>();
            actor.Setup(breed,new Vector2(6,4));
            actor.PlayAction(ActionNames.Idle);
            yield return new WaitForSeconds(.7f);
            var elapsed=(float)typeof(CatActor).GetField("_sleepAmount",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(actor);
            Assert.Greater(elapsed,.55f,"Legacy forced Idle must retain its original idle slowdown timer");
            Assert.AreEqual(ActionNames.Idle,actor.CurrentAction);
            Assert.IsFalse(breed.UseSleepWhileResting);
        }

        [UnityTest]
        public IEnumerator Rest_expires_and_missing_Sleep_or_short_stops_do_not_sleep()
        {
            var actor=NewActor();
            yield return null;
            Arrive(actor,true);
            yield return new WaitForSeconds(CatActor.SleepDelay+.1f);
            Assert.AreEqual(ActionNames.Sleep,actor.CurrentAction);
            Set(actor,"_pauseUntil",Time.time-1);
            yield return null;
            Assert.AreEqual(ActionNames.Walk,actor.CurrentAction);
            Arrive(actor,false);
            Assert.That(PauseUntil(actor)-Time.time,Is.InRange(.6f,2.4f));
            Assert.AreEqual(ActionNames.Idle,actor.CurrentAction);
            var missing=NewActor(false);
            Arrive(missing,true);
            Assert.That(PauseUntil(missing)-Time.time,Is.InRange(.6f,2.4f));
            Assert.AreEqual(ActionNames.Idle,missing.CurrentAction);
        }
    }
}
