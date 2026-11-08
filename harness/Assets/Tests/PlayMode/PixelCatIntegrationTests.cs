using System;
using System.Collections;
using System.IO;
using CatGame.Idle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatGame.Tests
{
    public class PixelCatIntegrationTests : CatGameTestBase
    {
        CatBreed Egypt() { return CatVisualLibrary.Load(Array.Find(CatCatalog.All, row => row.id == "Egypt")); }

        CatActor NewActor()
        {
            var go = Track(new GameObject("PixelEgypt.Test"));
            var actor = go.AddComponent<CatActor>();
            actor.Setup(Egypt(), new Vector2(6, 4));
            return actor;
        }

        [Test]
        public void Pixel_Egypt_loads_all_new_frames_without_changing_other_breeds()
        {
            var breed = Egypt();
            Assert.IsTrue(breed.UseWalkWhileMoving);
            Assert.AreEqual(8, breed.Idle.Length);
            Assert.AreEqual(8, breed.Walk.Length);
            Assert.AreEqual(12, breed.Jump.Length);
            Assert.AreEqual(8, breed.Sleep.Length);
            Assert.IsTrue(breed.UseSleepWhileResting);
            foreach (var action in new[] { breed.Idle, breed.Walk, breed.Jump, breed.Sleep })
                foreach (var frame in action)
                {
                    Assert.AreEqual(new Vector2(64,64), frame.rect.size);
                    Assert.AreEqual(new Vector2(32,32), frame.pivot);
                    Assert.AreEqual(32, frame.pixelsPerUnit);
                    Assert.AreEqual(FilterMode.Point, frame.texture.filterMode);
                }
            Assert.IsFalse(LegacyCalicoBreed().UseWalkWhileMoving);
            Assert.IsNull(LegacyCalicoBreed().Walk);
        }

        [UnityTest]
        public IEnumerator Pixel_Egypt_roams_with_Walk_stops_with_Idle_and_clicks_Jump_once()
        {
            var actor = NewActor();
            yield return null;
            Assert.AreEqual(ActionNames.Idle, actor.CurrentAction);
            float deadline = Time.time + 3;
            while (!actor.IsWalking && Time.time < deadline) yield return null;
            Assert.IsTrue(actor.IsWalking);
            Assert.AreEqual(ActionNames.Walk, actor.CurrentAction);
            Assert.Contains(actor.Renderer.sprite, Egypt().Walk);
            var before = actor.Position;
            yield return new WaitForSeconds(.15f);
            Assert.Greater((actor.Position-before).magnitude,.02f);
            int clicks=0;
            actor.Licked += ignored => clicks++;
            actor.OnLick();
            Assert.AreEqual(1,clicks);
            Assert.AreEqual(ActionNames.Jump,actor.CurrentAction);
            var jumpingPosition = actor.Position;
            yield return new WaitForSeconds(.35f);
            Assert.AreEqual(ActionNames.Jump,actor.CurrentAction);
            Assert.AreEqual(jumpingPosition,actor.Position);
            Assert.Contains(actor.Renderer.sprite,Egypt().Jump);
            yield return new WaitForSeconds(.5f);
            Assert.AreNotEqual(ActionNames.Jump,actor.CurrentAction);
            Assert.IsFalse(actor.IsForced);
            Assert.AreEqual(1,clicks);
            actor.ResumeAuto();
            yield return null;
            Assert.AreEqual(ActionNames.Idle,actor.CurrentAction);
            Assert.Contains(actor.Renderer.sprite,Egypt().Idle);
        }

        [UnityTest]
        public IEnumerator Pixel_Egypt_runtime_capture_keeps_size_and_shows_each_action()
        {
            var actor = NewActor();
            yield return null;
            Assert.AreEqual(2f,actor.Renderer.bounds.size.y,.001f,
                "New Egypt preserves the existing 2-world-unit frame height; global PPU remains deferred.");
            var cameraObject=Track(new GameObject("PixelEgypt.Camera"));
            var camera=cameraObject.AddComponent<Camera>();
            camera.orthographic=true;
            camera.orthographicSize=1.3f;
            camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.88f,.86f,.83f);
            camera.cullingMask=1<<30;
            actor.gameObject.layer=30;
            string folder=Environment.GetEnvironmentVariable("CATGAME_ART_QA_ROOT");
            if (string.IsNullOrEmpty(folder))
                folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Tests/robot/results/pixel-captures-"+
                    DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(folder);
            foreach (var action in new[]{ActionNames.Idle,ActionNames.Walk,ActionNames.Jump,ActionNames.Sleep})
            {
                actor.PlayAction(action);
                yield return new WaitForSeconds(.2f);
                camera.transform.position=actor.transform.position+new Vector3(0,0,-10);
                var texture=new RenderTexture(512,512,24);
                var previous=RenderTexture.active;
                var picture=new Texture2D(512,512,TextureFormat.RGBA32,false);
                try {
                    camera.targetTexture=texture;
                    camera.Render();
                    RenderTexture.active=texture;
                    picture.ReadPixels(new Rect(0,0,512,512),0,0);
                    picture.Apply();
                    File.WriteAllBytes(Path.Combine(folder,action+".png"),picture.EncodeToPNG());
                } finally {
                    camera.targetTexture=null;
                    RenderTexture.active=previous;
                    UnityEngine.Object.DestroyImmediate(picture);
                    texture.Release();
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
        }
    }
}

