using System.IO;
using UnityEditor;
using UnityEngine;

namespace CatGame.EditorTools
{
    /// <summary>
    /// 工程级操作：重新导入美术、构建 Windows 包、命令行一键跑。
    /// 场景生成在 SceneBuilders 里（主菜单 / 猫窝 / 公园）。
    /// </summary>
    public static class CatGameSetup
    {
        const string ArtRoot = "Assets/Resources/Art";

        /// <summary>构建场景，顺序即 Build Settings 顺序（第 0 个是启动场景）；Park 仅保留为预览资产。</summary>
        public static readonly string[] ScenePaths =
        {
            SceneBuilders.MenuPath,
            SceneBuilders.HomePath,
        };

        // ── 导入 ────────────────────────────────────────────────

        [MenuItem("CatGame/1. 重新导入美术资源", priority = 1)]
        public static void ForceReimportArt()
        {
            // 删掉 png 的 .meta，让 ArtImportRules 在"首次导入"时套用正确设置。
            // （不在批处理里反复 SaveAndReimport —— 那会让资源管线崩溃）
            int removed = 0;
            foreach (var meta in Directory.GetFiles(ArtRoot, "*.png.meta", SearchOption.AllDirectories))
            {
                File.Delete(meta);
                removed++;
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Debug.Log("[CatGame] 已重新导入美术资源（清理 meta " + removed + " 个）");
        }

        static bool HasArtMeta()
        {
            var metas = Directory.GetFiles(ArtRoot, "*.png.meta", SearchOption.AllDirectories);
            return metas.Length >= 20;
        }

        static bool ScenesMissing()
        {
            foreach (var p in ScenePaths)
                if (!File.Exists(p)) return true;
            return false;
        }

        // ── 构建 ────────────────────────────────────────────────

        [MenuItem("CatGame/4. 构建 Windows 版", priority = 4)]
        public static void BuildWindows()
        {
            if (ScenesMissing()) SceneBuilders.BuildAllScenes();

            var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Build/CatGame.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(outDir));

            var opts = new BuildPlayerOptions
            {
                scenes = ScenePaths,
                locationPathName = outDir,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var summary = report.summary;
            Debug.Log("[CatGame] 构建结果: " + summary.result + " / 大小 " + (summary.totalSize / 1024 / 1024) + " MB / 错误 " + summary.totalErrors);
            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        /// <summary>命令行一键：导入 + 生成三个场景 + 构建。</summary>
        public static void BuildAll()
        {
            if (!HasArtMeta()) ForceReimportArt();
            SceneBuilders.BuildAllScenes();
            BuildWindows();
            AssetDatabase.SaveAssets();
            Debug.Log("[CatGame] BuildAll 完成");
        }

        public static void BuildVisualQA()
        {
            BuildWindows();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = ScenePaths,
                locationPathName = Path.GetFullPath("Build/QA/CatGameQA.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            Debug.Log("[CatGame QA] Build result: " + report.summary.result);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }
        // Side-by-side repair delivery: never overwrite a user's running game or debugger.
        public static void BuildConnectionFix()
        {
            foreach(bool qa in new[]{false,true}) {
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes=ScenePaths,
                    locationPathName=Path.GetFullPath(qa?"Build/ConnectionFix/QA/CatGameQA.exe":"Build/ConnectionFix/CatGame.exe"),
                    target=BuildTarget.StandaloneWindows64,
                    options=qa?BuildOptions.Development:BuildOptions.None
                });
                Debug.Log("[Connection Fix] "+(qa?"QA":"Release")+": "+report.summary.result);
                if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) { EditorApplication.Exit(1); return; }
            }
        }
        public static void BuildBusinessQA()
        {
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=ScenePaths, locationPathName=Path.GetFullPath("Build/Quality/QA/CatGameQA.exe"),
                target=BuildTarget.StandaloneWindows64, options=BuildOptions.Development
            });
            Debug.Log("[Business QA] "+report.summary.result);
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }
        public static void CICompileCheck() { Debug.Log("[CI] Runtime and Editor assemblies compiled successfully."); }
        public static void BuildPerformanceQA()
        {
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=ScenePaths, locationPathName=Path.GetFullPath("Build/Performance/QA/CatGameQA.exe"),
                target=BuildTarget.StandaloneWindows64, options=BuildOptions.Development
            });
            Debug.Log("[Performance QA] "+report.summary.result);
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        }
        public static void CIControlledBuildFailure()
        {
            Debug.LogError("[CI] Deliberate build-stage failure fixture; no package is produced.");
            EditorApplication.Exit(1);
        }
        public static void BuildCIPackage()
        {
            string folder=System.Environment.GetEnvironmentVariable("CATGAME_CI_DELIVERY");
            if(string.IsNullOrEmpty(folder)) throw new System.InvalidOperationException("CI delivery root is required");
            folder=Path.GetFullPath(folder);
            string allowed=Path.GetFullPath("Build/ci/packages")+Path.DirectorySeparatorChar;
            if(!folder.StartsWith(allowed,System.StringComparison.OrdinalIgnoreCase))
                throw new System.InvalidOperationException("CI output must remain under Build/ci/packages");
            foreach(bool qa in new[]{false,true}) {
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes=ScenePaths, locationPathName=Path.Combine(folder,qa?"QA/CatGameQA.exe":"Release/CatGame.exe"),
                    target=BuildTarget.StandaloneWindows64, options=qa?BuildOptions.Development:BuildOptions.None
                });
                Debug.Log("[CI] "+(qa?"QA":"Release")+": "+report.summary.result);
                if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) { EditorApplication.Exit(1); return; }
            }
        }
    }
}
