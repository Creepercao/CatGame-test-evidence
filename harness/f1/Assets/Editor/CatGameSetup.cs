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
            WorldArtMigration.Apply();
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

            var args = System.Environment.GetCommandLineArgs();
            int outputIndex = System.Array.IndexOf(args, "-buildOutput");
            if (outputIndex == args.Length - 1)
                throw new UnityEditor.Build.BuildFailedException("-buildOutput requires an EXE path.");
            var outDir = Path.GetFullPath(outputIndex >= 0 ? args[outputIndex + 1]
                : Path.Combine(Application.dataPath, "../Build/CatGame.exe"));
            if (!string.Equals(Path.GetExtension(outDir), ".exe", System.StringComparison.OrdinalIgnoreCase))
                throw new UnityEditor.Build.BuildFailedException("The Windows output path must end in .exe.");
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

        [MenuItem("CatGame/5. 构建 Android APK", priority = 5)]
        public static void BuildAndroid()
        {
            BuildAndroidPlayer("Build/Android/CatGame.apk", BuildOptions.None);
        }

        internal static void BuildAndroidPlayer(string defaultOutput, BuildOptions options)
        {
            if (ScenesMissing()) SceneBuilders.BuildAllScenes();

            var args = System.Environment.GetCommandLineArgs();
            int outputIndex = System.Array.IndexOf(args, "-buildOutput");
            if (outputIndex == args.Length - 1)
                throw new UnityEditor.Build.BuildFailedException("-buildOutput requires an APK path.");
            var output = Path.GetFullPath(outputIndex >= 0 ? args[outputIndex + 1] : defaultOutput);
            if (!string.Equals(Path.GetExtension(output), ".apk", System.StringComparison.OrdinalIgnoreCase))
                throw new UnityEditor.Build.BuildFailedException("The Android output path must end in .apk.");
            Directory.CreateDirectory(Path.GetDirectoryName(output));

            var orientation = PlayerSettings.defaultInterfaceOrientation;
            var buildAppBundle = EditorUserBuildSettings.buildAppBundle;
            var exportGradle = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            try
            {
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = ScenePaths,
                    locationPathName = output,
                    target = BuildTarget.Android,
                    options = options,
                });
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new UnityEditor.Build.BuildFailedException($"CatGame APK build {report.summary.result} ({report.summary.totalErrors} errors).");
                Debug.Log($"[CatGame] Android APK: {output} ({report.summary.totalSize} bytes)");
            }
            finally
            {
                PlayerSettings.defaultInterfaceOrientation = orientation;
                EditorUserBuildSettings.buildAppBundle = buildAppBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = exportGradle;
            }
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
            var args = System.Environment.GetCommandLineArgs();
            int outputIndex = System.Array.IndexOf(args, "-buildOutput");
            if (outputIndex == args.Length - 1)
                throw new UnityEditor.Build.BuildFailedException("-buildOutput requires an EXE path.");
            string output = Path.GetFullPath(outputIndex >= 0 ? args[outputIndex + 1] : "Build/Quality/QA/CatGameQA.exe");
            if (!string.Equals(Path.GetExtension(output), ".exe", System.StringComparison.OrdinalIgnoreCase))
                throw new UnityEditor.Build.BuildFailedException("The QA output path must end in .exe.");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=ScenePaths, locationPathName=output,
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
