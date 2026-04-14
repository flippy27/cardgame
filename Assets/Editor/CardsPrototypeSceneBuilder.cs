using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Flippy.CardDuelMobile.Data;
using Flippy.CardDuelMobile.Input;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.UI;

namespace Flippy.CardDuelMobile.EditorTools
{
    /// <summary>
    /// Construye una escena de prototipo compatible con el BattleScreenPresenter actual
    /// (slots fijos en escena + drag and drop + matchmaking básico).
    /// </summary>
    public static class CardsPrototypeSceneBuilder
    {
        private static readonly Color PanelColor = new Color(0.12f, 0.13f, 0.17f, 0.82f);
        private static readonly Color BoardAreaColor = new Color(0.10f, 0.11f, 0.14f, 0.88f);
        private static readonly Color HandAreaColor = new Color(0.09f, 0.10f, 0.12f, 0.92f);
        private static readonly Color ButtonColor = new Color(0.24f, 0.36f, 0.62f, 1f);
        private static readonly Color InputColor = new Color(1f, 1f, 1f, 0.08f);

        public static void BuildScene()
        {
            CardsPrototypeContentGenerator.GenerateAll();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "CardDuelPrototype";

            var font = ResolveBuiltinFont();
            var tmpFont = ResolveTmpFont();

            CreateCamera();
            CreateEventSystem();

            var playerPrefab = RebuildPlayerPrefab();
            var boardCardPrefab = RebuildBoardCardPrefab(tmpFont);
            var handCardPrefab = RebuildHandCardPrefab(tmpFont);

            var networkManager = CreateNetworkStack(playerPrefab);
            var sessionService = CreateSessionService();
            CreateBootstrap(networkManager, sessionService);
            CreateCoordinator();

            var canvas = CreateCanvas();
            var dragLayer = CreateStretchPanel("DragLayer", canvas.transform, Color.clear);
            dragLayer.SetAsLastSibling();

            var root = CreateStretchPanel("Root", canvas.transform, Color.clear);
            var rootLayout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            rootLayout.spacing = 12f;
            rootLayout.padding = new RectOffset(12, 12, 12, 12);
            rootLayout.childAlignment = TextAnchor.MiddleCenter;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = true;

            var menuPanel = CreateLayoutPanel("MenuPanel", root, PanelColor, 360f);
            var menuLayout = menuPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            menuLayout.spacing = 8f;
            menuLayout.padding = new RectOffset(12, 12, 12, 12);
            menuLayout.childControlWidth = true;
            menuLayout.childControlHeight = false;
            menuLayout.childForceExpandWidth = true;
            menuLayout.childForceExpandHeight = false;

            var gameplayPanel = CreateLayoutPanel("GameplayPanel", root, PanelColor, -1f);
            var gameplayLayout = gameplayPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            gameplayLayout.spacing = 8f;
            gameplayLayout.padding = new RectOffset(12, 12, 12, 12);
            gameplayLayout.childControlWidth = true;
            gameplayLayout.childControlHeight = false;
            gameplayLayout.childForceExpandWidth = true;
            gameplayLayout.childForceExpandHeight = false;

            BuildMenuUi(menuPanel, font, tmpFont, out var matchmakingRefs);
            BuildGameplayUi(
                gameplayPanel,
                font,
                tmpFont,
                boardCardPrefab,
                handCardPrefab,
                dragLayer,
                out var presenterRefs);

            var matchmaking = menuPanel.gameObject.AddComponent<MatchmakingPanelController>();
            matchmaking.sessionService = sessionService;
            matchmaking.joinCodeField = matchmakingRefs.joinCodeField;
            matchmaking.privateMatchNameField = matchmakingRefs.privateNameField;
            matchmaking.statusText = matchmakingRefs.statusText;
            matchmaking.joinCodeText = matchmakingRefs.joinCodeText;
            matchmaking.quickMatchButton = matchmakingRefs.quickMatchButton;
            matchmaking.createPrivateButton = matchmakingRefs.createPrivateButton;
            matchmaking.joinByCodeButton = matchmakingRefs.joinByCodeButton;
            matchmaking.advancedQueueButton = matchmakingRefs.advancedQueueButton;
            matchmaking.leaveButton = matchmakingRefs.leaveButton;
            matchmaking.copyJoinCodeButton = matchmakingRefs.copyJoinCodeButton;

            var presenter = gameplayPanel.gameObject.AddComponent<BattleScreenPresenter>();
            presenter.localHandRoot = presenterRefs.localHandRoot;
            presenter.dragLayer = dragLayer;
            presenter.localFrontSlot = presenterRefs.localFrontSlot;
            presenter.localBackLeftSlot = presenterRefs.localBackLeftSlot;
            presenter.localBackRightSlot = presenterRefs.localBackRightSlot;
            presenter.remoteFrontSlot = presenterRefs.remoteFrontSlot;
            presenter.remoteBackLeftSlot = presenterRefs.remoteBackLeftSlot;
            presenter.remoteBackRightSlot = presenterRefs.remoteBackRightSlot;
            presenter.battleLogText = presenterRefs.battleLogText;
            presenter.turnInfoText = presenterRefs.turnInfoText;
            presenter.heroInfoText = presenterRefs.heroInfoText;
            presenter.selectedCardText = presenterRefs.selectedCardText;
            presenter.endTurnButton = presenterRefs.endTurnButton;
            presenter.handCardPrefab = handCardPrefab.GetComponent<HandCardButton>();
            presenter.boardCardPrefab = boardCardPrefab.GetComponent<CardViewWidget>();
            presenter.dragGhostPrefab = boardCardPrefab.GetComponent<CardViewWidget>();

            var scenePath = CardsEditorPaths.Scenes + "/CardDuelPrototype.unity";
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(scenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Prototype scene built at " + scenePath);
        }

        private static void CreateCamera()
        {
            var cameraGo = new GameObject("Main Camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.12f, 1f);
            camera.tag = "MainCamera";
        }

        private static void CreateEventSystem()
        {
            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<InputSystemUIInputModule>();
            eventSystemGo.AddComponent<InputSystemBootstrapper>();
        }

        private static NetworkManager CreateNetworkStack(GameObject playerPrefab)
        {
            var networkManagerGo = new GameObject("NetworkManager");
            var networkManager = networkManagerGo.AddComponent<NetworkManager>();
            networkManagerGo.AddComponent<UnityTransport>();
            networkManager.NetworkConfig.PlayerPrefab = playerPrefab;
            return networkManager;
        }

        private static MpsGameSessionService CreateSessionService()
        {
            var sessionGo = new GameObject("MpsGameSessionService");
            var sessionService = sessionGo.AddComponent<MpsGameSessionService>();
            sessionService.matchmakerConfig =
                AssetDatabase.LoadAssetAtPath<MatchmakerConfig>(CardsEditorPaths.Config + "/DefaultMatchmakerConfig.asset");
            return sessionService;
        }

        private static void CreateBootstrap(NetworkManager networkManager, MpsGameSessionService sessionService)
        {
            var bootstrapGo = new GameObject("NetworkBootstrap");
            var bootstrap = bootstrapGo.AddComponent<NetworkBootstrap>();
            bootstrap.networkManager = networkManager;
            bootstrap.sessionService = sessionService;
        }

        private static void CreateCoordinator()
        {
            var coordinatorGo = new GameObject("CardDuelNetworkCoordinator");
            coordinatorGo.AddComponent<NetworkObject>();
            var coordinator = coordinatorGo.AddComponent<CardDuelNetworkCoordinator>();
            coordinator.rulesProfile =
                AssetDatabase.LoadAssetAtPath<DuelRulesProfile>(CardsEditorPaths.Rules + "/DefaultDuelRules.asset");
            coordinator.deckPlayerA =
                AssetDatabase.LoadAssetAtPath<DeckDefinition>(CardsEditorPaths.Decks + "/Deck_PlayerA.asset");
            coordinator.deckPlayerB =
                AssetDatabase.LoadAssetAtPath<DeckDefinition>(CardsEditorPaths.Decks + "/Deck_PlayerB.asset");
        }

        private static Canvas CreateCanvas()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        private static void BuildMenuUi(
     RectTransform menuPanel,
     Font font,
     TMP_FontAsset tmpFont,
     out MatchmakingRefs refs)
        {
            refs = new MatchmakingRefs();

            CreateHeader("MenuHeader", menuPanel, tmpFont, "Matchmaking", 24);

            refs.joinCodeField = CreateInputField("JoinCodeField", menuPanel, font, "Join code");
            refs.privateNameField = CreateInputField("PrivateMatchField", menuPanel, font, "Private match name");

            refs.statusText = CreateText("StatusText", menuPanel, font, "Not in session", 20);
            refs.joinCodeText = CreateText("JoinCodeText", menuPanel, font, "Join Code: -", 20);

            refs.quickMatchButton = CreateButton("QuickMatchButton", menuPanel, font, "Quick Match");
            refs.createPrivateButton = CreateButton("CreatePrivateButton", menuPanel, font, "Create Private");
            refs.joinByCodeButton = CreateButton("JoinByCodeButton", menuPanel, font, "Join By Code");
            refs.advancedQueueButton = CreateButton("AdvancedQueueButton", menuPanel, font, "Advanced Queue");
            refs.copyJoinCodeButton = CreateButton("CopyJoinCodeButton", menuPanel, font, "Copy Join Code");
            refs.leaveButton = CreateButton("LeaveButton", menuPanel, font, "Leave");
        }

        private static void BuildGameplayUi(
            RectTransform gameplayPanel,
            Font font,
            TMP_FontAsset tmpFont,
            GameObject boardCardPrefab,
            GameObject handCardPrefab,
            RectTransform dragLayer,
            out PresenterRefs refs)
        {
            refs = new PresenterRefs();

            refs.turnInfoText = CreateText("TurnInfo", gameplayPanel, font, "Turn 1", 24);
            refs.heroInfoText = CreateText("HeroInfo", gameplayPanel, font, "You 20 HP | Enemy 20 HP", 20);
            refs.selectedCardText = CreateText("SelectedCard", gameplayPanel, font, "Selected: none", 18);

            var remoteBoardArea = CreateBoardArea("RemoteBoardArea", gameplayPanel);
            refs.remoteBackLeftSlot = CreateBoardSlot("RemoteBackLeftSlot", remoteBoardArea, tmpFont, Core.BoardSlot.BackLeft, false, new Vector2(-220f, 0f));
            refs.remoteFrontSlot = CreateBoardSlot("RemoteFrontSlot", remoteBoardArea, tmpFont, Core.BoardSlot.Front, false, new Vector2(0f, -80f));
            refs.remoteBackRightSlot = CreateBoardSlot("RemoteBackRightSlot", remoteBoardArea, tmpFont, Core.BoardSlot.BackRight, false, new Vector2(220f, 0f));

            var divider = CreateDivider("Divider", gameplayPanel);

            var localBoardArea = CreateBoardArea("LocalBoardArea", gameplayPanel);
            refs.localFrontSlot = CreateBoardSlot("LocalFrontSlot", localBoardArea, tmpFont, Core.BoardSlot.Front, true, new Vector2(0f, 80f));
            refs.localBackLeftSlot = CreateBoardSlot("LocalBackLeftSlot", localBoardArea, tmpFont, Core.BoardSlot.BackLeft, true, new Vector2(-220f, 0f));
            refs.localBackRightSlot = CreateBoardSlot("LocalBackRightSlot", localBoardArea, tmpFont, Core.BoardSlot.BackRight, true, new Vector2(220f, 0f));

            refs.localHandRoot = CreateHandArea("LocalHand", gameplayPanel);

            refs.endTurnButton = CreateButton("EndTurnButton", gameplayPanel, font, "End Turn");
            refs.battleLogText = CreateText("BattleLog", gameplayPanel, font, "Battle log...", 16);
            refs.battleLogText.alignment = TextAnchor.UpperLeft;
            refs.battleLogText.horizontalOverflow = HorizontalWrapMode.Wrap;
            refs.battleLogText.verticalOverflow = VerticalWrapMode.Overflow;

            var dragLayerLayout = dragLayer.gameObject.GetComponent<LayoutElement>();
            if (dragLayerLayout != null)
            {
                Object.DestroyImmediate(dragLayerLayout);
            }

            _ = boardCardPrefab;
            _ = handCardPrefab;
        }

        private static GameObject RebuildPlayerPrefab()
        {
            var path = CardsEditorPaths.Prefabs + "/CardDuelNetworkPlayer.prefab";
            AssetDatabase.DeleteAsset(path);

            var go = new GameObject("CardDuelNetworkPlayer");
            go.AddComponent<NetworkObject>();
            go.AddComponent<CardDuelNetworkPlayer>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject RebuildBoardCardPrefab(TMP_FontAsset tmpFont)
        {
            var path = CardsEditorPaths.Prefabs + "/BoardCard.prefab";
            AssetDatabase.DeleteAsset(path);

            var go = CreateCardVisualRoot("BoardCard", tmpFont);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject RebuildHandCardPrefab(TMP_FontAsset tmpFont)
        {
            var path = CardsEditorPaths.Prefabs + "/HandCardButton.prefab";
            AssetDatabase.DeleteAsset(path);

            var go = CreateCardVisualRoot("HandCardButton", tmpFont);
            go.AddComponent<CanvasGroup>();

            var button = go.AddComponent<Button>();
            var handButton = go.AddComponent<HandCardButton>();
            handButton.button = button;
            handButton.backgroundImage = go.GetComponent<Image>();
            handButton.canvasGroup = go.GetComponent<CanvasGroup>();
            handButton.cardView = go.GetComponent<CardViewWidget>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static RectTransform CreateBoardArea(string name, Transform parent)
        {
            var panel = CreateSizedPanel(name, parent, BoardAreaColor, new Vector2(0f, 300f));
            var layout = panel.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 300f;
            layout.flexibleWidth = 1f;
            return panel;
        }

        private static RectTransform CreateHandArea(string name, Transform parent)
        {
            var panel = CreateSizedPanel(name, parent, HandAreaColor, new Vector2(0f, 260f));
            var layout = panel.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 260f;
            layout.flexibleWidth = 1f;

            var group = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 10f;
            group.padding = new RectOffset(12, 12, 12, 12);
            group.childAlignment = TextAnchor.MiddleCenter;
            group.childControlWidth = false;
            group.childControlHeight = false;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;

            return panel;
        }

        private static Image CreateDivider(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 8f);

            var image = go.GetComponent<Image>();
            image.color = new Color(0.60f, 0.05f, 0.08f, 1f);

            var layout = go.GetComponent<LayoutElement>();
            layout.preferredHeight = 8f;
            layout.flexibleWidth = 1f;

            return image;
        }

        private static BoardSlotButton CreateBoardSlot(
            string name,
            Transform parent,
            TMP_FontAsset tmpFont,
            Core.BoardSlot slot,
            bool isLocalSide,
            Vector2 anchoredPosition)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);

            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(230f, 280f);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;

            var bg = root.GetComponent<Image>();
            bg.color = slot == Core.BoardSlot.Front
                ? new Color(0.70f, 0.16f, 0.16f, 0.88f)
                : new Color(0.16f, 0.72f, 0.28f, 0.88f);

            var button = root.GetComponent<Button>();
            button.targetGraphic = bg;

            var glowGo = new GameObject("HoverGlow", typeof(RectTransform), typeof(Image));
            glowGo.transform.SetParent(root.transform, false);
            var glowRect = glowGo.GetComponent<RectTransform>();
            Stretch(glowRect, 0f);
            var glowImage = glowGo.GetComponent<Image>();
            glowImage.color = new Color(1f, 1f, 1f, 0.18f);
            glowImage.enabled = false;

            var label = CreateTmpText("Label", root.transform, tmpFont, "Slot", 24);
            label.alignment = TextAlignmentOptions.Center;
            Stretch(label.rectTransform, 10f);

            var anchorGo = new GameObject("CardAnchor", typeof(RectTransform));
            anchorGo.transform.SetParent(root.transform, false);
            var anchorRect = anchorGo.GetComponent<RectTransform>();
            anchorRect.anchorMin = new Vector2(0.5f, 0.5f);
            anchorRect.anchorMax = new Vector2(0.5f, 0.5f);
            anchorRect.pivot = new Vector2(0.5f, 0.5f);
            anchorRect.sizeDelta = new Vector2(180f, 240f);
            anchorRect.anchoredPosition = Vector2.zero;

            var slotButton = root.AddComponent<BoardSlotButton>();
            slotButton.slot = slot;
            slotButton.isLocalSide = isLocalSide;
            slotButton.button = button;
            slotButton.placeholderImage = bg;
            slotButton.labelTextTMP = label;
            slotButton.cardAnchor = anchorRect;
            slotButton.hoverGlowImage = glowImage;

            return slotButton;
        }

        private static GameObject CreateCardVisualRoot(string name, TMP_FontAsset tmpFont)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rootRect = go.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(180f, 240f);

            var background = go.GetComponent<Image>();
            background.color = new Color(0.14f, 0.16f, 0.20f, 0.96f);

            var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            frameGo.transform.SetParent(go.transform, false);
            var frameRect = frameGo.GetComponent<RectTransform>();
            Stretch(frameRect, 6f);
            var frameImage = frameGo.GetComponent<Image>();
            frameImage.color = new Color(0.85f, 0.85f, 0.88f, 0.16f);

            var artGo = new GameObject("Art", typeof(RectTransform), typeof(Image));
            artGo.transform.SetParent(go.transform, false);
            var artRect = artGo.GetComponent<RectTransform>();
            artRect.anchorMin = new Vector2(0.08f, 0.28f);
            artRect.anchorMax = new Vector2(0.92f, 0.78f);
            artRect.offsetMin = Vector2.zero;
            artRect.offsetMax = Vector2.zero;
            var artImage = artGo.GetComponent<Image>();
            artImage.color = new Color(1f, 1f, 1f, 0.08f);

            var title = CreateTmpText("Title", go.transform, tmpFont, "Card", 22);
            title.alignment = TextAlignmentOptions.Center;
            title.rectTransform.anchorMin = new Vector2(0.08f, 0.82f);
            title.rectTransform.anchorMax = new Vector2(0.92f, 0.95f);
            title.rectTransform.offsetMin = Vector2.zero;
            title.rectTransform.offsetMax = Vector2.zero;

            var cost = CreateTmpText("Cost", go.transform, tmpFont, "1", 24);
            cost.alignment = TextAlignmentOptions.Center;
            cost.rectTransform.anchorMin = new Vector2(0.03f, 0.84f);
            cost.rectTransform.anchorMax = new Vector2(0.20f, 0.97f);
            cost.rectTransform.offsetMin = Vector2.zero;
            cost.rectTransform.offsetMax = Vector2.zero;

            var attack = CreateTmpText("Attack", go.transform, tmpFont, "1", 24);
            attack.alignment = TextAlignmentOptions.Center;
            attack.rectTransform.anchorMin = new Vector2(0.04f, 0.02f);
            attack.rectTransform.anchorMax = new Vector2(0.22f, 0.15f);
            attack.rectTransform.offsetMin = Vector2.zero;
            attack.rectTransform.offsetMax = Vector2.zero;

            var armor = CreateTmpText("Armor", go.transform, tmpFont, "", 22);
            armor.alignment = TextAlignmentOptions.Center;
            armor.rectTransform.anchorMin = new Vector2(0.40f, 0.02f);
            armor.rectTransform.anchorMax = new Vector2(0.60f, 0.15f);
            armor.rectTransform.offsetMin = Vector2.zero;
            armor.rectTransform.offsetMax = Vector2.zero;

            var health = CreateTmpText("Health", go.transform, tmpFont, "1", 24);
            health.alignment = TextAlignmentOptions.Center;
            health.rectTransform.anchorMin = new Vector2(0.78f, 0.02f);
            health.rectTransform.anchorMax = new Vector2(0.96f, 0.15f);
            health.rectTransform.offsetMin = Vector2.zero;
            health.rectTransform.offsetMax = Vector2.zero;

            var widget = go.AddComponent<CardViewWidget>();
            widget.artImage = artImage;
            widget.frameImage = frameImage;
            widget.titleText = title;
            widget.costText = cost;
            widget.attackText = attack;
            widget.healthText = health;
            widget.armorText = armor;

            return go;
        }

        private static RectTransform CreateStretchPanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = color.a > 0.001f;

            return rect;
        }

        private static RectTransform CreateLayoutPanel(string name, Transform parent, Color color, float preferredWidth)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.color = color;

            var layout = go.GetComponent<LayoutElement>();
            if (preferredWidth > 0f)
            {
                layout.preferredWidth = preferredWidth;
            }
            else
            {
                layout.flexibleWidth = 1f;
            }

            return rect;
        }

        private static RectTransform CreateSizedPanel(string name, Transform parent, Color color, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.color = color;

            return rect;
        }

        private static TMP_Text CreateHeader(string name, Transform parent, TMP_FontAsset font, string content, int size)
        {
            var header = CreateTmpText(name, parent, font, content, size);
            header.alignment = TMPro.TextAlignmentOptions.Center;
            return header;
        }

        private static Button CreateButton(string name, Transform parent, Font font, string label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = ButtonColor;

            var layout = go.GetComponent<LayoutElement>();
            layout.preferredHeight = 46f;
            layout.flexibleWidth = 1f;

            var text = CreateText("Label", go.transform, font, label, 18);
            Stretch(text.rectTransform, 6f);

            return go.GetComponent<Button>();
        }

        private static InputField CreateInputField(string name, Transform parent, Font font, string placeholder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = InputColor;

            var layout = go.GetComponent<LayoutElement>();
            layout.preferredHeight = 44f;
            layout.flexibleWidth = 1f;

            var text = CreateText("Text", go.transform, font, string.Empty, 18);
            text.alignment = TextAnchor.MiddleLeft;
            Stretch(text.rectTransform, 10f);

            var placeholderText = CreateText("Placeholder", go.transform, font, placeholder, 18);
            placeholderText.color = new Color(1f, 1f, 1f, 0.4f);
            placeholderText.alignment = TextAnchor.MiddleLeft;
            Stretch(placeholderText.rectTransform, 10f);

            var field = go.GetComponent<InputField>();
            field.textComponent = text;
            field.placeholder = placeholderText;

            return field;
        }

        private static Text CreateText(string name, Transform parent, Font font, string content, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = Color.white;
            text.text = content;
            text.alignment = TextAnchor.MiddleCenter;

            var layout = go.GetComponent<LayoutElement>();
            layout.preferredHeight = size + 18f;
            layout.flexibleWidth = 1f;

            return text;
        }

        private static TextMeshProUGUI CreateTmpText(string name, Transform parent, TMP_FontAsset font, string content, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.color = Color.white;
            text.text = content;
            text.enableWordWrapping = true;

            return text;
        }

        private static Font ResolveBuiltinFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return font;
        }

        private static TMP_FontAsset ResolveTmpFont()
        {
            if (TMP_Settings.defaultFontAsset != null)
            {
                return TMP_Settings.defaultFontAsset;
            }

            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        }

        private static void Stretch(RectTransform rt, float padding)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        private struct MatchmakingRefs
        {
            public InputField joinCodeField;
            public InputField privateNameField;
            public Text statusText;
            public Text joinCodeText;
            public Button quickMatchButton;
            public Button createPrivateButton;
            public Button joinByCodeButton;
            public Button advancedQueueButton;
            public Button leaveButton;
            public Button copyJoinCodeButton;
        }

        private struct PresenterRefs
        {
            public Transform localHandRoot;
            public BoardSlotButton localFrontSlot;
            public BoardSlotButton localBackLeftSlot;
            public BoardSlotButton localBackRightSlot;
            public BoardSlotButton remoteFrontSlot;
            public BoardSlotButton remoteBackLeftSlot;
            public BoardSlotButton remoteBackRightSlot;
            public Text battleLogText;
            public Text turnInfoText;
            public Text heroInfoText;
            public Text selectedCardText;
            public Button endTurnButton;
        }
    }
}