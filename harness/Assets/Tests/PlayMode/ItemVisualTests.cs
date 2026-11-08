using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.Tilemaps;
using CatGame.Idle;

namespace CatGame.Tests
{
    public class ItemVisualTests : CatGameTestBase
    {
        [UnityTest]
        public IEnumerator All_twenty_items_purchase_five_levels_and_share_card_marker_art()
        {
            global::CatGame.CatGame game;
            var world = NewWorld(out game);
            yield return null;
            Assert.AreEqual(0, world.Platform.ItemMarkerCount);
            world.Economy.State.catHair = 1e200;
            for (int level = 1; level <= 5; level++)
            {
                foreach (var item in ItemCatalog.All)
                {
                    Assert.IsTrue(world.Economy.BuyItem(item.id), item.id);
                    var marker = world.transform.Find("PlatformItem_" + item.id);
                    Assert.IsTrue(marker.gameObject.activeSelf);
                    var sprite = marker.Find("Icon").GetComponent<SpriteRenderer>();
                    var button = world.Hud.GetComponentsInChildren<Button>(true).Single(b => b.name == "Item_" + item.id);
                    var card = button.transform.Find("Icon").GetComponent<Image>();
                    Assert.AreSame(ItemVisualLibrary.Load(item.id), sprite.sprite);
                    Assert.AreSame(card.sprite, sprite.sprite);
                    Assert.AreEqual(Color.white, sprite.color);
                    Assert.IsFalse(card.raycastTarget, "art must not intercept purchase clicks");
                    StringAssert.Contains("Lv." + level, marker.Find("Level").GetComponent<TextMesh>().text);
                    if (item.id.EndsWith("_3")) Assert.NotNull(marker.GetComponent<RoomFurnitureDepth>(), "upright feeders use ground depth");
                    else Assert.Less(sprite.sortingOrder, world.Roster.GetFirstActor().Renderer.sortingOrder, "flat items stay below cats");
                }
                Assert.AreEqual(20, world.Platform.ItemMarkerCount);
                yield return null;
            }
            foreach (var item in ItemCatalog.All) Assert.IsFalse(world.Economy.BuyItem(item.id), "level cap " + item.id);
            Assert.IsFalse(world.Platform.GetComponentInChildren<TilemapRenderer>().enabled);
        }
    }
}
