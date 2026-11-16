#if DEVELOPMENT_BUILD && UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using CatGame.Idle;
using UnityEngine;
using UnityEngine.U2D;

namespace CatGame
{
    public sealed partial class PlayerVisualQA
    {
        [Serializable] sealed class ArtReport
        {
            public string schema = "f1-native-art-metrics-v1", sourceCommit, unity;
            public bool complete, requestedSizesHonored = true, simulationFrozenForSnapshots = true;
            public int pid;
            public List<ArtScreen> screens = new List<ArtScreen>();
            public List<ArtActor> poses = new List<ArtActor>();
        }
        [Serializable] sealed class ArtScreen
        {
            public int requestedWidth, requestedHeight, width, height, frameStart, frameEnd;
            public float orthographicSize;
            public Rect cameraPixelRect;
            public bool pixelPerfectEnabled;
            public int cameraAssetsPpu;
            public Vector2Int logicalRaster, actualRaster;
            public bool integerUpscale;
            public int worldRenderCount;
            public string screenshot;
            public bool nativeResizeAttempted, nativeResizeSucceeded;
            public int clientWidth, clientHeight, nativeResizeError;
            public List<ArtActor> cats = new List<ArtActor>();
            public List<ArtObject> sceneSprites = new List<ArtObject>();
        }
        [Serializable] sealed class ArtActor
        {
            public string id, action, frame;
            public Rect spriteRect, screenBounds;
            public float ppu, groundOffset;
            public Vector2 pivot;
            public Vector3 logicalPosition, objectPosition, visualPosition, visualScale, footFromSprite;
            public Bounds rendererBounds, colliderBounds;
        }
        [Serializable] sealed class ArtObject
        {
            public string hierarchy, sprite;
            public float ppu;
            public Vector2 pivot;
            public Vector3 position, scale;
            public Bounds bounds;
            public int sortingOrder;
        }
        [StructLayout(LayoutKind.Sequential)] struct F1WindowRect { public int left, top, right, bottom; }
        [DllImport("user32.dll", EntryPoint="GetClientRect", SetLastError=true)]
        static extern bool F1GetClientRect(IntPtr window, out F1WindowRect rect);
        [DllImport("user32.dll", EntryPoint="GetWindowRect", SetLastError=true)]
        static extern bool F1GetWindowRect(IntPtr window, out F1WindowRect rect);
        [DllImport("user32.dll", EntryPoint="SetWindowPos", SetLastError=true)]
        static extern bool F1SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

        IEnumerator F1ArtFlow()
        {
            var report = new ArtReport { sourceCommit = Environment.GetEnvironmentVariable("CATGAME_F1_SOURCE"),
                unity = Application.unityVersion, pid = System.Diagnostics.Process.GetCurrentProcess().Id };
            void Store() => File.WriteAllText(Path.Combine(folder, "art-metrics.json"), JsonUtility.ToJson(report, true));
            Application.logMessageReceived += (message, stack, type) => {
                if (type != LogType.Error && type != LogType.Exception) return;
                File.AppendAllText(Path.Combine(folder, "art-errors.txt"), message + "\n" + stack + "\n");
                Application.Quit(1);
            };
            Store();
            SaveSystem.Instance.BeginNewGame();
            var state = SaveSystem.Instance.State;
            state.introCompleted = true;
            foreach (var row in state.cats) row.count = 1;
            foreach (var row in state.items) row.level = 1;
            SaveSystem.Instance.SaveNow();
            UnityEngine.Random.InitState(20261115);
            SceneFlow.GoHome();
            yield return new WaitForSecondsRealtime(.5f);
            var world = CatWorld.I;
            if (world?.Hud == null || world.Roster.VisibleCount != CatCatalog.All.Length)
                throw new InvalidOperationException("F1 art fixture did not initialize all eleven cats");
            // Freeze simulation for comparable art poses, not a gameplay/performance verdict.
            Time.timeScale = 0;
            foreach (var actor in world.Roster.GetComponentsInChildren<CatActor>()) actor.PlayAction(ActionNames.Idle);
            foreach (var size in new[] { new Vector2Int(1920,1080), new Vector2Int(1600,900), new Vector2Int(2560,1440) })
            {
                Screen.SetResolution(size.x, size.y, false);
                yield return new WaitForSecondsRealtime(1);
                bool attempted = false, resized = false;
                int resizeError = 0;
                using var process = System.Diagnostics.Process.GetCurrentProcess();
                process.Refresh();
                IntPtr handle = process.MainWindowHandle;
                if ((Screen.width != size.x || Screen.height != size.y) && handle != IntPtr.Zero)
                {
                    attempted = true;
                    if (F1GetClientRect(handle, out var client) && F1GetWindowRect(handle, out var outer))
                    {
                        int borderWidth = outer.right - outer.left - (client.right - client.left);
                        int borderHeight = outer.bottom - outer.top - (client.bottom - client.top);
                        // Only this process's own QA window; no desktop display-mode change.
                        resized = F1SetWindowPos(handle, IntPtr.Zero, 0, 0, size.x + borderWidth,
                            size.y + borderHeight, 0x0002 | 0x0004 | 0x0010);
                        if (!resized) resizeError = Marshal.GetLastWin32Error();
                        yield return new WaitForSecondsRealtime(.5f);
                    }
                }
                for (int i = 0; i < 10; i++) yield return null;
                var camera = Camera.main;
                var pp = camera.GetComponent<PixelPerfectCamera>();
                var worldPixels = camera.GetComponent<WorldPixelCamera>();
                var sample = new ArtScreen { requestedWidth = size.x, requestedHeight = size.y,
                    width = Screen.width, height = Screen.height, frameStart = Time.frameCount,
                    orthographicSize = camera.orthographicSize, cameraPixelRect = camera.pixelRect,
                    pixelPerfectEnabled = pp != null && pp.enabled,
                    cameraAssetsPpu = WorldArtSettings.Current.WorldPixelsPerUnit,
                    logicalRaster = worldPixels != null ? worldPixels.LogicalRenderSize : Vector2Int.zero,
                    actualRaster = worldPixels != null ? worldPixels.LastRenderedTextureSize : Vector2Int.zero,
                    integerUpscale = worldPixels != null && worldPixels.IntegerUpscale,
                    worldRenderCount = worldPixels != null ? worldPixels.RenderCount : 0,
                    nativeResizeAttempted = attempted, nativeResizeSucceeded = resized, nativeResizeError = resizeError,
                    screenshot = "home-" + size.x + "x" + size.y + ".png" };
                if (handle != IntPtr.Zero && F1GetClientRect(handle, out var measured))
                {
                    sample.clientWidth = measured.right - measured.left;
                    sample.clientHeight = measured.bottom - measured.top;
                }
                report.requestedSizesHonored &= Screen.width == size.x && Screen.height == size.y;
                foreach (var actor in world.Roster.GetComponentsInChildren<CatActor>()) sample.cats.Add(ArtActorRow(actor, camera));
                foreach (var sprite in FindObjectsOfType<SpriteRenderer>())
                    if (sprite.sprite != null && sprite.GetComponentInParent<CatActor>() == null)
                        sample.sceneSprites.Add(new ArtObject { hierarchy = ArtHierarchy(sprite.transform),
                            sprite = sprite.sprite.name, ppu = sprite.sprite.pixelsPerUnit, pivot = sprite.sprite.pivot,
                            position = sprite.transform.position, scale = sprite.transform.lossyScale,
                            bounds = sprite.bounds, sortingOrder = sprite.sortingOrder });
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, sample.screenshot));
                yield return new WaitForEndOfFrame();
                yield return null;
                sample.frameEnd = Time.frameCount;
                report.screens.Add(sample);
                Store();
            }
            foreach (var actor in world.Roster.GetComponentsInChildren<CatActor>())
            {
                foreach (var action in new[] { ActionNames.Idle, ActionNames.Walk, ActionNames.Jump, ActionNames.Sleep })
                {
                    if (!actor.PlayAction(action)) throw new InvalidOperationException("F1 missing pose " + actor.Breed.Id + action);
                    yield return null;
                    report.poses.Add(ArtActorRow(actor, Camera.main));
                }
            }
            Time.timeScale = 1;
            report.complete = true;
            Store();
            Application.Quit(report.requestedSizesHonored ? 0 : 1);
        }

        static ArtActor ArtActorRow(CatActor actor, Camera camera)
        {
            var renderer = actor.Renderer;
            var sprite = renderer.sprite;
            var bottomLeft = camera.WorldToScreenPoint(renderer.bounds.min);
            var topRight = camera.WorldToScreenPoint(renderer.bounds.max);
            var collider = actor.GetComponent<BoxCollider2D>();
            return new ArtActor { id = actor.Breed.Id, action = actor.CurrentAction, frame = sprite.name,
                spriteRect = sprite.rect, ppu = sprite.pixelsPerUnit, pivot = sprite.pivot,
                groundOffset = actor.GroundOffset, logicalPosition = actor.Position,
                objectPosition = actor.transform.position, visualPosition = renderer.transform.position,
                visualScale = renderer.transform.lossyScale, rendererBounds = renderer.bounds,
                colliderBounds = collider != null ? collider.bounds : default,
                screenBounds = Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y),
                footFromSprite = renderer.transform.TransformPoint(new Vector3(sprite.bounds.center.x,
                    sprite.bounds.min.y + sprite.bounds.size.y * WorldArtSettings.Current.CatFootFraction, 0)) };
        }

        static string ArtHierarchy(Transform node)
        {
            string result = node.name;
            while (node.parent != null) { node = node.parent; result = node.name + "/" + result; }
            return result;
        }
    }
}
#endif
