using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Flippy.CardDuelMobile.Editor
{
    /// <summary>
    /// Complete project setup automation.
    /// One-click setup: Tools → CardGame Setup → Complete Setup
    /// </summary>
    public static class ProjectSetup
    {
        [MenuItem("Tools/CardGame Setup/1. Create All Scenes")]
        public static void Step1_CreateScenes()
        {
            Debug.Log("🎬 Creating scenes...");
            CreateScenes.CreateBothScenes();
            Debug.Log("✅ Scenes created. Next: Configure Build Settings");
        }

        [MenuItem("Tools/CardGame Setup/2. Configure Build Settings")]
        public static void Step2_ConfigureBuildSettings()
        {
            Debug.Log("⚙️ Configuring Build Settings...");
            ConfigureBuildSettings.SetupBuildSettings();
            Debug.Log("✅ Build Settings configured. Next: Test in Editor");
        }

        [MenuItem("Tools/CardGame Setup/Complete Setup")]
        public static void CompleteSetup()
        {
            Debug.Log("🚀 Starting complete CardGame setup...\n");

            // Step 1: Create scenes
            Debug.Log("[1/2] Creating scenes...");
            CreateScenes.CreateBothScenes();
            Debug.Log("✅ Scenes created\n");

            // Step 2: Configure build settings
            Debug.Log("[2/2] Configuring Build Settings...");
            ConfigureBuildSettings.SetupBuildSettings();
            Debug.Log("✅ Build Settings configured\n");

            Debug.Log("🎉 Setup complete! You can now:");
            Debug.Log("   1. Press Play to test LoginScene");
            Debug.Log("   2. Test login with valid credentials");
            Debug.Log("   3. Navigate to MenuScene after login");
            Debug.Log("   4. Test Battle flow from menu");
        }

        [MenuItem("Tools/CardGame Setup/Open LoginScene")]
        public static void OpenLoginScene()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/LoginScene.unity", OpenSceneMode.Single);
        }

        [MenuItem("Tools/CardGame Setup/Open MenuScene")]
        public static void OpenMenuScene()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/MenuScene.unity", OpenSceneMode.Single);
        }

        [MenuItem("Tools/CardGame Setup/Open BattleScene")]
        public static void OpenBattleScene()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/CardDuelPrototype.unity", OpenSceneMode.Single);
        }
    }
}
