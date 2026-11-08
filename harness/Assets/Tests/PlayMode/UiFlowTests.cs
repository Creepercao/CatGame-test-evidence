using System.Linq;
﻿using System.Collections;
using NUnit.Framework;
using CatGame.Idle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using CatGameType = global::CatGame.CatGame;
using HudType = global::CatGame.Hud;

namespace CatGame.Tests
{
    /// <summary>Player-facing economy HUD, catalog rows, and purchase interaction.</summary>
    public class UiFlowTests : CatGameTestBase
    {
        [UnityTest]
        public IEnumerator Revision_price_gates_sidebar_and_hidden_grid()
        {
            CatGameType game;
            var world = NewWorld(out game);
            yield return null;
            var s = world.Economy.State;
            s.totalEarned = 0;
            s.catHair = 10000000;
            Assert.IsTrue(world.Economy.BuyCat("Tuxedo"), "No cumulative unlock gate");
            Assert.IsTrue(world.Economy.BuyItem("item_2_1"), "No item generation gate");
            s.catHair = 0;
            Assert.IsFalse(world.Economy.BuyCat("Snow"));
            world.Hud.RefreshDisplay();
            Assert.AreEqual("猫窝", world.Hud.transform.Find("InfoPanel/Title").GetComponent<Text>().text);
            Assert.IsNotNull(world.Hud.transform.Find("Sidebar"));
            Assert.IsFalse(world.Hud.transform.Find("CatStackPanel").gameObject.activeSelf);
            foreach (var scroll in world.Hud.transform.Find("PurchasePanel").GetComponentsInChildren<ScrollRect>())
            {
                Assert.IsTrue(scroll.vertical); Assert.IsFalse(scroll.horizontal);
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                Canvas.ForceUpdateCanvases();
                scroll.verticalNormalizedPosition = 0;
                yield return null;
                Canvas.ForceUpdateCanvases();
                var last = (RectTransform)scroll.content.GetChild(scroll.content.childCount - 1);
                var lastCorners = new Vector3[4];
                last.GetWorldCorners(lastCorners);
                foreach (var corner in lastCorners)
                    Assert.That(scroll.viewport.InverseTransformPoint(corner).y,
                        Is.InRange(scroll.viewport.rect.yMin - 1, scroll.viewport.rect.yMax + 1), last.name + " must be fully reachable at the bottom");
                scroll.verticalNormalizedPosition = 1;
            }
            Assert.IsFalse(world.Platform.GetComponentInChildren<TilemapRenderer>().enabled);
            var calico = CatVisualLibrary.Load(CatCatalog.All[3]);
            Assert.AreEqual(8,calico.Walk.Length);
            Assert.IsEmpty(Resources.LoadAll<Sprite>("Art/Cats/Calico/Walk"),"The deleted old Walk must not be restored");
            Assert.IsNotEmpty(calico.Idle);
            Assert.IsNotEmpty(calico.Jump);
            FindButton(world.Hud, "Btn_pause").onClick.Invoke();
            FindButton(world.Hud, "PauseOverlay/Window/Navigation/Btn_page_Achievements").onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var viewport = (RectTransform)world.Hud.transform.Find("PauseOverlay/Window/PageContent/Achievements/Entries");
            var content = (RectTransform)viewport.Find("Content");
            Assert.AreEqual(20, content.childCount);
            var corners = new Vector3[4];
            foreach (RectTransform row in content)
            {
                row.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    float x = viewport.InverseTransformPoint(corner).x;
                    Assert.That(x, Is.InRange(viewport.rect.xMin - 0.1f, viewport.rect.xMax + 0.1f), row.name);
                }
                var label = row.GetComponentInChildren<Text>();
                Assert.LessOrEqual(label.preferredWidth, label.rectTransform.rect.width + 1,
                    "Achievement text must fit the available width");
            }
            FindButton(world.Hud, "PauseOverlay/Window/Btn_resume").onClick.Invoke();
        }

        [Test]
        public void Revision_numbers_use_scientific_only_from_one_hundred_million()
        {
            int previous = PlayerPrefs.GetInt("CatGame.NumberFormat", 0);
            try
            {
                foreach (int mode in new[] { 0, 2 })
                {
                    PlayerPrefs.SetInt("CatGame.NumberFormat", mode);
                    foreach (double number in new[] { 9999d, 10000d, 99999999d })
                        StringAssert.DoesNotContain("e", HudType.FormatNumber(number).ToLowerInvariant());
                    foreach (double number in new[] { 100000000d, 100000001d })
                        StringAssert.Contains("e", HudType.FormatNumber(number).ToLowerInvariant());
                }
            }
            finally { PlayerPrefs.SetInt("CatGame.NumberFormat", previous); }
        }

        static Button FindButton(HudType hud, string path)
        {
            Assert.IsNotNull(hud, "HUD 没有生成");
            var transform = hud.transform.Find(path);
            Assert.IsNotNull(transform, "界面上找不到节点: " + path);
            var button = transform.GetComponent<Button>();
            Assert.IsNotNull(button, path + " 上应该有 Button");
            return button;
        }

        [UnityTest]
        public IEnumerator 猫窝显示猫毛产量阶段及全部购买卡片()
        {
            CatGameType game;
            var world = NewWorld(out game);
            yield return null;

            Assert.IsNotNull(world.Economy);
            Assert.IsNotNull(world.Hud.GetComponent<Canvas>());
            Assert.IsNotNull(world.Hud.GetComponent<GraphicRaycaster>());
            Assert.IsNotNull(world.Hud.transform.Find("InfoPanel/Metrics"));
            Assert.IsNotNull(world.Hud.transform.Find("CatStackPanel/StackCount"));
            Assert.AreEqual(11, world.Hud.transform.Find("PurchasePanel/ShopViewport/Content").GetComponentsInChildren<Button>(true).Count(b => b.name.StartsWith("Cat_")));
            Assert.AreEqual(20, world.Hud.transform.Find("PurchasePanel/ShopViewport/Content").GetComponentsInChildren<Button>(true).Count(b => b.name.StartsWith("Item_")));
            FindButton(world.Hud, "PurchasePanel/ShopViewport/Content/Cat_Milk");
            FindButton(world.Hud, "PurchasePanel/ShopViewport/Content/Item_item_1_1");
        }

        [UnityTest]
        public IEnumerator 猫窝HUD保留暂停入口并移除公园场景入口()
        {
            CatGameType game;
            var world = NewWorld(out game);
            yield return null;
            FindButton(world.Hud, "Btn_pause");
            Assert.IsNull(world.Hud.transform.Find("Btn_scene"), "公园不再作为可进入的世界场景");
        }

        [UnityTest]
        public IEnumerator 齿轮打开暂停菜单并能恢复游戏()
        {
            CatGameType game;
            var world = NewWorld(out game);
            yield return null;

            var pause = world.Hud.GetComponent<PausePanel>();
            Assert.IsNotNull(pause);
            FindButton(world.Hud, "Btn_pause").onClick.Invoke();
            Assert.IsTrue(pause.IsOpen);
            Assert.AreEqual(0f, Time.timeScale);
            FindButton(world.Hud, "PauseOverlay/Window/Btn_resume").onClick.Invoke();
            Assert.IsFalse(pause.IsOpen);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator 暂停面板图鉴列出全部猫咪()
        {
            CatGameType game;
            var world = NewWorld(out game);
            yield return null;

            FindButton(world.Hud, "Btn_pause").onClick.Invoke();
            Assert.AreEqual(11, world.Hud.GetComponent<PausePanel>().CatalogEntryCount);
            FindButton(world.Hud, "PauseOverlay/Window/Navigation/Btn_page_Catalog").onClick.Invoke();
            var entries = world.Hud.transform.Find("PauseOverlay/Window/PageContent/Catalog/Entries/Content");
            Assert.AreEqual(11, entries.childCount);
            Assert.IsTrue(world.Hud.transform.Find("PauseOverlay/Window/PageContent/Catalog").gameObject.activeSelf);
            var background = world.Hud.transform.Find("PauseOverlay/Window/PageContent/Catalog/Background").GetComponent<Image>();
            Assert.IsNotNull(background.sprite, "猫图鉴应加载公园背景素材");
            FindButton(world.Hud, "PauseOverlay/Window/Btn_resume").onClick.Invoke();
        }

        [UnityTest]
        public IEnumerator 暂停面板显示全部二十项成就()
        {
            CatGameType game;
            var world = NewWorld(out game);
            yield return null;

            FindButton(world.Hud, "Btn_pause").onClick.Invoke();
            FindButton(world.Hud, "PauseOverlay/Window/Navigation/Btn_page_Achievements").onClick.Invoke();
            var entries = world.Hud.transform.Find("PauseOverlay/Window/PageContent/Achievements/Entries/Content");
            Assert.AreEqual(20, entries.childCount);
            FindButton(world.Hud, "PauseOverlay/Window/Btn_resume").onClick.Invoke();
        }

        [UnityTest]
        public IEnumerator 设置页可以切换数字格式和音量()
        {
            CatGameType game;
            float previousVolume = AudioListener.volume;
            float previousStoredVolume = PlayerPrefs.GetFloat("CatGame.Volume", previousVolume);
            PlayerPrefs.SetInt("CatGame.NumberFormat", 0);
            var world = NewWorld(out game);
            yield return null;

            FindButton(world.Hud, "Btn_pause").onClick.Invoke();
            FindButton(world.Hud, "PauseOverlay/Window/PageContent/Settings/Btn_number_format").onClick.Invoke();
            Assert.AreEqual("1.2K", HudType.FormatNumber(1200));
            var volume = world.Hud.transform.Find("PauseOverlay/Window/PageContent/Settings/VolumeSlider").GetComponent<Slider>();
            volume.value = 0.25f;
            Assert.AreEqual(0.25f, AudioListener.volume, 0.001f);
            Assert.AreEqual(0.25f, PlayerPrefs.GetFloat("CatGame.Volume"), 0.001f);
            FindButton(world.Hud, "PauseOverlay/Window/Btn_resume").onClick.Invoke();
            AudioListener.volume = previousVolume;
            PlayerPrefs.SetFloat("CatGame.Volume", previousStoredVolume);
            PlayerPrefs.Save();
            PlayerPrefs.SetInt("CatGame.NumberFormat", 0);
            Assert.AreEqual("1,200", HudType.FormatNumber(1200));
        }

        [UnityTest]
        public IEnumerator 主菜单提供继续新游戏设置与退出入口()
        {
            var menuObject = Track(new GameObject("MainMenu.Test", typeof(RectTransform)));
            var menu = menuObject.AddComponent<MainMenuUI>();
            yield return null;

            Assert.IsNotNull(menu.transform.Find("Panel/Btn_Continue"));
            Assert.IsNotNull(menu.transform.Find("Panel/Btn_NewGame"));
            Assert.IsNotNull(menu.transform.Find("Panel/Btn_Settings"));
            Assert.IsNotNull(menu.transform.Find("Panel/Btn_Quit"));
            menu.transform.Find("Panel/Btn_Settings").GetComponent<Button>().onClick.Invoke();
            Assert.IsNotNull(menu.transform.Find("SettingsOverlay/Card/Btn_number_format"));
            menu.transform.Find("SettingsOverlay/Card/Btn_close").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.IsNull(menu.transform.Find("SettingsOverlay"));
        }

        [UnityTest]
        public IEnumerator 界面可以接收点击_事件系统与射线检测都在()
        {
            CatGameType game;
            var world = NewWorld(out game);
            yield return null;

            var eventSystem = EventSystem.current;
            Assert.IsNotNull(eventSystem);
            Assert.IsNotNull(eventSystem.GetComponent<BaseInputModule>());
            Assert.IsNotNull(world.Hud.GetComponent<GraphicRaycaster>());
            var catButton = FindButton(world.Hud, "PurchasePanel/ShopViewport/Content/Cat_Milk");
            var data = new PointerEventData(eventSystem)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, catButton.transform.position),
                button = PointerEventData.InputButton.Left,
            };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            eventSystem.RaycastAll(data, hits);
            Assert.IsTrue(hits.Exists(hit => hit.gameObject == catButton.gameObject), "射线没有打到猫咪购买卡");
        }

        [UnityTest]
        public IEnumerator 购买猫咪扣除价格并增加实体()
        {
            CatGameType game;
            var world = NewWorld(out game);
            yield return null;

            var state = world.Economy.State;
            state.catHair = 1000;
            double price = CatCatalog.All[1].firstPrice;
            Assert.IsTrue(world.Economy.BuyCat("Tabby"));
            Assert.AreEqual(1, world.Roster.OwnedCountFor("Tabby"));
            Assert.AreEqual(2, world.Roster.VisibleCount);
            Assert.AreEqual(1000 - price, state.catHair, 0.001);
            Assert.AreEqual(ActionNames.Jump, world.Roster.FindActor("Tabby").CurrentAction);
        }

        [UnityTest]
        public IEnumerator 购买物品升级扣款并提升等级和产量()
        {
            CatGameType game;
            var world = NewWorld(out game);
            yield return null;

            var state = world.Economy.State;
            state.catHair = 1000;
            double oldRate = world.Economy.ProductionPerSecond;
            Assert.IsNotNull(world.Platform);
            Assert.AreEqual(PlatformGrid.Capacity, world.Platform.CellCount);
            var tiles = world.Platform.GetComponentInChildren<Tilemap>(true);
            Assert.IsNotNull(tiles);
            Assert.AreEqual(6, tiles.cellBounds.size.x, "设计要求六列");
            Assert.AreEqual(7, tiles.cellBounds.size.y, "设计要求七行");
            Assert.IsTrue(tiles.HasTile(new Vector3Int(5, 6, 0)));
            Assert.IsFalse(tiles.HasTile(new Vector3Int(6, 0, 0)));
            double itemPrice = IdleMath.ItemPrice("item_1_1", 0);
            Assert.IsTrue(world.Economy.BuyItem("item_1_1"));
            Assert.AreEqual(1, state.items[0].level);
            Assert.AreEqual(1000 - itemPrice, state.catHair, 0.001);
            Assert.Greater(world.Economy.ProductionPerSecond, oldRate);
            Assert.AreEqual(1, world.Platform.ItemMarkerCount, "Purchased items should appear in their platform cell");
        }

        [UnityTest]
        public IEnumerator 猫窝平台使用单个Tilemap渲染四十二格()
        {
            CatGameType game;
            var world = NewWorld(out game);
            yield return null;

            Assert.AreEqual(PlatformGrid.Capacity, world.Platform.CellCount);
            Assert.AreEqual(1, world.Platform.FloorRendererCount);
            Assert.AreEqual(1, world.Platform.GetComponentsInChildren<TilemapRenderer>(true).Length,
                "平台地面应由一个语义化 Tilemap 层统一渲染，不为每格创建独立对象");
        }

        [UnityTest]
        public IEnumerator 点击猫通过当前点击值增加猫毛()
        {
            CatGameType game;
            var world = NewWorld(out game);
            yield return null;

            double expected = world.Economy.CurrentClickValue;
            double balanceBefore = world.Economy.State.catHair;
            double totalBefore = world.Economy.State.totalEarned;
            world.Cat.OnLick();
            Assert.AreEqual(expected, world.Economy.State.catHair - balanceBefore, 0.001);
            Assert.AreEqual(expected, world.Economy.State.totalEarned - totalBefore, 0.001);
            Assert.AreEqual(1, world.Economy.State.clicks);
            Assert.AreEqual(ActionNames.Jump, world.Cat.CurrentAction);
        }

        [UnityTest]
        public IEnumerator 有足够猫毛时购买按钮能完成升级()
        {
            CatGameType game;
            var world = NewWorld(out game);
            yield return null;

            world.Economy.State.catHair = 500;
            yield return new WaitForSeconds(0.6f);
            var button = FindButton(world.Hud, "PurchasePanel/ShopViewport/Content/Item_item_1_1");
            Assert.IsTrue(button.interactable);
            button.onClick.Invoke();
            Assert.AreEqual(1, world.Economy.State.items[0].level);
        }

        [UnityTest]
        public IEnumerator 积累足够猫毛后可购买首件产量道具()
        {
            CatGameType game;
            var world = NewWorld(out game);
            yield return null;

            var state = world.Economy.State;
            var firstProductionItem = ItemCatalog.All[0];
            var button = FindButton(world.Hud, "PurchasePanel/ShopViewport/Content/Item_" + firstProductionItem.id);
            Assert.Greater(world.Economy.ProductionPerSecond, 0, "初始 Milk 猫应提供自动产出");
            Assert.IsFalse(button.interactable, "起始余额不足时首件产量道具应不可购买");

            for (int elapsed = 0; elapsed < 360 && state.items[0].level == 0; elapsed++)
            {
                if (elapsed % 2 == 0 && world.Cat != null)
                    world.Cat.OnLick(); // 0.5 次/秒模拟点击

                IdleMath.ApplyProduction(state, world.Economy.ProductionPerSecond, 1);
                world.Hud.RefreshDisplay();
                yield return null; // virtual seconds; actual Update is covered by other tests
                if (button.interactable)
                    button.onClick.Invoke();
            }

            Assert.AreEqual(1, state.items[0].level, "按零余额积累 360 个模拟秒后应能购买首件道具");
            Assert.AreEqual(1, world.Platform.ItemMarkerCount, "买入的道具应出现在平台上");
            Assert.Greater(world.Economy.ProductionPerSecond, 0, "购买后应继续有自动产出");
        }

        [UnityTest]
        public IEnumerator 超过实体上限时堆叠角标显示猫只数量()
        {
            CatGameType game;
            var world = NewWorld(out game);
            yield return null;

            foreach (var row in world.Economy.State.cats) row.count = 10;
            world.Roster.Rebuild(world.Economy.State);
            yield return new WaitForSeconds(0.6f);

            Assert.AreEqual(100, world.Roster.VisibleCount);
            Assert.AreEqual(10, world.Roster.StackedCount);
            Assert.IsTrue(world.Hud.transform.Find("CatStackPanel/StackCount").GetComponent<Text>().text.Contains("+10 只"));
            Assert.IsNotNull(world.Hud.transform.Find("CatStackPanel/StackBreed_0").GetComponent<Image>().sprite);
        }

        [UnityTest]
        public IEnumerator 成就条件满足时只解锁一次并排入提示()
        {
            CatGameType game;
            var world = NewWorld(out game);
            yield return null;

            int eventCount = 0;
            world.Economy.AchievementUnlocked += achievement => { if (achievement.id == "first_tuft") eventCount++; };
            world.Economy.State.totalEarned = 10;
            yield return new WaitForSeconds(0.35f);

            Assert.Contains("first_tuft", world.Economy.State.achievements);
            Assert.AreEqual("第一撮毛", world.Hud.Toast.CurrentMessage);
            Assert.AreEqual(1, eventCount);
            yield return new WaitForSeconds(0.25f);
            Assert.AreEqual(1, eventCount, "Repeated state refreshes must not re-unlock an achievement");
        }

        [UnityTest]
        public IEnumerator 离线结算弹窗暂停产出并可收下奖励()
        {
            CatGameType game;
            var world = NewWorld(out game);
            yield return null;

            double before = world.Economy.State.catHair;
            var result = new OfflineEarningsResult { elapsedSeconds = 600, catHair = 120 };
            Assert.IsTrue(world.Hud.ShowOfflineSummary(result));
            Assert.AreEqual(0f, Time.timeScale);
            var details = world.Hud.transform.Find("OfflineEarningsOverlay/Summary/Details").GetComponent<Text>();
            StringAssert.Contains("120 猫毛", details.text);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(before, world.Economy.State.catHair, 0.001, "Production should pause behind the summary");

            FindButton(world.Hud, "OfflineEarningsOverlay/Summary/Btn_Collect").onClick.Invoke();
            Assert.Greater(Time.timeScale, 0f);
        }

        [UnityTest]
        public IEnumerator 猫达到四十只上限后购买卡禁用()
        {
            CatGameType game;
            var world = NewWorld(out game);
            yield return null;

            var milk = world.Economy.State.cats.Find(row => row.id == "Milk");
            milk.count = 40;
            world.Economy.State.catHair = 1000000;
            world.Roster.Rebuild(world.Economy.State);
            yield return new WaitForSeconds(0.6f);

            var button = FindButton(world.Hud, "PurchasePanel/ShopViewport/Content/Cat_Milk");
            Assert.IsFalse(button.interactable);
            Assert.IsFalse(world.Economy.BuyCat("Milk"));
            Assert.AreEqual(40, milk.count);
        }
    }
}
