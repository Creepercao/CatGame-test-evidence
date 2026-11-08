using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CatGameNs = global::CatGame;

namespace CatGame.EditTests
{
    /// <summary>
    /// 场景与资源的静态检查：PlayMode 测试跑在空场景里，
    /// 抓不到"场景没配好""贴图切帧错了"这类问题，所以单独补一层。
    /// </summary>
    public class SceneAndAssetTests
    {
        // 测试程序集引用不到 Editor 程序集（Assets/Editor 是预定义程序集），所以路径在这里写死。
        // 改场景文件名时同步这里 + CatGame.EditorTools.SceneBuilders。
        const string MenuPath = "Assets/Scenes/MainMenu.unity";
        const string HomePath = "Assets/Scenes/Home.unity";
        const string ParkPath = "Assets/Scenes/Park.unity";

        [Test]
        public void Story_CG_and_Momo_portrait_are_single_uncompressed_illustration_sprites()
        {
            foreach (var name in new[]{"MenuMomo_v1","MomoPortrait_v1"})
            {
                string path="Assets/Resources/Art/Story/"+name+".png";
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                Assert.NotNull(importer);
                Assert.AreEqual(SpriteImportMode.Single,importer.spriteImportMode);
                Assert.AreEqual(FilterMode.Bilinear,importer.filterMode);
                Assert.AreEqual(TextureImporterCompression.Uncompressed,importer.textureCompression);
                Assert.IsFalse(importer.mipmapEnabled);
                Assert.NotNull(Resources.Load<Sprite>("Art/Story/"+name));
            }
        }

        [Test]
        public void Pixel_item_icons_and_UI_masks_are_imported_without_smoothing()
        {
            foreach (var definition in CatGameNs.Idle.ItemCatalog.All)
            {
                var sprite = CatGameNs.ItemVisualLibrary.Load(definition.id);
                Assert.NotNull(sprite, definition.id);
                Assert.AreEqual(64, sprite.rect.width);
                Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode);
                Assert.AreEqual(100, sprite.pixelsPerUnit);
            }
            foreach (var sprite in new[] { CatGameNs.UiKit.RoundSoft, CatGameNs.UiKit.RoundPill, CatGameNs.UiKit.Circle })
            {
                Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode);
                Assert.Greater(sprite.border.x, 0, "9-slice border must be imported");
                Assert.IsTrue(AssetDatabase.GetAssetPath(sprite).Contains("/UI/Pixel/"));
            }
        }

        [Test]
        public void Home_pixel_room_has_architecture_six_furniture_pieces_and_bound_surface()
        {
            EditorSceneManager.OpenScene(HomePath, OpenSceneMode.Single);
            var room = GameObject.Find("Room");
            var background = room.GetComponent<SpriteRenderer>();
            Assert.AreEqual("Assets/Resources/Art/Room/Pixel/architecture.png", AssetDatabase.GetAssetPath(background.sprite));
            Assert.AreEqual(512, background.sprite.rect.width);
            Assert.AreEqual(FilterMode.Point, background.sprite.texture.filterMode);
            var surface = room.GetComponent<CatGameNs.RoomWalkSurface>();
            Assert.AreSame(background, surface.Background);
            Assert.AreSame(surface, Object.FindObjectOfType<CatGameNs.CatWorld>().WalkSurface);
            var pieces = room.transform.Find("BackWallFurniture").GetComponentsInChildren<SpriteRenderer>();
            Assert.AreEqual(6, pieces.Length);
            Assert.IsTrue(pieces.All(p => p.sprite != null && p.sortingLayerID == background.sortingLayerID));
            Assert.AreEqual(4, room.GetComponentsInChildren<CatGameNs.RoomFurnitureDepth>().Length);
            Assert.AreEqual(2, room.transform.Find("BackWallFurniture/Bookshelf").GetComponent<CatGameNs.RoomFurnitureDepth>().Pieces.Length);
            Assert.AreEqual(100, background.sprite.pixelsPerUnit);
        }

        [Test]
        public void 三个场景都存在且能打开()
        {
            foreach (var path in new[] { MenuPath, HomePath, ParkPath })
            {
                Assert.IsTrue(System.IO.File.Exists(path), "缺少场景: " + path);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Assert.IsTrue(scene.IsValid(), "场景无法打开: " + path);
            }
        }

        [Test]
        public void 猫窝与公园有世界根与相机()
        {
            foreach (var path in new[] { HomePath, ParkPath })
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                var world = Object.FindObjectsOfType<CatGameNs.CatWorld>().FirstOrDefault();
                Assert.IsNotNull(world, path + " 里缺少 CatWorld（相机/猫/HUD 都没人管）");

                var cam = Object.FindObjectsOfType<Camera>().FirstOrDefault();
                Assert.IsNotNull(cam, path + " 里缺少相机");
                Assert.IsTrue(cam.orthographic, path + " 的相机应该是正交的");
                Assert.That(cam.orthographicSize, Is.EqualTo(5.4f).Within(0.01f),
                    "orthographicSize 应为 5.4（1 世界单位 = 100 像素）");
            }
        }

        [Test]
        public void 主菜单场景有菜单与事件系统()
        {
            EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Single);

            Assert.IsNotNull(Object.FindObjectsOfType<CatGameNs.MainMenuUI>().FirstOrDefault(),
                "主菜单场景缺少 MainMenuUI");

            var es = Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>().FirstOrDefault();
            Assert.IsNotNull(es, "主菜单场景缺少 EventSystem，按钮点不动");
            Assert.IsNotNull(es.GetComponent<UnityEngine.EventSystems.BaseInputModule>(),
                "EventSystem 上必须挂输入模块（StandaloneInputModule），否则点击不会被派发");

            Assert.IsNotNull(Object.FindObjectsOfType<Camera>().FirstOrDefault(), "主菜单场景缺少相机");
        }

        [Test]
        public void 公园场景有地面和摆件()
        {
            EditorSceneManager.OpenScene(ParkPath, OpenSceneMode.Single);

            var renderers = Object.FindObjectsOfType<SpriteRenderer>();
            Assert.Greater(renderers.Length, 40, "公园里的精灵太少了，场景大概没生成好");
            Assert.IsTrue(renderers.All(r => r.sprite != null), "有精灵没挂图");

            // 地面必须铺满：最宽的渲染器要覆盖约 1920 像素（19.2 世界单位）
            float widest = renderers.Max(r => r.bounds.size.x);
            Assert.Greater(widest, 18f, "地面没有铺满整屏");
        }

        [Test]
        public void 猫窝场景有房间背景()
        {
            EditorSceneManager.OpenScene(HomePath, OpenSceneMode.Single);

            var renderers = Object.FindObjectsOfType<SpriteRenderer>();
            Assert.Greater(renderers.Length, 0, "猫窝里没有任何背景精灵");
            Assert.Greater(renderers.Max(r => r.bounds.size.x), 18f, "房间背景没有铺满整屏");
        }

        [Test]
        public void 猫咪图鉴公园背景是单张Sprite并可从Resources加载()
        {
            const string assetPath = "Assets/Resources/Art/Backgrounds/Pixel/CatalogPark.png";
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            Assert.IsNotNull(importer, "图鉴背景贴图不存在或未导入");
            Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode);
            Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Sprite>(assetPath), "图鉴背景没有生成Sprite资源");
            var sprite = Resources.Load<Sprite>("Art/Backgrounds/Pixel/CatalogPark");
            Assert.IsNotNull(sprite, "Resources 路径未能加载图鉴背景");
            Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode);
        }

        [Test]
        public void 构建列表只包含主菜单和猫窝且主菜单排第一()
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var path in new[] { MenuPath, HomePath })
                Assert.IsTrue(scenes.Any(s => s.path == path && s.enabled),
                    "构建列表里缺少或没启用: " + path);

            Assert.AreEqual(MenuPath, scenes[0].path, "第一个场景必须是主菜单");
            Assert.IsFalse(scenes.Any(s => s.path == ParkPath && s.enabled), "公园保留在工程中，但不能从游戏场景列表进入");
            Assert.AreEqual(2, scenes.Count(s => s.enabled), "可构建场景应只有主菜单与猫窝");
        }

        [TestCase("Milk", "Idle", 5, 64)]
        [TestCase("Milk", "Walk", 6, 64)]
        [TestCase("Milk", "Jump", 5, 64)]
        [TestCase("Snow", "Idle", 5, 64)]
        public void 猫咪动画切帧数量正确(string breed, string clip, int expected, int cell)
        {
            var path = "Assets/Resources/Art/Cats/" + breed + "/" + clip + ".png";
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();

            Assert.AreEqual(expected, sprites.Length,
                breed + "/" + clip + " 应该切成 " + expected + " 帧，实际 " + sprites.Length);

            foreach (var s in sprites)
            {
                Assert.AreEqual((float)cell, s.rect.width, "帧宽应为 " + cell);
                Assert.AreEqual((float)cell, s.rect.height, "帧高应为 " + cell);
            }
        }

        [Test]
        public void 猫咪贴图用点采样_房间图用双线性()
        {
            var cat = (TextureImporter)AssetImporter.GetAtPath("Assets/Resources/Art/Cats/Milk/Idle.png");
            Assert.IsNotNull(cat);
            Assert.AreEqual(FilterMode.Point, cat.filterMode, "像素猫必须点采样，否则放大后糊");
            Assert.AreEqual(SpriteImportMode.Multiple, cat.spriteImportMode);

            var room = (TextureImporter)AssetImporter.GetAtPath("Assets/Resources/Art/Room/Room.png");
            Assert.IsNotNull(room);
            Assert.AreEqual(FilterMode.Bilinear, room.filterMode, "房间是平滑矢量风，应该用双线性");
            Assert.AreEqual(SpriteImportMode.Single, room.spriteImportMode);
        }

        [Test]
        public void 所有品种的动画资源都齐全()
        {
            var breeds = CatGameNs.BreedLibrary.LoadAll();
            Assert.Greater(breeds.Count, 0, "一个品种都没加载到");

            // 动作名 → 期望帧数（改素材帧数时必须同步这里）
            var expected = new (string clip, int frames)[]
            {
                (CatGameNs.ActionNames.Idle, 8),
                (CatGameNs.ActionNames.Walk, 8),
                (CatGameNs.ActionNames.Run, 5),
                (CatGameNs.ActionNames.Jump, 12),
                (CatGameNs.ActionNames.Sleep, 8),
                (CatGameNs.ActionNames.Eat, 10),
                (CatGameNs.ActionNames.Attack, 11),
            };

            foreach (var b in breeds)
            {
                foreach (var e in expected)
                {
                    var clip = b.Clip(e.clip);
                    Assert.IsNotNull(clip, b.Id + " 缺少动画: " + e.clip);
                    Assert.AreEqual(e.frames, clip.Length, b.Id + "/" + e.clip + " 帧数不对");
                }
            }
            Assert.AreEqual(3, breeds.Count, "品种数量应为 3（改品种表时要同步这里）");
        }

        [Test]
        public void 猫咪贴图没有被降采样()
        {
            var clips = new[] { "Idle", "Walk", "Run", "Jump", "Sleep", "Eat", "Attack" };
            foreach (var clip in clips)
            {
                var path = "Assets/Resources/Art/Cats/Milk/" + clip + ".png";
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.IsNotNull(imp, "找不到 " + path);
                Assert.AreEqual(2048, imp.maxTextureSize,
                    clip + " 的 maxTextureSize 太小：超过上限的横条会被降采样，像素直接糊");

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.IsNotNull(tex, clip + " 贴图加载不到");
                var frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                Assert.Greater(frames.Length, 0, clip + " 没切出帧");
                Assert.AreEqual(frames.Length * 64, tex.width,
                    clip + " 的贴图宽度被改了（降采样 / 压缩都会让像素糊）");
            }
        }

        [Test]
        public void 构建产物存在()
        {
            var exe = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Build/CatGame.exe"));
            Assert.IsTrue(System.IO.File.Exists(exe), "还没有构建，或构建输出不在 Build/CatGame.exe: " + exe);
        }
    }
}
