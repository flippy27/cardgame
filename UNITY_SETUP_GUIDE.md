# 🎮 Unity Setup Guide - CardGame

**Tarea:** Crear y configurar las escenas faltantes para que el juego sea funcional end-to-end.

---

## ✅ Estado Actual

- **Código:** 100% listo (todos los scripts compilados)
- **Gameplay:** Funcional
- **Authentication:** Implementado con persistencia
- **Match Completion:** Guardando resultados en API
- **Escenas:** Falta crear LoginScene y MenuScene

---

## 🎬 Step 1: Create LoginScene

### 1.1 Create New Scene
```
File → New Scene (Empty 2D)
Save as: Assets/Scenes/LoginScene.unity
```

### 1.2 Create Canvas
```
Right-click in Hierarchy → UI → Canvas
Name: "LoginCanvas"
Canvas Scaler settings:
  - Render Mode: Screen Space - Overlay
  - UI Scale Mode: Scale With Screen Size
  - Reference Resolution: 1920x1080
```

### 1.3 Build Scene Hierarchy
Create the following structure under LoginCanvas:

```
LoginCanvas (Canvas)
├── Background (Image component, optional)
│   └── Set color to dark background
├── LoginPanel (Panel - child of Canvas)
│   ├── Title (Text) "🎮 CardGame"
│   ├── EmailInput (InputField)
│   │   └── Configure:
│   │       - Placeholder: "email@example.com"
│   │       - Content Type: Email Address
│   ├── PasswordInput (InputField)
│   │   └── Configure:
│   │       - Placeholder: "password"
│   │       - Content Type: Password
│   │       - Input Type: Password
│   ├── LoginButton (Button) "Login"
│   ├── RegisterButton (Button) "Register"
│   └── StatusText (Text) "Enter credentials"
├── LoadingPanel (Image - invisible)
│   ├── Alpha: 0
│   ├── Interactable: OFF
│   └── Blocks Raycasts: OFF
└── MenuPanel (Panel - invisible)
    ├── Alpha: 0
    ├── Interactable: OFF
    ├── Blocks Raycasts: OFF
    └── Children (create as needed):
        ├── PlayerNameText (Text)
        ├── PlayButton (Button)
        ├── DeckBuilderButton (Button)
        ├── LeaderboardButton (Button)
        ├── ProfileButton (Button)
        ├── SettingsButton (Button)
        ├── ApiTestButton (Button)
        └── LogoutButton (Button)
```

### 1.4 Add MainMenuScreen Component
1. Select `LoginCanvas` in Hierarchy
2. Inspector → Add Component → Search "MainMenuScreen"
3. Drag UI elements to their Inspector slots:

**Inspector Fields to Wire:**
```
Login Panel:
  - Player Id Input → EmailInput field
  - Password Input → PasswordInput field
  - Login Button → LoginButton
  - Register Button → RegisterButton
  - Status Text → StatusText
  - Login Panel Group → LoginPanel (set as CanvasGroup)

Main Menu Panel:
  - Play Button → PlayButton
  - Deck Builder Button → DeckBuilderButton
  - Leaderboard Button → LeaderboardButton
  - Profile Button → ProfileButton
  - Settings Button → SettingsButton
  - Api Test Button → ApiTestButton
  - Logout Button → LogoutButton
  - Player Name Text → PlayerNameText
  - Menu Panel Group → MenuPanel (set as CanvasGroup)

Loading:
  - Loading Group → LoadingPanel (set as CanvasGroup)
  - Loading Text → Text component in LoadingPanel

Screens:
  - Leaderboard Screen → (will add after MenuScene)
  - Profile Screen → (will add after MenuScene)
  - Settings Panel → (will add later or create empty GameObject)
```

### 1.5 Add Bootstrap
1. Right-click in Hierarchy → Create Empty
2. Name it "Bootstrap"
3. Add Component → Search "SceneBootstrap"
4. Leave as is (no configuration needed)

### 1.6 Test LoginScene
- Press Play in Editor
- You should see login UI
- Try logging in (use test credentials from API)

---

## 🎬 Step 2: Create MenuScene

### 2.1 Create New Scene
```
File → New Scene (Empty 2D)
Save as: Assets/Scenes/MenuScene.unity
```

### 2.2 Create Canvas
Same settings as LoginScene

### 2.3 Build Scene Hierarchy
```
MenuCanvas (Canvas)
├── Header
│   ├── PlayerNameText (Text) "Player Name"
│   └── TitleText (Text) "Main Menu"
├── MainPanel (Panel)
│   ├── PlayButton (Button) "▶️ Play"
│   ├── DeckBuilderButton (Button) "🛠️ Deck Builder"
│   ├── LeaderboardButton (Button) "📊 Leaderboard"
│   ├── ProfileButton (Button) "👤 Profile"
│   ├── SettingsButton (Button) "⚙️ Settings"
│   ├── ApiTestButton (Button) "🔧 API Test"
│   └── LogoutButton (Button) "🚪 Logout"
├── LeaderboardScreen (Panel - invisible)
│   └── Add LeaderboardScreen component here
├── ProfileScreen (Panel - invisible)
│   └── Add ProfileScreen component here
└── SettingsPanel (Panel - invisible)
    └── Add simple text "Settings (TODO)"
```

### 2.4 Add MainMenuScreen Component
Same as LoginScene - wire all buttons

### 2.5 Add LeaderboardScreen & ProfileScreen
1. Select LeaderboardScreen Panel
2. Add Component → Search "LeaderboardScreen"
3. Drag in:
   - `entryPrefab` → create or assign prefab for leaderboard entry
   - `entryContainer` → the container where entries spawn
4. Repeat for ProfileScreen

### 2.6 Configure Back Buttons
- LeaderboardScreen Back Button → Calls `gameObject.SetActive(false)`
- ProfileScreen Back Button → Calls `gameObject.SetActive(false)`

---

## 📋 Step 3: Configure Build Settings

1. Open Build Settings: `File → Build Settings`
2. Add Scenes to Build:
   ```
   Index 0: Assets/Scenes/LoginScene.unity (drag & drop)
   Index 1: Assets/Scenes/MenuScene.unity
   Index 2: Assets/Scenes/BattleScene.unity (should exist)
   ```
3. Verify "Auto-load scene on startup" points to LoginScene

---

## 🔧 Step 4: Test Complete Flow

1. **Login Test**
   - Press Play from LoginScene
   - Enter valid test credentials
   - Should transition to MenuScene

2. **Menu Buttons Test**
   - Click "Play" → Should load BattleScene
   - Click "Leaderboard" → Should show LeaderboardScreen
   - Click "Profile" → Should show ProfileScreen
   - Click "Logout" → Should go back to LoginScene

3. **Persistent Login Test**
   - Enter login credentials
   - Close app
   - Reopen → Should load directly to MenuScene (if still authenticated)

---

## 📝 Prefab Creation (Optional but Recommended)

Create these prefabs for reusable UI elements:

### Leaderboard Entry Prefab
```
Assets/Resources/UI/LeaderboardEntryPrefab.prefab
  ├── RankText (Text) "#1"
  ├── NameText (Text) "PlayerName"
  ├── RatingText (Text) "2000"
  └── RecordText (Text) "50W-10L"
```

### Match History Entry Prefab
```
Assets/Resources/UI/MatchEntryPrefab.prefab
  ├── OpponentText (Text)
  ├── ResultText (Text) "WIN/LOSS"
  ├── RatingChangeText (Text) "+25"
  └── DateText (Text) "2h ago"
```

---

## 🚀 Quick Checklist

- [ ] LoginScene created
- [ ] MenuScene created
- [ ] MainMenuScreen wired in both scenes
- [ ] Build Settings configured with both scenes
- [ ] Test login → menu flow
- [ ] Test menu → battle flow
- [ ] Test logout → login flow
- [ ] Verify persistent login works

---

## 🆘 Troubleshooting

**"Button does nothing when clicked"**
- Verify MainMenuScreen component is added to Canvas
- Verify Button fields are wired in Inspector
- Check Console for errors

**"Scene doesn't load on Play"**
- Verify scene is added to Build Settings
- Check that LoginScene is at index 0

**"UI looks stretched/weird"**
- Verify Canvas Scaler settings match spec above
- Check RectTransform values aren't extreme

**"Login fails"**
- Verify API is running: `ASPNETCORE_ENVIRONMENT=Development dotnet run`
- Check in API TestMenu if endpoints work
- Verify correct credentials

---

**Status:** Ready to start scene creation in Unity Editor
