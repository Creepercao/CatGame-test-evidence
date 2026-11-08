using System;
using System.Collections;
using System.IO;
using CatGame.Idle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatGame.Tests
{
    public class PixelFinalIntegrationTests : CatGameTestBase
    {
        [Test]
        public void All_eleven_breeds_use_new_four_clips_and_old_Calico_Walk_stays_deleted()
        {
            Assert.AreEqual(11,CatCatalog.All.Length);
            foreach (var definition in CatCatalog.All)
            {
                Assert.AreEqual(64,definition.pixels,definition.id); Assert.AreEqual(1d,definition.scale);
                Assert.IsTrue(CatVisualLibrary.UsesPixel64Art(definition.id));
                var breed=CatVisualLibrary.Load(definition);
                Assert.IsTrue(breed.UseWalkWhileMoving); Assert.IsTrue(breed.UseSleepWhileResting);
                foreach (var action in new[]{ActionNames.Idle,ActionNames.Walk,ActionNames.Jump,ActionNames.Sleep})
                {
                    var frames=breed.Clip(action); Assert.AreEqual(action==ActionNames.Jump?12:8,frames.Length,definition.id+action);
                    foreach (var frame in frames)
                    {
                        Assert.AreEqual(new Vector2(64,64),frame.rect.size); Assert.AreEqual(new Vector2(32,32),frame.pivot);
                        Assert.AreEqual(32,frame.pixelsPerUnit); Assert.AreEqual(FilterMode.Point,frame.texture.filterMode);
                    }
                }
            }
            Assert.IsEmpty(Resources.LoadAll<Sprite>("Art/Cats/Calico/Walk"));
            Assert.IsFalse(File.Exists(Path.Combine(Application.dataPath,"Resources/Art/Cats/Calico/Walk.png")));
            Assert.AreEqual(8,CatVisualLibrary.Load(CatCatalog.All[3]).Walk.Length);
        }

        [UnityTest]
        public IEnumerator Final_four_purchases_jump_click_once_and_use_new_Walk()
        {
            global::CatGame.CatGame game; var world=NewWorld(out game);
            yield return null;
            var state=world.Economy.State; state.catHair=1e10;
            foreach (var id in new[]{"Calico","Batcat","Demon","Xmas"})
            {
                double before=state.catHair,price=IdleMath.CatPrice(id,0);
                Assert.IsTrue(world.Economy.BuyCat(id)); Assert.AreEqual(before-price,state.catHair,1e-5);
                var actor=world.Roster.FindActor(id); Assert.AreEqual(ActionNames.Jump,actor.CurrentAction);
                Assert.IsTrue(actor.Breed.UseSleepWhileResting);
                int clicks=state.clicks; before=state.catHair; actor.OnLick();
                Assert.AreEqual(clicks+1,state.clicks); Assert.AreEqual(before+IdleMath.ClickValue(state),state.catHair,1e-5);
                yield return new WaitForSeconds(.9f);
                Assert.IsFalse(actor.IsForced); Assert.AreEqual(ActionNames.Walk,actor.CurrentAction);
                Assert.Contains(actor.Renderer.sprite,actor.Breed.Walk);
            }
        }

        [UnityTest]
        public IEnumerator Final_four_actions_render_with_patches_cowls_and_Santa_hat()
        {
            string folder=Environment.GetEnvironmentVariable("CATGAME_ART_QA_ROOT");
            if (string.IsNullOrEmpty(folder)) folder=Path.GetFullPath("Tests/robot/results/final-captures-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var cameraObject=Track(new GameObject("Final.Camera")); var camera=cameraObject.AddComponent<Camera>();
            camera.orthographic=true; camera.orthographicSize=1.3f; camera.cullingMask=1<<30;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.88f,.86f,.83f);
            foreach (var id in new[]{"Calico","Batcat","Demon","Xmas"})
            {
                var go=Track(new GameObject("Capture_"+id)); go.layer=30;
                var actor=go.AddComponent<CatActor>(); actor.Setup(CatVisualLibrary.Load(Array.Find(CatCatalog.All,row=>row.id==id)),new Vector2(6,4));
                yield return CaptureActorActions(actor,id,folder,camera); go.SetActive(false);
            }
        }
    }
}
