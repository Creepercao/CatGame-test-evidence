using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using CatGame.Idle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatGame.EditTests
{
    public class SaveSystemTests
    {
        string _directory;
        string _savePath;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Application.temporaryCachePath, "CatGameSaveTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _savePath = Path.Combine(_directory, "save.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [Test]
        public void Save_path_is_inside_the_CatGame_folder()
        {
            var path = SaveSystem.GetSavePath("C:/PlayerData");
            Assert.AreEqual(Path.Combine("C:/PlayerData", "CatGame", "save.json"), path);
        }

        [Test]
        public void Save_round_trips_fields_and_replaces_existing_file()
        {
            var state = SaveSystem.CreateNewGame();
            state.catHair = 123456.75;
            state.totalEarned = 987654.5;
            state.clicks = 42;
            state.cats[0].count = 3;
            state.items[4].level = 2;
            state.achievements.Add("first_fur");
            state.lastSeenUnix = 1793290000;
            state.prestigeLevel = 2;
            state.prestigeMultiplier = 1.5;

            SaveSystem.SaveToPath(_savePath, state);
            state.catHair = 20;
            SaveSystem.SaveToPath(_savePath, state);
            var loaded = SaveSystem.LoadOrCreate(_savePath);

            Assert.AreEqual(SaveSystem.CurrentVersion, loaded.version);
            Assert.AreEqual(20, loaded.catHair);
            Assert.AreEqual(state.totalEarned, loaded.totalEarned);
            Assert.AreEqual(state.clicks, loaded.clicks);
            Assert.AreEqual(3, loaded.cats[0].count);
            Assert.AreEqual(2, loaded.items[4].level);
            CollectionAssert.AreEqual(state.achievements, loaded.achievements);
            Assert.AreEqual(state.lastSeenUnix, loaded.lastSeenUnix);
            Assert.AreEqual(2, loaded.prestigeLevel);
            Assert.AreEqual(1.5, loaded.prestigeMultiplier);
            Assert.IsFalse(File.Exists(_savePath + ".tmp"));
            var previous = JsonUtility.FromJson<GameState>(File.ReadAllText(_savePath + ".bak"));
            Assert.AreEqual(123456.75, previous.catHair);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Load_restores_previous_save_when_primary_is_missing_or_corrupt(bool corrupt)
        {
            var state = SaveSystem.CreateNewGame();
            state.cats[0].count = 2;
            state.items[0].level = 1;
            state.catHair = 50;
            SaveSystem.SaveToPath(_savePath, state);
            state.cats[0].count = 3;
            state.catHair = 20;
            SaveSystem.SaveToPath(_savePath, state);
            File.WriteAllText(_savePath + ".tmp", "{ interrupted write");

            if (corrupt) File.WriteAllText(_savePath, "{ corrupt main");
            else File.Delete(_savePath);

            LogAssert.Expect(LogType.Warning, new Regex("Recovered CatGame progress from backup"));
            var restored = SaveSystem.LoadOrCreate(_savePath);
            Assert.AreEqual(2, restored.cats[0].count);
            Assert.AreEqual(1, restored.items[0].level);
            Assert.AreEqual(50, restored.catHair);
            Assert.AreEqual(corrupt, File.Exists(Path.Combine(_directory, "save.bad.json")));
            Assert.IsTrue(File.Exists(_savePath + ".bak"));
            Assert.IsFalse(File.Exists(_savePath + ".tmp"));
            Assert.AreEqual(50, SaveSystem.LoadOrCreate(_savePath).catHair);
        }

        [Test]
        public void Corrupt_primary_and_backup_start_fresh_and_keep_the_backup_for_diagnosis()
        {
            const string damagedBackup = "{ invalid backup";
            File.WriteAllText(_savePath, "{ invalid primary");
            File.WriteAllText(_savePath + ".bak", damagedBackup);
            LogAssert.Expect(LogType.Warning, new Regex("Save backup could not be restored"));
            LogAssert.Expect(LogType.Warning, new Regex("Save data could not be loaded"));

            Assert.AreEqual(1, SaveSystem.LoadOrCreate(_savePath).cats[0].count);
            Assert.IsFalse(File.Exists(_savePath));
            Assert.IsTrue(File.Exists(Path.Combine(_directory, "save.bad.json")));
            Assert.AreEqual(damagedBackup, File.ReadAllText(_savePath + ".bak"));
        }

        [Test]
        public void Failed_replacement_preserves_last_committed_save_and_allows_retry()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
                Assert.Ignore("This case relies on Windows file sharing to block the replacement.");

            var state = SaveSystem.CreateNewGame();
            state.catHair = 80;
            SaveSystem.SaveToPath(_savePath, state);
            state.cats[0].count = 2;
            state.catHair = 50;
            SaveSystem.SaveToPath(_savePath, state);
            byte[] committed = File.ReadAllBytes(_savePath);
            byte[] previous = File.ReadAllBytes(_savePath + ".bak");

            state.cats[0].count = 3;
            state.catHair = 20;
            using (File.Open(_savePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.Catch<IOException>(() => SaveSystem.SaveToPath(_savePath, state));
                CollectionAssert.AreEqual(committed, File.ReadAllBytes(_savePath));
                CollectionAssert.AreEqual(previous, File.ReadAllBytes(_savePath + ".bak"));
                var pending = JsonUtility.FromJson<GameState>(File.ReadAllText(_savePath + ".tmp"));
                Assert.AreEqual(3, pending.cats[0].count, "The new state reached the temporary file before replacement failed.");
            }

            var reopened = SaveSystem.LoadOrCreate(_savePath);
            Assert.AreEqual(2, reopened.cats[0].count);
            Assert.AreEqual(50, reopened.catHair);

            SaveSystem.SaveToPath(_savePath, state);
            var retried = SaveSystem.LoadOrCreate(_savePath);
            Assert.AreEqual(3, retried.cats[0].count);
            Assert.AreEqual(20, retried.catHair);
            Assert.IsFalse(File.Exists(_savePath + ".tmp"));
        }

        sealed class ReplacementFailure : IOException
        {
            public ReplacementFailure(int code) : base("Controlled file replacement failure") { HResult = code; }
        }

        void WriteWithReplacement(GameState state, bool backup, Action<string, string, string> replace, List<int> waits)
        {
            try
            {
                typeof(SaveSystem).GetMethod("WriteSave", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { _savePath, state, backup, replace, new Action<int>(waits.Add) });
            }
            catch (TargetInvocationException exception)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            }
        }

        GameState ReplacementState()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
                Assert.Ignore("Win32 1175 recovery is Windows-specific.");
            var state = SaveSystem.CreateNewGame();
            state.catHair = 80;
            SaveSystem.SaveToPath(_savePath, state);
            state.catHair = 50;
            SaveSystem.SaveToPath(_savePath, state);
            state.catHair = 20;
            state.cats[0].count = 3;
            return state;
        }

        [TestCase(1, true)]
        [TestCase(3, true)]
        [TestCase(1, false)]
        [TestCase(3, false)]
        public void Windows_transient_replacement_retries_the_same_pending_save(int failures, bool backup)
        {
            var state = ReplacementState();
            byte[] committed = File.ReadAllBytes(_savePath);
            byte[] previous = File.ReadAllBytes(_savePath + ".bak");
            var waits = new List<int>();
            int calls = 0;
            WriteWithReplacement(state, backup, (source, destination, old) => {
                Assert.AreEqual(_savePath + ".tmp", source);
                Assert.AreEqual(_savePath, destination);
                Assert.AreEqual(backup ? _savePath + ".bak" : null, old);
                CollectionAssert.AreEqual(committed, File.ReadAllBytes(destination));
                Assert.AreEqual(20, JsonUtility.FromJson<GameState>(File.ReadAllText(source)).catHair);
                if (++calls <= failures) throw new ReplacementFailure(unchecked((int)0x80070497));
                File.Replace(source, destination, old);
            }, waits);
            Assert.AreEqual(failures + 1, calls);
            Assert.AreEqual(failures, waits.Count);
            Assert.That(waits, Has.All.EqualTo(10));
            Assert.AreEqual(20, SaveSystem.LoadOrCreate(_savePath).catHair);
            Assert.IsFalse(File.Exists(_savePath + ".tmp"));
            CollectionAssert.AreEqual(backup ? committed : previous, File.ReadAllBytes(_savePath + ".bak"));
        }

        [TestCase(unchecked((int)0x80070497), 4)]
        [TestCase(unchecked((int)0x80070020), 1)]
        [TestCase(unchecked((int)0x80070498), 1)]
        public void Windows_persistent_or_other_replacement_failures_remain_visible(int code, int expectedCalls)
        {
            var state = ReplacementState();
            byte[] committed = File.ReadAllBytes(_savePath);
            byte[] previous = File.ReadAllBytes(_savePath + ".bak");
            var waits = new List<int>();
            int calls = 0;
            var failure = new ReplacementFailure(code);
            Assert.AreSame(failure, Assert.Throws<ReplacementFailure>(() =>
                WriteWithReplacement(state, true, (source, destination, old) => { calls++; throw failure; }, waits)));
            Assert.AreEqual(expectedCalls, calls);
            Assert.AreEqual(expectedCalls - 1, waits.Count);
            Assert.That(waits, Has.All.EqualTo(10));
            CollectionAssert.AreEqual(committed, File.ReadAllBytes(_savePath));
            CollectionAssert.AreEqual(previous, File.ReadAllBytes(_savePath + ".bak"));
            Assert.AreEqual(20, JsonUtility.FromJson<GameState>(File.ReadAllText(_savePath + ".tmp")).catHair);
        }

        [Test]
        public void Stress_save_load_round_trips_one_thousand_cycles_and_recovers_midway_corruption()
        {
            var state = SaveSystem.CreateNewGame();
            var stopwatch = Stopwatch.StartNew();
            long memoryBefore = GC.GetTotalMemory(false);
            long finalFileBytes = 0;

            for (int i = 0; i < 1000; i++)
            {
                state.catHair = i + 0.125;
                state.totalEarned = i + 1000.25;
                state.clicks = i;
                state.cats[0].count = i % (IdleBalance.CatMaxOwned + 1);
                state.items[0].level = i % (ItemCatalog.LevelsPerItem + 1);
                state.achievements.Clear();
                if ((i & 1) == 0) state.achievements.Add("first_fur");
                state.lastSeenUnix = i + 1700000000L;
                state.prestigeLevel = i % 5;
                state.prestigeMultiplier = 1.0 + i / 1000.0;

                try
                {
                    SaveSystem.SaveToPath(_savePath, state);
                }
                catch (IOException exception)
                {
                    TestContext.WriteLine("STR-02 failed cycle=" + i + ", HResult=0x" + exception.HResult.ToString("X8")
                        + ", primary=" + File.Exists(_savePath) + ", backup=" + File.Exists(_savePath + ".bak")
                        + ", temporary=" + File.Exists(_savePath + ".tmp"));
                    string evidenceRoot = Environment.GetEnvironmentVariable("CATGAME_SAVE_FAILURE_ROOT");
                    if (!string.IsNullOrEmpty(evidenceRoot))
                    {
                        string evidenceDirectory = Path.Combine(evidenceRoot, "cycle-" + i + "-" + Guid.NewGuid().ToString("N"));
                        Directory.CreateDirectory(evidenceDirectory);
                        foreach (string source in Directory.GetFiles(_directory))
                            File.Copy(source, Path.Combine(evidenceDirectory, Path.GetFileName(source)));
                        File.WriteAllText(Path.Combine(evidenceDirectory, "failure.txt"),
                            "cycle=" + i + Environment.NewLine + "savePath=" + _savePath + Environment.NewLine + exception);
                        TestContext.WriteLine("STR-02 failure files: " + evidenceDirectory);
                    }
                    throw;
                }
                var loaded = SaveSystem.LoadOrCreate(_savePath);

                Assert.AreEqual(state.version, loaded.version, "version at cycle " + i);
                Assert.AreEqual(state.catHair, loaded.catHair, "catHair at cycle " + i);
                Assert.AreEqual(state.totalEarned, loaded.totalEarned, "totalEarned at cycle " + i);
                Assert.AreEqual(state.clicks, loaded.clicks, "clicks at cycle " + i);
                Assert.AreEqual(state.cats[0].count, loaded.cats[0].count, "cat count at cycle " + i);
                Assert.AreEqual(state.items[0].level, loaded.items[0].level, "item level at cycle " + i);
                CollectionAssert.AreEqual(state.achievements, loaded.achievements, "achievements at cycle " + i);
                Assert.AreEqual(state.lastSeenUnix, loaded.lastSeenUnix, "lastSeenUnix at cycle " + i);
                Assert.AreEqual(state.prestigeLevel, loaded.prestigeLevel, "prestigeLevel at cycle " + i);
                Assert.AreEqual(state.prestigeMultiplier, loaded.prestigeMultiplier, 1e-12,
                    "JSON decimal round-trip tolerance; prestigeMultiplier at cycle " + i);
                Assert.IsFalse(File.Exists(_savePath + ".tmp"), "temporary file leaked at cycle " + i);

                if (i == 499)
                {
                    File.WriteAllText(_savePath, "{ broken in the middle of stress run");
                    LogAssert.Expect(LogType.Warning, new Regex("Recovered CatGame progress from backup"));
                    var recovered = SaveSystem.LoadOrCreate(_savePath);
                    Assert.AreEqual(498, recovered.clicks, "restore the last committed backup, not the interrupted save");
                    Assert.IsTrue(File.Exists(Path.Combine(_directory, "save.bad.json")), "corrupt mid-run file should be quarantined");
                    Assert.IsTrue(File.Exists(_savePath), "the restored backup should become the active save");
                }
            }

            stopwatch.Stop();
            long memoryAfter = GC.GetTotalMemory(false);
            finalFileBytes = new FileInfo(_savePath).Length;
            UnityEngine.Debug.Log(string.Format(
                "STR-02 save stress: roundTrips=1000, corruptRecovery=1, elapsedMs={0}, fileBytes={1}, managedBytesBefore={2}, managedBytesAfter={3}",
                stopwatch.ElapsedMilliseconds, finalFileBytes, memoryBefore, memoryAfter));
        }

        [Test]
        public void Corrupt_save_is_quarantined_and_new_game_is_returned()
        {
            File.WriteAllText(_savePath, "{ definitely not json");
            LogAssert.Expect(LogType.Warning, new Regex("Save data could not be loaded"));

            var state = SaveSystem.LoadOrCreate(_savePath);

            Assert.AreEqual(SaveSystem.CurrentVersion, state.version);
            Assert.AreEqual(CatCatalog.All.Length, state.cats.Count);
            Assert.AreEqual(ItemCatalog.All.Length, state.items.Count);
            Assert.IsTrue(File.Exists(Path.Combine(_directory, "save.bad.json")));
            Assert.IsFalse(File.Exists(_savePath));
        }

        [Test]
        public void Save_with_missing_fields_is_quarantined()
        {
            File.WriteAllText(_savePath, "{\"version\":1,\"catHair\":0}");
            LogAssert.Expect(LogType.Warning, new Regex("Save data could not be loaded"));

            SaveSystem.LoadOrCreate(_savePath);

            Assert.IsTrue(File.Exists(Path.Combine(_directory, "save.bad.json")));
            Assert.IsFalse(File.Exists(_savePath));
        }

        [Test]
        public void Future_save_version_is_quarantined_instead_of_overwritten()
        {
            var state = SaveSystem.CreateNewGame();
            state.version = SaveSystem.CurrentVersion + 1;
            File.WriteAllText(_savePath, JsonUtility.ToJson(state));
            LogAssert.Expect(LogType.Warning, new Regex("Save data could not be loaded"));

            var loaded = SaveSystem.LoadOrCreate(_savePath);

            Assert.AreEqual(SaveSystem.CurrentVersion, loaded.version);
            Assert.IsTrue(File.Exists(Path.Combine(_directory, "save.bad.json")));
            Assert.IsFalse(File.Exists(_savePath));
        }

        [Test]
        public void Save_rejects_out_of_range_cat_counts()
        {
            var state = SaveSystem.CreateNewGame();
            state.cats[0].count = IdleBalance.CatMaxOwned + 1;
            Assert.Throws<InvalidDataException>(() => SaveSystem.SaveToPath(_savePath, state));
            Assert.IsFalse(File.Exists(_savePath));
        }
    }
}
