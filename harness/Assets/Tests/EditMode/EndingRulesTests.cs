using System;
using System.IO;
using CatGame.Idle;
using NUnit.Framework;
using UnityEngine;

namespace CatGame.EditTests
{
    public class EndingRulesTests
    {
        [Test] public void Rocket_checks_balance_not_lifetime_income_and_ten_thousand_requests_charge_once()
        {
            var state=GameStateFactory.CreateNewGame(); state.totalEarned=EndingRules.RocketPrice*5;
            state.catHair=EndingRules.RocketPrice-1;
            Assert.IsFalse(EndingRules.TryPurchase(state)); Assert.IsFalse(state.rocketPurchased);
            state.catHair=EndingRules.RocketPrice+7; state.playSeconds=123.25; state.clicks=37;
            state.cats[0].count=4; state.cats[1].count=1; state.items[0].level=3;
            Assert.IsTrue(EndingRules.TryPurchase(state)); Assert.AreEqual(7,state.catHair);
            Assert.AreEqual(5,state.firstEnding.totalCats); Assert.AreEqual(2,state.firstEnding.breeds);
            Assert.AreEqual(3,state.firstEnding.itemLevels); Assert.AreEqual(37,state.firstEnding.clicks);
            Assert.AreEqual(123.25,state.firstEnding.playSeconds); Assert.IsTrue(state.hasEndingSnapshot);
            string first=JsonUtility.ToJson(state.firstEnding);
            state.catHair=EndingRules.RocketPrice*2; state.totalEarned*=2; state.clicks++;
            for(int i=0;i<10000;i++) Assert.IsFalse(EndingRules.TryPurchase(state));
            Assert.AreEqual(EndingRules.RocketPrice*2,state.catHair);
            Assert.AreEqual(first,JsonUtility.ToJson(state.firstEnding),"continued play cannot rewrite the first launch");
            var bad=GameStateFactory.CreateNewGame(); bad.catHair=double.NaN;
            Assert.IsFalse(EndingRules.TryPurchase(bad)); bad.catHair=double.PositiveInfinity;
            Assert.IsFalse(EndingRules.TryPurchase(bad));
        }

        [Test] public void Purchased_interrupted_ending_and_pending_CGs_round_trip_without_a_second_charge()
        {
            string directory=Path.Combine(Application.temporaryCachePath,"EndingRules-"+Guid.NewGuid().ToString("N"));
            string path=Path.Combine(directory,"save.json");
            try
            {
                var state=GameStateFactory.CreateNewGame(); state.introCompleted=true;
                state.hasFullPlaytimeHistory=false; state.catHair=EndingRules.RocketPrice+5;
                state.totalEarned=1000000002867.6625; state.playSeconds=2.562422018032521;
                Assert.IsTrue(EndingRules.TryPurchase(state));
                SaveSystem.SaveToPath(path,state); var loaded=SaveSystem.LoadOrCreate(path);
                Assert.IsTrue(loaded.rocketPurchased); Assert.IsFalse(loaded.endingCompleted);
                Assert.AreEqual(5,loaded.catHair); Assert.IsFalse(EndingRules.TryPurchase(loaded));
                Debug.Log("SNAPSHOT before="+JsonUtility.ToJson(state.firstEnding)+" restored="+JsonUtility.ToJson(loaded.firstEnding));
                Assert.AreEqual(state.firstEnding.totalEarned,loaded.firstEnding.totalEarned,.001);
                Assert.AreEqual(state.firstEnding.playSeconds,loaded.firstEnding.playSeconds,1e-9);
                Assert.Contains("planet_cats",loaded.pendingStoryEvents);
                Assert.IsFalse(loaded.firstEnding.hasFullPlaytimeHistory);
                StringAssert.Contains("旧档此前未记录",StoryPlayer.StatisticsText(loaded.firstEnding));
                loaded.endingCompleted=true; SaveSystem.SaveToPath(path,loaded);
                Assert.IsTrue(SaveSystem.LoadOrCreate(path).endingCompleted);
            }
            finally
            {
                Assert.IsTrue(Path.GetFullPath(directory).StartsWith(Path.GetFullPath(Application.temporaryCachePath)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase));
                if(Directory.Exists(directory)) Directory.Delete(directory,true);
            }
        }

        [Test] public void Story_queue_is_stable_and_deduplicates_many_offline_unlock_evaluations()
        {
            var state=GameStateFactory.CreateNewGame();
            state.achievements.AddRange(new[]{"planet_cats","all_breeds","six_breeds","three_breeds"});
            Assert.IsTrue(StoryCatalog.EnqueueUnlocked(state));
            for(int i=0;i<10000;i++) Assert.IsFalse(StoryCatalog.EnqueueUnlocked(state));
            CollectionAssert.AreEqual(new[]{"three_breeds","six_breeds","all_breeds","planet_cats"},state.pendingStoryEvents);
            StoryCatalog.Complete(state,"three_breeds");
            Assert.IsFalse(StoryCatalog.EnqueueUnlocked(state)); Assert.AreEqual(3,state.pendingStoryEvents.Count);
        }

        [Test] public void Ending_save_rejects_missing_snapshot_and_invalid_statistics()
        {
            var state=GameStateFactory.CreateNewGame(); state.rocketPurchased=true;
            Assert.Throws<InvalidDataException>(()=>SaveSystem.SaveToPath("unused/save.json",state));
            state.hasEndingSnapshot=true; state.firstEnding.totalEarned=double.NaN;
            Assert.Throws<InvalidDataException>(()=>SaveSystem.SaveToPath("unused/save.json",state));
            state.firstEnding.totalEarned=0; state.firstEnding.itemLevels=101;
            Assert.Throws<InvalidDataException>(()=>SaveSystem.SaveToPath("unused/save.json",state));
        }
    }
}
