using UnityEditor;
using UnityEditor.Build;

namespace CatGame.EditorTools
{
    // Isolated diagnostic APK; never changes the production application's identity.
    public static class AndroidPurchaseTraceBuild
    {
        public static void Build()
        {
            var target = NamedBuildTarget.Android;
            string originalId = PlayerSettings.GetApplicationIdentifier(target);
            string originalName = PlayerSettings.productName;
            try
            {
                PlayerSettings.SetApplicationIdentifier(target, "com.catgamestudio.catgame.inputqa");
                PlayerSettings.productName = originalName + " Input QA";
                CatGameSetup.BuildAndroidPlayer("Build/Android/purchase-trace/CatGame-InputQA.apk", BuildOptions.Development);
            }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(target, originalId);
                PlayerSettings.productName = originalName;
                AssetDatabase.SaveAssets();
            }
        }
    }
}
