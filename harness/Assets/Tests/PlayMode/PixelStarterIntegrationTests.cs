using System;
using System.Collections;
using System.IO;
using CatGame.Idle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatGame.Tests
{
    public class PixelStarterIntegrationTests : CatGameTestBase
    {
        [Test]
        public void All_three_starters_share_preview_economy_and_new_frame_contracts()
        {
            var game=NewGame();
            foreach (var id in new[]{"Milk","Tabby","Snow"})
            {
                var breed=CatVisualLibrary.Load(Array.Find(CatCatalog.All,row=>row.id==id));
                Assert.IsTrue(game.SetActiveBreed(id));
                Assert.AreSame(breed,game.Current);
                Assert.IsTrue(breed.UseWalkWhileMoving);
                Assert.IsTrue(breed.UseSleepWhileResting);
                foreach (var action in new[]{ActionNames.Idle,ActionNames.Walk,ActionNames.Jump,ActionNames.Sleep})
                {
                    var frames=breed.Clip(action);
                    Assert.AreEqual(action==ActionNames.Jump?12:8,frames.Length,id+action);
                    foreach (var frame in frames)
                    {
                        Assert.AreEqual(new Vector2(64,64),frame.rect.size);
                        Assert.AreEqual(new Vector2(32,32),frame.pivot);
                        Assert.AreEqual(32,frame.pixelsPerUnit);
                    }
                }
            }
            var calico=LegacyCalicoBreed();
            Assert.IsNull(calico.Walk);
            Assert.IsFalse(calico.UseSleepWhileResting);
        }

        [UnityTest]
        public IEnumerator Tabby_Snow_purchases_click_once_and_resume_new_Walk()
        {
            global::CatGame.CatGame game;
            var world=NewWorld(out game);
            yield return null;
            var state=world.Economy.State;
            state.catHair=100000;
            foreach (var id in new[]{"Tabby","Snow"})
            {
                double before=state.catHair,price=IdleMath.CatPrice(id,0);
                Assert.IsTrue(world.Economy.BuyCat(id));
                Assert.AreEqual(before-price,state.catHair,1e-7);
                var actor=world.Roster.FindActor(id);
                Assert.AreEqual(ActionNames.Jump,actor.CurrentAction);
                Assert.AreSame(CatVisualLibrary.Load(Array.Find(CatCatalog.All,row=>row.id==id)),actor.Breed);
                int clicks=state.clicks;
                before=state.catHair;
                actor.OnLick();
                Assert.AreEqual(clicks+1,state.clicks);
                Assert.AreEqual(before+IdleMath.ClickValue(state),state.catHair,1e-7);
                yield return new WaitForSeconds(.9f);
                Assert.IsFalse(actor.IsForced);
                Assert.AreEqual(ActionNames.Walk,actor.CurrentAction);
                Assert.Contains(actor.Renderer.sprite,actor.Breed.Walk);
            }
        }

        [UnityTest]
        public IEnumerator Tabby_Snow_four_actions_render_with_their_color_roles()
        {
            string folder=Environment.GetEnvironmentVariable("CATGAME_ART_QA_ROOT");
            if (string.IsNullOrEmpty(folder)) folder=Path.GetFullPath("Tests/robot/results/starter-captures-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var cameraObject=Track(new GameObject("Starter.Camera"));
            var camera=cameraObject.AddComponent<Camera>();
            camera.orthographic=true; camera.orthographicSize=1.3f;
            camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.88f,.86f,.83f); camera.cullingMask=1<<30;
            foreach (var id in new[]{"Tabby","Snow"})
            {
                var go=Track(new GameObject("Capture_"+id)); go.layer=30;
                var actor=go.AddComponent<CatActor>();
                actor.Setup(CatVisualLibrary.Load(Array.Find(CatCatalog.All,row=>row.id==id)),new Vector2(6,4));
                foreach (var action in new[]{ActionNames.Idle,ActionNames.Walk,ActionNames.Jump,ActionNames.Sleep})
                {
                    actor.PlayAction(action);
                    yield return new WaitForSeconds(.2f);
                    Assert.AreEqual(2f,actor.Renderer.bounds.size.y,.001f);
                    camera.transform.position=actor.transform.position+new Vector3(0,0,-10);
                    var target=new RenderTexture(512,512,24);
                    var previous=RenderTexture.active;
                    var image=new Texture2D(512,512,TextureFormat.RGBA32,false);
                    try {
                        camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
                        image.ReadPixels(new Rect(0,0,512,512),0,0); image.Apply();
                        File.WriteAllBytes(Path.Combine(folder,id.ToLowerInvariant()+"-"+action+".png"),image.EncodeToPNG());
                    } finally {
                        camera.targetTexture=null; RenderTexture.active=previous;
                        UnityEngine.Object.DestroyImmediate(image); target.Release(); UnityEngine.Object.DestroyImmediate(target);
                    }
                }
                go.SetActive(false);
            }
        }
    }
}
