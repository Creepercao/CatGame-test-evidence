using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CatGame.Idle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatGame.EditorTools
{
    /// <summary>Read-only Unity-resolved source references; baked scene values precede CatWorld.Start.</summary>
    public static class F1ArtAudit
    {
        [Serializable] sealed class Report
        {
            public string schema = "f1-import-baseline-v1", unity, sourceCommit;
            public string scope = "Catalog-resolved clips and baked Home scene, not native Player screen measurements";
            public List<SpriteRow> sprites = new List<SpriteRow>();
            public List<SceneRow> bakedSceneRenderers = new List<SceneRow>();
            public string[] homeDependencies;
            public string desktopFontName, androidFontResourcePath;
            public Vector2 floorCenter, floorHalf;
        }
        [Serializable] sealed class SpriteRow
        {
            public string use, path, guid, localFileId, name, filter;
            public float ppu;
            public Rect rect;
            public Vector2 pivot;
            public Bounds bounds;
            public PlatformRow android, standalone;
        }
        [Serializable] sealed class PlatformRow
        {
            public bool overridden;
            public int maximumSize;
            public string format, compression;
        }
        [Serializable] sealed class SceneRow
        {
            public string name, path;
            public Vector3 position, scale;
            public Bounds bounds;
            public int sortingOrder;
        }

        public static void Export()
        {
            try
            {
                string root = Environment.GetEnvironmentVariable("CATGAME_F1_AUDIT_ROOT");
                if (string.IsNullOrEmpty(root)) throw new InvalidOperationException("Explicit F1 output directory required");
                root = Path.GetFullPath(root);
                string allowed = Path.GetFullPath(Path.Combine(Application.dataPath, "../Build/F1")) + Path.DirectorySeparatorChar;
                if (!root.StartsWith(allowed, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("F1 output must be below the project Build/F1 directory");
                string output = Path.Combine(root, "unity-references.json");
                if (File.Exists(output)) throw new InvalidOperationException("Refusing to overwrite a baseline");
                Directory.CreateDirectory(root);
                var report = new Report { unity = Application.unityVersion,
                    sourceCommit = Environment.GetEnvironmentVariable("CATGAME_F1_SOURCE") };
                foreach (var definition in CatCatalog.All)
                {
                    var breed = CatVisualLibrary.Load(definition);
                    foreach (var action in ActionNames.All)
                    {
                        var frames = breed.Clip(action);
                        if (frames == null) continue;
                        foreach (var sprite in frames)
                            report.sprites.Add(Read(sprite, "cat/" + definition.id + "/" + action));
                    }
                }
                foreach (var item in ItemCatalog.All)
                    report.sprites.Add(Read(ItemVisualLibrary.Load(item.id), "item/" + item.id));
                report.sprites.Add(Read(Resources.Load<Sprite>("Art/Items/Pixel/rocket"), "ending/rocket"));
                report.sprites.Add(Read(UiKit.RoundSoft, "ui/panel-soft"));
                report.sprites.Add(Read(UiKit.RoundPill, "ui/panel-pill"));
                report.sprites.Add(Read(UiKit.Circle, "ui/circle"));
                report.sprites.Add(Read(Resources.Load<Sprite>("Art/Backgrounds/Pixel/CatalogPark"), "ui/catalog-background"));
                report.sprites.Add(Read(Resources.Load<Sprite>("Art/Story/MenuMomo_v1"), "ui/menu-illustration"));
                foreach (var story in StoryCatalog.All)
                    report.sprites.Add(Read(Resources.Load<Sprite>(story.resource), "ui/achievement/" + story.id));
                report.sprites.Add(Read(Resources.Load<Sprite>("Art/Story/EndingRocket_v1"), "ui/ending-illustration"));
                report.desktopFontName = UiKit.UIFont.name;
                report.androidFontResourcePath = AssetDatabase.GetAssetPath(Resources.Load<Font>("Fonts/NotoSansSC-Regular"));
                EditorSceneManager.OpenScene(SceneBuilders.HomePath, OpenSceneMode.Single);
                foreach (var renderer in UnityEngine.Object.FindObjectsOfType<SpriteRenderer>())
                {
                    if (renderer.sprite == null) continue;
                    report.sprites.Add(Read(renderer.sprite, "baked-home/" + renderer.name));
                    report.bakedSceneRenderers.Add(new SceneRow {
                        name = renderer.name, path = AssetDatabase.GetAssetPath(renderer.sprite),
                        position = renderer.transform.position, scale = renderer.transform.lossyScale,
                        bounds = renderer.bounds, sortingOrder = renderer.sortingOrder
                    });
                }
                var world = UnityEngine.Object.FindObjectOfType<CatWorld>();
                if (world == null) throw new InvalidOperationException("Home scene has no CatWorld");
                report.floorCenter = world.FloorCenter;
                report.floorHalf = world.FloorHalf;
                report.homeDependencies = AssetDatabase.GetDependencies(SceneBuilders.HomePath, true);
                File.WriteAllText(output, JsonUtility.ToJson(report, true));
                Debug.Log("F1 AUDIT: " + report.sprites.Count + " resolved sprite uses, " + report.bakedSceneRenderers.Count + " baked renderers");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        static SpriteRow Read(Sprite sprite, string use)
        {
            if (sprite == null) throw new InvalidOperationException("Missing sprite: " + use);
            string path = AssetDatabase.GetAssetPath(sprite);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long fileId);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("No texture importer for " + use);
            return new SpriteRow { use = use, path = path, guid = guid,
                localFileId = fileId.ToString(CultureInfo.InvariantCulture), name = sprite.name,
                filter = sprite.texture.filterMode.ToString(), ppu = sprite.pixelsPerUnit,
                rect = sprite.rect, pivot = sprite.pivot, bounds = sprite.bounds,
                android = ReadPlatform(importer.GetPlatformTextureSettings("Android")),
                standalone = ReadPlatform(importer.GetPlatformTextureSettings("Standalone")) };
        }

        static PlatformRow ReadPlatform(TextureImporterPlatformSettings settings) => new PlatformRow {
            overridden = settings.overridden, maximumSize = settings.maxTextureSize,
            format = settings.format.ToString(), compression = settings.textureCompression.ToString()
        };
    }
}
