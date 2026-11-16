using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CatGame.Idle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatGame.Tests
{
    public sealed class F1ArtBaselineCaptureTests : CatGameTestBase
    {
        [Serializable] sealed class Pose
        {
            public string id, action, sprite, capture;
            public float ppu, worldWidth, worldHeight;
            public Vector2 pivot;
            public Vector3 objectPosition, rendererCenter;
            public int renderedColors, foregroundPixels;
        }
        [Serializable] sealed class CaptureReport
        {
            public string scope = "Isolated Unity renderer poses, not native gameplay or performance";
            public List<Pose> poses = new List<Pose>();
        }

        [UnityTest]
        public IEnumerator All_eleven_breeds_render_four_complete_visible_poses()
        {
            string folder = Environment.GetEnvironmentVariable("CATGAME_F1_POSE_ROOT");
            if (string.IsNullOrEmpty(folder))
                folder = Path.GetFullPath("Tests/robot/results/f1-poses-" + Guid.NewGuid().ToString("N"));
            if (Directory.Exists(folder) && Directory.GetFiles(folder, "*.png").Length > 0)
                throw new InvalidOperationException("Refusing to overwrite existing pose evidence");
            Directory.CreateDirectory(folder);
            var report = new CaptureReport();
            var camera = Track(new GameObject("F1 Pose Camera")).AddComponent<Camera>();
            camera.orthographic = true;
            camera.aspect = 1;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.88f, .86f, .83f);
            camera.cullingMask = 1 << 30;
            foreach (var definition in CatCatalog.All)
            {
                var go = Track(new GameObject("F1 Pose " + definition.id));
                go.layer = 30;
                var actor = go.AddComponent<CatActor>();
                actor.Setup(CatVisualLibrary.Load(definition), new Vector2(6, 4));
                foreach (var action in new[] { ActionNames.Idle, ActionNames.Walk, ActionNames.Jump, ActionNames.Sleep })
                {
                    Assert.IsTrue(actor.PlayAction(action));
                    yield return new WaitForSeconds(.2f);
                    var bounds = actor.Renderer.bounds;
                    camera.transform.position = bounds.center + new Vector3(0, 0, -10);
                    camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.3f;
                    var target = new RenderTexture(512, 512, 24);
                    var image = new Texture2D(512, 512, TextureFormat.RGBA32, false);
                    var previous = RenderTexture.active;
                    try
                    {
                        camera.targetTexture = target;
                        camera.Render();
                        var minimum = camera.WorldToViewportPoint(bounds.min);
                        var maximum = camera.WorldToViewportPoint(bounds.max);
                        Assert.Greater(minimum.x, .02f); Assert.Greater(minimum.y, .02f);
                        Assert.Less(maximum.x, .98f); Assert.Less(maximum.y, .98f);
                        RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                        image.Apply();
                        var pixels = image.GetPixels32();
                        var colors = new HashSet<Color32>(pixels);
                        Color32 background = pixels[0];
                        int foreground = 0;
                        foreach (var pixel in pixels)
                            if (pixel.r != background.r || pixel.g != background.g || pixel.b != background.b) foreground++;
                        // A palette cap is not a minimum: legitimate sleep art uses fewer colors.
                        Assert.Greater(colors.Count, 1, "Uniform image instead of cat: " + definition.id + action);
                        Assert.Greater(foreground, 0, "Missing cat foreground: " + definition.id + action);
                        string name = definition.id.ToLowerInvariant() + "-" + action.ToLowerInvariant() + ".png";
                        File.WriteAllBytes(Path.Combine(folder, name), image.EncodeToPNG());
                        report.poses.Add(new Pose { id = definition.id, action = action,
                            sprite = actor.Renderer.sprite.name, capture = name,
                            ppu = actor.Renderer.sprite.pixelsPerUnit, pivot = actor.Renderer.sprite.pivot,
                            worldWidth = bounds.size.x, worldHeight = bounds.size.y,
                            objectPosition = actor.transform.position, rendererCenter = bounds.center,
                            renderedColors = colors.Count, foregroundPixels = foreground });
                        File.WriteAllText(Path.Combine(folder, "pose-report.json"), JsonUtility.ToJson(report, true));
                    }
                    finally
                    {
                        camera.targetTexture = null;
                        RenderTexture.active = previous;
                        UnityEngine.Object.DestroyImmediate(image);
                        target.Release();
                        UnityEngine.Object.DestroyImmediate(target);
                    }
                }
                go.SetActive(false);
            }
            Assert.AreEqual(44, report.poses.Count);
        }
    }
}
