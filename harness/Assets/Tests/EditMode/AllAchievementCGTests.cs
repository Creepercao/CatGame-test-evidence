using System.Collections.Generic;
using CatGame.Idle;
using NUnit.Framework;
using UnityEngine;

namespace CatGame.EditTests
{
    public sealed class AllAchievementCGTests
    {
        [Test] public void Every_achievement_has_a_unique_imported_CG_with_matching_title_and_order()
        {
            Assert.AreEqual(20,AchievementCatalog.All.Length);
            Assert.AreEqual(AchievementCatalog.All.Length,StoryCatalog.All.Length);
            var ids=new HashSet<string>(); var resources=new HashSet<string>();
            for(int i=0;i<AchievementCatalog.All.Length;i++)
            {
                var achievement=AchievementCatalog.All[i]; var story=StoryCatalog.All[i];
                Assert.AreEqual(achievement.id,story.id); Assert.AreEqual(achievement.name,story.title);
                Assert.IsTrue(ids.Add(story.id)); Assert.IsTrue(resources.Add(story.resource));
                Assert.IsNotEmpty(story.caption);
                var sprite=Resources.Load<Sprite>(story.resource);
                Assert.IsNotNull(sprite,"Missing CG: "+story.id);
                Assert.AreEqual(FilterMode.Bilinear,sprite.texture.filterMode);
                Assert.GreaterOrEqual(sprite.rect.width,1280); Assert.GreaterOrEqual(sprite.rect.height,720);
                Assert.That(sprite.rect.width/sprite.rect.height,Is.InRange(1.7f,1.9f));
                Debug.Log("CG ASSET STEP: "+story.id+" -> "+story.resource+" "+sprite.rect.size);
            }
        }

        [Test] public void Genuine_all_twenty_unlocks_queue_once_and_remain_seen_after_repeated_evaluations()
        {
            var state=GameStateFactory.CreateNewGame(); state.totalEarned=1e12; state.clicks=1000;
            foreach(var row in state.cats) row.count=40;
            foreach(var row in state.items) row.level=5;
            var unlocked=AchievementCatalog.EvaluateNew(state); Assert.AreEqual(20,unlocked.Count);
            Assert.IsTrue(StoryCatalog.EnqueueUnlocked(state)); Assert.AreEqual(20,state.pendingStoryEvents.Count);
            for(int i=0;i<20;i++) Assert.AreEqual(AchievementCatalog.All[i].id,state.pendingStoryEvents[i]);
            foreach(var story in StoryCatalog.All) StoryCatalog.Complete(state,story.id);
            for(int i=0;i<10000;i++)
            {
                Assert.IsEmpty(AchievementCatalog.EvaluateNew(state));
                Assert.IsFalse(StoryCatalog.EnqueueUnlocked(state));
            }
            Assert.AreEqual(20,state.seenStoryEvents.Count); Assert.IsEmpty(state.pendingStoryEvents);
            Debug.Log("CG RULE STEP: 20 real unlock conditions, fixed queue, 10,000 duplicate checks passed.");
        }
    }
}
