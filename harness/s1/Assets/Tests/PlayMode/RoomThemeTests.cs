using System.Collections;
using CatGame.Idle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatGame.Tests
{
    public sealed class RoomThemeTests : CatGameTestBase
    {
        CatWorld World()
        {
            global::CatGame.CatGame game;
            var world=NewWorld(out game);
            var room=Track(new GameObject("ThemeFixtureRoom"));
            var renderer=room.AddComponent<SpriteRenderer>();
            renderer.sprite=Resources.Load<Sprite>("Art/Room/Pixel/Growth/Starter/architecture");
            room.transform.localScale=Vector3.one*1.25f;
            var surface=room.AddComponent<RoomWalkSurface>();surface.Background=renderer;world.WalkSurface=surface;
            world.FloorCenter=WorldArtSettings.Current.HomeFloorCenter;world.FloorHalf=WorldArtSettings.Current.HomeHalf;
            return world;
        }

        static IEnumerator WaitTheme(CatWorld world,string id)
        {
            float end=Time.realtimeSinceStartup+3;
            while((world.RoomThemes.CurrentId!=id || world.RoomThemes.IsTransitioning) && Time.realtimeSinceStartup<end)
                yield return null;
            Assert.AreEqual(id,world.RoomThemes.CurrentId);Assert.IsFalse(world.RoomThemes.IsTransitioning);
        }

        [UnityTest] public IEnumerator Crossing_theme_preserves_state_actors_items_and_spending_does_not_revert()
        {
            var world=World();yield return null;
            world.Economy.enabled=false;var state=world.Economy.State;state.introCompleted=true;
            var actor=world.Cat;var roster=world.Roster;var platform=world.Platform;var hud=world.Hud;
            state.catHair=1234;int owned=world.Roster.TotalOwned;
            Assert.AreEqual("starter",world.RoomThemes.CurrentId);
            state.totalEarned=100000;yield return WaitTheme(world,"companions");
            Assert.AreSame(actor,world.Cat);Assert.AreSame(roster,world.Roster);Assert.AreSame(platform,world.Platform);Assert.AreSame(hud,world.Hud);
            Assert.AreSame(state,world.Economy.State);Assert.AreEqual(1234,state.catHair);Assert.AreEqual(owned,roster.TotalOwned);
            Assert.IsTrue(world.WalkSurface.Contains(actor.Position));
            state.catHair=0;yield return new WaitForSecondsRealtime(.25f);
            Assert.AreEqual("companions",world.RoomThemes.CurrentId);Assert.AreEqual(2,world.RoomThemes.ChangeCount);
        }

        [UnityTest] public IEnumerator Existing_modal_and_its_close_frame_take_priority_over_transition()
        {
            var world=World();yield return null;
            world.Economy.enabled=false;var state=world.Economy.State;state.introCompleted=true;
            var owner=new object();Assert.IsTrue(world.Hud.Modals.TryAcquire(owner));
            state.totalEarned=100000000000d;yield return new WaitForSecondsRealtime(.2f);
            Assert.AreEqual("starter",world.RoomThemes.CurrentId);Assert.IsFalse(world.RoomThemes.IsTransitioning);
            world.Hud.Modals.SuppressGameplayThisFrame();world.Hud.Modals.Release(owner);
            Assert.IsFalse(world.RoomThemes.IsTransitioning);
            yield return WaitTheme(world,"companions");Assert.AreEqual(1,Time.timeScale);
        }

        [UnityTest] public IEnumerator Pending_story_is_not_skipped_and_transition_disable_releases_pause()
        {
            var world=World();yield return null;
            world.Economy.enabled=false;var state=world.Economy.State;state.introCompleted=true;
            world.EnableStoryEvents=true;state.pendingStoryEvents.Add("unknown_saved_memory");state.totalEarned=100000;
            yield return new WaitForSecondsRealtime(.15f);Assert.AreEqual("starter",world.RoomThemes.CurrentId);
            state.pendingStoryEvents.Clear();
            float end=Time.realtimeSinceStartup+2;
            while(!world.RoomThemes.IsTransitioning && Time.realtimeSinceStartup<end)yield return null;
            Assert.IsTrue(world.RoomThemes.IsTransitioning);Assert.AreEqual(0,Time.timeScale);
            world.RoomThemes.enabled=false;yield return null;
            Assert.AreEqual(1,Time.timeScale);Assert.IsFalse(world.Hud.Modals.IsBusy);
        }

        [UnityTest] public IEnumerator Newly_covered_cat_foot_is_relocated_without_recreating_actor()
        {
            var world=World();yield return null;
            world.Economy.enabled=false;var actor=world.Cat;
            var before=actor.Position;int id=actor.GetInstanceID();
            var p=world.WalkSurface.SourcePoint(before);
            world.WalkSurface.Obstacles=new[]{new Rect(p.x-5,p.y-5,10,10)};
            Assert.IsTrue(actor.RevalidateRoom());Assert.AreEqual(id,actor.GetInstanceID());
            Assert.IsTrue(world.WalkSurface.Contains(actor.Position));Assert.That(Vector3.Distance(before,actor.Position),Is.GreaterThan(.01f));
        }

        [UnityTest] public IEnumerator Hundred_actors_keep_identity_and_can_roam_after_the_room_change()
        {
            var world=World();yield return null;
            world.Economy.enabled=false;var state=world.Economy.State;state.introCompleted=true;
            foreach(var cat in state.cats)cat.count=40;
            world.Roster.Rebuild(state);
            // Rebuild destroys rejected candidates at frame end; observe the settled roster.
            yield return null;
            var actors=world.Roster.GetComponentsInChildren<CatActor>();Assert.AreEqual(100,actors.Length);
            state.totalEarned=100000;yield return WaitTheme(world,"companions");
            CollectionAssert.AreEquivalent(actors,world.Roster.GetComponentsInChildren<CatActor>());
            var step=typeof(CatActor).GetMethod("StepForced",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var apply=typeof(CatActor).GetMethod("ApplyVisual",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var starts=new Vector3[actors.Length];
            for(int i=0;i<actors.Length;i++){starts[i]=actors[i].Position;actors[i].PlayAction(ActionNames.Walk);}
            for(int tick=0;tick<240;tick++)foreach(var actor in actors)
            {
                var before=actor.Position;
                step.Invoke(actor,new object[]{.25f});apply.Invoke(actor,null);
                Assert.IsTrue(world.WalkSurface.Contains(actor.transform.position));
                Assert.IsTrue(world.WalkSurface.AllowsSegment(before,actor.Position));
            }
            int moved=0;for(int i=0;i<actors.Length;i++)if(Vector3.Distance(starts[i],actors[i].Position)>.1f)moved++;
            Assert.Greater(moved,90);Assert.AreEqual(440,world.Roster.TotalOwned);
        }

        [UnityTest] public IEnumerator Missing_next_theme_art_retains_the_old_room_and_releases_the_modal()
        {
            var world=World();yield return null;
            world.Economy.enabled=false;var state=world.Economy.State;state.introCompleted=true;
            var settings=Object.Instantiate(Resources.Load<RoomThemeSettings>(RoomThemeSettings.ResourcePath));
            Object.Destroy(world.RoomThemes);yield return null;
            var controller=world.gameObject.AddComponent<RoomThemeController>();controller.Initialize(world,state,settings);
            var before=world.WalkSurface.Background.sprite;
            settings.Themes[1].ResourceRoot="Missing/RoomTheme";
            LogAssert.Expect(LogType.Error,"Room theme could not be applied: Missing room theme art: companions");
            state.totalEarned=100000;yield return new WaitForSecondsRealtime(1);
            Assert.AreEqual("starter",controller.CurrentId);Assert.AreSame(before,world.WalkSurface.Background.sprite);
            Assert.AreEqual(1,Time.timeScale);Assert.IsFalse(world.Hud.Modals.IsBusy);
            Assert.IsFalse(controller.IsTransitioning);Object.Destroy(settings);
        }

        [UnityTest] public IEnumerator Live_debugger_counts_earned_only_when_requested_and_spending_keeps_the_room()
        {
            var world=World();yield return null;
            world.Economy.enabled=false;var state=world.Economy.State;state.introCompleted=true;
            // The first real frame already earns a small amount before Update is disabled.
            state.totalEarned=0;
            string output=System.IO.Path.Combine(Application.temporaryCachePath,"S1Debugger",System.Guid.NewGuid().ToString("N"));
            global::CatGame.Debugging.EditorDebugCommands.Apply(world,global::CatGame.Debugging.DebugCommand.SetHair,null,1000000,false,output);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.AreEqual("starter",world.RoomThemes.CurrentId);Assert.AreEqual(0,state.totalEarned);
            global::CatGame.Debugging.EditorDebugCommands.Apply(world,global::CatGame.Debugging.DebugCommand.AddHair,null,100000,true,output);
            yield return WaitTheme(world,"companions");
            global::CatGame.Debugging.EditorDebugCommands.Apply(world,global::CatGame.Debugging.DebugCommand.SetHair,null,0,false,output);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.AreEqual("companions",world.RoomThemes.CurrentId);Assert.AreEqual(100000,state.totalEarned);
            Assert.AreEqual(0,state.catHair);Assert.AreEqual(3,System.IO.Directory.GetDirectories(output).Length);
        }

        [UnityTest] public IEnumerator Actual_achievement_CG_finishes_before_the_room_transition()
        {
            var world=World();world.EnableStoryEvents=true;yield return null;
            var state=world.Economy.State;state.introCompleted=true;
            world.Cat.OnLick();
            float end=Time.realtimeSinceStartup+3;
            while(!world.Hud.Stories.IsOpen && Time.realtimeSinceStartup<end)yield return null;
            Assert.IsTrue(world.Hud.Stories.IsOpen);Assert.AreEqual("first_click",world.Hud.Stories.CurrentId);
            state.totalEarned=100000;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.AreEqual("starter",world.RoomThemes.CurrentId);Assert.IsFalse(world.RoomThemes.IsTransitioning);
            end=Time.realtimeSinceStartup+5;
            while((world.Hud.Stories.IsOpen || state.pendingStoryEvents.Count>0) && Time.realtimeSinceStartup<end)
            {
                Assert.AreEqual("starter",world.RoomThemes.CurrentId);
                if(world.Hud.Stories.IsOpen)world.Hud.Stories.Complete();
                yield return null;
            }
            Assert.Contains("first_click",state.seenStoryEvents);
            yield return WaitTheme(world,"companions");
            Assert.IsFalse(world.Hud.Stories.IsOpen);Assert.AreEqual(1,Time.timeScale);
        }
    }
}
