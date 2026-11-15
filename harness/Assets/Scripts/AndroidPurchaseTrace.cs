#if DEVELOPMENT_BUILD && UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace CatGame
{
    // Opt-in instrumentation, confined to a separate package and a validated run directory.
    internal sealed class AndroidPurchaseTrace : MonoBehaviour
    {
        string _path;
        int _fps;
        Button _button;
        double _nextSample;

        [Serializable]
        sealed class Entry
        {
            public string kind, utc;
            public int frame, fps, milk, width, height;
            public double realtime, frameMs, hair;
            public bool focused, interactable;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartTrace()
        {
            if (Application.identifier != "com.catgamestudio.catgame.inputqa") return;
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var intent = activity.Call<AndroidJavaObject>("getIntent");
            string run = intent.Call<string>("getStringExtra", "catgame_input_run");
            int fps = intent.Call<int>("getIntExtra", "catgame_input_fps", 60);
            if (!Guid.TryParseExact(run, "N", out _) || (fps != 6 && fps != 60)) return;
            using var files = activity.Call<AndroidJavaObject>("getFilesDir");
            string directory = Path.Combine(files.Call<string>("getAbsolutePath"), "PurchaseTrace", run);
            Directory.CreateDirectory(directory);
            var trace = new GameObject("Android Purchase Trace").AddComponent<AndroidPurchaseTrace>();
            DontDestroyOnLoad(trace.gameObject);
            trace._path = Path.Combine(directory, "events.jsonl");
            trace._fps = fps;
            trace.Record("started");
        }

        void Update()
        {
            if (_path == null) return;
            if (_button == null && CatWorld.I?.Hud != null)
            {
                foreach (var button in CatWorld.I.Hud.GetComponentsInChildren<Button>(true))
                {
                    if (button.name != "Cat_Milk") continue;
                    _button = button;
                    // A second listener runs after the existing purchase callback.
                    button.onClick.AddListener(AfterPurchaseCallback);
                    var probe = button.gameObject.AddComponent<AndroidPurchasePointerProbe>();
                    probe.Trace = this;
                    Application.targetFrameRate = _fps;
                    Record("ready");
                    break;
                }
            }
            if (_button != null && Time.realtimeSinceStartupAsDouble >= _nextSample)
            {
                _nextSample = Time.realtimeSinceStartupAsDouble + .5;
                Record("frame");
            }
        }

        void AfterPurchaseCallback() => Record("after-button-callback");
        void OnApplicationPause(bool paused) => Record(paused ? "paused" : "resumed");
        void OnApplicationFocus(bool focused) => Record(focused ? "focus" : "blur");

        internal void Record(string kind)
        {
            if (_path == null) return;
            var state = CatWorld.I?.Economy?.State;
            int milk = -1;
            if (state != null)
                foreach (var cat in state.cats)
                    if (cat.id == "Milk") { milk = cat.count; break; }
            var entry = new Entry {
                kind = kind, utc = DateTime.UtcNow.ToString("O"),
                frame = Time.frameCount, realtime = Time.realtimeSinceStartupAsDouble,
                frameMs = Time.unscaledDeltaTime * 1000, fps = Application.targetFrameRate,
                milk = milk, hair = state?.catHair ?? -1,
                focused = Application.isFocused, interactable = _button != null && _button.IsInteractable(),
                width = Screen.width, height = Screen.height
            };
            File.AppendAllText(_path, JsonUtility.ToJson(entry) + "\n");
        }
    }
}
#endif
