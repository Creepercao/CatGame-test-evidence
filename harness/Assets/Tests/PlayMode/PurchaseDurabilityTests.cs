using System;
using System.IO;
using CatGame.Idle;
using NUnit.Framework;
using UnityEngine;

namespace CatGame.Tests
{
    public sealed class PurchaseDurabilityTests : CatGameTestBase
    {
        [TestCase("cat")]
        [TestCase("item")]
        public void Successful_purchase_is_on_disk_before_any_pause_or_next_frame(string kind)
        {
            string previous = Environment.GetEnvironmentVariable("CATGAME_EDITOR_QA_ROOT");
            string root = Path.Combine(Application.temporaryCachePath, "PurchaseDurability", Guid.NewGuid().ToString("N"));
            SaveSystem save = null;
            IdleGameController economy = null;
            try
            {
                Environment.SetEnvironmentVariable("CATGAME_EDITOR_QA_ROOT", root);
                save = new GameObject("Purchase Save Test").AddComponent<SaveSystem>();
                save.BeginNewGame();
                save.State.catHair = 1000;
                save.SaveNow();
                economy = new GameObject("Purchase Economy Test").AddComponent<IdleGameController>();
                economy.Initialize(save.State, null);
                Assert.IsTrue(kind == "cat" ? economy.BuyCat("Milk") : economy.BuyItem("item_1_1"));
                // No yield, pause message, scene exit, or explicit post-purchase SaveNow.
                var restored = SaveSystem.LoadOrCreate(save.SavePath);
                Assert.AreEqual(save.State.catHair, restored.catHair, .000001, "Committed purchase balance");
                if (kind == "cat") Assert.AreEqual(2, restored.cats.Find(row => row.id == "Milk").count);
                else Assert.AreEqual(1, restored.items.Find(row => row.id == "item_1_1").level);
            }
            finally
            {
                if (economy != null) UnityEngine.Object.DestroyImmediate(economy.gameObject);
                if (save != null) UnityEngine.Object.DestroyImmediate(save.gameObject);
                Environment.SetEnvironmentVariable("CATGAME_EDITOR_QA_ROOT", previous);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
