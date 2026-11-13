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
                if (pixelPerfect.enabled)
                {
                    // Use the installed package's projection, independently of the game's cover calculation.
                    var type = typeof(PixelPerfectCamera).Assembly.GetType("UnityEngine.U2D.PixelPerfectCameraInternal");
                    var projection = Activator.CreateInstance(type, InstanceMembers, null, new object[] { pixelPerfect }, null);
                    type.GetMethod("CalculateCameraProperties", InstanceMembers).Invoke(projection, new object[] { width, height });
                    halfHeight = (float)type.GetField("orthoSize", InstanceMembers).GetValue(projection);
                }
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
