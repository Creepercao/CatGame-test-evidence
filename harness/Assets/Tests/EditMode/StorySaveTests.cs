using System;
using System.Collections.Generic;
using System.IO;
using CatGame.Idle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatGame.EditTests
{
    public class StorySaveTests
    {
        [Serializable] class LegacyV1
        {
            public int version=1,clicks=42,prestigeLevel=0;
            public double catHair=123.5,totalEarned=456,prestigeMultiplier=1;
            public List<CatCount> cats;
            public List<ItemLevel> items;
            public List<string> achievements=new List<string>{"three_breeds","first_click"};
            public long lastSeenUnix=1700000000;
        }
        string _directory,_path;
        [SetUp] public void SetUp()
        {
            _directory=Path.Combine(Application.temporaryCachePath,"StorySaveTests-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory); _path=Path.Combine(_directory,"save.json");
        }
        [TearDown] public void TearDown()
        {
            Assert.IsTrue(Path.GetFullPath(_directory).StartsWith(Path.GetFullPath(Application.temporaryCachePath)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase));
            if(Directory.Exists(_directory)) Directory.Delete(_directory,true);
        }
        [Test] public void V1_save_migrates_without_replaying_intro_or_historical_CG_and_preserves_progress()
        {
            var defaults=SaveSystem.CreateNewGame(); defaults.cats[0].count=4; defaults.items[0].level=3;
            var old=new LegacyV1{cats=defaults.cats,items=defaults.items};
            string original=JsonUtility.ToJson(old);
            File.WriteAllText(_path,original);
            var state=SaveSystem.LoadOrCreate(_path);
            Assert.AreEqual(2,state.version); Assert.IsTrue(state.introCompleted);
            Assert.AreEqual(123.5,state.catHair); Assert.AreEqual(456,state.totalEarned); Assert.AreEqual(42,state.clicks);
            Assert.AreEqual(4,state.cats[0].count); Assert.AreEqual(3,state.items[0].level);
            CollectionAssert.AreEquivalent(old.achievements,state.achievements);
            CollectionAssert.AreEquivalent(new[]{"three_breeds","first_click"},state.seenStoryEvents);
            Assert.IsEmpty(state.pendingStoryEvents); Assert.IsFalse(state.hasFullPlaytimeHistory);
            Assert.IsFalse(state.rocketPurchased); Assert.IsFalse(state.endingCompleted);
            Assert.AreEqual(original,File.ReadAllText(_path),"load migration does not overwrite old bytes");
            SaveSystem.SaveToPath(_path,state);
            Assert.IsTrue(SaveSystem.LoadOrCreate(_path).introCompleted);
            Assert.IsFalse(File.Exists(Path.Combine(_directory,"save.bad.json")));
        }
        [Test] public void V2_intro_and_pending_story_progress_survive_save_round_trip()
        {
            var state=SaveSystem.CreateNewGame();
            Assert.IsFalse(state.introCompleted); Assert.IsTrue(state.hasFullPlaytimeHistory);
            state.pendingStoryEvents.Add("six_breeds"); state.playSeconds=12.25;
            SaveSystem.SaveToPath(_path,state);
            Assert.IsFalse(SaveSystem.LoadOrCreate(_path).introCompleted,"interruption keeps intro pending");
            state.introCompleted=true; SaveSystem.SaveToPath(_path,state);
            var loaded=SaveSystem.LoadOrCreate(_path);
            Assert.IsTrue(loaded.introCompleted); Assert.AreEqual(12.25,loaded.playSeconds);
            CollectionAssert.AreEqual(state.pendingStoryEvents,loaded.pendingStoryEvents);
        }
        [Test] public void B4_v2_history_without_CG_markers_is_normalized_but_real_pending_memory_survives()
        {
            var state=SaveSystem.CreateNewGame(); state.introCompleted=true;
            state.achievements.AddRange(new[]{"three_breeds","six_breeds"});
            state.pendingStoryEvents.Add("six_breeds");
            string original=JsonUtility.ToJson(state);
            File.WriteAllText(_path,original);
            var loaded=SaveSystem.LoadOrCreate(_path);
            CollectionAssert.AreEqual(new[]{"three_breeds"},loaded.seenStoryEvents);
            CollectionAssert.AreEqual(new[]{"six_breeds"},loaded.pendingStoryEvents);
            Assert.AreEqual(original,File.ReadAllText(_path),"normalization on load does not overwrite historical bytes");
            SaveSystem.SaveToPath(_path,loaded);
            CollectionAssert.AreEqual(new[]{"six_breeds"},SaveSystem.LoadOrCreate(_path).pendingStoryEvents);
        }
        [Test] public void Story_save_rejects_repeated_events_or_nonfinite_playtime()
        {
            var state=SaveSystem.CreateNewGame(); state.playSeconds=double.NaN;
            Assert.Throws<InvalidDataException>(()=>SaveSystem.SaveToPath(_path,state));
            state.playSeconds=0; state.seenStoryEvents.Add("six_breeds"); state.pendingStoryEvents.Add("six_breeds");
            Assert.Throws<InvalidDataException>(()=>SaveSystem.SaveToPath(_path,state));
        }
        [Test] public void Existing_twenty_achievement_save_normalizes_new_historical_CGs_but_preserves_pending_first_pet()
        {
            var state=SaveSystem.CreateNewGame(); state.introCompleted=true;
            foreach(var achievement in AchievementCatalog.All) state.achievements.Add(achievement.id);
            state.seenStoryEvents.AddRange(new[]{"three_breeds","six_breeds","all_breeds","planet_cats"});
            state.pendingStoryEvents.Add("first_click");
            string original=JsonUtility.ToJson(state); File.WriteAllText(_path,original);
            var loaded=SaveSystem.LoadOrCreate(_path);
            Assert.AreEqual(19,loaded.seenStoryEvents.Count);
            CollectionAssert.AreEqual(new[]{"first_click"},loaded.pendingStoryEvents);
            Assert.IsFalse(StoryCatalog.EnqueueUnlocked(loaded));
            Assert.AreEqual(original,File.ReadAllText(_path));
            StoryCatalog.Complete(loaded,"first_click"); SaveSystem.SaveToPath(_path,loaded);
            var restored=SaveSystem.LoadOrCreate(_path);
            Assert.AreEqual(20,restored.seenStoryEvents.Count); Assert.IsEmpty(restored.pendingStoryEvents);
            Debug.Log("CG SAVE STEP: old 20-achievement save: 19 historical seen + 1 genuine pending; completion round-trips.");
        }
        [Test] public void V2_missing_intro_field_is_quarantined_instead_of_silently_resetting_story()
        {
            string original=JsonUtility.ToJson(SaveSystem.CreateNewGame());
            string missing=original.Replace("\"introCompleted\":false,","");
            Assert.AreNotEqual(original,missing);
            File.WriteAllText(_path,missing);
            LogAssert.Expect(LogType.Warning,new System.Text.RegularExpressions.Regex("Save data could not be loaded"));
            var state=SaveSystem.LoadOrCreate(_path);
            Assert.IsFalse(state.introCompleted);
            Assert.IsTrue(File.Exists(Path.Combine(_directory,"save.bad.json")));
        }
    }
}
