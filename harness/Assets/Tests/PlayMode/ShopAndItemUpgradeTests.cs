using System.Collections;
using System.Linq;
using CatGame.Idle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CatGame.Tests
{
    public class ShopAndItemUpgradeTests : CatGameTestBase
    {
        [UnityTest]
        public IEnumerator One_shop_scroll_jumps_sections_keeps_free_offset_and_partial_cards()
        {
            global::CatGame.CatGame game;
            var world = NewWorld(out game);
            yield return null;
            var panel = world.Hud.transform.Find("PurchasePanel");
            Assert.AreEqual(1, panel.GetComponentsInChildren<ScrollRect>().Length);
            var scroll = panel.GetComponentInChildren<ShopScrollRect>();
            Assert.AreEqual(32, scroll.content.GetComponentsInChildren<Button>(true).Length);
            panel.Find("Btn_shop_items").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.Greater(scroll.content.anchoredPosition.y, 1000);
            var first = scroll.content.Find("Item_item_1_1");
            Assert.AreEqual(1, first.GetComponent<CanvasGroup>().alpha);
            float before = scroll.content.anchoredPosition.y;
            world.Economy.State.catHair = 1e20;
            first.GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual(before, scroll.content.anchoredPosition.y, .1f, "purchase must not reset scrolling");
            scroll.StopMovement();
            scroll.content.anchoredPosition += new Vector2(0,37);
            float free=scroll.content.anchoredPosition.y;
            yield return new WaitForSecondsRealtime(.6f);
            Assert.AreEqual(free,scroll.content.anchoredPosition.y,.1f,"idle must not align cards");
            foreach(var button in scroll.content.GetComponentsInChildren<Button>(true))
                Assert.AreEqual(1,button.GetComponent<CanvasGroup>().alpha,"partial cards remain visible through viewport mask");
            scroll.verticalNormalizedPosition = 0;
            yield return null;
            Assert.AreEqual(1, scroll.content.Find("Item_item_5_4").GetComponent<CanvasGroup>().alpha);
            panel.Find("Btn_shop_cats").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual(0, scroll.content.anchoredPosition.y, .1f);
            Assert.AreEqual(1, scroll.content.Find("Cat_Milk").GetComponent<CanvasGroup>().alpha);
        }

        [UnityTest]
        public IEnumerator Items_use_cat_sized_opaque_bounds_ground_anchors_and_feeder_depth()
        {
            global::CatGame.CatGame game;
            var world = NewWorld(out game);
            yield return null;
            world.Economy.State.catHair = 1e200;
            foreach (var item in ItemCatalog.All) Assert.IsTrue(world.Economy.BuyItem(item.id));
            yield return null;
            foreach (var item in ItemCatalog.All)
            {
                var root = world.transform.Find("PlatformItem_" + item.id);
                var icon = root.Find("Icon");
                var sprite = icon.GetComponent<SpriteRenderer>();
                var profile = WorldItemArt.Get(item.id);
                float units = icon.lossyScale.y / sprite.sprite.pixelsPerUnit;
                float visibleWidth = profile.width * units, visibleHeight = profile.height * units;
                Assert.Greater(visibleWidth, .45f);
                Assert.AreEqual(0, icon.localPosition.y + (profile.bottom-32)*units, .001f, "opaque feet grounded");
                if (item.id.EndsWith("_2")) Assert.AreEqual(1.9f, visibleWidth, .001f);
                else if (item.id.EndsWith("_3"))
                {
                    Assert.AreEqual(1.15f, visibleHeight, .001f);
                    Assert.AreEqual(Mathf.RoundToInt((10-root.position.y)*100), sprite.sortingOrder);
                }
                else if (item.id.EndsWith("_4")) Assert.AreEqual(.7f, visibleHeight, .001f);
                else Assert.AreEqual(1.25f, visibleWidth, .001f);
            }
        }
    }
}
