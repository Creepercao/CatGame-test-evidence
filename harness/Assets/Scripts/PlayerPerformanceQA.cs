#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using CatGame.Idle;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace CatGame
{
    public sealed partial class PlayerVisualQA
    {
        [Serializable] sealed class PerfEnvironment {
            public string unity,cpu,gpu,graphicsApi,os,quality,savePath;
            public int width,height,targetFps,vsync,refreshRate,ramMb,logicalProcessors;
            public bool development,mainThreadValid,gcValid,profilerEnabled;
        }
        [Serializable] sealed class PerfPhase {
            public string name; public double started,elapsed,cpuMilliseconds;
            public long workingSetBefore,workingSetAfter;
            public int frames,visible,owned,boundaryChecks,boundaryErrors;
        }
        [Serializable] struct PerfOperation {
            public string phase,action,id; public int index,frame; public bool success;
            public double elapsedMs,realtime; public long utcTicks;
        }
        [Serializable] sealed class PerfResult {
            public bool complete,probe; public int errors,clicks,purchases,attempts,coldCG,warmCG,roundTrips,corruptRecovery;
            public PerfPhase[] phases; public PerfOperation[] operations;
        }
        [Serializable] struct PerfFrame {
            public int frame,gcCollections;
            public double realtime,frameMs;
            public long mainThreadNs,gcBytes,monoBytes,unityBytes,reservedBytes;
        }
        ProfilerRecorder perfMain,perfGc;
        List<PerfFrame> perfFrames;
        readonly List<PerfPhase> perfPhases=new List<PerfPhase>();
        readonly List<PerfOperation> perfOperations=new List<PerfOperation>(6000);
        PerfPhase perfPhase;
        CatActor[] perfActors;
        System.Diagnostics.Process perfProcess;
        double perfCpuBefore;
        int perfErrors;
        PerfResult perfResultState;
        bool perfFinalized,perfFaultProbe;
        [Serializable] sealed class PerfPartial { public string phase; public double elapsed; public PerfFrame[] frames; }
        static readonly CultureInfo PerfCulture=CultureInfo.InvariantCulture;
        [StructLayout(LayoutKind.Sequential)] struct PerfMemoryCounters {
            public uint cb,pageFaultCount;
            public UIntPtr peakWorkingSet,workingSet,quotaPeakPaged,quotaPaged,quotaPeakNonPaged,quotaNonPaged,pagefile,peakPagefile;
        }
        [DllImport("kernel32.dll",EntryPoint="GetCurrentProcess")] static extern IntPtr PerfProcessHandle();
        [DllImport("psapi.dll",EntryPoint="GetProcessMemoryInfo",SetLastError=true)]
        static extern bool PerfProcessMemory(IntPtr process,out PerfMemoryCounters counters,uint size);
        long PerfWorkingSet()
        {
            PerfMemoryCounters counters;
            bool ok=PerfProcessMemory(PerfProcessHandle(),out counters,(uint)Marshal.SizeOf(typeof(PerfMemoryCounters)));
            PerfCheck(ok && counters.workingSet.ToUInt64()>0,"Native RSS unavailable; error="+Marshal.GetLastWin32Error());
            return (long)counters.workingSet.ToUInt64();
        }

        void PerfCheck(bool condition,string message)
        {
            if(condition) return;
            File.AppendAllText(Path.Combine(folder,"performance-errors.txt"),message+"\n");
            Application.Quit(1);
            throw new InvalidOperationException(message);
        }
        void PerfLog(string message,string stack,LogType type)
        {
            if(type!=LogType.Error && type!=LogType.Exception && type!=LogType.Assert) return;
            perfErrors++;
            try { File.AppendAllText(Path.Combine(folder,"performance-errors.txt"),message+"\n"+stack+"\n"); } catch { }
            try { PerfFinish(false); } catch { }
            Application.Quit(1);
        }
        void PerfBegin(string name)
        {
            perfFrames=new List<PerfFrame>(20000);
            perfPhase=new PerfPhase {name=name};
            File.WriteAllText(Path.Combine(folder,"progress.json"),"{\"phase\":\""+name+"\",\"state\":\"active\"}");
        }
        void PerfStart()
        {
            // Rebuild destroys excess candidates at end-of-frame. Snapshot after warmup,
            // not immediately after Rebuild while doomed components still exist.
            perfActors=CatWorld.I.Roster.GetComponentsInChildren<CatActor>();
            PerfCheck(perfActors.Length==CatWorld.I.Roster.VisibleCount,"Actor snapshot does not match settled roster");
            perfProcess.Refresh(); perfCpuBefore=perfProcess.TotalProcessorTime.TotalMilliseconds;
            perfPhase.workingSetBefore=PerfWorkingSet();
            perfPhase.started=Time.realtimeSinceStartupAsDouble;
            perfPhase.visible=CatWorld.I.Roster.VisibleCount; perfPhase.owned=CatWorld.I.Roster.TotalOwned;
        }
        void PerfCapture()
        {
            perfFrames.Add(new PerfFrame {frame=Time.frameCount,realtime=Time.realtimeSinceStartupAsDouble,
                frameMs=Time.unscaledDeltaTime*1000.0,mainThreadNs=perfMain.LastValue,gcBytes=perfGc.LastValue,
                monoBytes=Profiler.GetMonoUsedSizeLong(),unityBytes=Profiler.GetTotalAllocatedMemoryLong(),
                reservedBytes=Profiler.GetTotalReservedMemoryLong(),gcCollections=GC.CollectionCount(0)});
            if(perfFrames.Count%60==0 && CatWorld.I.WalkSurface!=null) {
                perfPhase.boundaryChecks++;
                foreach(var actor in perfActors) {
                    if(actor==null) PerfCheck(false,"Actor destroyed during active performance phase: "+perfPhase.name);
                    if(!CatWorld.I.WalkSurface.Contains(actor.transform.position+Vector3.up*actor.GroundOffset)) perfPhase.boundaryErrors++;
                }
            }
        }
        void PerfEnd()
        {
            perfPhase.elapsed=Time.realtimeSinceStartupAsDouble-perfPhase.started;
            perfProcess.Refresh();
            perfPhase.cpuMilliseconds=perfProcess.TotalProcessorTime.TotalMilliseconds-perfCpuBefore;
            perfPhase.workingSetAfter=PerfWorkingSet();
            perfPhase.frames=perfFrames.Count;
            var text=new StringBuilder(perfFrames.Count*100);
            text.AppendLine("frame,realtime,frame_ms,main_thread_ns,gc_bytes,mono_bytes,unity_allocated_bytes,unity_reserved_bytes,gc_collections");
            foreach(var sample in perfFrames) {
                text.Append(sample.frame).Append(',').Append(sample.realtime.ToString("R",PerfCulture)).Append(',')
                    .Append(sample.frameMs.ToString("R",PerfCulture)).Append(',').Append(sample.mainThreadNs).Append(',')
                    .Append(sample.gcBytes).Append(',').Append(sample.monoBytes).Append(',').Append(sample.unityBytes).Append(',')
                    .Append(sample.reservedBytes).Append(',').Append(sample.gcCollections).AppendLine();
            }
            File.WriteAllText(Path.Combine(folder,"frames-"+perfPhase.name+".csv"),text.ToString());
            perfPhases.Add(perfPhase);
            File.WriteAllText(Path.Combine(folder,"phase-"+perfPhase.name+".json"),JsonUtility.ToJson(perfPhase,true));
            RecordStep("performance-phase",perfPhase.name+"; frames="+perfPhase.frames+"; seconds="+perfPhase.elapsed,true);
            PerfCheck(perfPhase.boundaryErrors==0,"Performance boundary observations failed: "+perfPhase.name);
            perfFrames=null;
        }
        void PerfFinish(bool complete)
        {
            if(perfFinalized || perfResultState==null) return;
            perfFinalized=true;
            Application.logMessageReceived-=PerfLog;
            try {
                if(perfFrames!=null && perfPhase!=null && !perfPhases.Contains(perfPhase))
                    File.WriteAllText(Path.Combine(folder,"performance-partial.json"),JsonUtility.ToJson(new PerfPartial {
                        phase=perfPhase.name,elapsed=Time.realtimeSinceStartupAsDouble-perfPhase.started,frames=perfFrames.ToArray()
                    },true));
                perfResultState.complete=complete && perfErrors==0;
                perfResultState.errors=perfErrors; perfResultState.phases=perfPhases.ToArray(); perfResultState.operations=perfOperations.ToArray();
                File.WriteAllText(Path.Combine(folder,"performance-result.json"),JsonUtility.ToJson(perfResultState,true));
            } finally {
                if(perfMain.Valid) perfMain.Dispose(); if(perfGc.Valid) perfGc.Dispose();
                if(perfProcess!=null) perfProcess.Dispose();
            }
        }
        void OnApplicationQuit() { PerfFinish(false); }
        void PerfOp(string phase,string action,int index,double milliseconds,bool success=true,string id=null)
        {
            perfOperations.Add(new PerfOperation {phase=phase,action=action,index=index,elapsedMs=milliseconds,
                frame=Time.frameCount,success=success,id=id,realtime=Time.realtimeSinceStartupAsDouble,utcTicks=DateTime.UtcNow.Ticks});
        }
        static double PerfMilliseconds(long started)
        { return (System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000.0/System.Diagnostics.Stopwatch.Frequency; }
        IEnumerator PerfTimed(string name,float seconds,float warmup)
        {
            PerfBegin(name); yield return new WaitForSecondsRealtime(warmup); PerfStart();
            while(Time.realtimeSinceStartupAsDouble-perfPhase.started<seconds) {
                yield return null; PerfCapture();
                if(perfFaultProbe && name=="probe" && perfFrames.Count>=5) PerfCheck(false,"Controlled sampling failure fixture");
            }
            PerfEnd();
        }
        IEnumerator PerformanceFlow()
        {
            Application.logMessageReceived+=PerfLog;
            Application.runInBackground=true; QualitySettings.vSyncCount=0; Application.targetFrameRate=60;
            Screen.SetResolution(1280,720,false);
            var result=new PerfResult {probe=Environment.GetEnvironmentVariable("CATGAME_PERF_PROBE")=="1"};
            perfResultState=result;
            perfFaultProbe=Environment.GetEnvironmentVariable("CATGAME_PERF_FAULT")=="1";
            try {
                perfProcess=System.Diagnostics.Process.GetCurrentProcess();
                long loading=System.Diagnostics.Stopwatch.GetTimestamp();
                SaveSystem.Instance.BeginNewGame(); SceneFlow.GoHome();
                double readyDeadline=Time.realtimeSinceStartupAsDouble+10;
                yield return null;
                while((CatWorld.I==null || CatWorld.I.Hud==null || CatWorld.I.Hud.Intro==null || !CatWorld.I.Hud.Intro.IsOpen)
                    && Time.realtimeSinceStartupAsDouble<readyDeadline) yield return null;
                PerfCheck(CatWorld.I!=null && CatWorld.I.Hud!=null && CatWorld.I.Hud.Intro!=null && CatWorld.I.Hud.Intro.IsOpen,"New-game scene did not become ready");
                PerfOp("startup","new-game-home-ready",0,PerfMilliseconds(loading));
                var world=CatWorld.I; world.Hud.Intro.Skip(); yield return null;
                // Prevent achievement popups during controlled loads; this is QA state, not production tuning.
                foreach(var story in StoryCatalog.All) {
                    if(!world.Economy.State.achievements.Contains(story.id)) world.Economy.State.achievements.Add(story.id);
                    if(!world.Economy.State.seenStoryEvents.Contains(story.id)) world.Economy.State.seenStoryEvents.Add(story.id);
                }
                world.Economy.State.pendingStoryEvents.Clear();
                perfMain=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1);
                perfGc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);
                yield return new WaitForSecondsRealtime(1);
                File.WriteAllText(Path.Combine(folder,"performance-environment.json"),JsonUtility.ToJson(new PerfEnvironment {
                    unity=Application.unityVersion,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,
                    graphicsApi=SystemInfo.graphicsDeviceType.ToString(),os=SystemInfo.operatingSystem,
                    quality=QualitySettings.names[QualitySettings.GetQualityLevel()],savePath=SaveSystem.Instance.SavePath,
                    width=Screen.width,height=Screen.height,targetFps=Application.targetFrameRate,vsync=QualitySettings.vSyncCount,
                    refreshRate=Screen.currentResolution.refreshRate,ramMb=SystemInfo.systemMemorySize,logicalProcessors=SystemInfo.processorCount,
                    development=Debug.isDebugBuild,mainThreadValid=perfMain.Valid,gcValid=perfGc.Valid,profilerEnabled=Profiler.enabled
                },true));
                PerfCheck(perfMain.Valid && perfGc.Valid && perfMain.Count>0 && perfGc.Count>0,"Required profiler counters unavailable");
                PerfCheck(Screen.width==1280 && Screen.height==720,"Unexpected performance resolution");
                if(result.probe) {
                    yield return PerfTimed("probe",2,1);
                } else {
                    yield return PerfTimed("single",60,10);
                    foreach(var cat in world.Economy.State.cats) cat.count=40;
                    world.Roster.Rebuild(world.Economy.State); world.Economy.Initialize(world.Economy.State,world.Roster);
                    PerfCheck(world.Roster.VisibleCount==100 && world.Roster.TotalOwned==440,"Performance roster setup mismatch");
                    yield return PerfTimed("hundred",90,10);
                    world.Economy.State.catHair=1e12; world.Economy.State.totalEarned=1e12;
                    int initialClicks=world.Economy.State.clicks;
                    PerfBegin("input"); yield return new WaitForSecondsRealtime(10); PerfStart();
                    double nextClick=perfPhase.started,nextBuy=nextClick;
                    while(Time.realtimeSinceStartupAsDouble-perfPhase.started<60) {
                        double now=Time.realtimeSinceStartupAsDouble;
                        if(now>=nextClick) {
                            long started=System.Diagnostics.Stopwatch.GetTimestamp(); world.Cat.OnLick();
                            PerfOp("input","click",result.clicks++,PerfMilliseconds(started)); nextClick+=.05;
                        }
                        if(now>=nextBuy) {
                            var item=ItemCatalog.All[result.attempts%ItemCatalog.All.Length];
                            long started=System.Diagnostics.Stopwatch.GetTimestamp(); bool bought=world.Economy.BuyItem(item.id);
                            PerfOp("input","buy",result.attempts++,PerfMilliseconds(started),bought,item.id);
                            if(bought) result.purchases++; nextBuy+=.1;
                        }
                        yield return null; PerfCapture();
                    }
                    PerfEnd();
                    PerfCheck(world.Economy.State.clicks-initialClicks==result.clicks && result.purchases==100,"Performance input counters or purchases lost");
                    yield return new WaitForSecondsRealtime(3); // Let input reward labels expire before checking CG cleanup.
                    int childCount=world.Hud.transform.childCount;
                    for(int cycle=0;cycle<11;cycle++) {
                        if(cycle==0) { PerfBegin("cg-cold"); PerfStart(); }
                        if(cycle==1) { PerfEnd(); PerfBegin("cg-warm"); yield return new WaitForSecondsRealtime(10); PerfStart(); }
                        var preview=GameStateFactory.CreateNewGame(); preview.introCompleted=true;
                        foreach(var story in StoryCatalog.All) preview.achievements.Add(story.id);
                        var player=world.Hud.gameObject.AddComponent<StoryPlayer>();
                        player.Initialize((RectTransform)world.Hud.transform,preview,world.Hud.Modals);
                        foreach(var story in StoryCatalog.All) {
                            double end=Time.realtimeSinceStartupAsDouble+5;
                            while(!player.IsOpen && Time.realtimeSinceStartupAsDouble<end) { yield return null; PerfCapture(); }
                            PerfCheck(player.IsOpen && player.CurrentId==story.id,"CG sequence failed: "+story.id);
                            int operationIndex=cycle==0?result.coldCG:result.warmCG;
                            PerfOp(cycle==0?"cg-cold":"cg-warm","open",operationIndex,player.LastOpenMilliseconds,true,story.id);
                            long started=System.Diagnostics.Stopwatch.GetTimestamp(); player.Complete();
                            PerfOp(cycle==0?"cg-cold":"cg-warm","complete",operationIndex,PerfMilliseconds(started),true,story.id);
                            if(cycle==0) result.coldCG++; else result.warmCG++;
                            yield return null; PerfCapture();
                        }
                        Destroy(player); yield return null; PerfCapture();
                        PerfCheck(!world.Hud.Modals.IsBusy && world.Hud.transform.childCount==childCount,"CG modal or overlay leak");
                    }
                    PerfEnd();
                    var fixture=GameStateFactory.CreateNewGame(); fixture.catHair=12345.5; fixture.totalEarned=50000; fixture.clicks=777;
                    foreach(var cat in fixture.cats) cat.count=40;
                    foreach(var item in fixture.items) item.level=5;
                    string savePath=Path.Combine(folder,"save-stress","CatGame","save.json");
                    PerfBegin("save"); yield return new WaitForSecondsRealtime(10); PerfStart();
                    for(int i=0;i<1000;i++) {
                        long started=System.Diagnostics.Stopwatch.GetTimestamp(); SaveSystem.SaveToPath(savePath,fixture);
                        PerfOp("save","write",i,PerfMilliseconds(started));
                        started=System.Diagnostics.Stopwatch.GetTimestamp(); var loaded=SaveSystem.LoadOrCreate(savePath);
                        PerfOp("save","read",i,PerfMilliseconds(started));
                        PerfCheck(loaded.clicks==777 && loaded.catHair==12345.5 && loaded.cats[0].count==40,"Save roundtrip mismatch");
                        if(i==500) {
                            File.WriteAllText(savePath,"{ deliberate corruption in isolated stress fixture");
                            var recovered=SaveSystem.LoadOrCreate(savePath);
                            PerfCheck(recovered.clicks==0 && recovered.cats[0].count==1,"Corrupt save recovery failed");
                            result.corruptRecovery++;
                        }
                        result.roundTrips++; yield return null; PerfCapture();
                    }
                    PerfEnd();
                }
                result.complete=true;
            } finally {
                PerfFinish(result.complete);
                Application.logMessageReceived-=PerfLog;
            }
        }
    }
}
#endif
