using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.U2D;

namespace CatGame.EditTests
{
    public class RoomViewportTests
    {
        const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [TestCase(1600, 900)]
        [TestCase(2000, 900)]
        [TestCase(1200, 900)]
        [TestCase(1920, 1080)]
        [TestCase(2712, 1220)]
        [TestCase(2560, 1440)]
        [TestCase(3840, 2160)]
        public void Room_covers_the_actual_pixel_perfect_projection(int width, int height)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            var pixelPerfect = cameraObject.AddComponent<PixelPerfectCamera>();
            pixelPerfect.assetsPPU = 100;
            pixelPerfect.refResolutionX = 1920;
            pixelPerfect.refResolutionY = 1080;
            typeof(PixelPerfectCamera).GetMethod("Awake", InstanceMembers).Invoke(pixelPerfect, null);
            var roomObject = new GameObject("Room");
            var room = roomObject.AddComponent<SpriteRenderer>();
            room.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/Room/Pixel/architecture.png");
            Assert.IsNotNull(room.sprite);
            var worldObject = new GameObject("World");
            worldObject.SetActive(false);
            var world = worldObject.AddComponent<CatWorld>();
            try
            {
                typeof(CatWorld).GetMethod("FitWindow", InstanceMembers).Invoke(world, new object[] { width, height });
                float halfHeight = camera.orthographicSize;
                Assert.IsFalse(pixelPerfect.enabled, "legacy and world-grid camera controllers cannot both run");
                Assert.That(halfHeight, Is.EqualTo(Mathf.Max(5.625f, 10f * height / width)).Within(.0001f));
                Assert.AreEqual(new Rect(0, 0, 1, 1), camera.rect,
                    "full native input viewport; EditMode does not resize the actual screen to each synthetic size");
                var body = new Vector3(2, -1, 0);
                var screen = camera.WorldToScreenPoint(body);
                Assert.That(Vector3.Distance(body, camera.ScreenToWorldPoint(screen)), Is.LessThan(.0001f));
                Assert.That(room.bounds.size.y, Is.GreaterThanOrEqualTo(2 * halfHeight - .001f));
                Assert.That(room.bounds.size.x, Is.GreaterThanOrEqualTo(2 * halfHeight * width / height - .001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(worldObject);
                UnityEngine.Object.DestroyImmediate(roomObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
