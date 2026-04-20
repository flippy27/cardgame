using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using Flippy.CardDuelMobile.UI;
using System.IO;

public class HUDSetupTool : EditorWindow
{
    [MenuItem("Tools/Battle System/Setup HUD Canvas")]
    public static void ShowWindow()
    {
        GetWindow<HUDSetupTool>("HUD Canvas Setup");
    }

    private void OnGUI()
    {
        GUILayout.Label("HUD Canvas Generator", EditorStyles.boldLabel);
        GUILayout.Space(10);
        GUILayout.Label("Genera Canvas limpio + HUD elements correctamente posicionados", EditorStyles.helpBox);
        GUILayout.Space(10);

        if (GUILayout.Button("Generate Clean HUD Canvas", GUILayout.Height(40)))
        {
            GenerateHUD();
            EditorUtility.DisplayDialog("Success", "HUD Canvas generated!", "OK");
        }

        GUILayout.Space(10);
        if (GUILayout.Button("Delete Existing Canvas", GUILayout.Height(30)))
        {
            var existing = FindObjectOfType<Canvas>();
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
                EditorUtility.DisplayDialog("Deleted", "Canvas removed", "OK");
            }
        }
    }

    private void GenerateHUD()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var rootObjects = scene.GetRootGameObjects();

        // Remover canvas existente si existe
        foreach (var root in rootObjects)
        {
            if (root.name == "Canvas")
            {
                DestroyImmediate(root);
            }
        }

        // ===== CANVAS =====
        GameObject canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var canvasScaler = canvasGo.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920, 1080);

        canvasGo.AddComponent<GraphicRaycaster>();

        // ===== HUD3D Component =====
        var hud3D = canvasGo.AddComponent<HUD3D>();

        // ===== LocalHeroInfo =====
        var localHeroGo = CreateHUDText("LocalHeroInfo", canvasGo.transform, new Vector2(100, -50), new Vector2(300, 150));
        hud3D.LocalHeroInfoText = localHeroGo.GetComponent<TextMeshProUGUI>();

        // ===== RemoteHeroInfo =====
        var remoteHeroGo = CreateHUDText("RemoteHeroInfo", canvasGo.transform, new Vector2(100, 50), new Vector2(300, 150));
        hud3D.RemoteHeroInfoText = remoteHeroGo.GetComponent<TextMeshProUGUI>();

        // ===== TurnInfo =====
        var turnInfoGo = CreateHUDText("TurnInfo", canvasGo.transform, new Vector2(0, -50), new Vector2(400, 100));
        hud3D.TurnInfoText = turnInfoGo.GetComponent<TextMeshProUGUI>();

        // ===== BattleLog =====
        var battleLogGo = CreateHUDText("BattleLog", canvasGo.transform, new Vector2(-100, 0), new Vector2(300, 400));
        hud3D.BattleLogText = battleLogGo.GetComponent<TextMeshProUGUI>();

        // ===== END TURN BUTTON =====
        GameObject buttonGo = new GameObject("EndTurnButton");
        buttonGo.transform.SetParent(canvasGo.transform);

        var btnRect = buttonGo.AddComponent<RectTransform>();
        btnRect.anchoredPosition = new Vector2(-100, -50);
        btnRect.sizeDelta = new Vector2(200, 60);

        var button = buttonGo.AddComponent<Button>();
        var image = buttonGo.AddComponent<Image>();
        image.color = new Color(0.2f, 0.2f, 0.3f, 1f);
        button.targetGraphic = image;

        var buttonText = new GameObject("Text");
        buttonText.transform.SetParent(buttonGo.transform);
        var textComp = buttonText.AddComponent<TextMeshProUGUI>();
        textComp.text = "END TURN";
        textComp.alignment = TextAlignmentOptions.Center;
        textComp.fontSize = 32;
        textComp.color = Color.white;

        var textRect = buttonText.GetComponent<RectTransform>();
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(200, 60);

        var endTurnButton = buttonGo.AddComponent<EndTurnButton3D>();

        // Wire to HUD3D
        var gameplayPresenter = FindObjectOfType<GameplayPresenter3D>();
        if (gameplayPresenter != null)
        {
            gameplayPresenter.Hud3D = hud3D;
            endTurnButton.Presenter = gameplayPresenter;
        }

        // ===== MATCH COMPLETION SCREEN =====
        GameObject completionGo = new GameObject("MatchCompletionScreen");
        completionGo.transform.SetParent(canvasGo.transform);
        var completionRect = completionGo.AddComponent<RectTransform>();
        completionRect.anchoredPosition = Vector2.zero;
        completionRect.sizeDelta = new Vector2(1920, 1080);
        var completionScreen = completionGo.AddComponent<MatchCompletionScreen>();

        var victoryGo = new GameObject("VictoryText");
        victoryGo.transform.SetParent(completionGo.transform);
        var victoryText = victoryGo.AddComponent<TextMeshProUGUI>();
        victoryText.text = "VICTORY!";
        victoryText.fontSize = 80;
        victoryText.alignment = TextAlignmentOptions.Center;
        victoryText.color = Color.white;
        var victoryRect = victoryGo.GetComponent<RectTransform>();
        victoryRect.anchoredPosition = Vector2.zero;
        victoryRect.sizeDelta = new Vector2(1920, 1080);

        EditorSceneManager.MarkSceneDirty(scene);
    }

    private GameObject CreateHUDText(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);

        var rect = go.AddComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = $"{name}";
        text.fontSize = 32;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.color = Color.white;
        text.enableWordWrapping = true;

        return go;
    }
}
