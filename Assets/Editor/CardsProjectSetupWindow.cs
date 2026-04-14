using UnityEditor;
using UnityEngine;

namespace Flippy.CardDuelMobile.EditorTools
{
    /// <summary>
    /// Ventana de bootstrap del proyecto.
    /// </summary>
    public sealed class CardsProjectSetupWindow : EditorWindow
    {
        [MenuItem("Tools/Cards/Setup Project")]
        public static void Open()
        {
            GetWindow<CardsProjectSetupWindow>("Cards Setup");
        }

        [MenuItem("Tools/Cards/Ensure Folders")]
        public static void EnsureFoldersMenu()
        {
            CardsPrototypeContentGenerator.EnsureFolders();
        }

        [MenuItem("Tools/Cards/Generate Prototype Content")]
        public static void GenerateContentMenu()
        {
            CardsPrototypeContentGenerator.GenerateAll();
        }

        [MenuItem("Tools/Cards/Build Prototype Scene")]
        public static void BuildSceneMenu()
        {
            CardsPrototypeSceneBuilder.BuildScene();
        }

        private void OnGUI()
        {
            GUILayout.Label("Flippy Card Duel Mobile", EditorStyles.boldLabel);
            GUILayout.Space(8f);

            if (GUILayout.Button("Ensure Folders", GUILayout.Height(32f)))
            {
                CardsPrototypeContentGenerator.EnsureFolders();
            }

            if (GUILayout.Button("Generate Prototype Content", GUILayout.Height(32f)))
            {
                CardsPrototypeContentGenerator.GenerateAll();
            }

            if (GUILayout.Button("Build Prototype Scene", GUILayout.Height(32f)))
            {
                CardsPrototypeSceneBuilder.BuildScene();
            }

            EditorGUILayout.HelpBox(
                "Orden recomendado: 1) Ensure Folders  2) Generate Prototype Content  3) Build Prototype Scene",
                MessageType.Info);
        }
    }
}
