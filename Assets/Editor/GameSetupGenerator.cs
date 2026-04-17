using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using TMPro;
using Flippy.CardDuelMobile.Core;
using static Flippy.CardDuelMobile.Editor.GameSetupWindow;

namespace Flippy.CardDuelMobile.Editor
{
    public static class GameSetupGenerator
    {
        public static void GenerateBoard(GameSetupPreset preset, bool overwrite)
        {
            Debug.Log("[GameSetup] GenerateBoard started");
            var scene = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("Error", "Please save the scene first", "OK");
                return;
            }

            if (overwrite)
            {
                Debug.Log("[GameSetup] Overwrite enabled, clearing old board");
                ClearBoard();
            }

            try
            {
                Debug.Log("[GameSetup] Creating root gameobjects and canvas");
                // Create root gameobjects and canvas
                CreateRootGameObjects();
                var canvas = CreateOrGetCanvas();
                Debug.Log($"[GameSetup] Canvas created: {canvas != null}");

                // Create full UI hierarchy
                Debug.Log("[GameSetup] Creating menu panel");
                CreateMenuPanel(canvas);
                Debug.Log("[GameSetup] Creating gameplay panel");
                var gameplayPanel = CreateGameplayPanel(canvas);
                Debug.Log($"[GameSetup] GameplayPanel created: {gameplayPanel != null}");
                CreateDragLayer(canvas);

                // Wire up all scripts
                WireMatchmakingPanel(canvas);
                WireUIManager(canvas);
                WireBattleScreenPresenter(gameplayPanel);
                WireBoardSlots(gameplayPanel);

                // Apply preset-specific board positions
                ApplyPresetLayout(gameplayPanel, preset);

                // Create card prefabs and wire them
                CreateCardPrefabs(overwrite);
                WireCardPrefabs(gameplayPanel);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorUtility.DisplayDialog("Success", $"Generated {preset} preset - all ready to play!", "OK");
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Generation failed: {e.Message}\n{e.StackTrace}", "OK");
                Debug.LogError($"[GameSetup] {e}");
            }
        }

        public static void ClearBoard()
        {
            var canvas = GameObject.Find("Canvas");
            if (canvas != null)
            {
                Object.DestroyImmediate(canvas);
            }

            var rootObj = GameObject.Find("Root");
            if (rootObj != null && rootObj.transform.parent == null)
            {
                Object.DestroyImmediate(rootObj);
            }
        }

        // ============ ROOT GAMEOBJECTS ============

        private static void CreateRootGameObjects()
        {
            // Main Camera
            if (FindFirstObjectByType<Camera>() == null)
            {
                var cameraObj = new GameObject("Main Camera");
                var camera = cameraObj.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.orthographicSize = 5;
                cameraObj.AddComponent<AudioListener>();
                Undo.RegisterCreatedObjectUndo(cameraObj, "Create Main Camera");
            }

            // EventSystem
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var eventObj = new GameObject("EventSystem");
                eventObj.AddComponent<EventSystem>();
                Undo.RegisterCreatedObjectUndo(eventObj, "Create EventSystem");
            }

            // NetworkManager (required for NGO)
            var netMgr = FindFirstObjectByType<Unity.Netcode.NetworkManager>();
            if (netMgr == null)
            {
                var netMgrObj = new GameObject("NetworkManager");
                netMgr = netMgrObj.AddComponent<Unity.Netcode.NetworkManager>();
                // Add transport
                var transport = netMgrObj.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
                netMgr.NetworkConfig.NetworkTransport = transport;
                Undo.RegisterCreatedObjectUndo(netMgrObj, "Create NetworkManager");
            }

            // NetworkBootstrap
            var netBoot = FindFirstObjectByType<Flippy.CardDuelMobile.Networking.NetworkBootstrap>();
            if (netBoot == null)
            {
                var netBootObj = new GameObject("NetworkBootstrap");
                netBoot = netBootObj.AddComponent<Flippy.CardDuelMobile.Networking.NetworkBootstrap>();
                Undo.RegisterCreatedObjectUndo(netBootObj, "Create NetworkBootstrap");
            }
            // Wire references using SerializedObject for private fields
            var mpsService = FindFirstObjectByType<Flippy.CardDuelMobile.Networking.MpsGameSessionService>();
            if (netBoot != null && mpsService != null && netMgr != null)
            {
                var so = new SerializedObject(netBoot);
                so.FindProperty("networkManager").objectReferenceValue = netMgr;
                so.FindProperty("sessionService").objectReferenceValue = mpsService;
                so.ApplyModifiedProperties();
            }

            // CardDuelNetworkCoordinator - create as prefab and spawn via network
            CreateCardDuelNetworkCoordinatorPrefab();
            var netCoordInstance = FindFirstObjectByType<Flippy.CardDuelMobile.Networking.CardDuelNetworkCoordinator>();
            if (netCoordInstance == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath("Assets/Prefabs/CardDuelNetworkCoordinator.prefab", typeof(GameObject)) as GameObject;
                if (prefab != null)
                {
                    var instance = Object.Instantiate(prefab);
                    instance.name = "CardDuelNetworkCoordinator";
                    var netObj = instance.GetComponent<Unity.Netcode.NetworkObject>();
                    if (netObj != null && netMgr != null && netMgr.IsListening)
                    {
                        netObj.Spawn();
                    }
                    Undo.RegisterCreatedObjectUndo(instance, "Create CardDuelNetworkCoordinator Instance");
                }
            }

            // MpsGameSessionService
            if (mpsService == null)
            {
                var mpsObj = new GameObject("MpsGameSessionService");
                mpsService = mpsObj.AddComponent<Flippy.CardDuelMobile.Networking.MpsGameSessionService>();
                Undo.RegisterCreatedObjectUndo(mpsObj, "Create MpsGameSessionService");
            }

            // GameModeManager
            if (FindFirstObjectByType<Flippy.CardDuelMobile.Core.GameModeManager>() == null)
            {
                var gmObj = new GameObject("GameModeManager");
                gmObj.AddComponent<Flippy.CardDuelMobile.Core.GameModeManager>();
                Undo.RegisterCreatedObjectUndo(gmObj, "Create GameModeManager");
            }

            // LocalSinglePlayerCoordinator
            var localCoord = FindFirstObjectByType<Flippy.CardDuelMobile.SinglePlayer.LocalSinglePlayerCoordinator>();
            if (localCoord == null)
            {
                var localObj = new GameObject("LocalSinglePlayerCoordinator");
                localCoord = localObj.AddComponent<Flippy.CardDuelMobile.SinglePlayer.LocalSinglePlayerCoordinator>();
                Undo.RegisterCreatedObjectUndo(localObj, "Create LocalSinglePlayerCoordinator");
            }

            // Wire LocalSinglePlayerCoordinator with default assets
            if (localCoord != null)
            {
                localCoord.autoStartOnStart = false;

                // Load default rule profile
                var rulesPath = "Assets/CardDuelMobile/Generated/Data/Rules/DefaultDuelRules.asset";
                var rulesProfile = AssetDatabase.LoadAssetAtPath(rulesPath, typeof(Flippy.CardDuelMobile.Data.DuelRulesProfile)) as Flippy.CardDuelMobile.Data.DuelRulesProfile;
                if (rulesProfile != null)
                    localCoord.rulesProfile = rulesProfile;

                // Load default decks
                var deckAPath = "Assets/CardDuelMobile/Generated/Data/Decks/Deck_PlayerA.asset";
                var deckBPath = "Assets/CardDuelMobile/Generated/Data/Decks/Deck_PlayerB.asset";
                var deckA = AssetDatabase.LoadAssetAtPath(deckAPath, typeof(Flippy.CardDuelMobile.Data.DeckDefinition)) as Flippy.CardDuelMobile.Data.DeckDefinition;
                var deckB = AssetDatabase.LoadAssetAtPath(deckBPath, typeof(Flippy.CardDuelMobile.Data.DeckDefinition)) as Flippy.CardDuelMobile.Data.DeckDefinition;
                if (deckA != null)
                    localCoord.localPlayerDeck = deckA;
                if (deckB != null)
                    localCoord.enemyDeck = deckB;

                EditorUtility.SetDirty(localCoord);
            }
        }

        // ============ CANVAS & UI HIERARCHY ============

        private static Canvas CreateOrGetCanvas()
        {
            var existing = GameObject.Find("Canvas");
            if (existing != null)
                return existing.GetComponent<Canvas>();

            var canvasObj = new GameObject("Canvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();

            // Add UIManager to control panel visibility
            var uiManager = canvasObj.AddComponent<Flippy.CardDuelMobile.UI.UIManager>();

            // Create Root child for UI elements
            var rootObj = new GameObject("Root");
            rootObj.transform.SetParent(canvasObj.transform);
            var rootRect = rootObj.AddComponent<RectTransform>();
            SetRectTransformFullScreen(rootRect);
            Undo.RegisterCreatedObjectUndo(rootObj, "Create Root");

            Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");
            return canvas;
        }

        private static GameObject CreateMenuPanel(Canvas canvas)
        {
            var rootObj = canvas.transform.Find("Root");
            if (rootObj == null)
            {
                Debug.LogError("[GameSetup] Root not found in Canvas");
                return null;
            }

            // Find or create MenuPanel - full screen
            var menuPanelObj = rootObj.Find("MenuPanel")?.gameObject;
            if (menuPanelObj == null)
            {
                menuPanelObj = CreateUIElement("MenuPanel", rootObj, fullScreen: true);
            }

            var image = menuPanelObj.GetComponent<Image>() ?? menuPanelObj.AddComponent<Image>();
            image.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

            // Ensure CanvasGroup for visibility control
            var _ = menuPanelObj.GetComponent<CanvasGroup>() ?? menuPanelObj.AddComponent<CanvasGroup>();

            // Add layout group - grid 2 columns, bigger buttons
            var layout = menuPanelObj.GetComponent<GridLayoutGroup>();
            if (layout == null)
            {
                layout = menuPanelObj.AddComponent<GridLayoutGroup>();
                layout.cellSize = new Vector2(340, 140);
                layout.spacing = new Vector2(20, 20);
                layout.padding = new RectOffset(40, 40, 40, 40);
                layout.startAxis = GridLayoutGroup.Axis.Horizontal;
            }

            // Create button infrastructure
            CreateButton(menuPanelObj.transform, "QuickMatchButton", "Quick Match");
            CreateButton(menuPanelObj.transform, "CreatePrivateButton", "Create Private");
            CreateButton(menuPanelObj.transform, "JoinByCodeButton", "Join By Code");
            CreateButton(menuPanelObj.transform, "AdvancedQueueButton", "Advanced Queue");
            CreateButton(menuPanelObj.transform, "ReadyUpButton", "Ready Up");
            CreateButton(menuPanelObj.transform, "LocalMatch", "Local Match (AI)");
            CreateButton(menuPanelObj.transform, "LeaveButton", "Leave");

            CreateInputField(menuPanelObj.transform, "JoinCodeField", "Join Code");
            CreateInputField(menuPanelObj.transform, "PrivateMatchField", "Private Match Name");

            CreateText(menuPanelObj.transform, "StatusText", "Waiting...");
            CreateText(menuPanelObj.transform, "MatchStatus", "");
            CreateText(menuPanelObj.transform, "JoinCodeText", "");
            CreateButton(menuPanelObj.transform, "CopyJoinCodeButton", "Copy Code");

            return menuPanelObj;
        }

        private static GameObject CreateGameplayPanel(Canvas canvas)
        {
            var rootObj = canvas.transform.Find("Root");
            if (rootObj == null)
            {
                Debug.LogError("[GameSetup] Root not found in Canvas");
                return null;
            }

            var gameplayObj = CreateUIElement("GameplayPanel", rootObj, fullScreen: true);
            gameplayObj.SetActive(false);

            // Add image background
            var bgImage = gameplayObj.GetComponent<Image>() ?? gameplayObj.AddComponent<Image>();
            bgImage.color = new Color(0.05f, 0.05f, 0.05f, 1f);

            // Ensure CanvasGroup for visibility control
            var _ = gameplayObj.GetComponent<CanvasGroup>() ?? gameplayObj.AddComponent<CanvasGroup>();

            // HUD texts - top center for HP/Mana
            var turnInfoTxt = CreateText(gameplayObj.transform, "TurnInfo", "HP: 30");
            var turnRect = turnInfoTxt.GetComponent<RectTransform>();
            turnRect.anchorMin = new Vector2(0.5f, 1);
            turnRect.anchorMax = new Vector2(0.5f, 1);
            turnRect.anchoredPosition = new Vector2(-120, -40);
            turnRect.sizeDelta = new Vector2(200, 60);

            var heroInfoTxt = CreateText(gameplayObj.transform, "HeroInfo", "Mana: 5");
            var heroRect = heroInfoTxt.GetComponent<RectTransform>();
            heroRect.anchorMin = new Vector2(0.5f, 1);
            heroRect.anchorMax = new Vector2(0.5f, 1);
            heroRect.anchoredPosition = new Vector2(120, -40);
            heroRect.sizeDelta = new Vector2(200, 60);

            var selectedCardTxt = CreateText(gameplayObj.transform, "SelectedCard", "");
            SetRectTransformCorner(selectedCardTxt.GetComponent<RectTransform>(), new Vector2(1, 0.9f), new Vector2(250, 50), anchorX: 1);

            var battleLogTxt = CreateText(gameplayObj.transform, "BattleLog", "Battle started...");
            SetRectTransformCorner(battleLogTxt.GetComponent<RectTransform>(), new Vector2(1, -100), new Vector2(300, 100), anchorX: 1);

            // Board areas - Enemy board top 35%, Player board bottom 40%, Hand bottom 15%
            var remoteArea = CreateUIElement("RemoteBoardArea", gameplayObj.transform);
            var remoteRect = remoteArea.GetComponent<RectTransform>();
            remoteRect.anchorMin = new Vector2(0, 0.65f);
            remoteRect.anchorMax = new Vector2(1, 1);
            remoteRect.offsetMin = Vector2.zero;
            remoteRect.offsetMax = Vector2.zero;

            CreateBoardSlot(remoteArea.transform, "RemoteBackLeftSlot", BoardSlot.BackLeft, false, new Vector2(240, 336));
            CreateBoardSlot(remoteArea.transform, "RemoteFrontSlot", BoardSlot.Front, false, new Vector2(240, 336));
            CreateBoardSlot(remoteArea.transform, "RemoteBackRightSlot", BoardSlot.BackRight, false, new Vector2(240, 336));

            var localArea = CreateUIElement("LocalBoardArea", gameplayObj.transform);
            var localRect = localArea.GetComponent<RectTransform>();
            localRect.anchorMin = new Vector2(0, 0.25f);
            localRect.anchorMax = new Vector2(1, 0.65f);
            localRect.offsetMin = Vector2.zero;
            localRect.offsetMax = Vector2.zero;

            CreateBoardSlot(localArea.transform, "LocalFrontSlot", BoardSlot.Front, true);
            CreateBoardSlot(localArea.transform, "LocalBackLeftSlot", BoardSlot.BackLeft, true);
            CreateBoardSlot(localArea.transform, "LocalBackRightSlot", BoardSlot.BackRight, true);

            // Hand and buttons
            var handObj = CreateUIElement("LocalHand", gameplayObj.transform);
            var handRect = handObj.GetComponent<RectTransform>();
            handRect.anchorMin = new Vector2(0, 0);
            handRect.anchorMax = new Vector2(1, 0.25f);
            handRect.offsetMin = Vector2.zero;
            handRect.offsetMax = Vector2.zero;

            // Add arc layout for hand cards
            var arcLayout = handObj.AddComponent<Flippy.CardDuelMobile.UI.HandArcLayout>();
            arcLayout.arcRadius = 300f;
            arcLayout.arcAngle = 60f;

            var endTurnBtn = CreateButton(gameplayObj.transform, "EndTurnButton", "End Turn", new Vector2(200, 60));
            var endTurnRect = endTurnBtn.GetComponent<RectTransform>();
            endTurnRect.anchorMin = new Vector2(1, 0);
            endTurnRect.anchorMax = new Vector2(1, 0);
            endTurnRect.anchoredPosition = new Vector2(-220, 20);

            // Create debug panel
            CreateDebugPanel(gameplayObj.transform);

            return gameplayObj;
        }

        private static void CreateDragLayer(Canvas canvas)
        {
            var existing = canvas.transform.Find("DragLayer");
            if (existing != null)
                return;

            var dragLayer = new GameObject("DragLayer");
            dragLayer.transform.SetParent(canvas.transform);
            var dragRect = dragLayer.AddComponent<RectTransform>();
            SetRectTransformFullScreen(dragRect);

            var dragCanvasGroup = dragLayer.AddComponent<CanvasGroup>();
            dragCanvasGroup.blocksRaycasts = false;

            Undo.RegisterCreatedObjectUndo(dragLayer, "Create DragLayer");
        }

        private static void CreateDebugPanel(Transform parent)
        {
            var existing = parent.Find("DebugPanel");
            if (existing != null)
                return;

            var debugObj = CreateUIElement("DebugPanel", parent);
            var debugRect = debugObj.GetComponent<RectTransform>();
            debugRect.anchorMin = Vector2.zero;
            debugRect.anchorMax = new Vector2(0.35f, 0.8f);
            debugRect.offsetMin = Vector2.zero;
            debugRect.offsetMax = Vector2.zero;

            var bgImage = debugObj.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);

            var canvasGroup = debugObj.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            debugObj.AddComponent<Flippy.CardDuelMobile.UI.DebugPanel>();

            // Add scroll view for debug options
            var scrollObj = CreateUIElement("ScrollView", debugObj.transform);
            var scrollRect = scrollObj.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(10, 10);
            scrollRect.offsetMax = new Vector2(-10, -50);

            var scroll = scrollObj.AddComponent<ScrollRect>();
            scroll.horizontal = false;

            var viewportObj = CreateUIElement("Viewport", scrollObj.transform);
            var viewportRect = viewportObj.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            var viewportImage = viewportObj.AddComponent<Image>();
            viewportImage.color = Color.clear;

            scroll.viewport = viewportRect;

            var contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewportObj.transform);
            var contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 1000);

            var verticalLayout = contentObj.AddComponent<VerticalLayoutGroup>();
            verticalLayout.childForceExpandWidth = true;
            verticalLayout.childForceExpandHeight = false;

            var layoutGroup = contentObj.AddComponent<LayoutGroup>();
            scroll.content = contentRect;

            // Add debug buttons
            CreateDebugButton(contentObj.transform, "Print State", () => Debug.Log("DEBUG: Check console for full game state"));
            CreateDebugButton(contentObj.transform, "P0 +10 HP", () => Debug.Log("DEBUG: Use console to run DebugPanel methods"));
            CreateDebugButton(contentObj.transform, "P0 -10 HP", () => Debug.Log("DEBUG: Use console to run DebugPanel methods"));
            CreateDebugButton(contentObj.transform, "P0 +5 Mana", () => Debug.Log("DEBUG: Use console to run DebugPanel methods"));
            CreateDebugButton(contentObj.transform, "P0 Clear Board", () => Debug.Log("DEBUG: Use console to run DebugPanel methods"));
            CreateDebugButton(contentObj.transform, "P1 +10 HP", () => Debug.Log("DEBUG: Use console to run DebugPanel methods"));
            CreateDebugButton(contentObj.transform, "P1 -10 HP", () => Debug.Log("DEBUG: Use console to run DebugPanel methods"));
            CreateDebugButton(contentObj.transform, "End Turn", () => Debug.Log("DEBUG: Use console to run DebugPanel methods"));

            var closeBtn = CreateButton(debugObj.transform, "CloseButton", "X", new Vector2(40, 40));
            var closeRect = closeBtn.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1, 1);
            closeRect.anchorMax = new Vector2(1, 1);
            closeRect.anchoredPosition = new Vector2(-10, -10);
            closeRect.sizeDelta = new Vector2(40, 40);

            Undo.RegisterCreatedObjectUndo(debugObj, "Create DebugPanel");
        }

        private static Button CreateDebugButton(Transform parent, string label, System.Action onClick)
        {
            var btn = CreateButton(parent, $"DebugBtn_{label}", label);
            if (onClick != null)
            {
                btn.onClick.AddListener(() => onClick?.Invoke());
            }
            return btn;
        }

        // ============ UI ELEMENT CREATION ============

        private static GameObject CreateUIElement(string name, Transform parent, bool fullScreen = false)
        {
            var go = new GameObject(name);
            var rect = go.AddComponent<RectTransform>();
            go.transform.SetParent(parent);

            if (fullScreen)
            {
                SetRectTransformFullScreen(rect);
            }
            else
            {
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(600, 400);
            }

            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            return go;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2? size = null)
        {
            // Check if already exists
            var existing = parent.Find(name);
            if (existing != null)
                return existing.GetComponent<Button>();

            var btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent);
            var rect = btnObj.AddComponent<RectTransform>();

            // GridLayoutGroup handles sizing via cellSize, VerticalLayoutGroup uses LayoutElement
            var gridLayout = parent.GetComponent<GridLayoutGroup>();
            var verticalLayout = parent.GetComponent<VerticalLayoutGroup>();

            if (gridLayout != null)
            {
                // GridLayoutGroup - no LayoutElement needed
                rect.sizeDelta = Vector2.zero;
            }
            else if (verticalLayout != null)
            {
                var layout = btnObj.AddComponent<LayoutElement>();
                layout.preferredHeight = 80;
                layout.preferredWidth = -1;
            }
            else
            {
                rect.sizeDelta = size ?? new Vector2(200, 80);
                rect.anchoredPosition = Vector2.zero;
            }

            var image = btnObj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.6f, 1f, 1f);

            var button = btnObj.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Button.Transition.ColorTint;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.normalColor = new Color(0.2f, 0.6f, 1f, 1f);
            colors.highlightedColor = new Color(0.3f, 0.7f, 1f, 1f);
            colors.pressedColor = new Color(0.1f, 0.4f, 0.8f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            button.colors = colors;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 48;
            text.alignment = TextAlignmentOptions.Center;

            Undo.RegisterCreatedObjectUndo(btnObj, $"Create {name}");
            return button;
        }

        private static InputField CreateInputField(Transform parent, string name, string placeholder)
        {
            var existing = parent.Find(name);
            if (existing != null)
                return existing.GetComponent<InputField>();

            var ifObj = new GameObject(name);
            ifObj.transform.SetParent(parent);
            var rect = ifObj.AddComponent<RectTransform>();

            var gridLayout = parent.GetComponent<GridLayoutGroup>();
            var verticalLayout = parent.GetComponent<VerticalLayoutGroup>();

            if (gridLayout != null)
            {
                rect.sizeDelta = Vector2.zero;
            }
            else if (verticalLayout != null)
            {
                var layout = ifObj.AddComponent<LayoutElement>();
                layout.preferredHeight = 80;
                layout.preferredWidth = -1;
            }
            else
            {
                rect.sizeDelta = new Vector2(400, 80);
            }

            var image = ifObj.AddComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            var inputField = ifObj.AddComponent<InputField>();
            inputField.targetGraphic = image;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(ifObj.transform);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObj.AddComponent<Text>();
            text.text = "";
            text.fontSize = 32;
            text.font = null; // Use default font
            inputField.textComponent = text;

            var placeholderObj = new GameObject("Placeholder");
            placeholderObj.transform.SetParent(ifObj.transform);
            var phRect = placeholderObj.AddComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.offsetMin = Vector2.zero;
            phRect.offsetMax = Vector2.zero;

            var phText = placeholderObj.AddComponent<Text>();
            phText.text = placeholder;
            phText.fontSize = 32;
            phText.color = new Color(1, 1, 1, 0.5f);
            phText.font = null; // Use default font
            inputField.placeholder = phText;

            Undo.RegisterCreatedObjectUndo(ifObj, $"Create {name}");
            return inputField;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string content)
        {
            var existing = parent.Find(name);
            if (existing != null)
                return existing.GetComponent<TextMeshProUGUI>();

            var textObj = new GameObject(name);
            textObj.transform.SetParent(parent);
            var rect = textObj.AddComponent<RectTransform>();

            var gridLayout = parent.GetComponent<GridLayoutGroup>();
            var verticalLayout = parent.GetComponent<VerticalLayoutGroup>();

            if (gridLayout != null)
            {
                rect.sizeDelta = Vector2.zero;
            }
            else if (verticalLayout != null)
            {
                var layout = textObj.AddComponent<LayoutElement>();
                layout.preferredHeight = 60;
                layout.preferredWidth = -1;
            }
            else
            {
                rect.sizeDelta = new Vector2(400, 100);
            }

            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = 56;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            Undo.RegisterCreatedObjectUndo(textObj, $"Create {name}");
            return text;
        }

        private static void CreateBoardSlot(Transform parent, string name, BoardSlot slot, bool isLocal, Vector2? size = null)
        {
            var existing = parent.Find(name);
            if (existing != null)
                return;

            var slotObj = new GameObject(name);
            slotObj.transform.SetParent(parent);
            var rect = slotObj.AddComponent<RectTransform>();
            rect.sizeDelta = size ?? new Vector2(400, 560);
            rect.anchoredPosition = Vector2.zero;

            // Button
            var image = slotObj.AddComponent<Image>();
            image.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            var button = slotObj.AddComponent<Button>();
            button.targetGraphic = image;

            // Hover Glow
            var glowObj = new GameObject("HoverGlow");
            glowObj.transform.SetParent(slotObj.transform);
            var glowRect = glowObj.AddComponent<RectTransform>();
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.offsetMin = Vector2.zero;
            glowRect.offsetMax = Vector2.zero;
            var glowImage = glowObj.AddComponent<Image>();
            glowImage.color = new Color(1, 1, 0, 0f);

            // Label
            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(slotObj.transform);
            var labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = slot.ToString();
            labelText.fontSize = 48;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = new Color(1, 1, 1, 0.8f);

            // CardAnchor
            var anchorObj = new GameObject("CardAnchor");
            anchorObj.transform.SetParent(slotObj.transform);
            var anchorRect = anchorObj.AddComponent<RectTransform>();
            anchorRect.anchorMin = Vector2.zero;
            anchorRect.anchorMax = Vector2.one;
            anchorRect.offsetMin = Vector2.zero;
            anchorRect.offsetMax = Vector2.zero;

            // BoardSlotButton component
            var boardSlotBtn = slotObj.AddComponent<Flippy.CardDuelMobile.UI.BoardSlotButton>();
            boardSlotBtn.slot = slot;
            boardSlotBtn.isLocalSide = isLocal;
            boardSlotBtn.button = button;
            boardSlotBtn.placeholderImage = image;
            boardSlotBtn.labelTextTMP = labelText;
            boardSlotBtn.cardAnchor = anchorRect;
            boardSlotBtn.hoverGlowImage = glowImage;

            Undo.RegisterCreatedObjectUndo(slotObj, $"Create {name}");
        }

        // ============ SCRIPT WIRING ============

        private static void WireMatchmakingPanel(Canvas canvas)
        {
            var menuPanelObj = GameObject.Find("Canvas/Root/MenuPanel");
            if (menuPanelObj == null) return;

            var controller = menuPanelObj.GetComponent<Flippy.CardDuelMobile.UI.MatchmakingPanelController>();
            if (controller == null)
                controller = menuPanelObj.AddComponent<Flippy.CardDuelMobile.UI.MatchmakingPanelController>();

            // Wire session service
            var sessionService = FindFirstObjectByType<Flippy.CardDuelMobile.Networking.MpsGameSessionService>();
            controller.sessionService = sessionService;

            // Wire button references
            controller.quickMatchButton = GameObject.Find("Canvas/Root/MenuPanel/QuickMatchButton")?.GetComponent<Button>();
            controller.createPrivateButton = GameObject.Find("Canvas/Root/MenuPanel/CreatePrivateButton")?.GetComponent<Button>();
            controller.joinByCodeButton = GameObject.Find("Canvas/Root/MenuPanel/JoinByCodeButton")?.GetComponent<Button>();
            controller.advancedQueueButton = GameObject.Find("Canvas/Root/MenuPanel/AdvancedQueueButton")?.GetComponent<Button>();
            controller.readyButton = GameObject.Find("Canvas/Root/MenuPanel/ReadyUpButton")?.GetComponent<Button>();
            controller.localMatchButton = GameObject.Find("Canvas/Root/MenuPanel/LocalMatch")?.GetComponent<Button>();
            controller.leaveButton = GameObject.Find("Canvas/Root/MenuPanel/LeaveButton")?.GetComponent<Button>();
            controller.copyJoinCodeButton = GameObject.Find("Canvas/Root/MenuPanel/CopyJoinCodeButton")?.GetComponent<Button>();

            // Wire input fields
            controller.joinCodeField = GameObject.Find("Canvas/Root/MenuPanel/JoinCodeField")?.GetComponent<InputField>();
            controller.privateMatchNameField = GameObject.Find("Canvas/Root/MenuPanel/PrivateMatchField")?.GetComponent<InputField>();

            // Wire text fields
            controller.statusText = GameObject.Find("Canvas/Root/MenuPanel/StatusText")?.GetComponent<TextMeshProUGUI>();
            controller.joinCodeText = GameObject.Find("Canvas/Root/MenuPanel/JoinCodeText")?.GetComponent<TextMeshProUGUI>();

            // Wire visibility arrays
            var hideWhenInSession = new List<GameObject>();
            hideWhenInSession.Add(GameObject.Find("Canvas/Root/MenuPanel/QuickMatchButton"));
            hideWhenInSession.Add(GameObject.Find("Canvas/Root/MenuPanel/CreatePrivateButton"));
            hideWhenInSession.Add(GameObject.Find("Canvas/Root/MenuPanel/JoinByCodeButton"));
            hideWhenInSession.Add(GameObject.Find("Canvas/Root/MenuPanel/AdvancedQueueButton"));
            hideWhenInSession.Add(GameObject.Find("Canvas/Root/MenuPanel/LocalMatch"));
            controller.hideWhenInSession = hideWhenInSession.ToArray();

            var showWhenInSession = new List<GameObject>();
            showWhenInSession.Add(GameObject.Find("Canvas/Root/MenuPanel/LeaveButton"));
            showWhenInSession.Add(GameObject.Find("Canvas/Root/MenuPanel/ReadyUpButton"));
            controller.showWhenInSession = showWhenInSession.ToArray();

            EditorUtility.SetDirty(controller);
        }

        private static void WireUIManager(Canvas canvas)
        {
            var uiManager = canvas.GetComponent<Flippy.CardDuelMobile.UI.UIManager>();
            if (uiManager == null) return;

            uiManager.menuPanel = GameObject.Find("Canvas/Root/MenuPanel");
            uiManager.gameplayPanel = GameObject.Find("Canvas/Root/GameplayPanel");

            EditorUtility.SetDirty(uiManager);
            Debug.Log("[GameSetup] Wired UIManager");
        }

        private static void WireBattleScreenPresenter(GameObject gameplayPanel)
        {
            if (gameplayPanel == null)
            {
                Debug.LogError("[GameSetup] gameplayPanel is null, cannot wire BattleScreenPresenter");
                return;
            }

            var presenter = gameplayPanel.GetComponent<Flippy.CardDuelMobile.UI.BattleScreenPresenter>();
            if (presenter == null)
            {
                presenter = gameplayPanel.AddComponent<Flippy.CardDuelMobile.UI.BattleScreenPresenter>();
                Debug.Log("[GameSetup] Added BattleScreenPresenter to GameplayPanel");
            }
            else
            {
                Debug.Log("[GameSetup] BattleScreenPresenter already exists, reusing");
            }

            // Wire hand root
            presenter.localHandRoot = gameplayPanel.transform.Find("LocalHand");

            // Wire drag layer
            var dragLayer = GameObject.Find("Canvas/DragLayer");
            if (dragLayer != null)
                presenter.dragLayer = dragLayer.GetComponent<RectTransform>();

            // Wire HUD texts
            presenter.battleLogText = gameplayPanel.transform.Find("BattleLog")?.GetComponent<TextMeshProUGUI>();
            presenter.turnInfoText = gameplayPanel.transform.Find("TurnInfo")?.GetComponent<TextMeshProUGUI>();
            presenter.heroInfoText = gameplayPanel.transform.Find("HeroInfo")?.GetComponent<TextMeshProUGUI>();
            presenter.selectedCardText = gameplayPanel.transform.Find("SelectedCard")?.GetComponent<TextMeshProUGUI>();

            // Wire end turn button
            presenter.endTurnButton = gameplayPanel.transform.Find("EndTurnButton")?.GetComponent<Button>();

            EditorUtility.SetDirty(presenter);
        }

        private static void WireBoardSlots(GameObject gameplayPanel)
        {
            var presenter = gameplayPanel.GetComponent<Flippy.CardDuelMobile.UI.BattleScreenPresenter>();
            if (presenter == null) return;

            // Local slots
            var localFront = gameplayPanel.transform.Find("LocalBoardArea/LocalFrontSlot")?.GetComponent<Flippy.CardDuelMobile.UI.BoardSlotButton>();
            var localBackLeft = gameplayPanel.transform.Find("LocalBoardArea/LocalBackLeftSlot")?.GetComponent<Flippy.CardDuelMobile.UI.BoardSlotButton>();
            var localBackRight = gameplayPanel.transform.Find("LocalBoardArea/LocalBackRightSlot")?.GetComponent<Flippy.CardDuelMobile.UI.BoardSlotButton>();

            presenter.localFrontSlot = localFront;
            presenter.localBackLeftSlot = localBackLeft;
            presenter.localBackRightSlot = localBackRight;

            // Remote slots
            var remoteFront = gameplayPanel.transform.Find("RemoteBoardArea/RemoteFrontSlot")?.GetComponent<Flippy.CardDuelMobile.UI.BoardSlotButton>();
            var remoteBackLeft = gameplayPanel.transform.Find("RemoteBoardArea/RemoteBackLeftSlot")?.GetComponent<Flippy.CardDuelMobile.UI.BoardSlotButton>();
            var remoteBackRight = gameplayPanel.transform.Find("RemoteBoardArea/RemoteBackRightSlot")?.GetComponent<Flippy.CardDuelMobile.UI.BoardSlotButton>();

            presenter.remoteFrontSlot = remoteFront;
            presenter.remoteBackLeftSlot = remoteBackLeft;
            presenter.remoteBackRightSlot = remoteBackRight;

            EditorUtility.SetDirty(presenter);
        }

        // ============ PRESET LAYOUTS ============

        private static void ApplyPresetLayout(GameObject gameplayPanel, GameSetupPreset preset)
        {
            var layoutData = GetPresetLayout(preset);
            if (layoutData == null) return;

            ApplySlotPositions(gameplayPanel, "RemoteBoardArea", layoutData.remotePositions);
            ApplySlotPositions(gameplayPanel, "LocalBoardArea", layoutData.localPositions);
        }

        private static void ApplySlotPositions(GameObject gameplayPanel, string areaName, SlotPositions positions)
        {
            var area = gameplayPanel.transform.Find(areaName);
            if (area == null) return;

            var frontSlot = area.Find(areaName == "RemoteBoardArea" ? "RemoteFrontSlot" : "LocalFrontSlot");
            var backLeftSlot = area.Find(areaName == "RemoteBoardArea" ? "RemoteBackLeftSlot" : "LocalBackLeftSlot");
            var backRightSlot = area.Find(areaName == "RemoteBoardArea" ? "RemoteBackRightSlot" : "LocalBackRightSlot");

            if (frontSlot != null)
                frontSlot.GetComponent<RectTransform>().anchoredPosition = positions.front;
            if (backLeftSlot != null)
                backLeftSlot.GetComponent<RectTransform>().anchoredPosition = positions.backLeft;
            if (backRightSlot != null)
                backRightSlot.GetComponent<RectTransform>().anchoredPosition = positions.backRight;
        }

        private static PresetLayoutData GetPresetLayout(GameSetupPreset preset)
        {
            return preset switch
            {
                GameSetupPreset.HearthstoneStyle => new PresetLayoutData
                {
                    remotePositions = new SlotPositions(Vector2.zero, new Vector2(-180, 0), new Vector2(180, 0)),
                    localPositions = new SlotPositions(Vector2.zero, new Vector2(-180, 0), new Vector2(180, 0))
                },
                GameSetupPreset.TriangleFormation => new PresetLayoutData
                {
                    remotePositions = new SlotPositions(new Vector2(0, 60), new Vector2(-150, -60), new Vector2(150, -60)),
                    localPositions = new SlotPositions(new Vector2(0, -60), new Vector2(-150, 60), new Vector2(150, 60))
                },
                GameSetupPreset.LinearDefense => new PresetLayoutData
                {
                    remotePositions = new SlotPositions(new Vector2(-200, 0), new Vector2(50, 0), new Vector2(220, 0)),
                    localPositions = new SlotPositions(new Vector2(-200, 0), new Vector2(50, 0), new Vector2(220, 0))
                },
                GameSetupPreset.CircularArena => new PresetLayoutData
                {
                    remotePositions = new SlotPositions(new Vector2(0, -80), new Vector2(-160, 60), new Vector2(160, 60)),
                    localPositions = new SlotPositions(new Vector2(0, 80), new Vector2(-160, -60), new Vector2(160, -60))
                },
                GameSetupPreset.TieredHeights => new PresetLayoutData
                {
                    remotePositions = new SlotPositions(new Vector2(0, 70), new Vector2(-160, -40), new Vector2(160, -40)),
                    localPositions = new SlotPositions(new Vector2(0, -70), new Vector2(-160, 40), new Vector2(160, 40))
                },
                GameSetupPreset.SymmetricalBalance => new PresetLayoutData
                {
                    remotePositions = new SlotPositions(new Vector2(0, 30), new Vector2(-180, -50), new Vector2(180, -50)),
                    localPositions = new SlotPositions(new Vector2(0, -30), new Vector2(-180, 50), new Vector2(180, 50))
                },
                GameSetupPreset.DynamicCombat => new PresetLayoutData
                {
                    remotePositions = new SlotPositions(Vector2.zero, new Vector2(-220, 0), new Vector2(220, 0)),
                    localPositions = new SlotPositions(Vector2.zero, new Vector2(-220, 0), new Vector2(220, 0))
                },
                GameSetupPreset.CompactVertical => new PresetLayoutData
                {
                    remotePositions = new SlotPositions(Vector2.zero, new Vector2(-120, 0), new Vector2(120, 0)),
                    localPositions = new SlotPositions(Vector2.zero, new Vector2(-120, 0), new Vector2(120, 0))
                },
                GameSetupPreset.TacticalIsometric => new PresetLayoutData
                {
                    remotePositions = new SlotPositions(new Vector2(30, -30), new Vector2(-150, 0), new Vector2(150, 0)),
                    localPositions = new SlotPositions(new Vector2(-30, 30), new Vector2(-150, 0), new Vector2(150, 0))
                },
                GameSetupPreset.MinimalistUI => new PresetLayoutData
                {
                    remotePositions = new SlotPositions(Vector2.zero, new Vector2(-160, 0), new Vector2(160, 0)),
                    localPositions = new SlotPositions(Vector2.zero, new Vector2(-160, 0), new Vector2(160, 0))
                },
                _ => null
            };
        }

        private static void SetRectTransformFullScreen(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRectTransformCorner(RectTransform rect, Vector2 position, Vector2 size, float anchorX = 0, float anchorY = 1)
        {
            rect.anchorMin = new Vector2(anchorX, anchorY);
            rect.anchorMax = new Vector2(anchorX, anchorY);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        // ============ HELPERS ============

        private class SlotPositions
        {
            public Vector2 front;
            public Vector2 backLeft;
            public Vector2 backRight;

            public SlotPositions(Vector2 f, Vector2 bl, Vector2 br)
            {
                front = f;
                backLeft = bl;
                backRight = br;
            }
        }

        private class PresetLayoutData
        {
            public SlotPositions remotePositions;
            public SlotPositions localPositions;
        }

        private static T FindFirstObjectByType<T>() where T : Component
        {
            var all = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            return all.Length > 0 ? all[0] : null;
        }

        private static void CreateCardDuelNetworkCoordinatorPrefab()
        {
            var prefabPath = "Assets/Prefabs";
            var path = $"{prefabPath}/CardDuelNetworkCoordinator.prefab";

            if (AssetDatabase.LoadAssetAtPath(path, typeof(GameObject)) != null)
                return;

            if (!AssetDatabase.IsValidFolder(prefabPath))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            var coordObj = new GameObject("CardDuelNetworkCoordinator");
            coordObj.AddComponent<Flippy.CardDuelMobile.Networking.CardDuelNetworkCoordinator>();
            coordObj.AddComponent<Unity.Netcode.NetworkObject>();

            PrefabUtility.SaveAsPrefabAsset(coordObj, path);
            Object.DestroyImmediate(coordObj);
            Debug.Log($"[GameSetup] Created CardDuelNetworkCoordinator prefab at {path}");
        }

        private static void CreateCardPrefabs(bool overwrite = false)
        {
            var prefabPath = "Assets/Prefabs/Cards";
            if (!AssetDatabase.IsValidFolder(prefabPath))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                    AssetDatabase.CreateFolder("Assets", "Prefabs");
                AssetDatabase.CreateFolder("Assets/Prefabs", "Cards");
            }

            // Always delete old prefabs when overwriting
            if (overwrite)
            {
                AssetDatabase.DeleteAsset($"{prefabPath}/HandCardButton.prefab");
                AssetDatabase.DeleteAsset($"{prefabPath}/CardViewWidget.prefab");
                AssetDatabase.DeleteAsset($"{prefabPath}/DragGhost.prefab");
                AssetDatabase.Refresh();
            }

            // HandCardButton prefab - card in hand with drag support
            CreateHandCardButtonPrefab(prefabPath);

            // CardViewWidget prefab - card on board
            CreateCardViewWidgetPrefab(prefabPath);

            // DragGhost prefab - drag preview
            CreateDragGhostPrefab(prefabPath);

            AssetDatabase.Refresh();
            Debug.Log("[GameSetup] Card prefabs created");
        }

        private static void CreateHandCardButtonPrefab(string prefabPath)
        {
            var path = $"{prefabPath}/HandCardButton.prefab";
            if (AssetDatabase.LoadAssetAtPath(path, typeof(GameObject)) != null)
                return;

            var handCardObj = new GameObject("HandCardButton");
            var rect = handCardObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(120, 160);

            var image = handCardObj.AddComponent<Image>();
            image.color = new Color(0.25f, 0.35f, 0.5f, 1f);

            var button = handCardObj.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Button.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = new Color(0.25f, 0.35f, 0.5f, 1f);
            colors.highlightedColor = new Color(0.35f, 0.45f, 0.6f, 1f);
            colors.pressedColor = new Color(0.15f, 0.25f, 0.4f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            button.colors = colors;

            handCardObj.AddComponent<CanvasGroup>();

            // Create CardViewWidget child to display card info
            var cardViewObj = new GameObject("CardView");
            cardViewObj.transform.SetParent(handCardObj.transform);
            var cardViewRect = cardViewObj.AddComponent<RectTransform>();
            cardViewRect.anchorMin = Vector2.zero;
            cardViewRect.anchorMax = Vector2.one;
            cardViewRect.offsetMin = Vector2.zero;
            cardViewRect.offsetMax = Vector2.zero;

            var cardViewImage = cardViewObj.AddComponent<Image>();
            cardViewImage.color = new Color(0.2f, 0.2f, 0.3f, 0);

            var cardViewWidget = cardViewObj.AddComponent<Flippy.CardDuelMobile.UI.CardViewWidget>();
            cardViewWidget.frameImage = image;
            cardViewWidget.artImage = cardViewImage;

            // Create minimal text fields for cardView
            CreateCardViewTextField(cardViewObj.transform, "Title", out var titleText);
            CreateCardViewTextField(cardViewObj.transform, "Cost", out var costText);
            CreateCardViewTextField(cardViewObj.transform, "Attack", out var attackText);
            CreateCardViewTextField(cardViewObj.transform, "Health", out var healthText);
            CreateCardViewTextField(cardViewObj.transform, "Armor", out var armorText);

            cardViewWidget.titleText = titleText;
            cardViewWidget.costText = costText;
            cardViewWidget.attackText = attackText;
            cardViewWidget.healthText = healthText;
            cardViewWidget.armorText = armorText;

            // Wire HandCardButton component
            var handCardButton = handCardObj.AddComponent<Flippy.CardDuelMobile.UI.HandCardButton>();
            handCardButton.button = button;
            handCardButton.backgroundImage = image;
            handCardButton.canvasGroup = handCardObj.GetComponent<CanvasGroup>();
            handCardButton.cardView = cardViewWidget;

            PrefabUtility.SaveAsPrefabAsset(handCardObj, path);
            Object.DestroyImmediate(handCardObj);
            Debug.Log($"[GameSetup] Created HandCardButton prefab at {path}");
        }

        private static void CreateCardViewTextField(Transform parent, string name, out TextMeshProUGUI textComponent)
        {
            var textObj = new GameObject(name);
            textObj.transform.SetParent(parent);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            textComponent = textObj.AddComponent<TextMeshProUGUI>();
            textComponent.text = "";
            textComponent.fontSize = 10;
            textComponent.alignment = TextAlignmentOptions.Center;
        }

        private static void CreateCardViewWidgetPrefab(string prefabPath)
        {
            var path = $"{prefabPath}/CardViewWidget.prefab";
            if (AssetDatabase.LoadAssetAtPath(path, typeof(GameObject)) != null)
                return;

            var cardViewObj = new GameObject("CardViewWidget");
            var rect = cardViewObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(120, 160);

            var frameImage = cardViewObj.AddComponent<Image>();
            frameImage.color = new Color(0.35f, 0.35f, 0.45f, 1f);

            // Art image (background)
            var artObj = new GameObject("Art");
            artObj.transform.SetParent(cardViewObj.transform);
            var artRect = artObj.AddComponent<RectTransform>();
            artRect.anchorMin = Vector2.zero;
            artRect.anchorMax = Vector2.one;
            artRect.offsetMin = Vector2.zero;
            artRect.offsetMax = Vector2.zero;
            var artImage = artObj.AddComponent<Image>();
            artImage.color = new Color(0.2f, 0.2f, 0.3f, 1f);

            // Title text
            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(cardViewObj.transform);
            var titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.sizeDelta = new Vector2(0, 30);
            titleRect.anchoredPosition = new Vector2(0, -5);
            var titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "Card";
            titleText.fontSize = 16;
            titleText.alignment = TextAlignmentOptions.Center;

            // Cost text
            var costObj = new GameObject("Cost");
            costObj.transform.SetParent(cardViewObj.transform);
            var costRect = costObj.AddComponent<RectTransform>();
            costRect.anchorMin = Vector2.zero;
            costRect.anchorMax = new Vector2(0, 1);
            costRect.sizeDelta = new Vector2(25, 25);
            costRect.anchoredPosition = new Vector2(5, -5);
            var costText = costObj.AddComponent<TextMeshProUGUI>();
            costText.text = "0";
            costText.fontSize = 14;
            costText.alignment = TextAlignmentOptions.Center;

            // Attack text
            var attackObj = new GameObject("Attack");
            attackObj.transform.SetParent(cardViewObj.transform);
            var attackRect = attackObj.AddComponent<RectTransform>();
            attackRect.anchorMin = new Vector2(0, 0);
            attackRect.anchorMax = new Vector2(0.5f, 0);
            attackRect.sizeDelta = new Vector2(30, 25);
            attackRect.anchoredPosition = new Vector2(15, 5);
            var attackText = attackObj.AddComponent<TextMeshProUGUI>();
            attackText.text = "0";
            attackText.fontSize = 14;
            attackText.alignment = TextAlignmentOptions.Center;

            // Health text
            var healthObj = new GameObject("Health");
            healthObj.transform.SetParent(cardViewObj.transform);
            var healthRect = healthObj.AddComponent<RectTransform>();
            healthRect.anchorMin = new Vector2(0.5f, 0);
            healthRect.anchorMax = Vector2.one;
            healthRect.sizeDelta = new Vector2(30, 25);
            healthRect.anchoredPosition = new Vector2(-15, 5);
            var healthText = healthObj.AddComponent<TextMeshProUGUI>();
            healthText.text = "0";
            healthText.fontSize = 14;
            healthText.alignment = TextAlignmentOptions.Center;

            // Armor text
            var armorObj = new GameObject("Armor");
            armorObj.transform.SetParent(cardViewObj.transform);
            var armorRect = armorObj.AddComponent<RectTransform>();
            armorRect.anchorMin = new Vector2(1, 0);
            armorRect.anchorMax = Vector2.one;
            armorRect.sizeDelta = new Vector2(25, 25);
            armorRect.anchoredPosition = new Vector2(-5, 5);
            var armorText = armorObj.AddComponent<TextMeshProUGUI>();
            armorText.text = "";
            armorText.fontSize = 14;
            armorText.alignment = TextAlignmentOptions.Center;

            var cardViewWidget = cardViewObj.AddComponent<Flippy.CardDuelMobile.UI.CardViewWidget>();
            cardViewWidget.frameImage = frameImage;
            cardViewWidget.artImage = artImage;
            cardViewWidget.titleText = titleText;
            cardViewWidget.costText = costText;
            cardViewWidget.attackText = attackText;
            cardViewWidget.healthText = healthText;
            cardViewWidget.armorText = armorText;

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
            rect.sizeDelta = new Vector2(120, 160);

            var image = dragGhostObj.AddComponent<Image>();
            image.color = new Color(0.5f, 0.6f, 0.8f, 0.8f);

            var canvasGroup = dragGhostObj.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;

            // Create minimal text fields for CardViewWidget (drag preview doesn't display but component needs fields)
            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(dragGhostObj.transform);
            var titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "";

            var costObj = new GameObject("Cost");
            costObj.transform.SetParent(dragGhostObj.transform);
            var costText = costObj.AddComponent<TextMeshProUGUI>();
            costText.text = "";

            var attackObj = new GameObject("Attack");
            attackObj.transform.SetParent(dragGhostObj.transform);
            var attackText = attackObj.AddComponent<TextMeshProUGUI>();
            attackText.text = "";

            var healthObj = new GameObject("Health");
            healthObj.transform.SetParent(dragGhostObj.transform);
            var healthText = healthObj.AddComponent<TextMeshProUGUI>();
            healthText.text = "";

            var armorObj = new GameObject("Armor");
            armorObj.transform.SetParent(dragGhostObj.transform);
            var armorText = armorObj.AddComponent<TextMeshProUGUI>();
            armorText.text = "";

            var cardViewWidget = dragGhostObj.AddComponent<Flippy.CardDuelMobile.UI.CardViewWidget>();
            cardViewWidget.frameImage = image;
            cardViewWidget.artImage = image;
            cardViewWidget.titleText = titleText;
            cardViewWidget.costText = costText;
            cardViewWidget.attackText = attackText;
            cardViewWidget.healthText = healthText;
            cardViewWidget.armorText = armorText;

            PrefabUtility.SaveAsPrefabAsset(dragGhostObj, path);
            Object.DestroyImmediate(dragGhostObj);
            Debug.Log($"[GameSetup] Created DragGhost prefab at {path}");
        }

        private static void WireCardPrefabs(GameObject gameplayPanel)
        {
            var presenter = gameplayPanel.GetComponent<Flippy.CardDuelMobile.UI.BattleScreenPresenter>();
            if (presenter == null) return;

            var prefabPath = "Assets/Prefabs/Cards";

            // Wire HandCardButton prefab
            var handCardPrefab = AssetDatabase.LoadAssetAtPath($"{prefabPath}/HandCardButton.prefab", typeof(GameObject)) as GameObject;
            Debug.Log($"[GameSetup] Loading HandCardButton prefab from {prefabPath}/HandCardButton.prefab: {(handCardPrefab != null ? "SUCCESS" : "FAILED")}");
            if (handCardPrefab != null)
            {
                var handCardButton = handCardPrefab.GetComponent<Flippy.CardDuelMobile.UI.HandCardButton>();
                Debug.Log($"[GameSetup] HandCardButton component found: {(handCardButton != null ? "YES" : "NO")}");
                if (handCardButton != null)
                {
                    presenter.handCardPrefab = handCardButton;
                    Debug.Log($"[GameSetup] Assigned handCardPrefab to presenter");
                }
            }

            // Wire CardViewWidget prefab (for board cards)
            var boardCardPrefab = AssetDatabase.LoadAssetAtPath($"{prefabPath}/CardViewWidget.prefab", typeof(GameObject)) as GameObject;
            if (boardCardPrefab != null)
            {
                var cardViewWidget = boardCardPrefab.GetComponent<Flippy.CardDuelMobile.UI.CardViewWidget>();
                if (cardViewWidget != null)
                    presenter.boardCardPrefab = cardViewWidget;
            }

            // DragGhost3D prefab must be assigned manually in inspector (3D GameObject with DragGhost3D script)

            EditorUtility.SetDirty(presenter);
        }
    }
}
