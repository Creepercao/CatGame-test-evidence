#if DEVELOPMENT_BUILD && UNITY_STANDALONE_WIN && !UNITY_EDITOR
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
        [Serializable] sealed class GrowthScreen
        {
            public string theme;
            public int width,height,frame;
            public Vector2Int worldRaster;
        }
        [Serializable] sealed class GrowthReport
        {
            public string source,fixture="explicit QA checkpoint; not natural full-playthrough",theme;
            public int pid,visible,owned,footErrors,depthErrors,samples,nativeHits,clicks;
            public bool complete,restart,identitiesPreserved;
            public List<GrowthScreen> screens=new List<GrowthScreen>();
        }

        IEnumerator GrowthSize(int width,int height)
        {
            Screen.SetResolution(width,height,false);yield return new WaitForSecondsRealtime(.7f);
            if(Screen.width!=width || Screen.height!=height)
            {
                using var process=System.Diagnostics.Process.GetCurrentProcess();process.Refresh();
                var handle=process.MainWindowHandle;
                if(handle!=IntPtr.Zero && F1GetClientRect(handle,out var c) && F1GetWindowRect(handle,out var w))
                    F1SetWindowPos(handle,IntPtr.Zero,0,0,width+(w.right-w.left)-(c.right-c.left),
                        height+(w.bottom-w.top)-(c.bottom-c.top),0x0002|0x0004|0x0010);
                yield return new WaitForSecondsRealtime(.5f);
            }
            Require(Screen.width==width && Screen.height==height,"actual room viewport "+width+"x"+height);
        }

        IEnumerator GrowthNativePet(CatWorld world,GrowthReport report)
        {
            // Freeze only for this one native body hit. This is input verification, not a performance sample.
            Time.timeScale=0;IsScriptedFlow=false;
            foreach(var module in blockedInput)if(module!=null)module.enabled=true;
            blockedInput.Clear();
            var actor=world.Roster.GetFirstActor();var point=Camera.main.WorldToScreenPoint(actor.Renderer.bounds.center);
            using var process=System.Diagnostics.Process.GetCurrentProcess();process.Refresh();var handle=process.MainWindowHandle;
            uint owner;GetWindowThreadProcessId(handle,out owner);
            Require(handle!=IntPtr.Zero && owner==(uint)process.Id && IsWindowVisible(handle),"native pointer target belongs to this Player");
            var previousForeground=GetForegroundWindow();GetCursorPos(out Point previousCursor);
            bool held=false,moved=false;
            try
            {
                SetForegroundWindow(handle);yield return new WaitForSecondsRealtime(.3f);
                if(GetForegroundWindow()!=handle)
                {
                    File.WriteAllText(Path.Combine(folder,"needs-focus.txt"),"Click this owned CatGame QA window title bar; PID="+process.Id);
                    float deadline=Time.realtimeSinceStartup+90;
                    while(GetForegroundWindow()!=handle && Time.realtimeSinceStartup<deadline)yield return null;
                }
                Require(GetForegroundWindow()==handle,"owned QA window has foreground focus before any input");
                var target=new Point{x=Mathf.RoundToInt(point.x),y=Screen.height-Mathf.RoundToInt(point.y)};
                Require(ClientToScreen(handle,ref target),"native client point translated");
                moved=SetCursorPos(target.x,target.y);Require(moved,"cursor positioned in owned Player");
                yield return new WaitForSecondsRealtime(.2f);
                int before=world.Economy.State.clicks;
                RecordStep("native-mouse-down","body screen="+point+" desktop="+target.x+","+target.y,true);
                mouse_event(2,0,0,0,UIntPtr.Zero);held=true;yield return new WaitForSecondsRealtime(.15f);
                mouse_event(4,0,0,0,UIntPtr.Zero);held=false;yield return new WaitForSecondsRealtime(.2f);
                Require(world.Economy.State.clicks==before+1,"one native body click adds exactly one pet after room transition");
                report.nativeHits++;report.clicks=world.Economy.State.clicks;
            }
            finally
            {
                if(held)mouse_event(4,0,0,0,UIntPtr.Zero);
                if(moved)SetCursorPos(previousCursor.x,previousCursor.y);
                if(previousForeground!=IntPtr.Zero)SetForegroundWindow(previousForeground);
                Time.timeScale=1;IsScriptedFlow=true;
            }
        }

        IEnumerator RoomGrowthFlow()
        {
            var report=new GrowthReport{source=Environment.GetEnvironmentVariable("CATGAME_S1_SOURCE"),
                pid=System.Diagnostics.Process.GetCurrentProcess().Id,restart=Environment.GetEnvironmentVariable("CATGAME_S1_RESTART")=="1"};
            Action store=()=>File.WriteAllText(Path.Combine(folder,report.restart?"restart-result.json":"result.json"),JsonUtility.ToJson(report,true));
            Application.logMessageReceived+=(message,stack,type)=>{
                if(type!=LogType.Error && type!=LogType.Exception)return;
                File.AppendAllText(Path.Combine(folder,"errors.txt"),message+"\n"+stack+"\n");store();Application.Quit(1);
            };
            store();
            if(!report.restart)
            {
                SaveSystem.Instance.BeginNewGame();var seed=SaveSystem.Instance.State;seed.introCompleted=true;
                seed.cats[0].count=2;seed.items[0].level=1;seed.items[1].level=1;
                SaveSystem.Instance.SaveNow();
            }
            SceneFlow.GoHome();yield return new WaitForSecondsRealtime(.7f);
            var world=CatWorld.I;Require(world!=null && world.RoomThemes!=null,"real Home scene has room growth controller");
            var collect=world.Hud.transform.Find("OfflineEarningsOverlay/Summary/Btn_Collect");
            if(collect!=null){collect.GetComponent<Button>().onClick.Invoke();yield return null;}
            if(report.restart)
            {
                Require(!world.Hud.Modals.InputBlocked && Time.timeScale>0,"fresh Player resumed playable state after offline summary");
                Require(world.RoomThemes.CurrentId=="companions" && world.RoomThemes.ChangeCount==1,"saved high progress starts directly in companion room");
                Require(world.Roster.TotalOwned==440 && world.Economy.State.clicks>=1,"fresh Player restored saved cats and native pet");
                yield return GrowthSize(1600,900);yield return Shot("restart-companions");
                report.theme=world.RoomThemes.CurrentId;report.owned=world.Roster.TotalOwned;report.visible=world.Roster.VisibleCount;report.clicks=world.Economy.State.clicks;
                report.complete=true;store();Application.Quit(0);yield break;
            }
            Require(world.RoomThemes.CurrentId=="starter","new game starts in warm starter room");
            var state=world.Economy.State;var actorIds=new HashSet<int>();
            foreach(var actor in world.Roster.GetComponentsInChildren<CatActor>())actorIds.Add(actor.GetInstanceID());
            var roster=world.Roster;var platform=world.Platform;var hud=world.Hud;
            foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1600,900),new Vector2Int(2560,1440)})
            {
                yield return GrowthSize(size.x,size.y);yield return Shot("starter");
                report.screens.Add(new GrowthScreen{theme="starter",width=Screen.width,height=Screen.height,frame=Time.frameCount,
                    worldRaster=Camera.main.GetComponent<WorldPixelCamera>().LogicalRenderSize});
            }
            var modal=new object();Require(hud.Modals.TryAcquire(modal),"hold foreground modal while income approaches threshold");
            state.totalEarned=99999.95;yield return new WaitForSecondsRealtime(.3f);
            Require(world.RoomThemes.CurrentId=="starter" && !world.RoomThemes.IsTransitioning,"room defers behind foreground modal");
            hud.Modals.SuppressGameplayThisFrame();hud.Modals.Release(modal);
            float end=Time.realtimeSinceStartup+5;
            while((world.RoomThemes.CurrentId!="companions" || world.RoomThemes.IsTransitioning) && Time.realtimeSinceStartup<end)yield return null;
            Require(world.RoomThemes.CurrentId=="companions" && !world.RoomThemes.IsTransitioning,"live passive income crossed threshold and transitioned");
            report.identitiesPreserved=ReferenceEquals(roster,world.Roster)&&ReferenceEquals(platform,world.Platform)&&ReferenceEquals(hud,world.Hud);
            foreach(var actor in world.Roster.GetComponentsInChildren<CatActor>())report.identitiesPreserved &= actorIds.Contains(actor.GetInstanceID());
            Require(report.identitiesPreserved,"room swap preserved roster, actors, items and HUD");
            state.catHair=0;yield return new WaitForSecondsRealtime(.3f);
            Require(world.RoomThemes.CurrentId=="companions","empty wallet does not revert room");
            foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1600,900),new Vector2Int(2560,1440)})
            {
                yield return GrowthSize(size.x,size.y);yield return Shot("companions");
                report.screens.Add(new GrowthScreen{theme="companions",width=Screen.width,height=Screen.height,frame=Time.frameCount,
                    worldRaster=Camera.main.GetComponent<WorldPixelCamera>().LogicalRenderSize});
            }
            yield return GrowthSize(1600,900);yield return GrowthNativePet(world,report);
            foreach(var cat in state.cats)cat.count=40;
            world.Roster.Rebuild(state);yield return null;yield return new WaitForSecondsRealtime(1);
            foreach(var item in state.items)item.level=1;world.Economy.Initialize(state,world.Roster);
            var actors=world.Roster.GetComponentsInChildren<CatActor>();Require(actors.Length==100,"settled hundred-cat roster");
            float until=Time.realtimeSinceStartup+20;
            while(Time.realtimeSinceStartup<until)
            {
                report.samples++;
                foreach(var actor in actors)if(!world.WalkSurface.Contains(actor.transform.position))report.footErrors++;
                foreach(var depth in world.WalkSurface.GetComponentsInChildren<RoomFurnitureDepth>())
                {
                    int expected=Mathf.RoundToInt((10-depth.transform.TransformPoint(depth.LocalGround).y)*100);
                    foreach(var piece in depth.Pieces)if(piece.sortingOrder!=expected)report.depthErrors++;
                }
                yield return null;
            }
            Require(report.footErrors==0 && report.depthErrors==0,"real-time hundred-cat feet and furniture depth remain valid");
            yield return Shot("companions-hundred");
            SaveSystem.Instance.SaveNow();report.visible=world.Roster.VisibleCount;report.owned=world.Roster.TotalOwned;
            report.theme=world.RoomThemes.CurrentId;report.complete=true;store();Application.Quit(0);
        }
    }
}
#endif
