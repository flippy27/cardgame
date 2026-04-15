using UnityEditor.SceneManagement;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace Flippy.CardDuelMobile.Editor
{
    /// <summary>
    /// Configures Build Settings with correct scene order.
    /// Run from menu: Tools → Configure Build Settings
    /// </summary>
    public static class ConfigureBuildSettings
    {
        [MenuItem("Tools/Configure Build Settings")]
        public static void SetupBuildSettings()
        {
            var scenes = new[]
            {
                "Assets/Scenes/LoginScene.unity",
                "Assets/Scenes/MenuScene.unity",
                "Assets/Scenes/CardDuelPrototype.unity"  // or BattleScene
            };

            var editorScenes = new EditorBuildSettingsScene[scenes.Length];

            for (int i = 0; i < scenes.Length; i++)
            {
                editorScenes[i] = new EditorBuildSettingsScene(scenes[i], true);
            }

            EditorBuildSettings.scenes = editorScenes;

            Debug.Log("✅ Build Settings configured:");
            for (int i = 0; i < scenes.Length; i++)
            {
                Debug.Log($"   [{i}] {scenes[i]}");
            }
        }
    }
}
