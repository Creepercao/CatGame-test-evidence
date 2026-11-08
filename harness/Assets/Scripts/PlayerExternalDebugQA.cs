#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System.Collections;
using System.IO;
using CatGame.Idle;
using UnityEngine;
namespace CatGame
{
    public sealed partial class PlayerVisualQA
    {
        IEnumerator ExternalDebugFlow()
        {
            SaveSystem.Instance.BeginNewGame(); SceneFlow.GoHome(); yield return new WaitForSecondsRealtime(.5f);
            var world=CatWorld.I; world.Hud.Intro.Skip(); yield return null;
            File.WriteAllText(Path.Combine(folder,"ready.flag"),"ready");
            var owner=new object(); bool paused=false;
            while(!File.Exists(Path.Combine(folder,"quit.flag"))) {
                if(!paused && File.Exists(Path.Combine(folder,"pause.flag"))) {
                    Require(world.Hud.Modals.TryAcquire(owner),"External test owns modal"); paused=true;
                    File.WriteAllText(Path.Combine(folder,"paused.flag"),"paused");
                }
                if(paused && File.Exists(Path.Combine(folder,"release.flag"))) {
                    world.Hud.Modals.Release(owner); paused=false;
                    File.Delete(Path.Combine(folder,"pause.flag")); File.Delete(Path.Combine(folder,"paused.flag"));
                }
                yield return null;
            }
            if(paused) world.Hud.Modals.Release(owner);
            Require(world.Roster.TotalOwned==11 && world.Roster.VisibleCount==11,"Remote roster edits reached actual actors");
            Require(world.Economy.ProductionPerSecond>0,"Remote item edits recalculated production");
            SaveSystem.Instance.SaveNow(); yield return Shot("external-debug-final");
            File.WriteAllText(Path.Combine(folder,"external.txt"),"externalPassed=true");
        }
    }
}
#endif
