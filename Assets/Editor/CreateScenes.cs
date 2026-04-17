using UnityEngine;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using UnityEditor;
using Flippy.CardDuelMobile.UI;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Editor
{
    /// <summary>
    /// Automatic scene generator for LoginScene and MenuScene.
    /// Run from menu: Tools → Create Scenes
    /// </summary>
    public static class CreateScenes
    {
        [MenuItem("Tools/Create Scenes/Create LoginScene")]
        public static void CreateLoginScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateLoginSceneHierarchy();

            var path = "Assets/Scenes/LoginScene.unity";
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"✅ LoginScene created at {path}");
        }

        [MenuItem("Tools/Create Scenes/Create MenuScene")]
        public static void CreateMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateMenuSceneHierarchy();

            var path = "Assets/Scenes/MenuScene.unity";
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"✅ MenuScene created at {path}");
        }

        [MenuItem("Tools/Create Scenes/Create Both Scenes")]
        public static void CreateBothScenes()
        {
            CreateLoginScene();
            CreateMenuScene();
            AssetDatabase.Refresh();
        }

        private static void CreateLoginSceneHierarchy()
        {
            // Canvas
            var canvasGo = new GameObject("LoginCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var canvasScaler = canvasGo.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);

            var rectTransform = canvasGo.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            // GraphicRaycaster
            canvasGo.AddComponent<GraphicRaycaster>();

            // Background
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(canvasGo.transform, false);
            var bgImage = bgGo.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // LoginPanel
            var loginPanelGo = CreatePanel(canvasGo.transform, "LoginPanel", new Color(0.2f, 0.2f, 0.2f, 0.9f));
            loginPanelGo.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 300);

            CreateText(loginPanelGo.transform, "Title", "🎮 CardGame", 36, new Color(1, 1, 1, 1), TextAnchor.UpperCenter, new Vector2(0, -50));
            CreateInputField(loginPanelGo.transform, "PlayerIdInput", "email@example.com", InputField.ContentType.EmailAddress, new Vector2(0, -100));
            CreateInputField(loginPanelGo.transform, "PasswordInput", "password", InputField.ContentType.Password, new Vector2(0, -150));
            CreateButton(loginPanelGo.transform, "LoginButton", "Login", new Vector2(-100, -200));
            CreateButton(loginPanelGo.transform, "RegisterButton", "Register", new Vector2(100, -200));
            CreateText(loginPanelGo.transform, "StatusText", "Enter credentials", 14, new Color(1, 1, 1, 1), TextAnchor.MiddleCenter, new Vector2(0, -250));

            // LoadingPanel (invisible)
            var loadingPanelGo = CreatePanel(canvasGo.transform, "LoadingPanel", new Color(0, 0, 0, 0.7f));
            var loadingGroup = loadingPanelGo.GetComponent<CanvasGroup>();
            loadingGroup.alpha = 0;
            loadingGroup.interactable = false;
            loadingGroup.blocksRaycasts = false;
            CreateText(loadingPanelGo.transform, "LoadingText", "Loading...", 24, new Color(1, 1, 1, 1), TextAnchor.MiddleCenter, Vector2.zero);

            // MenuPanel (invisible)
            var menuPanelGo = CreatePanel(canvasGo.transform, "MenuPanel", new Color(0.2f, 0.2f, 0.2f, 0.9f));
            var menuGroup = menuPanelGo.GetComponent<CanvasGroup>();
            menuGroup.alpha = 0;
            menuGroup.interactable = false;
            menuGroup.blocksRaycasts = false;
            menuPanelGo.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 400);

            CreateText(menuPanelGo.transform, "PlayerNameText", "Player Name", 18, new Color(1, 1, 1, 1), TextAnchor.UpperCenter, new Vector2(0, -50));
            CreateButton(menuPanelGo.transform, "PlayButton", "▶️ Play", new Vector2(0, -120));
            CreateButton(menuPanelGo.transform, "DeckBuilderButton", "🛠️ Deck Builder", new Vector2(0, -170));
            CreateButton(menuPanelGo.transform, "LeaderboardButton", "📊 Leaderboard", new Vector2(0, -220));
            CreateButton(menuPanelGo.transform, "ProfileButton", "👤 Profile", new Vector2(0, -270));
            CreateButton(menuPanelGo.transform, "SettingsButton", "⚙️ Settings", new Vector2(0, -320));
            CreateButton(menuPanelGo.transform, "ApiTestButton", "🔧 API Test", new Vector2(0, -370));
            CreateButton(menuPanelGo.transform, "LogoutButton", "🚪 Logout", new Vector2(0, -420));

            // MainMenuScreen component
            var mainMenuScreen = canvasGo.AddComponent<MainMenuScreen>();
            WireUpMainMenuScreen(mainMenuScreen, canvasGo, loginPanelGo, menuPanelGo, loadingPanelGo);

            // Bootstrap
            var bootstrapGo = new GameObject("Bootstrap");
            bootstrapGo.AddComponent<SceneBootstrap>();
        }

        private static void CreateMenuSceneHierarchy()
        {
            // Canvas
            var canvasGo = new GameObject("MenuCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var canvasScaler = canvasGo.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);

            var rectTransform = canvasGo.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            canvasGo.AddComponent<GraphicRaycaster>();

            // Header
            var headerGo = new GameObject("Header");
            headerGo.transform.SetParent(canvasGo.transform, false);
            CreateText(headerGo.transform, "PlayerNameText", "Player Name", 18, Color.white, TextAnchor.UpperLeft, new Vector2(20, -20));
            CreateText(headerGo.transform, "TitleText", "Main Menu", 32, Color.white, TextAnchor.UpperCenter, Vector2.zero);

            // MainPanel
            var mainPanelGo = CreatePanel(canvasGo.transform, "MainPanel", new Color(0.2f, 0.2f, 0.2f, 0.9f));
            mainPanelGo.GetComponent<RectTransform>().sizeDelta = new Vector2(400, 450);

            CreateButton(mainPanelGo.transform, "PlayButton", "▶️ Play", new Vector2(0, -50));
            CreateButton(mainPanelGo.transform, "DeckBuilderButton", "🛠️ Deck Builder", new Vector2(0, -100));
            CreateButton(mainPanelGo.transform, "LeaderboardButton", "📊 Leaderboard", new Vector2(0, -150));
            CreateButton(mainPanelGo.transform, "ProfileButton", "👤 Profile", new Vector2(0, -200));
            CreateButton(mainPanelGo.transform, "SettingsButton", "⚙️ Settings", new Vector2(0, -250));
            CreateButton(mainPanelGo.transform, "ApiTestButton", "🔧 API Test", new Vector2(0, -300));
            CreateButton(mainPanelGo.transform, "LogoutButton", "🚪 Logout", new Vector2(0, -350));

            // LeaderboardScreen (invisible)
            var leaderboardScreenGo = CreatePanel(canvasGo.transform, "LeaderboardScreen", new Color(0.2f, 0.2f, 0.2f, 0.95f));
            var leaderboardGroup = leaderboardScreenGo.GetComponent<CanvasGroup>();
            leaderboardGroup.alpha = 0;
            leaderboardGroup.interactable = false;
            leaderboardGroup.blocksRaycasts = false;

            // ProfileScreen (invisible)
            var profileScreenGo = CreatePanel(canvasGo.transform, "ProfileScreen", new Color(0.2f, 0.2f, 0.2f, 0.95f));
            var profileGroup = profileScreenGo.GetComponent<CanvasGroup>();
            profileGroup.alpha = 0;
            profileGroup.interactable = false;
            profileGroup.blocksRaycasts = false;

            // SettingsPanel (invisible)
            var settingsPanelGo = CreatePanel(canvasGo.transform, "SettingsPanel", new Color(0.2f, 0.2f, 0.2f, 0.95f));
            var settingsGroup = settingsPanelGo.GetComponent<CanvasGroup>();
            settingsGroup.alpha = 0;
            settingsGroup.interactable = false;
            settingsGroup.blocksRaycasts = false;
            CreateText(settingsPanelGo.transform, "SettingsText", "Settings (TODO)", 24, Color.white, TextAnchor.MiddleCenter, Vector2.zero);

            // MainMenuScreen component
            var mainMenuScreen = canvasGo.AddComponent<MainMenuScreen>();
            WireUpMenuSceneMainMenuScreen(mainMenuScreen, mainPanelGo, leaderboardScreenGo, profileScreenGo, settingsPanelGo);
        }

        private static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            var panelGo = new GameObject(name);
            panelGo.transform.SetParent(parent, false);

            var image = panelGo.AddComponent<Image>();
            image.color = color;

            panelGo.AddComponent<CanvasGroup>();

            var rect = panelGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return panelGo;
        }

        private static GameObject CreateButton(Transform parent, string name, string text, Vector2 pos)
        {
            var buttonGo = new GameObject(name);
            buttonGo.transform.SetParent(parent, false);

            var image = buttonGo.AddComponent<Image>();
            image.color = new Color(0.3f, 0.3f, 0.3f, 1f);

            buttonGo.AddComponent<Button>();

            var rect = buttonGo.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(200, 40);

            // Button text
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(buttonGo.transform, false);
            var textComponent = textGo.AddComponent<Text>();
            textComponent.text = text;
            textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            textComponent.fontSize = 14;
            textComponent.fontStyle = FontStyle.Bold;
            textComponent.alignment = TextAnchor.MiddleCenter;
            textComponent.color = Color.white;

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return buttonGo;
        }

        private static GameObject CreateText(Transform parent, string name, string text, int fontSize, Color color, TextAnchor anchor, Vector2 pos)
        {
            var textGo = new GameObject(name);
            textGo.transform.SetParent(parent, false);

            var textComponent = textGo.AddComponent<Text>();
            textComponent.text = text;
            textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            textComponent.fontSize = fontSize;
            textComponent.fontStyle = FontStyle.Bold;
            textComponent.alignment = anchor;
            textComponent.color = color;

            var rect = textGo.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(300, 50);

            return textGo;
        }

        private static GameObject CreateInputField(Transform parent, string name, string placeholder, InputField.ContentType contentType, Vector2 pos)
        {
            var inputGo = new GameObject(name);
            inputGo.transform.SetParent(parent, false);

            var image = inputGo.AddComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            var inputField = inputGo.AddComponent<InputField>();
            inputField.contentType = contentType;

            var rect = inputGo.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(250, 40);

            // Placeholder
            var placeholderGo = new GameObject("Placeholder");
            placeholderGo.transform.SetParent(inputGo.transform, false);
            var placeholderText = placeholderGo.AddComponent<Text>();
            placeholderText.text = placeholder;
            placeholderText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            placeholderText.fontSize = 14;
            placeholderText.alignment = TextAnchor.MiddleLeft;
            placeholderText.color = new Color(1, 1, 1, 0.5f);

            var placeholderRect = placeholderGo.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(10, 0);
            placeholderRect.offsetMax = new Vector2(-10, 0);

            inputField.placeholder = placeholderText;

            // Text
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(inputGo.transform, false);
            var textComponent = textGo.AddComponent<Text>();
            textComponent.text = "";
            textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            textComponent.fontSize = 14;
            textComponent.alignment = TextAnchor.MiddleLeft;
            textComponent.color = Color.white;

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 0);
            textRect.offsetMax = new Vector2(-10, 0);

            inputField.textComponent = textComponent;

            return inputGo;
        }

        private static void WireUpMainMenuScreen(MainMenuScreen screen, GameObject canvas, GameObject loginPanel, GameObject menuPanel, GameObject loadingPanel)
        {
            // Login Panel
            screen.playerIdInput = loginPanel.transform.Find("PlayerIdInput").GetComponent<InputField>();
            screen.passwordInput = loginPanel.transform.Find("PasswordInput").GetComponent<InputField>();
            screen.loginButton = loginPanel.transform.Find("LoginButton").GetComponent<Button>();
            screen.registerButton = loginPanel.transform.Find("RegisterButton").GetComponent<Button>();
            screen.statusText = loginPanel.transform.Find("StatusText").GetComponent<Text>();
            screen.loginPanelGroup = loginPanel.GetComponent<CanvasGroup>();

            // Menu Panel
            screen.playButton = menuPanel.transform.Find("PlayButton").GetComponent<Button>();
            screen.deckBuilderButton = menuPanel.transform.Find("DeckBuilderButton").GetComponent<Button>();
            screen.leaderboardButton = menuPanel.transform.Find("LeaderboardButton").GetComponent<Button>();
            screen.profileButton = menuPanel.transform.Find("ProfileButton").GetComponent<Button>();
            screen.settingsButton = menuPanel.transform.Find("SettingsButton").GetComponent<Button>();
            screen.apiTestButton = menuPanel.transform.Find("ApiTestButton").GetComponent<Button>();
            screen.logoutButton = menuPanel.transform.Find("LogoutButton").GetComponent<Button>();
            screen.playerNameText = menuPanel.transform.Find("PlayerNameText").GetComponent<Text>();
            screen.menuPanelGroup = menuPanel.GetComponent<CanvasGroup>();

            // Loading
            screen.loadingGroup = loadingPanel.GetComponent<CanvasGroup>();
            screen.loadingText = loadingPanel.transform.Find("LoadingText").GetComponent<Text>();
        }

        private static void WireUpMenuSceneMainMenuScreen(MainMenuScreen screen, GameObject mainPanel, GameObject leaderboardScreen, GameObject profileScreen, GameObject settingsPanel)
        {
            // Menu Panel buttons
            screen.playButton = mainPanel.transform.Find("PlayButton").GetComponent<Button>();
            screen.deckBuilderButton = mainPanel.transform.Find("DeckBuilderButton").GetComponent<Button>();
            screen.leaderboardButton = mainPanel.transform.Find("LeaderboardButton").GetComponent<Button>();
            screen.profileButton = mainPanel.transform.Find("ProfileButton").GetComponent<Button>();
            screen.settingsButton = mainPanel.transform.Find("SettingsButton").GetComponent<Button>();
            screen.apiTestButton = mainPanel.transform.Find("ApiTestButton").GetComponent<Button>();
            screen.logoutButton = mainPanel.transform.Find("LogoutButton").GetComponent<Button>();
            screen.menuPanelGroup = mainPanel.GetComponent<CanvasGroup>();

            // Screens
            screen.leaderboardScreen = leaderboardScreen.GetComponent<LeaderboardScreen>();
            if (screen.leaderboardScreen == null)
                screen.leaderboardScreen = leaderboardScreen.AddComponent<LeaderboardScreen>();

            screen.profileScreen = profileScreen.GetComponent<ProfileScreen>();
            if (screen.profileScreen == null)
                screen.profileScreen = profileScreen.AddComponent<ProfileScreen>();

            screen.settingsPanel = settingsPanel;
        }
    }
}
