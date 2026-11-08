#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CatGame.Idle;
using UnityEngine;
using UnityEngine.UI;

namespace CatGame
{
    public sealed partial class PlayerVisualQA
    {
        static readonly Vector2Int[] StorySizes={new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(1024,768)};
        IEnumerator WaitForStory(StoryPlayer player,string id)
        {
            float deadline=Time.realtimeSinceStartup+5;
            while(!player.IsOpen && Time.realtimeSinceStartup<deadline) yield return null;
            Require(player.IsOpen && player.CurrentId==id,"Expected story: "+id);
        }
        void FitsScreen(RectTransform rect,string name)
        {
            var corners=new Vector3[4]; rect.GetWorldCorners(corners);
            foreach(var p in corners) Require(p.x>=-1 && p.x<=Screen.width+1 && p.y>=-1 && p.y<=Screen.height+1,name+" clips screen");
        }
        void FitsText(Text text)
        { Require(text.preferredHeight<=text.rectTransform.rect.height+2,text.name+" text exceeds available height"); FitsScreen(text.rectTransform,text.name); }
        IEnumerator StoryFlow()
        {
            SaveSystem.Instance.BeginNewGame(); SceneFlow.GoHome();
            yield return new WaitForSecondsRealtime(.5f);
            var world=CatWorld.I; world.Hud.Intro.Skip(); yield return null;
            var state=world.Economy.State;
            foreach(var c in state.cats) c.count=40;
            foreach(var item in state.items) item.level=ItemCatalog.LevelsPerItem;
            state.clicks=1000;
            world.Roster.Rebuild(state);
            state.totalEarned=IdleBalance.WinThreshold;
            world.Economy.Initialize(state,world.Roster);
            AchievementCatalog.EvaluateNew(state); StoryCatalog.EnqueueUnlocked(state);
            SaveSystem.Instance.SaveNow();
            var timings=new List<double>();
            foreach(var story in StoryCatalog.All)
            {
                Debug.Log("[Story QA STEP] display all three sizes, verify input block and complete: "+story.id);
                yield return WaitForStory(world.Hud.Stories,story.id);
                timings.Add(world.Hud.Stories.LastOpenMilliseconds);
                double money=state.catHair, play=state.playSeconds; int clicks=state.clicks;
                foreach(var size in StorySizes)
                {
                    Screen.SetResolution(size.x,size.y,false); yield return new WaitForSecondsRealtime(.5f);
                    var overlay=world.Hud.transform.Find("StoryCG");
                    var art=overlay.Find("Art").GetComponent<Image>();
                    Require(art.sprite!=null && art.preserveAspect,"CG art remains aspect-preserved");
                    FitsScreen(art.rectTransform,"CG art"); FitsText(overlay.Find("Title").GetComponent<Text>()); FitsText(overlay.Find("Caption").GetComponent<Text>());
                    FitsScreen((RectTransform)overlay.Find("Btn_close_CG"),"CG continue");
                    world.Cat.OnLick(); Require(!world.Economy.BuyCat("Milk") && !world.Economy.BuyItem("item_1_1"),"CG blocks purchases");
                    yield return Shot("cg-"+story.id);
                }
                Require(state.catHair==money && state.playSeconds==play && state.clicks==clicks,"CG pauses production/playtime and click-through");
                UnityEngine.EventSystems.ExecuteEvents.Execute(world.Hud.transform.Find("StoryCG").gameObject,new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current),UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler); yield return null;
            }
            yield return null;
            Require(!world.Hud.Stories.IsOpen && state.seenStoryEvents.Count==AchievementCatalog.All.Length,"Every achievement memory completes once");
            // Let the injected roster render before photographing the unobscured shop.
            yield return new WaitForSecondsRealtime(.25f);
            var loaded=SaveSystem.LoadOrCreate(SaveSystem.Instance.SavePath);
            Require(loaded.seenStoryEvents.Count==AchievementCatalog.All.Length && loaded.pendingStoryEvents.Count==0,"Seen/pending saved immediately");
            state.catHair=EndingRules.RocketPrice+123; world.Hud.RefreshDisplay();
            Click(world,"PurchasePanel/Btn_shop_rocket");
            foreach(var size in StorySizes)
            { Screen.SetResolution(size.x,size.y,false); yield return new WaitForSecondsRealtime(.4f); yield return Shot("shop-rocket"); }
            Require(world.Hud.Rocket.Open(),"Rocket confirmation opens");
            double beforeCancel=state.catHair; world.Hud.Rocket.Cancel(); Require(state.catHair==beforeCancel,"Cancel does not charge");
            yield return null;
            Require(world.Hud.Rocket.Open(),"Rocket confirmation reopens");
            foreach(var size in StorySizes)
            {
                Screen.SetResolution(size.x,size.y,false); yield return new WaitForSecondsRealtime(.4f);
                FitsScreen((RectTransform)world.Hud.transform.Find("RocketConfirmation/Card"),"Rocket confirmation");
                yield return Shot("rocket-confirmation");
            }
            double beforeBuy=state.catHair;
            world.Hud.Rocket.Confirm(); world.Hud.Rocket.Confirm();
            Require(Math.Abs(state.catHair-(beforeBuy-EndingRules.RocketPrice))<.001,"Rocket charged once");
            loaded=SaveSystem.LoadOrCreate(SaveSystem.Instance.SavePath);
            Require(loaded.rocketPurchased && loaded.hasEndingSnapshot && !loaded.endingCompleted,"Paid pending ending saved before presentation");
            string first=JsonUtility.ToJson(state.firstEnding);
            File.WriteAllText(Path.Combine(folder,"first-ending-before.json"),first);
            yield return WaitForStory(world.Hud.Stories,StoryPlayer.EndingId);
            timings.Add(world.Hud.Stories.LastOpenMilliseconds);
            foreach(var size in StorySizes)
            { Screen.SetResolution(size.x,size.y,false); yield return new WaitForSecondsRealtime(.4f); yield return Shot("ending-CG"); }
            world.Hud.Stories.Complete(); yield return null;
            foreach(var size in StorySizes)
            {
                Screen.SetResolution(size.x,size.y,false); yield return new WaitForSecondsRealtime(.4f);
                var card=world.Hud.transform.Find("EndingStatistics/Card"); FitsScreen((RectTransform)card,"Ending statistics");
                FitsText(card.Find("Details").GetComponent<Text>()); FitsScreen((RectTransform)card.Find("Btn_continue_ending"),"Ending continue");
                FitsScreen((RectTransform)card.Find("Btn_menu_ending"),"Ending menu");
                yield return Shot("ending-statistics");
            }
            Click(world,"EndingStatistics/Card/Btn_continue_ending"); yield return null;
            Require(state.endingCompleted && !world.Hud.Stories.IsOpen && Time.timeScale>0,"Continue ends the modal once");
            state.clicks++; state.catHair+=5; state.totalEarned+=5; SaveSystem.Instance.SaveNow();
            Require(first==JsonUtility.ToJson(state.firstEnding),"First launch snapshot stays byte-identical in memory");
            var restoredSnapshot=SaveSystem.LoadOrCreate(SaveSystem.Instance.SavePath).firstEnding;
            File.WriteAllText(Path.Combine(folder,"first-ending-restored.json"),JsonUtility.ToJson(restoredSnapshot));
            var originalSnapshot=state.firstEnding;
            Require(Math.Abs(originalSnapshot.playSeconds-restoredSnapshot.playSeconds)<1e-9
                && Math.Abs(originalSnapshot.totalEarned-restoredSnapshot.totalEarned)<=Math.Max(1e-9,Math.Abs(originalSnapshot.totalEarned)*2e-15)
                && originalSnapshot.clicks==restoredSnapshot.clicks && originalSnapshot.totalCats==restoredSnapshot.totalCats
                && originalSnapshot.breeds==restoredSnapshot.breeds && originalSnapshot.itemLevels==restoredSnapshot.itemLevels
                && originalSnapshot.achievementCount==restoredSnapshot.achievementCount && originalSnapshot.completedUnix==restoredSnapshot.completedUnix
                && originalSnapshot.hasFullPlaytimeHistory==restoredSnapshot.hasFullPlaytimeHistory,"Saved first snapshot fields stay immutable within double serialization precision");
            Require(!world.Economy.BuyRocket(),"Already launched rocket cannot be repurchased");

            // Repeated CG display uses fresh isolated preview states; it never resets real per-save history.
            world.Hud.Stories.enabled=false;
            GC.Collect(); long memoryBefore=GC.GetTotalMemory(true);
            int childrenBefore=world.Hud.transform.childCount;
            var frameMs=new List<float>();
            for(int cycle=0;cycle<10;cycle++)
            {
                var preview=GameStateFactory.CreateNewGame(); preview.introCompleted=true;
                foreach(var story in StoryCatalog.All) preview.achievements.Add(story.id);
                var player=world.Hud.gameObject.AddComponent<StoryPlayer>();
                player.Initialize((RectTransform)world.Hud.transform,preview,world.Hud.Modals);
                foreach(var story in StoryCatalog.All)
                {
                    yield return WaitForStory(player,story.id); timings.Add(player.LastOpenMilliseconds);
                    frameMs.Add(Time.unscaledDeltaTime*1000); player.Complete();
                    RecordStep("stress-complete","cycle="+cycle+"; story="+story.id,true); yield return null;
                }
                Destroy(player); yield return null;
                Require(!world.Hud.Modals.IsBusy && world.Hud.transform.childCount==childrenBefore,"CG stress releases modal and destroys every overlay");
            }
            GC.Collect(); long memoryAfter=GC.GetTotalMemory(true);
            frameMs.Sort();
            File.WriteAllText(Path.Combine(folder,"story-performance.json"),JsonUtility.ToJson(new StoryPerformance {
                openCpuMs=timings.ToArray(),samples=frameMs.Count,p95FrameMs=frameMs[(int)(frameMs.Count*.95f)],
                managedBefore=memoryBefore,managedAfter=memoryAfter,cycles=10,switches=10*StoryCatalog.All.Length,
                device=SystemInfo.deviceModel,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,os=SystemInfo.operatingSystem
            },true));
            // A second isolated new save verifies the real menu button branch.
            SaveSystem.Instance.BeginNewGame(); SceneFlow.GoHome(); yield return new WaitForSecondsRealtime(.5f);
            world=CatWorld.I; world.Hud.Intro.Skip();
            world.Economy.State.catHair=EndingRules.RocketPrice;
            Require(world.Hud.Rocket.Open(),"Second save rocket opens"); world.Hud.Rocket.Confirm();
            yield return WaitForStory(world.Hud.Stories,StoryPlayer.EndingId);
            world.Hud.Stories.Complete(); yield return null;
            Click(world,"EndingStatistics/Card/Btn_menu_ending"); yield return new WaitForSecondsRealtime(.5f);
            Require(SceneFlow.Current==SceneFlow.Menu && SaveSystem.LoadOrCreate(SaveSystem.Instance.SavePath).endingCompleted,"Ending menu saves completion before navigation");
            FindObjectOfType<MainMenuUI>().transform.Find("Panel/Btn_Continue").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(.5f);
            Require(!CatWorld.I.Hud.Intro.IsOpen && !CatWorld.I.Hud.Stories.IsOpen,"Continue from ending menu does not replay intro or ending");
            File.WriteAllText(Path.Combine(folder,"story.txt"),"storyPassed=true\nmilestones="+StoryCatalog.All.Length+"\nsizes=3\nchargedOnce=true\nfirstSnapshotImmutable=true\ncontinueAndMenu=true\nstressSwitches="+(10*StoryCatalog.All.Length)+"\n");
        }
        [Serializable] sealed class StoryPerformance
        {
            public double[] openCpuMs;
            public int samples,cycles,switches;
            public float p95FrameMs;
            public long managedBefore,managedAfter;
            public string device,cpu,gpu,os;
        }
    }
}
#endif
