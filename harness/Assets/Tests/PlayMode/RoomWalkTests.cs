using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatGame.Idle;

namespace CatGame.Tests
{
    public class RoomWalkTests : CatGameTestBase
    {
        RoomWalkSurface NewSurface(float scale)
        {
            var go = Track(new GameObject("Room.Test"));
            go.transform.localScale = Vector3.one * scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Resources.Load<Sprite>("Art/Room/Pixel/architecture");
            Assert.NotNull(renderer.sprite);
            var surface = go.AddComponent<RoomWalkSurface>();
            surface.Background = renderer;
            return surface;
        }

        static Vector3 World(RoomWalkSurface surface, float x, float y)
        {
            return surface.transform.TransformPoint(new Vector3((x - 256) / 100, (144 - y) / 100, 0));
        }

        [Test]
        public void Floor_boundary_move_keeps_pixel_snapped_feet_on_floor()
        {
            var surface = NewSurface(3.75f);
            var actor = Track(new GameObject("Boundary.Repro")).AddComponent<CatActor>();
            actor.Setup(CatVisualLibrary.Load(CatCatalog.All[0]), new Vector3(2.2f, -.2f), new Vector2(6.5f, 3.6f));
            actor.SetWalkSurface(surface);
            var position = typeof(CatActor).GetField("_pos", BindingFlags.Instance | BindingFlags.NonPublic);
            var move = typeof(CatActor).GetMethod("MoveTo", BindingFlags.Instance | BindingFlags.NonPublic);
            var apply = typeof(CatActor).GetMethod("ApplyVisual", BindingFlags.Instance | BindingFlags.NonPublic);
            position.SetValue(actor, new Vector3(7.5f, 1.78f));
            var candidate = new Vector3(7.5f, 1.7874f);
            Assert.IsTrue(surface.Contains(candidate + Vector3.up * actor.GroundOffset), "logical destination is valid");
            move.Invoke(actor, new object[] { candidate });
            apply.Invoke(actor, null);
            Assert.IsTrue(surface.Contains(actor.transform.position + Vector3.up * actor.GroundOffset),
                "displayed feet cross the wall after rounding root y to 1.79");
            var safe = new Vector3(7.5f, 1.76f);
            move.Invoke(actor, new object[] { safe });
            apply.Invoke(actor, null);
            Assert.AreEqual(safe, actor.Position, "valid movement must remain possible");
        }

        [Test]
        public void Room_surface_rejects_walls_props_and_crossing_segments_after_resize()
        {
            var surface = NewSurface(3.75f);
            foreach (float scale in new[] { 3.75f, 5f, 6f })
            {
                surface.transform.localScale = Vector3.one * scale;
                Assert.IsFalse(surface.Contains(World(surface, 470, 100)), "right wall");
                Assert.IsTrue(surface.Contains(World(surface, 470, 145)), "right floor");
                Assert.IsFalse(surface.Contains(World(surface, 144, 118)), "bookshelf footprint");
                Assert.IsTrue(surface.Contains(World(surface, 144, 140)), "in front of bookshelf");
                Assert.IsFalse(surface.AllowsSegment(World(surface, 112, 118), World(surface, 178, 118)), "no tunnelling through shelf");
                Assert.IsTrue(surface.AllowsSegment(World(surface, 190, 180), World(surface, 450, 240)));
            }
        }

        [UnityTest]
        public IEnumerator One_hundred_cats_move_on_floor_without_crossing_furniture()
        {
            var surface = NewSurface(3.75f);
            var state = SaveSystem.CreateNewGame();
            foreach (var count in state.cats) count.count = 40;
            var roster = Track(new GameObject("Roster.Room.Test")).AddComponent<CatRoster>();
            roster.Initialize(state, new Vector3(2.2f, -.2f), new Vector2(6.5f, 3.6f), walkSurface: surface);
            Assert.AreEqual(100, roster.VisibleCount);
            var actors = roster.GetComponentsInChildren<CatActor>();
            Assert.AreEqual(100, actors.Length);
            var step = typeof(CatActor).GetMethod("StepForced", BindingFlags.Instance | BindingFlags.NonPublic);
            var apply = typeof(CatActor).GetMethod("ApplyVisual", BindingFlags.Instance | BindingFlags.NonPublic);
            var starts = new Vector3[actors.Length];
            for (int i = 0; i < actors.Length; i++) { starts[i] = actors[i].Position; actors[i].PlayAction(ActionNames.Walk); }
            for (int tick = 0; tick < 240; tick++)
            {
                foreach (var actor in actors)
                {
                    var previous = actor.Position + Vector3.up * actor.GroundOffset;
                    step.Invoke(actor, new object[] { .25f });
                    apply.Invoke(actor, null);
                    var foot = actor.transform.position + Vector3.up * actor.GroundOffset;
                    Assert.IsTrue(surface.Contains(foot), "rendered foot remains on floor");
                    Assert.IsTrue(surface.AllowsSegment(previous, actor.Position + Vector3.up * actor.GroundOffset));
                }
            }
            int moved = 0;
            for (int i = 0; i < actors.Length; i++) if (Vector3.Distance(starts[i], actors[i].Position) > .1f) moved++;
            Assert.Greater(moved, 90, "floor constraint must permit roaming");
            yield return null;
        }
    }
}
