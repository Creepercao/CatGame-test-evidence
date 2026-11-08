#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using CatGame.Debugging;
using CatGame.Idle;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatGame.Tests
{
    public sealed class EditorDebuggerTests : CatGameTestBase
    {
        string Evidence(string test)
        {
            string root = Environment.GetEnvironmentVariable("CATGAME_EDITOR_QA_ROOT");
            if (string.IsNullOrEmpty(root)) root = Application.temporaryCachePath;
            return Path.Combine(root, "debugger-evidence", test + "-" + Guid.NewGuid().ToString("N"));
        }

        [UnityTest]
        public IEnumerator Debugger_menu_opens_window_and_commands_update_the_live_save_owner()
        {
            string previous = Environment.GetEnvironmentVariable("CATGAME_EDITOR_QA_ROOT");
            string output = Evidence("live-save");
            SaveSystem save = null; EditorWindow window = null;
            try
            {
                Environment.SetEnvironmentVariable("CATGAME_EDITOR_QA_ROOT", output);
                Debug.Log("DEBUGGER STEP 1: create isolated production save owner and open the actual Editor menu entry.");
                save = Track(new GameObject("Debugger.SaveOwner")).AddComponent<SaveSystem>();
                save.BeginNewGame();
                global::CatGame.CatGame game; var world = NewWorld(out game); yield return null;
                Assert.AreSame(save.State, world.Economy.State);
                Assert.IsTrue(EditorApplication.ExecuteMenuItem("CatGame/调试工具/游戏修改器"));
                foreach (var candidate in Resources.FindObjectsOfTypeAll<EditorWindow>())
                    if (candidate.GetType().FullName == "CatGame.EditorTools.CatGameDebuggerWindow") window = candidate;
                Assert.IsNotNull(window); Assert.AreEqual("猫窝调试器", window.titleContent.text);
                yield return null;
                Debug.Log("DEBUGGER STEP 2: change live progress, use production save, reload and verify amounts/levels/backup.");
                EditorDebugCommands.Apply(world, DebugCommand.SetHair, null, 12345, false, output);
                EditorDebugCommands.Apply(world, DebugCommand.SetItem, ItemCatalog.All[0].id, 3, false, output);
                save.SaveNow();
                var restored = SaveSystem.LoadOrCreate(save.SavePath);
                Assert.AreEqual(12345, restored.catHair); Assert.AreEqual(3, restored.items[0].level);
                Assert.AreEqual(2, Directory.GetFiles(output, "operation.json", SearchOption.AllDirectories).Length);
                Debug.Log("DEBUGGER EVIDENCE " + output);
            }
            finally
            {
                if (window != null) window.Close();
                if (save != null) UnityEngine.Object.DestroyImmediate(save.gameObject);
                Environment.SetEnvironmentVariable("CATGAME_EDITOR_QA_ROOT", previous);
            }
        }

        [UnityTest]
        public IEnumerator Debugger_currency_keeps_lifetime_separate_rejects_bad_values_and_backs_up_each_change()
        {
            global::CatGame.CatGame game;
            var world = NewWorld(out game); yield return null;
            string output = Evidence("currency");
            var state = world.Economy.State;
            double original = state.catHair, lifetime = state.totalEarned;
            Debug.Log("DEBUGGER STEP 1: set balance 1e12 without increasing lifetime; inspect recovery file.");
            string first = EditorDebugCommands.Apply(world, DebugCommand.SetHair, null, 1e12, false, output);
            Assert.AreEqual(1e12, state.catHair); Assert.AreEqual(lifetime, state.totalEarned);
            var before = SaveSystem.LoadOrCreate(Path.Combine(first, "before.json"));
            Assert.AreEqual(original, before.catHair, .001);
            StringAssert.Contains("\"status\": \"applied\"", File.ReadAllText(Path.Combine(first, "operation.json")));
            Debug.Log("DEBUGGER STEP 2: grant 25 with lifetime enabled, lower balance without subtracting lifetime.");
            EditorDebugCommands.Apply(world, DebugCommand.AddHair, null, 25, true, output);
            Assert.AreEqual(1e12 + 25, state.catHair); Assert.AreEqual(lifetime + 25, state.totalEarned);
            EditorDebugCommands.Apply(world, DebugCommand.SetHair, null, 10, true, output);
            Assert.AreEqual(10, state.catHair); Assert.AreEqual(lifetime + 25, state.totalEarned);
            Debug.Log("DEBUGGER STEP 3: invalid and overflow inputs leave state and backup count unchanged.");
            string stable = JsonUtility.ToJson(state);
            foreach (double bad in new[] { -1, double.NaN, double.PositiveInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => EditorDebugCommands.Apply(world, DebugCommand.SetHair, null, bad, false, output));
            Assert.AreEqual(stable, JsonUtility.ToJson(state)); Assert.AreEqual(3, Directory.GetDirectories(output).Length);
            EditorDebugCommands.Apply(world, DebugCommand.SetHair, null, double.MaxValue, false, output);
            Assert.Throws<ArgumentOutOfRangeException>(() => EditorDebugCommands.Apply(world, DebugCommand.AddHair, null, double.MaxValue, false, output));
            Assert.AreEqual(double.MaxValue, state.catHair); Assert.AreEqual(4, Directory.GetDirectories(output).Length);
            Debug.Log("DEBUGGER EVIDENCE " + output);
        }

        [UnityTest]
        public IEnumerator Debugger_cat_and_item_edits_refresh_roster_income_and_enforce_caps_without_rewriting_ending()
        {
            global::CatGame.CatGame game;
            var world = NewWorld(out game); yield return null;
            string output = Evidence("roster-items"); var state = world.Economy.State;
            state.catHair = 1000000002867.6625; state.totalEarned = 1000000002867.6625;
            Assert.IsTrue(world.Economy.BuyRocket());
            string snapshot = JsonUtility.ToJson(state.firstEnding);
            double balance = state.catHair, lifetime = state.totalEarned;
            int changes = 0; world.Economy.StateChanged += () => changes++;
            Debug.Log("DEBUGGER STEP 1: add Egypt, assert visible actor and income recalculation without currency drift.");
            EditorDebugCommands.Apply(world, DebugCommand.SetCat, "Egypt", 3, false, output);
            Assert.AreEqual(3, world.Roster.OwnedCountFor("Egypt"));
            Assert.IsNotNull(world.Roster.FindActor("Egypt")); Assert.AreEqual(4, world.Roster.TotalOwned);
            double rate = world.Economy.ProductionPerSecond;
            EditorDebugCommands.Apply(world, DebugCommand.SetItem, ItemCatalog.All[0].id, 5, false, output);
            Assert.Greater(world.Economy.ProductionPerSecond, rate); Assert.AreEqual(2, changes);
            Assert.AreEqual(balance, state.catHair); Assert.AreEqual(lifetime, state.totalEarned);
            Debug.Log("DEBUGGER STEP 2: caps, invalid IDs and fractional quantities reject without mutation.");
            string stable = JsonUtility.ToJson(state);
            Assert.Throws<ArgumentOutOfRangeException>(() => EditorDebugCommands.Apply(world, DebugCommand.SetCat, "Milk", 41, false, output));
            Assert.Throws<ArgumentOutOfRangeException>(() => EditorDebugCommands.Apply(world, DebugCommand.SetCat, "Milk", .5, false, output));
            Assert.Throws<ArgumentException>(() => EditorDebugCommands.Apply(world, DebugCommand.SetCat, "unknown", 1, false, output));
            Assert.Throws<ArgumentException>(() => EditorDebugCommands.Apply(world, DebugCommand.SetItem, "unknown", 1, false, output));
            Assert.Throws<ArgumentOutOfRangeException>(() => EditorDebugCommands.Apply(world, DebugCommand.AllItems, null, 6, false, output));
            Assert.Throws<ArgumentOutOfRangeException>(() => EditorDebugCommands.Apply(world, (DebugCommand)999, null, 1, false, output));
            Assert.AreEqual(stable, JsonUtility.ToJson(state)); Assert.AreEqual(2, Directory.GetDirectories(output).Length);
            Debug.Log("DEBUGGER STEP 3: 440 owned / 100 visible, then one per breed, then all items max and zero.");
            EditorDebugCommands.Apply(world, DebugCommand.AllCats, null, 40, false, output);
            Assert.AreEqual(440, world.Roster.TotalOwned); Assert.AreEqual(100, world.Roster.VisibleCount);
            EditorDebugCommands.Apply(world, DebugCommand.AllCats, null, 1, false, output);
            Assert.AreEqual(11, world.Roster.TotalOwned); Assert.AreEqual(11, world.Roster.VisibleCount);
            EditorDebugCommands.Apply(world, DebugCommand.AllItems, null, 5, false, output);
            Assert.IsTrue(state.items.TrueForAll(row => row.level == 5));
            EditorDebugCommands.Apply(world, DebugCommand.AllItems, null, 0, false, output);
            Assert.IsTrue(state.items.TrueForAll(row => row.level == 0));
            Assert.AreEqual(IdleMath.BasePerSecond(state), world.Economy.ProductionPerSecond);
            Assert.AreEqual(snapshot, JsonUtility.ToJson(state.firstEnding));
            Debug.Log("DEBUGGER EVIDENCE " + output);
        }

        [UnityTest]
        public IEnumerator Debugger_modal_pause_and_backup_failure_block_edits_then_normal_rocket_flow_still_works()
        {
            global::CatGame.CatGame game;
            var world = NewWorld(out game); yield return null;
            var state = world.Economy.State; string output = Evidence("blocked");
            string stable = JsonUtility.ToJson(state);
            var owner = new object();
            Debug.Log("DEBUGGER STEP 1: active modal and zero simulation speed reject without backup or state edits.");
            Assert.IsTrue(world.Hud.Modals.TryAcquire(owner));
            Assert.Throws<InvalidOperationException>(() => EditorDebugCommands.Apply(world, DebugCommand.SetHair, null, 1e12, false, output));
            Assert.IsFalse(Directory.Exists(output)); Assert.AreEqual(stable, JsonUtility.ToJson(state));
            world.Hud.Modals.Release(owner); Time.timeScale = 0;
            Assert.Throws<InvalidOperationException>(() => EditorDebugCommands.Apply(world, DebugCommand.SetHair, null, 1e12, false, output));
            Time.timeScale = 1;
            Debug.Log("DEBUGGER STEP 2: a file in place of the record directory must cancel before mutation.");
            Directory.CreateDirectory(output); string occupied = Path.Combine(output, "occupied"); File.WriteAllText(occupied, "fixture");
            Assert.Throws<IOException>(() => EditorDebugCommands.Apply(world, DebugCommand.SetHair, null, 1e12, false, occupied));
            Assert.AreEqual(stable, JsonUtility.ToJson(state));
            Debug.Log("DEBUGGER STEP 3: funding is not a rocket purchase; original confirm performs the one-time transaction.");
            EditorDebugCommands.Apply(world, DebugCommand.SetHair, null, EndingRules.RocketPrice, false, output);
            Assert.IsFalse(state.rocketPurchased); Assert.IsFalse(state.hasEndingSnapshot);
            Assert.IsTrue(world.Hud.Rocket.Open()); world.Hud.Rocket.Confirm();
            Assert.IsTrue(state.rocketPurchased); Assert.IsTrue(state.hasEndingSnapshot); Assert.AreEqual(0, state.catHair);
            Assert.IsFalse(world.Economy.BuyRocket());
            Debug.Log("DEBUGGER EVIDENCE " + output);
        }

        [UnityTest]
        public IEnumerator Debugger_repeated_item_edits_keep_auditable_backups_and_persist_reloadable_progress()
        {
            global::CatGame.CatGame game;
            var world = NewWorld(out game); yield return null;
            var state = world.Economy.State; string output = Evidence("repeat");
            Debug.Log("DEBUGGER STEP 1: 40 alternating item commands, every request produces independent before/requested/applied records.");
            for (int i = 0; i < 40; i++)
                EditorDebugCommands.Apply(world, DebugCommand.AllItems, null, i % 2 == 0 ? 5 : 0, false, output);
            var directories = Directory.GetDirectories(output); Assert.AreEqual(40, directories.Length);
            foreach (string directory in directories)
            {
                Assert.IsTrue(File.Exists(Path.Combine(directory, "before.json")));
                Assert.IsTrue(File.Exists(Path.Combine(directory, "requested.json")));
                StringAssert.Contains("\"status\": \"applied\"", File.ReadAllText(Path.Combine(directory, "operation.json")));
            }
            Debug.Log("DEBUGGER STEP 2: save/reload with production SaveSystem; no quarantine and exact final item levels.");
            string path = Path.Combine(output, "final-save.json"); SaveSystem.SaveToPath(path, state);
            var loaded = SaveSystem.LoadOrCreate(path);
            Assert.IsTrue(loaded.items.TrueForAll(row => row.level == 0));
            Assert.AreEqual(state.catHair, loaded.catHair, .001); Assert.AreEqual(state.cats[0].count, loaded.cats[0].count);
            Assert.IsFalse(File.Exists(Path.Combine(output, "save.bad.json")));
            Debug.Log("DEBUGGER EVIDENCE " + output);
        }
    }
}
#endif
