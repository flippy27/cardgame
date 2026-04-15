# 🎬 Scene Setup Instructions

Este documento explica cómo crear y configurar las escenas principales del juego.

---

## 📋 Escenas a Crear

### 1. **LoginScene** (Pantalla de Login)

**Pasos:**

1. Crear nueva escena: `File > New Scene`
2. Guardar como `Assets/Scenes/LoginScene.unity`
3. Crear Canvas:
   - Right-click en Hierarchy → UI > Canvas
   - Name: "LoginCanvas"
   
4. Crear estructura UI:
   ```
   LoginCanvas
   ├── Background (Image)
   ├── LoginPanel (Panel)
   │   ├── Title (Text) "CardGame Login"
   │   ├── EmailField (InputField)
   │   ├── PasswordField (InputField) - input type: Password
   │   ├── LoginButton (Button)
   │   ├── RegisterButton (Button)
   │   └── StatusText (Text)
   ├── LoadingPanel (Image - invisible by default)
   │   ├── Spinner (Image animada)
   │   └── LoadingText (Text)
   └── MenuPanel (Panel - invisible initially)
       ├── Title (Text) "Menu"
       ├── PlayButton (Button)
       ├── DecksButton (Button)
       ├── LeaderboardButton (Button)
       ├── ProfileButton (Button)
       ├── SettingsButton (Button)
       ├── ApiTestButton (Button)
       └── LogoutButton (Button)
   ```

5. Agregar scripts:
   - Seleccionar LoginCanvas → Add Component
   - Agregar: `MainMenuScreen` (del namespace Flippy.CardDuelMobile.UI)
   - Asignar todos los InputFields, Buttons, Texts en inspector

6. Agregar evento de Canvas:
   - Crear un GameObject vacío: "Bootstrap"
   - Agregar componente: `SceneBootstrap` (del namespace Flippy.CardDuelMobile.Core)

7. Configurar Build Settings:
   - File > Build Settings
   - Drag LoginScene al index 0 (primera escena)

**Configuración de Inputs:**
```
Email Input:
  - Placeholder: "tu@email.com"
  - Content Type: Email Address
  
Password Input:
  - Placeholder: "contraseña"
  - Content Type: Password
  - Input Type: Password
```

---

### 2. **MenuScene** (Menú Principal después de Login)

**Pasos:**

1. Crear nueva escena: `File > New Scene`
2. Guardar como `Assets/Scenes/MenuScene.unity`
3. Crear Canvas con opciones:
   ```
   MenuCanvas
   ├── Header
   │   ├── PlayerName (Text)
   │   └── StatusText (Text)
   ├── MainPanel
   │   ├── PlayButton (Button)
   │   ├── DeckBuilderButton (Button)
   │   ├── LeaderboardButton (Button)
   │   ├── ProfileButton (Button)
   │   ├── SettingsButton (Button)
   │   ├── ApiTestButton (Button - para debugging)
   │   └── LogoutButton (Button)
   ├── LeaderboardScreen (Panel - desactivado)
   ├── ProfileScreen (Panel - desactivado)
   └── SettingsPanel (Panel - desactivado)
   ```

4. Agregar scripts:
   - MainMenuScreen (para coordinar botones)
   - LeaderboardScreen (agregar a LeaderboardScreen Panel)
   - ProfileScreen (agregar a ProfileScreen Panel)
   - ApiTestMenu (agregar para debug)

5. Configurar eventos de botones:
   - PlayButton → `SceneBootstrap.LoadBattle()`
   - LeaderboardButton → activate LeaderboardScreen
   - ProfileButton → activate ProfileScreen
   - LogoutButton → `SceneBootstrap.LoadLoginAndLogout()`

---

### 3. **BattleScene** (Escena de Batalla - Existente)

**Solo verificar:**
- Que tenga NetworkBootstrap configurado
- Que BattleScreenPresenter esté en la escena
- Que apunte al canvas correcto

---

## ⚙️ Configuración de Build Settings

1. File > Build Settings
2. Escenas en orden:
   - Index 0: LoginScene ✅
   - Index 1: MenuScene
   - Index 2: BattleScene
   - Index 3: (otras escenas)

3. Verificar:
   - Target Platform: iOS o Android
   - Scenes In Build: ✅ todas las escenas
   - Auto-load scene on startup: ✅ LoginScene (index 0)

---

## 🔌 Wiring (Conexiones)

### LoginScene

```csharp
// MainMenuScreen debe tener asignado:
- playerIdInput → EmailInput field
- passwordInput → PasswordInput field
- loginButton → Login button
- registerButton → Register button
- statusText → Status text
- playButton → Play button
- logoutButton → Logout button
```

### MenuScene

```csharp
// MainMenuScreen en MenuCanvas:
- playButton → Play button
- leaderboardButton → Leaderboard button
- profileButton → Profile button
- logoutButton → Logout button
- playerNameText → PlayerName text

// Botones desactivan/activan panels:
- LeaderboardButton → LeaderboardScreen.SetActive(true)
- ProfileButton → ProfileScreen.SetActive(true)
- SettingsButton → SettingsPanel.SetActive(true)
```

---

## 🧪 Testing Checklist

- [ ] LoginScene se abre al iniciar
- [ ] Email + password válidos → login exitoso → va a MenuScene
- [ ] Email + password inválidos → mostrar error
- [ ] Botón Register → intenta registrar
- [ ] En MenuScene: Click Play → va a BattleScene
- [ ] En MenuScene: Click Logout → vuelve a LoginScene
- [ ] LeaderboardScreen se abre/cierra
- [ ] ProfileScreen se abre/cierra
- [ ] F10 en MenuScene abre ApiTestMenu
- [ ] ApiTestMenu: Login test → muestra respuesta

---

## 🎨 UI Prefabs (Recomendado)

Para reutilizar, crear prefabs:

1. `Assets/Resources/UI/LeaderboardEntryPrefab.prefab`
   - Layout para una entrada en leaderboard
   - Texts: Rank, Name, Rating, Record

2. `Assets/Resources/UI/MatchEntryPrefab.prefab`
   - Layout para una entrada en historial
   - Texts: Opponent, Result, RatingChange, Date

3. `Assets/Resources/UI/AchievementBadgePrefab.prefab`
   - Layout para un achievement
   - Image: Badge icon
   - Text: Achievement name

---

## 📱 Canvas Settings

Para todos los canvas, usar:

```
Canvas Component:
  - Render Mode: Screen Space - Overlay
  - Scale Mode: Scale With Screen Size
  - Reference Resolution: 1920x1080

CanvasScaler Component:
  - UI Scale Mode: Scale With Screen Size
  - Screen Match Mode: Expand
```

---

## 🔄 Navigation Flow

```
LoginScene
    ↓ (login exitoso)
MenuScene
    ├─ Play → BattleScene
    ├─ Leaderboard → LeaderboardScreen (popup)
    ├─ Profile → ProfileScreen (popup)
    ├─ Settings → SettingsPanel (popup)
    └─ Logout → LoginScene

BattleScene
    ├─ Match end → MenuScene
    └─ Forfeit → MenuScene

BattleScene (multiplayer)
    ├─ Host creates match
    └─ Guest joins → Sync via NGO
```

---

## 🔑 Key Scripts to Add

| Escena | Script | Componente |
|--------|--------|-----------|
| LoginScene | SceneBootstrap | Canvas (nuevo GameObject) |
| LoginScene | MainMenuScreen | Canvas |
| MenuScene | MainMenuScreen | Canvas |
| MenuScene | LeaderboardScreen | LeaderboardScreen Panel |
| MenuScene | ProfileScreen | ProfileScreen Panel |
| MenuScene | ApiTestMenu | Canvas |
| BattleScene | (existente) | (existente) |

---

## 💾 Save & Test

1. Guardar todas las escenas: Ctrl+S
2. Build & Run: Ctrl+B
3. Test login flow
4. Check console para debug logs

---

## 🐛 Troubleshooting

**"SceneBootstrap no se encuentra"**
- Verificar namespace: `Flippy.CardDuelMobile.Core`
- Asegurar archivo: `Assets/Runtime/Core/SceneBootstrap.cs`

**"MainMenuScreen no responde"**
- Verificar que inputs estén asignados en inspector
- Verificar que botones tengan onClick listeners

**"Login scene no se abre"**
- Verificar Build Settings index 0 = LoginScene
- Verificar SceneBootstrap está en la escena

---

**Estado:** Ready para implementar  
**Tiempo estimado:** 1-2 horas crear todas las escenas  
**Prioridad:** Alta (UI bloquer para testing)
