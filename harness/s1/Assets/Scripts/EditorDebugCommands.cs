using System;
using System.Globalization;
using System.IO;
using System.Text;
using CatGame.Idle;
using UnityEngine;

namespace CatGame.Debugging
{
    public enum DebugCommand { SetHair, AddHair, SetCat, SetItem, AllCats, AllItems }

    /// <summary>Authoring commands shared by the Editor and explicitly enabled local debugger.</summary>
    public static class EditorDebugCommands
    {
        [Serializable]
        sealed class Operation
        {
            public string utc, command, target, value, sourceSave, status;
            public bool countAsEarned;
            public GameState before, after;
        }

        public static string Apply(CatWorld world, DebugCommand command, string target, double value,
            bool countAsEarned, string outputDirectory)
        {
            if (!Application.isPlaying || world == null || world.Economy == null || world.Economy.State == null)
                throw new InvalidOperationException("请先进入游戏猫窝。");
            if (world.Hud == null || world.Hud.Modals.InputBlocked || Time.timeScale <= 0)
                throw new InvalidOperationException("请先完成对话、CG、离线提示或关闭暂停菜单，再使用调试修改。");
            if (string.IsNullOrWhiteSpace(outputDirectory)) throw new ArgumentException("记录目录不能为空。");
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "数值必须有限且不小于零。");

            var state = world.Economy.State;
            var candidate = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state));
            // JsonUtility may round a large double by one ULP; unrelated commands must preserve it exactly.
            candidate.catHair = state.catHair;
            candidate.totalEarned = state.totalEarned;
            switch (command)
            {
                case DebugCommand.SetHair:
                case DebugCommand.AddHair:
                    double next = command == DebugCommand.SetHair ? value : candidate.catHair + value;
                    double earned = countAsEarned ? Math.Max(0, next - candidate.catHair) : 0;
                    if (double.IsInfinity(next) || double.IsInfinity(candidate.totalEarned + earned))
                        throw new ArgumentOutOfRangeException(nameof(value), "相加后超出数值范围。");
                    candidate.catHair = next;
                    candidate.totalEarned += earned;
                    break;
                case DebugCommand.SetCat:
                    RequireInteger(value, IdleBalance.CatMaxOwned);
                    var cat = candidate.cats.Find(row => row.id == target);
                    if (cat == null) throw new ArgumentException("猫咪 ID 无效。", nameof(target));
                    cat.count = (int)value;
                    break;
                case DebugCommand.SetItem:
                    RequireInteger(value, ItemCatalog.LevelsPerItem);
                    var item = candidate.items.Find(row => row.id == target);
                    if (item == null) throw new ArgumentException("物品 ID 无效。", nameof(target));
                    item.level = (int)value;
                    break;
                case DebugCommand.AllCats:
                    RequireInteger(value, IdleBalance.CatMaxOwned);
                    foreach (var row in candidate.cats) row.count = (int)value;
                    break;
                case DebugCommand.AllItems:
                    RequireInteger(value, ItemCatalog.LevelsPerItem);
                    foreach (var row in candidate.items) row.level = (int)value;
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(command));
            }

            // Persist recovery material BEFORE touching live state. A failed backup cancels the command.
            Directory.CreateDirectory(outputDirectory);
            string operationDirectory = Path.Combine(outputDirectory,
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff") + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(operationDirectory);
            SaveSystem.SaveToPath(Path.Combine(operationDirectory, "before.json"), state);
            SaveSystem.SaveToPath(Path.Combine(operationDirectory, "requested.json"), candidate);
            var operation = new Operation {
                utc = DateTime.UtcNow.ToString("O"), command = command.ToString(), target = target,
                value = value.ToString("R", CultureInfo.InvariantCulture), countAsEarned = countAsEarned,
                sourceSave = SaveSystem.Instance != null ? SaveSystem.Instance.SavePath : "(in-memory fixture)",
                status = "prepared", before = state, after = candidate
            };
            string log = Path.Combine(operationDirectory, "operation.json");
            File.WriteAllText(log, JsonUtility.ToJson(operation, true), new UTF8Encoding(false));

            state.catHair = candidate.catHair;
            state.totalEarned = candidate.totalEarned;
            for (int i = 0; i < state.cats.Count; i++) state.cats[i].count = candidate.cats[i].count;
            for (int i = 0; i < state.items.Count; i++) state.items[i].level = candidate.items[i].level;
            if (world.Roster != null) world.Roster.Rebuild(state, command == DebugCommand.SetCat ? target : null);
            world.Economy.Initialize(state, world.Roster);
            if (SaveSystem.Instance != null && ReferenceEquals(SaveSystem.Instance.State, state))
                SaveSystem.Instance.MarkDirty();

            // before.json is authoritative; preserve it even if a later renderer or disk operation fails.
            operation.before = JsonUtility.FromJson<GameState>(File.ReadAllText(Path.Combine(operationDirectory, "before.json")));
            operation.after = state;
            operation.status = "applied";
            File.WriteAllText(log, JsonUtility.ToJson(operation, true), new UTF8Encoding(false));
            Debug.Log("[CatGame Debugger] " + command + " " + target + "=" + operation.value + " backup=" + operationDirectory);
            return operationDirectory;
        }

        static void RequireInteger(double value, int maximum)
        {
            if (value > maximum || value != Math.Floor(value))
                throw new ArgumentOutOfRangeException(nameof(value), "必须为 0–" + maximum + " 的整数。");
        }
    }
}
