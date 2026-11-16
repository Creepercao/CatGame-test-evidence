using CatGame.Idle;
using NUnit.Framework;
using UnityEngine;

namespace CatGame.Tests
{
    public sealed class WorldArtContractTests : CatGameTestBase
    {
        [Test]
        public void Actor_origin_is_the_authored_foot_for_every_primary_action()
        {
            foreach (var definition in CatCatalog.All)
            {
                var actor = Track(new GameObject("Foot." + definition.id)).AddComponent<CatActor>();
                actor.Setup(CatVisualLibrary.Load(definition), new Vector3(2, -1, 0), new Vector2(6, 4));
                Assert.That(Vector2.Distance(actor.Renderer.bounds.center, new Vector2(2, -1)), Is.LessThan(.0001f),
                    "spawn body stays centred in the configured visual area after foot pivot migration");
                foreach (var action in new[] { ActionNames.Idle, ActionNames.Walk, ActionNames.Jump, ActionNames.Sleep })
                {
                    Assert.IsTrue(actor.PlayAction(action));
                    var sprite = actor.Renderer.sprite;
                    var foot = actor.transform.TransformPoint(new Vector3(0,
                        sprite.bounds.min.y + sprite.bounds.size.y * (6f / 64f), 0));
                    Assert.That(Vector3.Distance(actor.transform.position, foot), Is.LessThan(.0001f),
                        definition.id + "/" + action + " root must be the stable authored foot, not the canvas centre");
                    Assert.That(actor.Renderer.bounds.size.y, Is.EqualTo(2f).Within(.0001f));
                }
            }
        }

        [Test]
        public void Actor_click_collider_follows_body_after_action_and_facing_changes()
        {
            var actor = Track(new GameObject("Collider.Art")).AddComponent<CatActor>();
            actor.Setup(CatVisualLibrary.Load(CatCatalog.All[0]), new Vector2(6, 4));
            foreach (float facing in new[] { 1f, -1f })
            foreach (var action in new[] { ActionNames.Idle, ActionNames.Jump, ActionNames.Sleep })
            {
                typeof(CatActor).GetField("_facing", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(actor, facing);
                actor.PlayAction(action);
                var collider = actor.GetComponent<BoxCollider2D>();
                Physics2D.SyncTransforms();
                Assert.That(Vector2.Distance(collider.bounds.center, actor.Renderer.bounds.center), Is.LessThan(.0001f));
                Assert.That(Vector2.Distance(collider.bounds.size, actor.Renderer.bounds.size), Is.LessThan(.0001f));
                Assert.IsTrue(collider.OverlapPoint(actor.Renderer.bounds.center));
                Assert.IsFalse(collider.OverlapPoint(actor.Renderer.bounds.max + Vector3.one * .1f));
            }
        }
    }
}
