#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.IO;
using System.Collections;
using CatGame.Idle;
using UnityEngine;
using UnityEngine.UI;
namespace CatGame
{
    public sealed partial class PlayerVisualQA
    {
        [Serializable] sealed class OriginalPreferences
        {
            public bool formatExists,volumeExists,widthExists,heightExists;
            public int format,width,height,screenWidth,screenHeight,mode;
            public float volume;
        }
        IEnumerator SettingsFlow()
        {
            string backup=Path.Combine(folder,"original-preferences.json");
            bool restart=Environment.GetEnvironmentVariable("CATGAME_QA_SETTINGS_RESTART")=="1", completed=false;
            OriginalPreferences original;
            if(restart) original=JsonUtility.FromJson<OriginalPreferences>(File.ReadAllText(backup));
            else {
                original=new OriginalPreferences { formatExists=PlayerPrefs.HasKey("CatGame.NumberFormat"),volumeExists=PlayerPrefs.HasKey("CatGame.Volume"),widthExists=PlayerPrefs.HasKey("CatGame.Width"),heightExists=PlayerPrefs.HasKey("CatGame.Height"),format=GlobalSettings.NumberFormat,volume=GlobalSettings.Volume,width=PlayerPrefs.GetInt("CatGame.Width"),height=PlayerPrefs.GetInt("CatGame.Height"),screenWidth=Screen.width,screenHeight=Screen.height,mode=(int)Screen.fullScreenMode };
                File.WriteAllText(backup,JsonUtility.ToJson(original,true));
            }
            try {
                yield return new WaitForSecondsRealtime(1);
                if(restart) {
                    Require(Screen.width==1280 && Screen.height==720,"Resolution restored on actual process restart");
                    Require(GlobalSettings.NumberFormat==1 && Mathf.Abs(GlobalSettings.Volume-.31f)<.001f,"Format and volume restored on process restart");
                    yield return Shot("settings-restart");
                    File.WriteAllText(Path.Combine(folder,"restart.txt"),"restartPassed=true"); completed=true; yield break;
                }
                var menu=FindObjectOfType<MainMenuUI>();
                menu.transform.Find("Panel/Btn_Settings").GetComponent<Button>().onClick.Invoke();
                var card=menu.transform.Find("SettingsOverlay/Card");
                foreach(var size in GlobalSettings.Resolutions) {
                    GlobalSettings.SetResolution(size.x,size.y); yield return new WaitForSecondsRealtime(.5f);
                    Require(Screen.width==size.x && Screen.height==size.y,"Actual window resolution "+size);
                    yield return Shot("menu-settings");
                }
                card.Find("VolumeSlider").GetComponent<Slider>().value=.31f;
                while(GlobalSettings.NumberFormat!=1) card.Find("Btn_number_format").GetComponent<Button>().onClick.Invoke();
                menu.transform.Find("SettingsOverlay/Card/Btn_close").GetComponent<Button>().onClick.Invoke();
                SaveSystem.Instance.BeginNewGame(); SceneFlow.GoHome(); yield return new WaitForSecondsRealtime(.5f);
                var world=CatWorld.I; world.Hud.Intro.Skip(); yield return null;
                var pause=world.Hud.GetComponent<PausePanel>(); Require(pause.Open(),"Open gameplay settings");
                var settings=world.Hud.transform.Find("PauseOverlay/Window/PageContent/Settings");
                Require(Mathf.Abs(settings.Find("VolumeSlider").GetComponent<Slider>().value-.31f)<.001f,"Menu volume shared with game");
                Require(settings.Find("Btn_number_format/Label").GetComponent<Text>().text==GlobalSettings.FormatLabel,"Menu format shared with game");
                foreach(var size in GlobalSettings.Resolutions) {
                    GlobalSettings.SetResolution(size.x,size.y); yield return new WaitForSecondsRealtime(.5f);
                    FitsScreen((RectTransform)settings.Find("Btn_resolution"),"Resolution control");
                    yield return Shot("game-settings");
                }
                settings.Find("Btn_number_format").GetComponent<Button>().onClick.Invoke();
                int changed=GlobalSettings.NumberFormat;
                Click(world,"PauseOverlay/Window/Navigation/Btn_main_menu"); yield return new WaitForSecondsRealtime(.5f);
                menu=FindObjectOfType<MainMenuUI>(); menu.transform.Find("Panel/Btn_Settings").GetComponent<Button>().onClick.Invoke(); yield return null;
                Require(changed==GlobalSettings.NumberFormat && menu.transform.Find("SettingsOverlay/Card/Btn_number_format/Label").GetComponent<Text>().text==GlobalSettings.FormatLabel,"Game setting returns to menu");
                while(GlobalSettings.NumberFormat!=1) GlobalSettings.CycleFormat();
                GlobalSettings.SetResolution(1280,720); File.WriteAllText(Path.Combine(folder,"settings.txt"),"settingsPassed=true"); completed=true;
            } finally {
                if(restart || !completed) {
                    PlayerPrefs.SetInt("CatGame.NumberFormat",original.format); PlayerPrefs.SetFloat("CatGame.Volume",original.volume);
                    PlayerPrefs.SetInt("CatGame.Width",original.width); PlayerPrefs.SetInt("CatGame.Height",original.height);
                    if(!original.formatExists) PlayerPrefs.DeleteKey("CatGame.NumberFormat"); if(!original.volumeExists) PlayerPrefs.DeleteKey("CatGame.Volume");
                    if(!original.widthExists) PlayerPrefs.DeleteKey("CatGame.Width"); if(!original.heightExists) PlayerPrefs.DeleteKey("CatGame.Height");
                    PlayerPrefs.Save(); AudioListener.volume=original.volume;
                }
            }
        }
    }
}
#endif
