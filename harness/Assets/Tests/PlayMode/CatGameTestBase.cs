using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CatGame.Idle;
using CatGameType = global::CatGame.CatGame;
using CatWorldType = global::CatGame.CatWorld;
using CatActorType = global::CatGame.CatActor;

namespace CatGame.Tests
{
    /// <summary>
    /// 测试基类：每个用例跑在一个干净的实例上，结束后清掉所有临时对象。
    /// 现在的结构是"世界场景 + 猫实体 + HUD"，所以基类提供 NewWorld()。
    /// </summary>
    public abstract class CatGameTestBase
    {
        readonly List<GameObject> _spawned = new List<GameObject>();

        [UnitySetUp]
        public IEnumerator BaseSetUp()
        {
            Time.timeScale = 1f;
            // 清掉上一个用例残留的单例（OnDestroy 里会把 CatGame.I / CatWorld.I 置空）
            foreach (var w in Object.FindObjectsOfType<CatWorldType>())
                if (w != null) Object.DestroyImmediate(w.gameObject);
            foreach (var g in Object.FindObjectsOfType<CatGameType>())
                if (g != null && g != null) Object.DestroyImmediate(g.gameObject);
            foreach (var save in Object.FindObjectsOfType<SaveSystem>())
                if (save != null) Object.DestroyImmediate(save.gameObject);

            CatActorType.LastPosition = Vector3.zero;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator BaseTearDown()
        {
            Time.timeScale = 1f;
            foreach (var go in _spawned)
                if (go != null) Object.Destroy(go);
            _spawned.Clear();

            yield return null;
        }

        /// <summary>创建一个干净的游戏核心（Awake 立即执行，品种表已加载）。</summary>
        protected CatGameType NewGame()
        {
            var go = new GameObject("CatGame.Test");
            _spawned.Add(go);
            return go.AddComponent<CatGameType>();
        }

        // Original 32px art remains available for compatibility checks without restoring old Walk.
        protected static CatBreed LegacyCalicoBreed()
        {
            return new CatBreed { Id="LegacyCalico",Name="Legacy Calico",
                Idle=Resources.LoadAll<Sprite>("Art/Cats/Calico/Idle"),
                Jump=Resources.LoadAll<Sprite>("Art/Cats/Calico/Jump") };
        }

        protected IEnumerator CaptureActorActions(CatActor actor,string id,string folder,Camera camera)
        {
            foreach (var action in new[]{ActionNames.Idle,ActionNames.Walk,ActionNames.Jump,ActionNames.Sleep})
            {
                actor.PlayAction(action);
                yield return new WaitForSeconds(.2f);
                Assert.AreEqual(2f,actor.Renderer.bounds.size.y,.001f);
                camera.transform.position=actor.transform.position+new Vector3(0,0,-10);
                var target=new RenderTexture(512,512,24); var previous=RenderTexture.active;
                var image=new Texture2D(512,512,TextureFormat.RGBA32,false);
                try {
                    camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
                    image.ReadPixels(new Rect(0,0,512,512),0,0); image.Apply();
                    File.WriteAllBytes(Path.Combine(folder,id.ToLowerInvariant()+"-"+action+".png"),image.EncodeToPNG());
                } finally {
                    camera.targetTexture=null; RenderTexture.active=previous; Object.DestroyImmediate(image);
                    target.Release(); Object.DestroyImmediate(target);
                }
            }
        }

        protected GameObject Track(GameObject go)
        {
            _spawned.Add(go);
            return go;
        }

        /// <summary>
        /// 建一个世界（等价于 Home / Park 场景）：相机 + 猫 + HUD。
        /// CatWorld 在 Start 里生成猫与 HUD，所以调用后要 yield 一帧。
        /// </summary>
        protected CatWorldType NewWorld(out CatGameType game, bool withHud = true)
        {
            var go = Track(new GameObject("World.Test"));
            var world = go.AddComponent<CatWorldType>();
            world.Title = "测试";
            world.FloorCenter = Vector2.zero;
            world.FloorHalf = new Vector2(6f, 2f);
            world.SpawnCat = true;
            world.EnableStoryIntro = false; // Unit fixtures opt into onboarding explicitly; production defaults on.
            world.EnableStoryEvents = false; // CG integration tests opt in without interrupting economy fixtures.
            game = go.GetComponent<CatGameType>();
            return world;
        }
    }
}
