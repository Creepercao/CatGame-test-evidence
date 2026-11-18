using System;
using NUnit.Framework;
using UnityEngine;

namespace CatGame.Tests
{
    public sealed class RoomThemeSettingsTests
    {
        [TestCase(0,0)] [TestCase(99999.99,0)] [TestCase(100000,1)]
        [TestCase(1000000000000d,1)]
        public void Cumulative_boundary_and_later_progress_resolve_existing_theme(double total,int expected)
        {
            var settings=ScriptableObject.CreateInstance<RoomThemeSettings>();
            try { Assert.AreEqual(expected,settings.Resolve(total)); }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }

        [Test] public void Duplicate_ids_and_unordered_or_invalid_thresholds_are_rejected()
        {
            var settings=ScriptableObject.CreateInstance<RoomThemeSettings>();
            try
            {
                settings.Themes[1].Id="starter";
                Assert.Throws<InvalidOperationException>(()=>settings.Validate());
                settings.Themes[1].Id="companions";settings.Themes[1].MinimumTotalEarned=0;
                Assert.Throws<InvalidOperationException>(()=>settings.Validate());
                settings.Themes[1].MinimumTotalEarned=double.NaN;
                Assert.Throws<InvalidOperationException>(()=>settings.Validate());
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }

        [Test] public void Invalid_observed_progress_does_not_silently_select_a_room()
        {
            var settings=ScriptableObject.CreateInstance<RoomThemeSettings>();
            try
            {
                Assert.Throws<ArgumentOutOfRangeException>(()=>settings.Resolve(double.NaN));
                Assert.Throws<ArgumentOutOfRangeException>(()=>settings.Resolve(double.PositiveInfinity));
                Assert.Throws<ArgumentOutOfRangeException>(()=>settings.Resolve(-1));
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }

        [Test] public void All_configured_assets_match_world_grid_and_complete_layouts()
        {
            var settings=Resources.Load<RoomThemeSettings>(RoomThemeSettings.ResourcePath);
            Assert.IsNotNull(settings);settings.Validate();
            foreach(var theme in settings.Themes)
            {
                var background=Resources.Load<Sprite>(theme.ResourceRoot+"/architecture");
                Assert.IsNotNull(background);Assert.AreEqual(new Vector2(512,288),background.rect.size);
                Assert.AreEqual(32,background.pixelsPerUnit);Assert.AreEqual(FilterMode.Point,background.texture.filterMode);
                var layout=JsonUtility.FromJson<RoomThemeLayout>(Resources.Load<TextAsset>(theme.ResourceRoot+"/layout").text);
                RoomThemeController.ValidateLayout(layout);
                foreach(var placement in layout.placements)
                {
                    var sprite=Resources.Load<Sprite>(theme.ResourceRoot+"/"+placement.asset);
                    Assert.IsNotNull(sprite);Assert.AreEqual(new Vector2(64,64),sprite.rect.size);
                    Assert.AreEqual(32,sprite.pixelsPerUnit);Assert.AreEqual(FilterMode.Point,sprite.texture.filterMode);
                }
            }
        }

        [Test] public void Existing_save_and_offline_earnings_select_the_final_room_without_a_new_save_field()
        {
            var state=global::CatGame.Idle.SaveSystem.CreateNewGame();
            state.totalEarned=99999;state.catHair=42;state.lastSeenUnix=1000;
            var folder=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,
                "../Build/S1/fixtures/"+Guid.NewGuid().ToString("N")));
            System.IO.Directory.CreateDirectory(folder);
            var path=System.IO.Path.Combine(folder,"save.json");
            global::CatGame.Idle.SaveSystem.SaveToPath(path,state);
            var loaded=global::CatGame.Idle.SaveSystem.LoadOrCreate(path);
            global::CatGame.Idle.OfflineEarnings.Apply(loaded,1010);
            var settings=Resources.Load<RoomThemeSettings>(RoomThemeSettings.ResourcePath);
            Assert.That(loaded.totalEarned,Is.GreaterThanOrEqualTo(100000));
            Assert.AreEqual(1,settings.Resolve(loaded.totalEarned));
            Assert.AreEqual(state.cats[0].count,loaded.cats[0].count);
            Assert.IsFalse(System.IO.File.ReadAllText(path).Contains("roomTheme"));
        }
    }
}
