using NUnit.Framework;
using UnityEngine;

namespace CatGame.EditTests
{
    public sealed class WorldArtSettingsTests
    {
        [TestCase(1920, 1080, 640, 360, 192)]
        [TestCase(2560, 1440, 640, 360, 256)]
        [TestCase(1600, 900, 640, 360, 160)]
        [TestCase(2000, 900, 800, 360, 160)]
        [TestCase(1200, 900, 640, 480, 120)]
        [TestCase(2712, 1220, 800, 360, 216.8889f)]
        public void World_canvas_keeps_its_proportion_and_fits_the_reference(int width, int height,
            int rasterWidth, int rasterHeight, float catScreenHeight)
        {
            var rules = ScriptableObject.CreateInstance<WorldArtSettings>();
            try
            {
                rules.Validate();
                float half = rules.HalfHeight(width, height);
                Assert.That(half * 2 * width / height, Is.GreaterThanOrEqualTo(20f - .0001f));
                Assert.That(half * 2, Is.GreaterThanOrEqualTo(11.25f - .0001f));
                Assert.That(height / half, Is.EqualTo(catScreenHeight).Within(.002f));
                Assert.AreEqual(new Vector2Int(rasterWidth, rasterHeight), rules.RenderSize(width, height));
            }
            finally { Object.DestroyImmediate(rules); }
        }

        [TestCase(32, 32)]
        [TestCase(64, 32)]
        [TestCase(32, 100)]
        [TestCase(64, 100)]
        public void Legacy_and_current_frames_share_the_same_world_canvas(int frameSize, int ppu)
        {
            var rules = ScriptableObject.CreateInstance<WorldArtSettings>();
            var texture = new Texture2D(frameSize, frameSize);
            var sprite = Sprite.Create(texture, new Rect(0, 0, frameSize, frameSize), new Vector2(.5f, .09375f), ppu);
            try { Assert.That(sprite.bounds.size.y * rules.CatScale(sprite), Is.EqualTo(2f).Within(.0001f)); }
            finally { Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); Object.DestroyImmediate(rules); }
        }

        [Test]
        public void Home_visual_area_and_item_grid_keep_the_authored_foot_anchor()
        {
            var rules = ScriptableObject.CreateInstance<WorldArtSettings>();
            try
            {
                Assert.AreEqual(rules.HomeFootCenter + Vector2.up * .8125f, rules.HomeFloorCenter);
                Assert.AreEqual(rules.HomeFootCenter + rules.ItemGridOffsetPixels / 32f,
                    rules.HomeFloorCenter + rules.ItemGridOffset);
            }
            finally { Object.DestroyImmediate(rules); }
        }

        [Test]
        public void Invalid_grid_or_viewport_is_reported_instead_of_dividing_by_zero()
        {
            var rules = ScriptableObject.CreateInstance<WorldArtSettings>();
            try
            {
                Assert.Throws<System.ArgumentOutOfRangeException>(() => rules.HalfHeight(0, 900));
                rules.WorldPixelsPerUnit = 0;
                Assert.Throws<System.InvalidOperationException>(() => rules.Validate());
            }
            finally { Object.DestroyImmediate(rules); }
        }
    }
}
