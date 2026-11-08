using System;
using System.Collections;
using System.IO;
using CatGame.Idle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatGame.Tests
{
    public class IntroSaveLifecycleTests : CatGameTestBase
    {
        [UnityTest]
        public IEnumerator Quit_before_intro_completion_updates_clock_but_first_menu_does_not_create_save()
        {
            string previous=Environment.GetEnvironmentVariable("CATGAME_EDITOR_QA_ROOT");
            string folder=Path.Combine(Application.temporaryCachePath,"IntroLifecycle-"+Guid.NewGuid().ToString("N"));
            SaveSystem save=null;
            try
            {
                Environment.SetEnvironmentVariable("CATGAME_EDITOR_QA_ROOT",folder);
                save=new GameObject("Save.Lifecycle.Test").AddComponent<SaveSystem>();
                yield return null;
                save.SendMessage("OnApplicationQuit");
                Assert.IsFalse(File.Exists(save.SavePath),"merely viewing the menu must not create progress");
                save.BeginNewGame();
                save.State.lastSeenUnix=100;
                Assert.IsFalse(save.State.introCompleted);
                save.SendMessage("OnApplicationQuit");
                var loaded=SaveSystem.LoadOrCreate(save.SavePath);
                Assert.Greater(loaded.lastSeenUnix,100,"paused intro must not become phantom offline time on quit");
                Assert.IsFalse(loaded.introCompleted,"interrupted introduction remains pending");
            }
            finally
            {
                if(save != null) UnityEngine.Object.DestroyImmediate(save.gameObject);
                Environment.SetEnvironmentVariable("CATGAME_EDITOR_QA_ROOT",previous);
                Assert.IsTrue(Path.GetFullPath(folder).StartsWith(Path.GetFullPath(Application.temporaryCachePath)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase));
                if(Directory.Exists(folder)) Directory.Delete(folder,true);
            }
        }
    }
}
