using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatGame.Idle;

namespace CatGame.Tests
{
    public class AchievementToastVisualTests : CatGameTestBase
    {
        [UnityTest]
        public IEnumerator Achievement_toast_fades_and_next_queued_message_restores_opacity()
        {
            var host = Track(new GameObject("Toast.Test", typeof(RectTransform)));
            var toast = host.AddComponent<AchievementToast>();
            toast.Build(host.transform);
            toast.Enqueue(AchievementCatalog.All[0]);
            toast.Enqueue(AchievementCatalog.All[1]);
            var panel = host.transform.Find("AchievementToast");
            var opacity = panel.GetComponent<CanvasGroup>();
            Assert.NotNull(panel.Find("Icon"));
            Assert.IsFalse(opacity.blocksRaycasts);
            yield return null;
            StringAssert.StartsWith("已解锁", panel.Find("Condition").GetComponent<UnityEngine.UI.Text>().text);
            Assert.AreEqual(1, opacity.alpha);
            yield return new WaitForSecondsRealtime(2.9f);
            Assert.AreEqual(1, opacity.alpha, "stay opaque during the three-second hold");
            bool sawFade = false, sawNext = false;
            float deadline = Time.realtimeSinceStartup + 2;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (toast.CurrentMessage == AchievementCatalog.All[0].name && opacity.alpha > 0 && opacity.alpha < .98f) sawFade = true;
                if (toast.CurrentMessage == AchievementCatalog.All[1].name)
                {
                    Assert.AreEqual(1, opacity.alpha, "next queued message must not stay transparent");
                    sawNext = true;
                    break;
                }
                yield return null;
            }
            Assert.IsTrue(sawFade, "outgoing toast must visibly fade");
            Assert.IsTrue(sawNext, "queued message must continue");
        }
    }
}
