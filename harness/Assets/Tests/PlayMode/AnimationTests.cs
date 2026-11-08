using System;
using System.Collections;
using CatGame.Idle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatGameType = global::CatGame.CatGame;
using CatWorldType = global::CatGame.CatWorld;

namespace CatGame.Tests
{
    /// <summary>
    /// 验证动画与猫这个实体的行为：品种表、七套帧动画、走路/原地动作的空间行为。
    /// 猫现在是世界实体（SpriteRenderer），所以断言落点从 Image.sprite 换成了 Renderer.sprite。
    /// </summary>
    public class AnimationTests : CatGameTestBase
    {
        [UnityTest]
        public IEnumerator 启动后加载三个品种并选中墨墨()
        {
            var game = NewGame();
            yield return null;

            Assert.AreEqual(3, game.Breeds.Count, "品种数量应为 3");
            Assert.IsNotNull(game.Current, "必须选中一个品种");
            Assert.AreEqual("Milk", game.Current.Id, "默认品种是墨墨");
            Assert.IsNotNull(game.Current.Idle, "默认品种必须有 Idle 帧");
        }

        [UnityTest]
        public IEnumerator 每个品种的七套动画都齐全()
        {
            var game = NewGame();
            yield return null;

            Assert.Greater(game.Breeds.Count, 0, "一个品种都没加载到");
            foreach (var breed in game.Breeds)
            {
                foreach (var action in ActionNames.All)
                {
                    var clip = breed.Clip(action);
                    Assert.IsNotNull(clip, breed.Id + " 缺少动画: " + action);
                    Assert.Greater(clip.Length, 0, breed.Id + "/" + action + " 是空的");
                }
            }
        }

        [UnityTest]
        public IEnumerator 切换预览品种不改变已拥有猫_绑定预览使用新品种帧()
        {
            CatGameType game;
            var world = NewWorld(out game);
            yield return null;

            Assert.IsTrue(game.SetActiveBreed("Snow"), "雪球是存在的品种，应能切换");
            yield return null;

            Assert.AreEqual("Snow", game.Current.Id);
            var cat = world.Cat;
            Assert.IsNotNull(cat);
            Assert.AreEqual("Milk", cat.Breed.Id, "预览选择不能改变已拥有猫的品种");
            var preview = Track(new GameObject("Preview.Test")).AddComponent<CatActor>();
            preview.Setup(game.Current, Vector2.one);
            Assert.AreEqual("Snow", preview.Breed.Id);
            Assert.IsTrue(preview.PlayAction(ActionNames.Eat), "新绑定的品种也要能播吃饭");
            Assert.That(Array.IndexOf(game.Current.Eat, preview.Renderer.sprite), Is.GreaterThanOrEqualTo(0),
                "换品种后必须用新品种的帧");
        }

        [UnityTest]
        public IEnumerator 切到不存在的品种_拒绝并保持当前()
        {
            var game = NewGame();
            yield return null;

            var before = game.Current;
            Assert.IsFalse(game.SetActiveBreed("Xmas"), "不存在的品种应被拒绝");
            Assert.AreSame(before, game.Current, "当前品种不该被改掉");
        }

        [UnityTest]
        public IEnumerator 猫默认在自动模式下播待机()
        {
            CatGameType game;
            var world = NewWorld(out game);
            yield return null;

            var cat = world.Cat;
            Assert.IsNotNull(cat, "世界里必须生成一只猫");
            Assert.IsFalse(cat.IsForced, "默认是自动模式");
            Assert.AreEqual(ActionNames.Idle, cat.CurrentAction);
            Assert.That(Array.IndexOf(game.Current.Idle, cat.Renderer.sprite), Is.GreaterThanOrEqualTo(0),
                "待机时播的必须是 Idle 的帧");
        }

        [UnityTest]
        public IEnumerator 强制播放每个动作都能出帧()
        {
            CatGameType game;
            var world = NewWorld(out game);
            yield return null;

            var cat = world.Cat;
            var renderer = cat.Renderer;

            foreach (var action in ActionNames.All)
            {
                var clip = game.Current.Clip(action);
                Assert.IsTrue(cat.PlayAction(action), "播放失败，缺少帧资源: " + action);
                Assert.AreEqual(action, cat.CurrentAction, "动作没切过去: " + action);
                Assert.That(Array.IndexOf(clip, renderer.sprite), Is.GreaterThanOrEqualTo(0),
                    action + " 的第一帧不属于该动作");

                // 播一小会儿，确认帧在推进且没有串到别的动作上
                yield return new WaitForSeconds(0.3f);
                Assert.AreEqual(action, cat.CurrentAction, "动作被意外改掉了: " + action);
                Assert.That(Array.IndexOf(clip, renderer.sprite), Is.GreaterThanOrEqualTo(0),
                    action + " 播放中出现了不属于该动作的帧");
            }
        }

        [UnityTest]
        public IEnumerator 走路动作会让猫在地板里移动()
        {
            CatGameType game;
            var world = NewWorld(out game);
            yield return null;

            var cat = world.Cat;
            var start = cat.Position;
            Assert.IsTrue(cat.PlayAction(ActionNames.Walk));
            yield return new WaitForSeconds(0.8f);

            Assert.Greater((cat.Position - start).magnitude, 0.2f, "走路时位置应该变化");
            Assert.That(Mathf.Abs(cat.Position.x), Is.LessThanOrEqualTo(cat.FloorHalfAxes.x + 0.5f), "不能走出地板");
            Assert.That(Mathf.Abs(cat.Position.y), Is.LessThanOrEqualTo(cat.FloorHalfAxes.y + 0.5f), "不能走出地板");
        }

        [UnityTest]
        public IEnumerator 原地动作不会移动猫()
        {
            CatGameType game;
            var world = NewWorld(out game);
            yield return null;

            var cat = world.Cat;
            Assert.IsTrue(cat.PlayAction(ActionNames.Sleep));
            var start = cat.Position;
            yield return new WaitForSeconds(0.5f);

            Assert.AreEqual(start, cat.Position, "睡觉时猫应该待在原地");
        }

        [UnityTest]
        public IEnumerator 自动模式下猫会自己走动()
        {
            CatGameType game;
            var world = NewWorld(out game);
            yield return null;

            var cat = world.Cat;
            var start = cat.Position;

            // 自动模式会先站 0.6~2.4 秒再出发
            var calico = Track(new GameObject("Calico.Roaming")).AddComponent<CatActor>();
            var legacy=LegacyCalicoBreed();
            legacy.UseWalkWhileMoving=true; // Missing Walk must fall back to Idle even with the new opt-in flag.
            calico.Setup(legacy, world.FloorHalf);
            var calicoStart = calico.Position;
            float distance = 0, calicoDistance = 0;
            float until = Time.time + 3.2f;
            while (Time.time < until)
            {
                yield return null;
                distance = Mathf.Max(distance, (cat.Position - start).magnitude);
                calicoDistance = Mathf.Max(calicoDistance, (calico.Position - calicoStart).magnitude);
                if (cat.IsWalking)
                {
                    Assert.AreEqual(ActionNames.Walk, cat.CurrentAction);
                    Assert.Contains(cat.Renderer.sprite, cat.Breed.Walk);
                }
                else Assert.That(cat.CurrentAction, Is.EqualTo(ActionNames.Idle).Or.EqualTo(ActionNames.Sleep));
                Assert.AreEqual(ActionNames.Idle, calico.CurrentAction);
            }
            Assert.Greater(distance, 0.3f, "自动模式下猫应该会自己漫游");
            Assert.Greater(calicoDistance, 0.3f, "原素材缺Walk时仍应使用Idle帧正常漫游");
            Assert.IsFalse(cat.IsForced, "自动模式下不该被强制指定动作");
            Assert.IsTrue(cat.Breed.UseWalkWhileMoving, "New Milk uses Walk; the original missing-Walk compatibility case stays Idle");
        }

        [Test]
        public void Assigning_open_walk_surface_preserves_initial_roaming_distance()
        {
            var random=UnityEngine.Random.state;
            var go=Track(new GameObject("Roaming.InitialTarget"));
            var actor=go.AddComponent<CatActor>();
            var target=typeof(CatActor).GetField("_target",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var breed=CatVisualLibrary.Load(CatCatalog.All[0]);
            try {
                for(int seed=0;seed<256;seed++) {
                    UnityEngine.Random.InitState(seed);
                    actor.Setup(breed,new Vector2(6,2));
                    actor.SetWalkSurface(null); // Same public initialization chain used by CatRoster.
                    float distance=((Vector3)target.GetValue(actor)-actor.Position).magnitude;
                    Assert.GreaterOrEqual(distance,.8f,"seed="+seed+"; attaching an open surface shortened the initial target");
                }
            } finally { UnityEngine.Random.state=random; }
        }

        [UnityTest]
        public IEnumerator 切回自动模式后恢复待机()
        {
            CatGameType game;
            var world = NewWorld(out game);
            yield return null;

            var cat = world.Cat;
            Assert.IsTrue(cat.PlayAction(ActionNames.Attack));
            yield return null;
            Assert.IsTrue(cat.IsForced);

            cat.ResumeAuto();
            yield return null;

            Assert.IsFalse(cat.IsForced, "应该回到自动模式");
            Assert.AreEqual(ActionNames.Idle, cat.CurrentAction);
            Assert.That(Array.IndexOf(game.Current.Idle, cat.Renderer.sprite), Is.GreaterThanOrEqualTo(0),
                "回到自动模式应该重播 Idle 帧");
        }

        [UnityTest]
        public IEnumerator 猫是按y排序的实体()
        {
            CatGameType game;
            var world = NewWorld(out game);
            yield return null;

            var cat = world.Cat;
            int before = cat.Renderer.sortingOrder;
            cat.SetFloor(new Vector3(0f, -2f, 0f), new Vector2(6f, 1f));
            yield return null;

            Assert.Less(before, cat.Renderer.sortingOrder, "越靠下（y 越小）排序应越靠前");
            Assert.IsNotNull(cat.Renderer.sprite, "实体必须有自己的 SpriteRenderer");
        }
    }
}
