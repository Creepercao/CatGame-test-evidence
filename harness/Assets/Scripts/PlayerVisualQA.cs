#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using CatGame.Idle;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace CatGame
{
    // Development Player only; runs only with an explicit isolated QA directory.
    public sealed partial class PlayerVisualQA : MonoBehaviour
    {
        string folder;
        int boundsErrors;
        int roomFootErrors, furnitureDepthErrors;
        int nativePointerHits;
        int introRuns;
        int _step;
        public static bool IsScriptedFlow { get; private set; }
        readonly List<BaseInputModule> blockedInput = new List<BaseInputModule>();
        void OnEnable() { SceneManager.sceneLoaded += PrepareScene; }
        void OnDisable() { SceneManager.sceneLoaded -= PrepareScene; }
        static void PrepareScene(Scene scene, LoadSceneMode mode)
        {
            // The independent story run exercises every CG. Other QA flows drive
            // economy/menu/performance without an achievement modal interrupting them.
            // Configure before CatWorld.Start builds its HUD, including Continue loads.
            if (Environment.GetEnvironmentVariable("CATGAME_QA_STORY_ONLY") == "1") return;
            var world=FindObjectOfType<CatWorld>();
            if(world!=null) world.EnableStoryEvents=false;
        }
        void Update()
        {
            if (!IsScriptedFlow || EventSystem.current == null) return;
            foreach (var module in EventSystem.current.GetComponents<BaseInputModule>())
                if (module.enabled) { blockedInput.Add(module); module.enabled=false; }
        }
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
        [StructLayout(LayoutKind.Sequential)] struct Point { public int x, y; }
        [DllImport("user32.dll")] static extern bool GetCursorPos(out Point point);
        [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr window, ref Point point);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            string root = Environment.GetEnvironmentVariable("CATGAME_PLAYER_QA_ROOT");
            if (string.IsNullOrEmpty(root)) return;
            var go = new GameObject("PlayerVisualQA");
            DontDestroyOnLoad(go);
            go.AddComponent<PlayerVisualQA>().folder = root;
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(folder);
            if(Environment.GetEnvironmentVariable("CATGAME_QA_PERFORMANCE_ONLY")=="1")
            {
                IsScriptedFlow=true; yield return PerformanceFlow(); IsScriptedFlow=false;
                Application.Quit(0); yield break;
            }
            if(Environment.GetEnvironmentVariable("CATGAME_QA_BUSINESS_ONLY")=="1")
            {
                IsScriptedFlow=true; yield return BusinessFlow(); IsScriptedFlow=false;
                Application.Quit(0); yield break;
            }
            if(Environment.GetEnvironmentVariable("CATGAME_QA_EXTERNAL_ONLY")=="1")
            {
                IsScriptedFlow=true; yield return ExternalDebugFlow(); IsScriptedFlow=false;
                Application.Quit(0); yield break;
            }
            if(Environment.GetEnvironmentVariable("CATGAME_QA_SETTINGS_ONLY")=="1")
            {
                IsScriptedFlow=true; yield return SettingsFlow(); IsScriptedFlow=false;
                Application.Quit(0); yield break;
            }
            if (Environment.GetEnvironmentVariable("CATGAME_QA_STORY_ONLY") == "1")
            {
                IsScriptedFlow=true;
                yield return StoryFlow();
                IsScriptedFlow=false;
                Application.Quit(0); yield break;
            }
            if (Environment.GetEnvironmentVariable("CATGAME_QA_INTRO_ONLY") == "1")
            {
                IsScriptedFlow=true;
                SaveSystem.Instance.BeginNewGame(); SceneFlow.GoHome();
                yield return new WaitForSecondsRealtime(1);
                yield return FinishFreshIntro();
                IsScriptedFlow=false;
                Application.Quit(0); yield break;
            }
            if (Environment.GetEnvironmentVariable("CATGAME_QA_MENU_ONLY") == "1")
            {
                yield return MenuPreview();
                Application.Quit(0);
                yield break;
            }
            if (Environment.GetEnvironmentVariable("CATGAME_QA_FLOW") == "1")
            {
                IsScriptedFlow=true;
                try { yield return MenuFlow(); }
                finally
                {
                    IsScriptedFlow=false;
                    foreach(var module in blockedInput) if(module != null) module.enabled=true;
                    blockedInput.Clear();
                }
            }
            else
            {
                SaveSystem.Instance.BeginNewGame();
                SceneFlow.GoHome();
            }
            yield return null;
            yield return new WaitForSecondsRealtime(2);
            var world = CatWorld.I;
            if (world.Hud.Intro != null && world.Hud.Intro.IsOpen) yield return FinishFreshIntro();
            // Story/ending presentation has its own actual-Player suite; this legacy
            // exercise deliberately drives all purchases without opening unrelated CGs.
            if(world.Hud.Stories != null) world.Hud.Stories.enabled=false;
            world.Hud.Toast.Enqueue(AchievementCatalog.All[0]);
            yield return new WaitForSecondsRealtime(.4f);
            yield return Shot("toast-visible");
            yield return new WaitForSecondsRealtime(3f);
            var toastOpacity = world.Hud.transform.Find("AchievementToast").GetComponent<CanvasGroup>();
            Require(toastOpacity.alpha > 0 && toastOpacity.alpha < 1, "Toast must fade in the actual Player");
            yield return Shot("toast-fading");
            foreach (var size in new[] { new Vector2Int(1920,1080), new Vector2Int(1280,720), new Vector2Int(1024,768) })
            {
                Screen.SetResolution(size.x, size.y, false);
                yield return new WaitForSecondsRealtime(2);
                var camera = Camera.main;
                var minimum = camera.WorldToScreenPoint(world.FloorCenter - world.FloorHalf);
                var maximum = camera.WorldToScreenPoint(world.FloorCenter + world.FloorHalf);
                var sidebar = (RectTransform)world.Hud.transform.Find("Sidebar");
                var corners = new Vector3[4];
                sidebar.GetWorldCorners(corners);
                if (minimum.x < corners[2].x - 2 || maximum.x > Screen.width || minimum.y < 0 || maximum.y > Screen.height)
                    boundsErrors++;
                Debug.Log("[PlayerVisualQA] viewport=" + Screen.width + "x" + Screen.height + " floor=" + minimum + ".." + maximum + " sidebarRight=" + corners[2].x);
                if (Environment.GetEnvironmentVariable("CATGAME_QA_SYSTEM_POINTER") == "1")
                {
                    Time.timeScale = 0;
                    int beforeClick = world.Economy.State.clicks;
                    var point = camera.WorldToScreenPoint(world.Cat.Renderer.bounds.center);
                    var handle = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                    Point original; GetCursorPos(out original);
                    var foreground = GetForegroundWindow();
                    try
                    {
                        SetForegroundWindow(handle);
                        var target = new Point { x = (int)point.x, y = Screen.height - (int)point.y };
                        ClientToScreen(handle, ref target);
                        SetCursorPos(target.x, target.y);
                        yield return new WaitForSecondsRealtime(0.25f);
                        mouse_event(2, 0, 0, 0, UIntPtr.Zero);
                        yield return new WaitForSecondsRealtime(0.15f);
                        mouse_event(4, 0, 0, 0, UIntPtr.Zero);
                        yield return new WaitForSecondsRealtime(0.15f);
                    }
                    finally
                    {
                        mouse_event(4, 0, 0, 0, UIntPtr.Zero);
                        SetCursorPos(original.x, original.y);
                        SetForegroundWindow(foreground);
                        Time.timeScale = 1;
                    }
                    if (world.Economy.State.clicks == beforeClick + 1) nativePointerHits++;
                    Debug.Log("[PlayerVisualQA] system pointer click delta=" + (world.Economy.State.clicks - beforeClick));
                }
                yield return Shot("home");
                Click(world, "PurchasePanel/Btn_shop_items");
                yield return null;
                yield return Shot("shop-items");
                var shop = world.Hud.transform.Find("PurchasePanel/ShopViewport").GetComponent<ScrollRect>();
                shop.verticalNormalizedPosition = 0;
                yield return null;
                yield return Shot("shop-bottom");
                Click(world, "PurchasePanel/Btn_shop_cats");
                Click(world, "Btn_pause");
                Click(world, "PauseOverlay/Window/Navigation/Btn_page_Achievements");
                yield return null;
                yield return Shot("achievements-top");
                var scroll = world.Hud.transform.Find("PauseOverlay/Window/PageContent/Achievements/Entries").GetComponent<ScrollRect>();
                Canvas.ForceUpdateCanvases();
                scroll.verticalNormalizedPosition = 0;
                yield return null;
                yield return Shot("achievements-bottom");
                Click(world, "PauseOverlay/Window/Navigation/Btn_page_Catalog");
                yield return null;
                var catalog = world.Hud.transform.Find("PauseOverlay/Window/PageContent/Catalog");
                Require(catalog.Find("Background").GetComponent<Image>().sprite == Resources.Load<Sprite>("Art/Backgrounds/Pixel/CatalogPark"), "Catalog must display pixel background");
                var cats = catalog.Find("Entries").GetComponent<ScrollRect>();
                Require(cats.content.childCount == 11, "Catalog requires all eleven cats");
                cats.verticalNormalizedPosition = 1;
                yield return null;
                yield return Shot("catalog-top");
                cats.verticalNormalizedPosition = 0;
                yield return null;
                yield return Shot("catalog-bottom");
                Click(world, "PauseOverlay/Window/Btn_resume");
                yield return null;
            }
            world.Economy.State.catHair = 1e200;
            int itemPurchases = 0;
            for (int level = 1; level <= 5; level++)
            {
                foreach (var item in ItemCatalog.All)
                {
                    Require(world.Economy.BuyItem(item.id), "Item purchase failed: " + item.id);
                    itemPurchases++;
                    var marker = world.transform.Find("PlatformItem_" + item.id);
                    Require(marker != null && marker.gameObject.activeSelf, "Item marker hidden: " + item.id);
                    Require(marker.Find("Icon").GetComponent<SpriteRenderer>().sprite == ItemVisualLibrary.Load(item.id), "Wrong item art");
                    Require(marker.Find("Level").GetComponent<TextMesh>().text.Contains("Lv." + level), "Wrong item level");
                }
                world.Hud.RefreshDisplay();
                if (level == 1) yield return Shot("all-items-level1");
            }
            foreach (var item in ItemCatalog.All) Require(!world.Economy.BuyItem(item.id), "Item exceeded cap");
            foreach (var size in new[] { new Vector2Int(1920,1080), new Vector2Int(1280,720), new Vector2Int(1024,768) })
            {
                Screen.SetResolution(size.x, size.y, false);
                yield return new WaitForSecondsRealtime(2);
                yield return Shot("all-items-level5");
            }
            File.WriteAllText(Path.Combine(folder, "items.txt"), "purchases=" + itemPurchases + "\nvisible=" + world.Platform.ItemMarkerCount + "\ncapRejected=20\n");
            Screen.SetResolution(1280,720,false);
            foreach (var row in world.Economy.State.cats) row.count = IdleBalance.CatMaxOwned;
            world.Roster.Rebuild(world.Economy.State);
            world.Economy.Initialize(world.Economy.State, world.Roster);
            world.Hud.RefreshDisplay();
            yield return new WaitForSecondsRealtime(2);
            yield return Shot("roster-440");
            long before = GC.GetTotalMemory(false);
            var samples = new List<float>();
            float until = Time.realtimeSinceStartup + 30;
            var roomActors = world.Roster.GetComponentsInChildren<CatActor>();
            var roomDepths = world.WalkSurface != null ? world.WalkSurface.GetComponentsInChildren<RoomFurnitureDepth>() : new RoomFurnitureDepth[0];
            if (world.WalkSurface == null || roomDepths.Length != 4) throw new InvalidOperationException("Pixel room surface/furniture missing from Player");
            while (Time.realtimeSinceStartup < until)
            {
                samples.Add(Time.unscaledDeltaTime * 1000);
                foreach (var actor in roomActors)
                    if (!world.WalkSurface.Contains(actor.transform.position + Vector3.up * actor.GroundOffset)) roomFootErrors++;
                foreach (var depth in roomDepths)
                {
                    int expected = Mathf.RoundToInt((10 - depth.transform.TransformPoint(depth.LocalGround).y) * 100);
                    foreach (var piece in depth.Pieces) if (piece.sortingOrder != expected) furnitureDepthErrors++;
                }
                yield return null;
            }
            samples.Sort();
            float sum = 0; foreach (float sample in samples) sum += sample;
            string report = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "samples={0}\nmeanFrameMs={1:F3}\np95FrameMs={2:F3}\nmanagedBefore={3}\nmanagedAfter={4}\nvisible={5}\nowned={6}\nsave={7}\nboundsErrors={8}\nnativePointerHits={9}\n",
                samples.Count, sum/samples.Count, samples[(int)(samples.Count*0.95f)], before, GC.GetTotalMemory(false),
                world.Roster.VisibleCount, world.Roster.TotalOwned, SaveSystem.Instance.SavePath, boundsErrors, nativePointerHits);
            File.WriteAllText(Path.Combine(folder,"performance.txt"), report);
            File.AppendAllText(Path.Combine(folder,"performance.txt"), "roomFootErrors=" + roomFootErrors + "\nfurnitureDepthErrors=" + furnitureDepthErrors + "\nroomActors=" + roomActors.Length + "\n");
            Debug.Log("[PlayerVisualQA] Completed\n" + report);
            Application.Quit(0);
        }
        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(folder, name + "-" + Screen.width + "x" + Screen.height + ".png"), texture.EncodeToPNG());
            Destroy(texture);
            RecordStep("screenshot",name+"-"+Screen.width+"x"+Screen.height+".png",true);
        }
        IEnumerator MenuPreview()
        {
            yield return new WaitForSecondsRealtime(1);
            var menu = FindObjectOfType<MainMenuUI>();
            Require(menu != null && SceneFlow.Current == SceneFlow.Menu, "Menu preview starts in MainMenu");
            foreach (var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1024,768),new Vector2Int(1280,720)})
            {
                Screen.SetResolution(size.x,size.y,false);
                yield return new WaitForSecondsRealtime(1);
                var panel=(RectTransform)menu.transform.Find("Panel");
                var corners=new Vector3[4]; panel.GetWorldCorners(corners);
                Require(corners[0].x >= Screen.width*.65f-2 && corners[2].x <= Screen.width+1,"Options remain in right pane");
                yield return Shot("menu-fresh");
            }
        }
        IEnumerator FinishFreshIntro()
        {
            var world=CatWorld.I;
            Require(world != null && world.Hud.Intro != null && world.Hud.Intro.IsOpen,"Fresh save must show intro");
            Require(!world.Economy.State.introCompleted && world.Hud.Modals.IsBusy && Time.timeScale==0,"Intro owns paused simulation");
            double balance=world.Economy.State.catHair;
            double play=world.Economy.State.playSeconds;
            int clicks=world.Economy.State.clicks;
            world.Cat.OnLick();
            Require(world.Economy.State.clicks==clicks,"No click-through during dialogue");
            Require(!world.Hud.GetComponent<PausePanel>().Open(),"Pause cannot replace dialogue");
            if(introRuns==0)
            {
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1024,768),new Vector2Int(1280,720)})
                {
                    Screen.SetResolution(size.x,size.y,false);
                    yield return new WaitForSecondsRealtime(1);
                    var text=world.Hud.transform.Find("IntroDialogue/DialogueBox/DialogueText").GetComponent<Text>();
                    Canvas.ForceUpdateCanvases();
                    var portrait=world.Hud.transform.Find("IntroDialogue/Portrait").GetComponent<Image>();
                    Require(portrait.sprite==CatVisualLibrary.Load(CatCatalog.All[0]).Portrait,"Dialogue reuses Milk pixel art");
                    Require(portrait.sprite.texture.filterMode==FilterMode.Point,"Dialogue portrait keeps point filtering");
                    float zoom=portrait.rectTransform.rect.width*Mathf.Abs(portrait.rectTransform.lossyScale.x)/portrait.sprite.rect.width;
                    Require(Mathf.Abs(zoom-Mathf.Round(zoom))<.001f,"Dialogue portrait integer screen magnification");
                    Require(text.preferredHeight<=text.rectTransform.rect.height+1,"Dialogue text fits its box");
                    yield return Shot("intro-welcome");
                }
                world.Hud.Intro.Advance(); yield return Shot("intro-touch");
                world.Hud.Intro.Advance(); yield return Shot("intro-auto");
            }
            Require(world.Economy.State.catHair==balance && world.Economy.State.playSeconds==play,"Dialogue time must not produce currency or playtime");
            world.Hud.transform.Find("IntroDialogue/DialogueBox/Btn_skip_intro").GetComponent<Button>().onClick.Invoke();
            Require(world.Economy.State.introCompleted && !world.Hud.Modals.IsBusy && Time.timeScale>0,"Intro completion resumes once");
            introRuns++;
            File.WriteAllText(Path.Combine(folder,"intro.txt"),"completedRuns="+introRuns+"\nintroCompleted=true\nnoClickThrough=true\npausedProduction=true\n");
        }
        void Require(bool condition, string message)
        {
            RecordStep("assertion",message,condition);
            if (condition) return;
            Debug.LogError("[PlayerVisualQA] " + message);
            Application.Quit(1);
            throw new InvalidOperationException(message);
        }
        void RecordStep(string type,string message,bool passed)
        {
            File.AppendAllText(Path.Combine(folder,"steps.jsonl"),JsonUtility.ToJson(new QaStep {
                step=++_step,utc=DateTime.UtcNow.ToString("O"),scene=SceneFlow.Current,
                realtime=Time.realtimeSinceStartup,timeScale=Time.timeScale,type=type,message=message,passed=passed
            })+"\n");
        }
        [Serializable] sealed class QaStep
        {
            public int step;
            public string utc,scene,type,message;
            public float realtime,timeScale;
            public bool passed;
        }
        IEnumerator MenuFlow()
        {
            yield return new WaitForSecondsRealtime(1);
            var menu = FindObjectOfType<MainMenuUI>();
            Require(menu != null && SceneFlow.Current == SceneFlow.Menu, "Must start in MainMenu");
            Require(!menu.transform.Find("Panel/Btn_Continue").GetComponent<Button>().interactable, "Fresh isolated save must disable Continue");
            foreach (var menuSize in new[]{new Vector2Int(1920,1080),new Vector2Int(1024,768),new Vector2Int(1280,720)})
            {
                Screen.SetResolution(menuSize.x,menuSize.y,false);
                yield return new WaitForSecondsRealtime(1);
                yield return Shot("menu-fresh");
            }
            menu.transform.Find("Panel/Btn_NewGame").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(1);
            Require(SceneFlow.Current == SceneFlow.Home && CatWorld.I != null, "New game must load Home");
            yield return FinishFreshIntro();
            var state = SaveSystem.Instance.State;
            state.catHair = 123.45;
            state.totalEarned = 123.45;
            SaveSystem.Instance.SaveNow();
            Click(CatWorld.I, "Btn_pause");
            Click(CatWorld.I, "PauseOverlay/Window/Navigation/Btn_main_menu");
            yield return new WaitForSecondsRealtime(1);
            Require(SceneFlow.Current == SceneFlow.Menu, "Pause menu must return to MainMenu");
            menu = FindObjectOfType<MainMenuUI>();
            Require(menu.transform.Find("Panel/Btn_Continue").GetComponent<Button>().interactable, "Saved game must enable Continue");
            menu.transform.Find("Panel/Btn_NewGame").GetComponent<Button>().onClick.Invoke();
            Require(menu.transform.Find("NewGameConfirmation") != null, "Existing save needs confirmation");
            yield return Shot("menu-confirmation");
            menu.transform.Find("NewGameConfirmation/Card/Btn_Cancel").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Require(SaveSystem.Instance.State.catHair >= 123.45, "Cancel must retain save");
            menu.transform.Find("Panel/Btn_Continue").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(1);
            Require(SceneFlow.Current == SceneFlow.Home && CatWorld.I.Economy.State.catHair >= 123.45, "Continue must restore progress");
            Require(CatWorld.I.Hud.Intro != null && !CatWorld.I.Hud.Intro.IsOpen && CatWorld.I.Economy.State.introCompleted,"Continue must not replay intro");
            Click(CatWorld.I, "Btn_pause");
            Click(CatWorld.I, "PauseOverlay/Window/Navigation/Btn_main_menu");
            yield return new WaitForSecondsRealtime(1);
            menu = FindObjectOfType<MainMenuUI>();
            menu.transform.Find("Panel/Btn_NewGame").GetComponent<Button>().onClick.Invoke();
            menu.transform.Find("NewGameConfirmation/Card/Btn_Confirm").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(1);
            Require(SceneFlow.Current == SceneFlow.Home && CatWorld.I.Economy.State.catHair < 5, "Confirmed new game must reset progress");
            var world = CatWorld.I;
            Require(world.Economy.State.cats[0].count == 1 && world.Economy.State.cats[1].count == 0, "New game cat roster defaults");
            yield return FinishFreshIntro();
            float started = Time.realtimeSinceStartup, nextClick = started;
            float milkAt = -1, tabbyAt = -1;
            int clicks = 0;
            while (Time.realtimeSinceStartup - started < 95 && tabbyAt < 0)
            {
                float now = Time.realtimeSinceStartup;
                if (now >= nextClick) { world.Cat.OnLick(); nextClick += 2; clicks++; }
                var milkButton = world.Hud.transform.Find("PurchasePanel/ShopViewport/Content/Cat_Milk").GetComponent<Button>();
                var tabbyButton = world.Hud.transform.Find("PurchasePanel/ShopViewport/Content/Cat_Tabby").GetComponent<Button>();
                if (clicks <= 2) Require(!milkButton.interactable && !tabbyButton.interactable, "Two early clicks cannot afford first purchases");
                if (milkAt < 0 && milkButton.interactable) { milkButton.onClick.Invoke(); milkAt = now - started; }
                if (milkAt >= 0 && tabbyButton.interactable) { tabbyButton.onClick.Invoke(); tabbyAt = now - started; }
                yield return null;
            }
            Require(milkAt > 2 && tabbyAt > milkAt, "Real Update/click accumulation must allow successive purchases");
            Require(world.Economy.State.clicks == clicks,"Scripted click pacing must not contain extra physical input");
            File.WriteAllText(Path.Combine(folder, "flow.txt"), string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "flowPassed=true\nfreshContinueDisabled=true\nconfirmationCancelRetainsSave=true\ncontinueRetainsSave=true\nnewGameResetsSave=true\nearlyTwoClicksBlocked=true\nsecondCatSeconds={0:F3}\nfirstTabbySeconds={1:F3}\n", milkAt, tabbyAt));
            yield return Shot("opening-after-purchases");
            File.AppendAllText(Path.Combine(folder,"flow.txt"),"driverClicks="+clicks+"\nrecordedClicks="+world.Economy.State.clicks+"\nclickPacingMatched=true\n");
        }
        static void Click(CatWorld world, string path) { world.Hud.transform.Find(path).GetComponent<Button>().onClick.Invoke(); }
    }
}
#endif
