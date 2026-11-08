using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CatGame.Tests
{
    public class StoryMenuLayoutTests : CatGameTestBase
    {
        [UnityTest]
        public IEnumerator Main_menu_places_CG_left_and_existing_options_right()
        {
            var menu = Track(new GameObject("Menu.Test",typeof(RectTransform))).AddComponent<MainMenuUI>();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var panel = (RectTransform)menu.transform.Find("Panel");
            Assert.AreEqual(.65f,panel.anchorMin.x);
            Assert.AreEqual(1,panel.anchorMax.x);
            var cg = menu.transform.Find("MenuIllustration/CG").GetComponent<Image>();
            Assert.AreSame(Resources.Load<Sprite>("Art/Story/MenuMomo_v1"),cg.sprite);
            Assert.IsFalse(cg.raycastTarget);
            Assert.AreEqual(AspectRatioFitter.AspectMode.EnvelopeParent,cg.GetComponent<AspectRatioFitter>().aspectMode);
            foreach (var name in new[]{"Btn_Continue","Btn_NewGame","Btn_Settings","Btn_Quit"})
                Assert.NotNull(panel.Find(name).GetComponent<Button>());
        }
    }
}
