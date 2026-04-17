using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Flippy.CardDuelMobile.Editor
{
    public class GameSetupWindow : EditorWindow
    {
        private GameSetupPreset _selectedPreset = GameSetupPreset.HearthstoneStyle;
        private bool _overwriteExisting = false;
        private Vector2 _scrollPos;

        [MenuItem("Tools/Game/Generate or Refresh Base Game")]
        public static void ShowWindow()
        {
            GetWindow<GameSetupWindow>("Game Setup");
        }

        [MenuItem("Tools/Game/Create Cards")]
        public static void CreateCardPrefabs()
        {
            CreateCardPrefabsInternal();
            EditorUtility.DisplayDialog("Success", "Card prefabs created at Assets/Prefabs/Cards/", "OK");
        }

        private void OnGUI()
        {
            GUILayout.Label("Board Generation Presets", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _selectedPreset = (GameSetupPreset)EditorGUILayout.EnumPopup("Preset:", _selectedPreset);
            EditorGUILayout.Space();

            // Preset descriptions
            DrawPresetDescription(_selectedPreset);
            EditorGUILayout.Space(10);

            _overwriteExisting = EditorGUILayout.Toggle("Overwrite Existing", _overwriteExisting);
            EditorGUILayout.Space();

            if (GUILayout.Button("Generate Board", GUILayout.Height(40)))
            {
                GenerateBoard();
            }

            if (GUILayout.Button("Clear Board", GUILayout.Height(30)))
            {
                ClearBoard();
            }
        }

        private void DrawPresetDescription(GameSetupPreset preset)
        {
            EditorGUILayout.HelpBox(GetPresetDescription(preset), MessageType.Info);
        }

        private string GetPresetDescription(GameSetupPreset preset)
        {
            return preset switch
            {
                GameSetupPreset.HearthstoneStyle =>
                    "Hearthstone-style layout:\n" +
                    "- Horizontal board layout\n" +
                    "- Cards in 3D worldspace canvas\n" +
                    "- Large card models with glow effects\n" +
                    "- Hand at bottom\n" +
                    "- Enemy board at top",

                GameSetupPreset.CompactVertical =>
                    "Compact vertical layout:\n" +
                    "- Narrow vertical board\n" +
                    "- Smaller card models\n" +
                    "- Hand on the side\n" +
                    "- Good for mobile screens",

                GameSetupPreset.TacticalIsometric =>
                    "Tactical isometric view:\n" +
                    "- Isometric camera angle\n" +
                    "- 3D board with depth\n" +
                    "- Cards positioned in 3D space\n" +
                    "- Chess-like positioning",

                GameSetupPreset.MinimalistUI =>
                    "Minimalist UI layout:\n" +
                    "- Simple rectangular cards\n" +
                    "- Focus on clarity\n" +
                    "- Compact, no excess graphics\n" +
                    "- Fast iteration friendly",

                GameSetupPreset.TriangleFormation =>
                    "Triangle Formation (3-card optimized):\n" +
                    "- Front card (melee) at center-top\n" +
                    "- Back-left and Back-right (ranged) below\n" +
                    "- Forms protective triangle\n" +
                    "- Clear role visualization",

                GameSetupPreset.LinearDefense =>
                    "Linear Defense (3-card optimized):\n" +
                    "- Cards in single horizontal line\n" +
                    "- Front card on the left (melee)\n" +
                    "- Two back cards on the right (ranged)\n" +
                    "- Simple, scannable layout",

                GameSetupPreset.CircularArena =>
                    "Circular Arena (3-card optimized):\n" +
                    "- Cards arranged in circle\n" +
                    "- Front card at bottom (melee)\n" +
                    "- Back cards at top-sides (ranged)\n" +
                    "- Dynamic, energetic feel",

                GameSetupPreset.TieredHeights =>
                    "Tiered Heights (3-card optimized):\n" +
                    "- Front card elevated (melee - aggressive)\n" +
                    "- Back cards lower (ranged - support)\n" +
                    "- Visual hierarchy by elevation\n" +
                    "- 3D depth emphasis",

                GameSetupPreset.SymmetricalBalance =>
                    "Symmetrical Balance (3-card optimized):\n" +
                    "- Front card centered (melee)\n" +
                    "- Back cards perfectly mirrored\n" +
                    "- Balanced, clean aesthetic\n" +
                    "- Easy to compare sides",

                GameSetupPreset.DynamicCombat =>
                    "Dynamic Combat (3-card optimized):\n" +
                    "- Front card forward (aggressive melee)\n" +
                    "- Back cards spread wide (ranged support)\n" +
                    "- Clear separation by role\n" +
                    "- Emphasizes attack ranges",

                _ => "Unknown preset"
            };
        }

        private void GenerateBoard()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Error", "Please save the scene first", "OK");
                return;
            }

            GameSetupGenerator.GenerateBoard(_selectedPreset, _overwriteExisting);
            EditorUtility.DisplayDialog("Success", $"Generated {_selectedPreset} preset", "OK");
        }

        private void ClearBoard()
        {
            if (EditorUtility.DisplayDialog("Clear Board", "Remove generated board elements?", "Yes", "No"))
            {
                GameSetupGenerator.ClearBoard();
                EditorUtility.DisplayDialog("Done", "Board cleared", "OK");
            }
        }

        private static void CreateCardPrefabsInternal()
        {
            var prefabPath = "Assets/Prefabs/Cards";

            // Create folders
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(prefabPath))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Cards");

            // HandCardButton prefab
            CreateHandCardPrefab(prefabPath);

            // CardViewWidget prefab (for board)
            CreateBoardCardPrefab(prefabPath);

            // DragGhost prefab
            CreateDragGhostPrefab(prefabPath);

            AssetDatabase.Refresh();
        }

        private static void CreateHandCardPrefab(string prefabPath)
        {
            var path = $"{prefabPath}/HandCardButton.prefab";
            if (AssetDatabase.LoadAssetAtPath(path, typeof(GameObject)) != null)
                return;

            var handCardObj = new GameObject("HandCardButton");
            var rect = handCardObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(240, 320);

            var button = handCardObj.AddComponent<Button>();
            var image = handCardObj.AddComponent<Image>();
            image.color = new Color(0.25f, 0.35f, 0.5f, 1f);
            button.targetGraphic = image;

            var canvasGroup = handCardObj.AddComponent<CanvasGroup>();

            // Add label
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(handCardObj.transform);
            var labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = "Card";
            labelText.fontSize = 48;
            labelText.alignment = TextAlignmentOptions.Center;

            PrefabUtility.SaveAsPrefabAsset(handCardObj, path);
            Object.DestroyImmediate(handCardObj);
            Debug.Log($"[GameSetup] Created HandCardButton prefab at {path}");
        }

        private static void CreateBoardCardPrefab(string prefabPath)
        {
            var path = $"{prefabPath}/CardViewWidget.prefab";
            if (AssetDatabase.LoadAssetAtPath(path, typeof(GameObject)) != null)
                return;

            var cardViewObj = new GameObject("CardViewWidget");
            var rect = cardViewObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(100, 140);

            var image = cardViewObj.AddComponent<Image>();
            image.color = new Color(0.35f, 0.35f, 0.45f, 1f);

            // Border highlight
            var borderObj = new GameObject("Border");
            borderObj.transform.SetParent(cardViewObj.transform);
            var borderRect = borderObj.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(2, 2);
            borderRect.offsetMax = new Vector2(-2, -2);
            var borderImage = borderObj.AddComponent<Image>();
            borderImage.color = new Color(1, 0.84f, 0, 0f);

            // Card info
            var infoObj = new GameObject("Info");
            infoObj.transform.SetParent(cardViewObj.transform);
            var infoRect = infoObj.AddComponent<RectTransform>();
            infoRect.anchorMin = Vector2.zero;
            infoRect.anchorMax = Vector2.one;
            infoRect.offsetMin = Vector2.zero;
            infoRect.offsetMax = Vector2.zero;
            var infoText = infoObj.AddComponent<TextMeshProUGUI>();
            infoText.text = "Card";
            infoText.fontSize = 20;
            infoText.alignment = TextAlignmentOptions.Center;

            PrefabUtility.SaveAsPrefabAsset(cardViewObj, path);
            Object.DestroyImmediate(cardViewObj);
            Debug.Log($"[GameSetup] Created CardViewWidget prefab at {path}");
        }

        private static void CreateDragGhostPrefab(string prefabPath)
        {
            var path = $"{prefabPath}/DragGhost.prefab";
            if (AssetDatabase.LoadAssetAtPath(path, typeof(GameObject)) != null)
                return;

            var dragGhostObj = new GameObject("DragGhost");
            var rect = dragGhostObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(100, 140);

            var image = dragGhostObj.AddComponent<Image>();
            image.color = new Color(0.5f, 0.6f, 0.8f, 0.8f);

            var canvasGroup = dragGhostObj.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.8f;

            // Info text
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(dragGhostObj.transform);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = "...";
            text.fontSize = 20;
            text.alignment = TextAlignmentOptions.Center;

            PrefabUtility.SaveAsPrefabAsset(dragGhostObj, path);
            Object.DestroyImmediate(dragGhostObj);
            Debug.Log($"[GameSetup] Created DragGhost prefab at {path}");
        }

        public enum GameSetupPreset
        {
            // Original presets
            HearthstoneStyle,
            CompactVertical,
            TacticalIsometric,
            MinimalistUI,

            // New presets optimized for 3-card mechanic
            TriangleFormation,
            LinearDefense,
            CircularArena,
            TieredHeights,
            SymmetricalBalance,
            DynamicCombat
        }
    }
}
