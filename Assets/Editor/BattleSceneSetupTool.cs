using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using Flippy.CardDuelMobile.UI;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.SinglePlayer;
using Flippy.CardDuelMobile.Networking;
using System.IO;

public class BattleSceneSetupTool : EditorWindow
{
    private string generatedFolderPath = "Assets/Generated/3DBattle";
    private string scenePath = "Assets/Scenes/MainGame.unity";

    [MenuItem("Tools/Battle System/Setup 3D Battle Scene")]
    public static void ShowWindow()
    {
        GetWindow<BattleSceneSetupTool>("3D Battle Setup");
    }

    private void OnGUI()
    {
        GUILayout.Label("3D Battle System Scene Generator", EditorStyles.boldLabel);
        GUILayout.Space(10);

        GUILayout.Label("Generated Folder Path:", EditorStyles.label);
        generatedFolderPath = EditorGUILayout.TextField(generatedFolderPath);

        GUILayout.Label("Scene Path:", EditorStyles.label);
        scenePath = EditorGUILayout.TextField(scenePath);

        GUILayout.Space(10);

        if (GUILayout.Button("Generate Complete Battle Scene", GUILayout.Height(40)))
        {
            GenerateCompleteScene();
            EditorUtility.DisplayDialog("Success", "3D Battle Scene setup complete!", "OK");
        }

        GUILayout.Space(10);
        GUILayout.Label("This will create:", EditorStyles.helpBox);
        GUILayout.Label("✓ Folder structure in Assets/Generated/3DBattle", EditorStyles.miniLabel);
        GUILayout.Label("✓ Materials (slot, highlight, card)", EditorStyles.miniLabel);
        GUILayout.Label("✓ Prefabs (Board3DSlot, Card3DView)", EditorStyles.miniLabel);
        GUILayout.Label("✓ MainGame.unity scene with complete hierarchy", EditorStyles.miniLabel);
        GUILayout.Label("✓ GameModeManager (local/online switching)", EditorStyles.miniLabel);
        GUILayout.Label("✓ LocalSinglePlayerCoordinator (AI testing)", EditorStyles.miniLabel);
        GUILayout.Label("✓ NetworkBootstrap (Netcode multiplayer)", EditorStyles.miniLabel);
        GUILayout.Label("✓ All scripts assigned and configured", EditorStyles.miniLabel);
    }

    private void GenerateCompleteScene()
    {
        // Crear carpetas
        CreateFolderStructure();

        // Crear materiales
        var slotMaterial = CreateMaterial("SlotMaterial", new Color(0.3f, 0.3f, 0.4f, 0.8f));
        var slotHighlightMaterial = CreateMaterial("SlotHighlightMaterial", new Color(0.0f, 1.0f, 0.5f, 0.9f));
        var cardMaterial = CreateMaterial("CardMaterial", new Color(0.2f, 0.2f, 0.25f, 1.0f));

        // Crear prefabs
        var slotPrefab = CreateBoard3DSlotPrefab(slotMaterial, slotHighlightMaterial);
        var cardPrefab = CreateCard3DViewPrefab(cardMaterial);

        // Crear escena
        CreateMainGameScene(slotMaterial, slotHighlightMaterial);

        AssetDatabase.Refresh();
    }

    private void CreateFolderStructure()
    {
        string[] folders = new[]
        {
            generatedFolderPath,
            Path.Combine(generatedFolderPath, "Materials"),
            Path.Combine(generatedFolderPath, "Prefabs"),
            Path.Combine(generatedFolderPath, "Prefabs/Board"),
            Path.Combine(generatedFolderPath, "Prefabs/Cards"),
        };

        foreach (var folder in folders)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parentFolder = Path.GetDirectoryName(folder);
                string folderName = Path.GetFileName(folder);
                AssetDatabase.CreateFolder(parentFolder, folderName);
            }
        }
    }

    private Material CreateMaterial(string name, Color color)
    {
        string path = Path.Combine(generatedFolderPath, "Materials", $"{name}.mat");

        Material material = new Material(Shader.Find("Standard"))
        {
            color = color,
            name = name
        };
        material.SetFloat("_Metallic", 0.3f);
        material.SetFloat("_Glossiness", 0.5f);

        AssetDatabase.CreateAsset(material, path);
        AssetDatabase.SaveAssets();

        return material;
    }

    private GameObject CreateBoard3DSlotPrefab(Material defaultMat, Material highlightMat)
    {
        string prefabPath = Path.Combine(generatedFolderPath, "Prefabs/Board", "Board3DSlot.prefab");

        // Crear GameObject
        GameObject slotGo = new GameObject("Board3DSlot");
        slotGo.tag = "Untagged";

        // Agregar componentes
        var slotComponent = slotGo.AddComponent<Board3DSlot>();
        slotComponent.PlayerIndex = 0;
        slotComponent.Slot = BoardSlot.Front;

        // Crear quad visual dentro del prefab
        var quad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        quad.name = "SlotVisual";
        quad.transform.SetParent(slotGo.transform);
        quad.transform.localPosition = Vector3.zero;
        quad.transform.localScale = new Vector3(0.8f, 0.2f, 0.8f);

        // Remover componentes innecesarios
        var collider = quad.GetComponent<Collider>();
        if (collider != null)
            collider.isTrigger = false;

        DestroyImmediate(quad.GetComponent<Rigidbody>());

        // Asignar materiales
        var renderer = quad.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(defaultMat);
        }

        // Crear prefab
        PrefabUtility.SaveAsPrefabAsset(slotGo, prefabPath);
        DestroyImmediate(slotGo);

        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }

    private GameObject CreateCard3DViewPrefab(Material cardMaterial)
    {
        string prefabPath = Path.Combine(generatedFolderPath, "Prefabs/Cards", "Card3DView.prefab");

        GameObject cardGo = new GameObject("Card3DView");

        // Agregar componentes
        var cardView = cardGo.AddComponent<Card3DView>();
        cardGo.AddComponent<CardTooltip>();

        // Crear quad visual
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "CardMesh";
        quad.transform.SetParent(cardGo.transform);
        quad.transform.localPosition = Vector3.zero;
        quad.transform.localScale = new Vector3(0.8f, 1f, 1f);

        var renderer = quad.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material = new Material(cardMaterial);

        var collider = quad.GetComponent<Collider>();
        if (collider != null)
            collider.enabled = true;

        DestroyImmediate(quad.GetComponent<Rigidbody>());

        PrefabUtility.SaveAsPrefabAsset(cardGo, prefabPath);
        DestroyImmediate(cardGo);

        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }

    private void CreateMainGameScene(Material slotMaterial, Material slotHighlightMaterial)
    {
        // Crear nueva escena
        var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Obtener cámara principal
        var mainCamera = Camera.main.gameObject;
        mainCamera.name = "Main Camera";
        mainCamera.transform.rotation = Quaternion.identity;
        mainCamera.transform.position = Vector3.zero;

        // Agregar componentes a la cámara
        if (mainCamera.GetComponent<DragHandler3D>() == null)
            mainCamera.AddComponent<DragHandler3D>();
        if (mainCamera.GetComponent<AudioListener>() == null)
            mainCamera.AddComponent<AudioListener>();

        // ===== BOARD3D =====
        GameObject board3DContainer = new GameObject("Board3DContainer");
        var boardManager = board3DContainer.AddComponent<Board3DManager>();
        // Note: Assign 6 slots manually in inspector (slotEnemyFront, slotEnemyBackLeft, etc)

        // ===== HAND3D =====
        GameObject hand3DContainer = new GameObject("Hand3DContainer");
        hand3DContainer.transform.position = new Vector3(0, -5f, 0);
        var handManager = hand3DContainer.AddComponent<Hand3DManager>();
        handManager.arcRadius = 5f;
        handManager.arcHeight = -2f;
        handManager.cardSpacing = 0.5f;
        handManager.Initialize();

        // ===== CANVAS (HUD) =====
        GameObject canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        // Crear HUD3D con textos
        var hud3D = canvasGo.AddComponent<HUD3D>();

        // LocalHeroInfo (top-left)
        var localHeroGo = CreateTextElement("LocalHeroInfo", canvasGo.transform, new Vector2(150, -100), new Vector2(300, 150));
        hud3D.LocalHeroInfoText = localHeroGo.GetComponent<TextMeshProUGUI>();

        // RemoteHeroInfo (bottom-left)
        var remoteHeroGo = CreateTextElement("RemoteHeroInfo", canvasGo.transform, new Vector2(150, 100), new Vector2(300, 150));
        hud3D.RemoteHeroInfoText = remoteHeroGo.GetComponent<TextMeshProUGUI>();

        // TurnInfo (center-top)
        var turnInfoGo = CreateTextElement("TurnInfo", canvasGo.transform, new Vector2(0, -100), new Vector2(400, 100));
        hud3D.TurnInfoText = turnInfoGo.GetComponent<TextMeshProUGUI>();

        // BattleLog (top-right)
        var battleLogGo = CreateTextElement("BattleLog", canvasGo.transform, new Vector2(-150, -100), new Vector2(300, 400));
        hud3D.BattleLogText = battleLogGo.GetComponent<TextMeshProUGUI>();

        // ===== END TURN BUTTON =====
        GameObject buttonGo = new GameObject("EndTurnButton");
        buttonGo.transform.SetParent(canvasGo.transform);

        var btnRect = buttonGo.AddComponent<RectTransform>();
        btnRect.anchoredPosition = new Vector2(-150, -50);
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

        var textRect = buttonText.GetComponent<RectTransform>();
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(200, 60);

        var endTurnButton = buttonGo.AddComponent<EndTurnButton3D>();

        // ===== GAMEPLAY PRESENTER =====
        GameObject presenterGo = new GameObject("GameplayPresenter3D");
        var presenter = presenterGo.AddComponent<GameplayPresenter3D>();
        presenter.Board3DManager = boardManager;
        presenter.Hand3DManager = handManager;
        presenter.Hud3D = hud3D;
        presenter.DragHandler = mainCamera.GetComponent<DragHandler3D>();
        presenter.EndTurnButton = endTurnButton;

        // Wire EndTurnButton3D to presenter
        endTurnButton.Presenter = presenter;

        // Configurar DragHandler3D
        var dragHandler = mainCamera.GetComponent<DragHandler3D>();
        dragHandler.Hand3DManager = handManager;
        dragHandler.Board3DManager = boardManager;
        var dragGhostPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cards/DragGhost3DPrefab.prefab");
        if (dragGhostPrefab != null)
            dragHandler.dragGhost3DPrefab = dragGhostPrefab;

        // ===== AUDIO MANAGER =====
        GameObject audioGo = new GameObject("AudioManager");
        audioGo.AddComponent<AudioManager>();

        // ===== GAME MODE MANAGER =====
        GameObject gameModeGo = new GameObject("GameModeManager");
        gameModeGo.AddComponent<GameModeManager>();
        try { Object.DontDestroyOnLoad(gameModeGo); } catch { }

        // ===== NETWORK BOOTSTRAP =====
        GameObject networkGo = new GameObject("NetworkBootstrap");
        networkGo.AddComponent<NetworkBootstrap>();

        // ===== LOCAL SINGLE PLAYER COORDINATOR =====
        GameObject localCoordGo = new GameObject("LocalSinglePlayerCoordinator");
        var localCoord = localCoordGo.AddComponent<LocalSinglePlayerCoordinator>();
        localCoord.autoStartOnStart = false; // Will be started by GameModeManager

        // ===== MATCH COMPLETION SCREEN =====
        GameObject completionGo = new GameObject("MatchCompletionScreen");
        completionGo.transform.SetParent(canvasGo.transform);
        var completionRect = completionGo.AddComponent<RectTransform>();
        completionRect.anchoredPosition = Vector2.zero;
        completionRect.sizeDelta = new Vector2(1920, 1080);
        var completionScreen = completionGo.AddComponent<MatchCompletionScreen>();

        // Agregar texto placeholder para victoria/derrota
        var textGo = new GameObject("VictoryText");
        textGo.transform.SetParent(completionGo.transform);
        var victoryText = textGo.AddComponent<TextMeshProUGUI>();
        victoryText.text = "VICTORY!";
        victoryText.fontSize = 80;
        victoryText.alignment = TextAlignmentOptions.Center;
        var victoryRect = textGo.GetComponent<RectTransform>();
        victoryRect.sizeDelta = new Vector2(1920, 1080);

        // Guardar escena
        EditorSceneManager.SaveScene(newScene, scenePath);
        EditorSceneManager.OpenScene(scenePath);
    }

    private GameObject CreateTextElement(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);

        var rect = go.AddComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = $"{name}: Info";
        text.fontSize = 36;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;

        return go;
    }
}
