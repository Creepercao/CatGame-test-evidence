#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using CatGame.Idle;
using UnityEngine;
using UnityEngine.UI;

namespace CatGame
{
    public sealed partial class PlayerVisualQA
    {
        [Serializable] sealed class BusinessRequest { public string id, action; public int count; }
        [Serializable] sealed class BusinessReply {
            public bool ok; public string id, action, error, scene, savePath;
            public int pid; public GameState state;
        }
        // File bridge is compiled only into Development players and requires explicit QA mode.
        // Input here is Unity callbacks, not native device input. Saves remain game-owned.
        IEnumerator BusinessFlow()
        {
            Screen.SetResolution(1280,720,false);
            yield return new WaitForSecondsRealtime(1);
            File.WriteAllText(Path.Combine(folder,"business-ready.flag"),"ready");
            float last=Time.realtimeSinceStartup;
            while(Time.realtimeSinceStartup-last<120) {
                string input=Path.Combine(folder,"business-request.json");
                if(!File.Exists(input)) { yield return null; continue; }
                last=Time.realtimeSinceStartup;
                BusinessRequest request=null; string error=null;
                try {
                    request=JsonUtility.FromJson<BusinessRequest>(File.ReadAllText(input));
                    File.Delete(input);
                    Guid parsed;
                    if(request==null || !Guid.TryParseExact(request.id,"N",out parsed)) throw new InvalidOperationException("Invalid request id");
                    if(request.action=="new" || request.action=="continue") {
                        var menu=FindObjectOfType<MainMenuUI>();
                        if(menu==null) throw new InvalidOperationException("Expected main menu");
                        var button=menu.transform.Find("Panel/Btn_"+(request.action=="new"?"NewGame":"Continue")).GetComponent<Button>();
                        if(!button.interactable) throw new InvalidOperationException("Menu action unavailable");
                        button.onClick.Invoke();
                    } else if(request.action=="click") {
                        if(request.count<1 || request.count>1000) throw new InvalidOperationException("Click count out of range");
                        for(int n=0;n<request.count;n++) CatWorld.I.Cat.OnLick();
                    } else if(request.action=="buy") {
                        Button button=null;
                        foreach(var candidate in CatWorld.I.Hud.GetComponentsInChildren<Button>(true))
                            if(candidate.name=="Cat_Milk") button=candidate;
                        if(button==null || !button.interactable) throw new InvalidOperationException("Milk purchase unavailable");
                        button.onClick.Invoke();
                    } else if(request.action=="quit") SaveSystem.Instance.SaveNow();
                    else if(request.action!="snapshot") throw new InvalidOperationException("Unknown business action");
                } catch(Exception exception) { error=exception.ToString(); }
                yield return new WaitForSecondsRealtime(.25f);
                if(error==null && (request.action=="new" || request.action=="continue")) {
                    try {
                        foreach(var button in CatWorld.I.Hud.GetComponentsInChildren<Button>(true))
                            if(button.name=="Btn_Collect") button.onClick.Invoke();
                        if(CatWorld.I.Hud.Intro.IsOpen) CatWorld.I.Hud.Intro.Skip();
                    } catch(Exception exception) { error=exception.ToString(); }
                }
                yield return null;
                if(request==null || string.IsNullOrEmpty(request.id)) { Application.Quit(1); yield break; }
                var reply=new BusinessReply {id=request.id,action=request.action,ok=error==null,error=error,
                    scene=SceneFlow.Current,pid=System.Diagnostics.Process.GetCurrentProcess().Id,
                    state=SaveSystem.Instance.State,savePath=SaveSystem.Instance.SavePath};
                // Capture before ACK so callers can retain the failing step's actual frame.
                ScreenCapture.CaptureScreenshot(Path.Combine(folder,request.id+".png"));
                yield return new WaitForEndOfFrame(); yield return null;
                string replyPath=Path.Combine(folder,request.id+".json");
                File.WriteAllText(replyPath+".tmp",JsonUtility.ToJson(reply,true));
                File.Move(replyPath+".tmp",replyPath);
                if(request.action=="quit") yield break;
            }
            File.WriteAllText(Path.Combine(folder,"business-idle-timeout.txt"),"No command received for 120 seconds");
        }
    }
}
#endif
