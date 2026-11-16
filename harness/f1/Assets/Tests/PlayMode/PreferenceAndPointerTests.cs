using System.Collections;
using CatGame.Idle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace CatGame.Tests
{
    public class PreferenceAndPointerTests : CatGameTestBase
    {
        [UnityTest] public IEnumerator Both_settings_windows_share_preferences_and_resolution_survives_reapply()
        {
            string[] keys={"CatGame.NumberFormat","CatGame.Volume","CatGame.Width","CatGame.Height"};
            bool[] existed=System.Array.ConvertAll(keys,PlayerPrefs.HasKey);
            int format=GlobalSettings.NumberFormat,w=PlayerPrefs.GetInt(keys[2]),h=PlayerPrefs.GetInt(keys[3]);
            float volume=GlobalSettings.Volume; int sw=Screen.width,sh=Screen.height; var mode=Screen.fullScreenMode;
            try {
                var menu=Track(new GameObject("SettingsMenu",typeof(RectTransform))).AddComponent<MainMenuUI>();
                global::CatGame.CatGame game; var world=NewWorld(out game); yield return null;
                menu.transform.Find("Panel/Btn_Settings").GetComponent<Button>().onClick.Invoke();
                var pause=world.Hud.GetComponent<PausePanel>(); Assert.IsTrue(pause.Open());
                var a=menu.transform.Find("SettingsOverlay/Card"); var b=world.Hud.transform.Find("PauseOverlay/Window/PageContent/Settings");
                Assert.IsNotNull(a.Find("Btn_resolution")); Assert.IsNotNull(b.Find("Btn_resolution"));
                Debug.Log("PREF STEP 1: menu format changes update already-open game settings and HUD.");
                a.Find("Btn_number_format").GetComponent<Button>().onClick.Invoke(); yield return null;
                Assert.AreEqual(a.Find("Btn_number_format/Label").GetComponent<Text>().text,b.Find("Btn_number_format/Label").GetComponent<Text>().text);
                Debug.Log("PREF STEP 2: game volume/format changes update menu; creating a fresh state does not reset preferences.");
                b.Find("VolumeSlider").GetComponent<Slider>().value=.27f;
                b.Find("Btn_number_format").GetComponent<Button>().onClick.Invoke(); yield return null;
                Assert.AreEqual(.27f,a.Find("VolumeSlider").GetComponent<Slider>().value,.001f);
                Assert.AreEqual(GlobalSettings.FormatLabel,a.Find("Btn_number_format/Label").GetComponent<Text>().text);
                int selected=GlobalSettings.NumberFormat; GameStateFactory.CreateNewGame(); Assert.AreEqual(selected,GlobalSettings.NumberFormat);
                Debug.Log("PREF STEP 3: request window resolution, persist, and reapply startup settings.");
                GlobalSettings.SetResolution(1280,720); yield return null; yield return null;
                Assert.AreEqual(1280,PlayerPrefs.GetInt(keys[2])); Assert.AreEqual(720,PlayerPrefs.GetInt(keys[3]));
                GlobalSettings.ApplySaved(); Assert.AreEqual(.27f,AudioListener.volume,.001f);
                Assert.Throws<System.ArgumentOutOfRangeException>(()=>GlobalSettings.SetResolution(0,0)); world.Hud.transform.Find("PauseOverlay/Window/Btn_resume").GetComponent<Button>().onClick.Invoke();
            } finally {
                PlayerPrefs.SetInt(keys[0],format); PlayerPrefs.SetFloat(keys[1],volume); PlayerPrefs.SetInt(keys[2],w); PlayerPrefs.SetInt(keys[3],h);
                for(int i=0;i<keys.Length;i++) if(!existed[i]) PlayerPrefs.DeleteKey(keys[i]);
                PlayerPrefs.Save(); AudioListener.volume=volume; Screen.SetResolution(sw,sh,mode);
            }
        }
        [UnityTest] public IEnumerator CG_background_pointer_closes_once_blocks_same_frame_clicks_and_preserves_ending_choices()
        {
            global::CatGame.CatGame game; var world=NewWorld(out game); world.EnableStoryEvents=true; yield return null;
            var state=world.Economy.State; state.introCompleted=true; state.achievements.AddRange(new[]{"three_breeds","six_breeds"});
            yield return null;
            Debug.Log("CG POINTER STEP 1: raycast image and empty background; dispatch a real pointer click to the overlay.");
            var root=world.Hud.transform.Find("StoryCG").gameObject;
            var data=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
            ExecuteEvents.Execute(root,data,ExecuteEvents.pointerClickHandler);
            int clicks=state.clicks; world.Cat.OnLick(); Assert.AreEqual(clicks,state.clicks);
            Assert.IsFalse(world.Economy.BuyCat("Milk"));
            ExecuteEvents.Execute(root,data,ExecuteEvents.pointerClickHandler);
            Assert.AreEqual(1,state.seenStoryEvents.Count); Assert.Contains("six_breeds",state.pendingStoryEvents);
            yield return null;
            Assert.AreEqual("six_breeds",world.Hud.Stories.CurrentId);
            ExecuteEvents.Execute(world.Hud.transform.Find("StoryCG").gameObject,data,ExecuteEvents.pointerClickHandler); yield return null;
            Assert.IsFalse(world.Hud.Stories.IsOpen);
            Debug.Log("CG POINTER STEP 2: ending background advances to statistics, which keeps explicit choices.");
            state.catHair=EndingRules.RocketPrice; Assert.IsTrue(world.Economy.BuyRocket()); yield return null;
            ExecuteEvents.Execute(world.Hud.transform.Find("StoryCG").gameObject,data,ExecuteEvents.pointerClickHandler);
            Assert.IsTrue(world.Hud.Stories.ShowingStatistics); Assert.IsFalse(state.endingCompleted);
            Assert.IsNotNull(world.Hud.transform.Find("EndingStatistics/Card/Btn_continue_ending"));
            world.Hud.Stories.FinishEnding(false);
        }
    }
}
