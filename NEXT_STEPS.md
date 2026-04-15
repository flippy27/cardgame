# 🎯 NEXT STEPS: CardGame Development Roadmap

**Compilado:** 2026-04-14  
**Responsable:** Development Team

---

## 📊 ESTADO ACTUAL

| Aspecto | Status | Detalles |
|--------|--------|----------|
| **Gameplay** | ✅ FUNCIONAL | Drag-drop, validación, slots |
| **Multiplayer** | ✅ FUNCIONAL | NGO + Netcode |
| **Autenticación** | ✅ IMPLEMENTADO | Real endpoints + secure storage |
| **Persistencia** | ⚠️ PARCIAL | Tokens OK, match results faltantes |
| **Menú** | ✅ BÁSICO | Login/menu principal |
| **API Testing** | ❌ FALTANTE | Menu de debug para endpoints |

---

## 🔴 CRÍTICOS (Esta semana)

### 1. **API Testing Menu** (2-4 horas)
**Ubicación:** `Tools/UI/Menu` → Create `ApiTestMenu.cs`

**Requerimientos:**
- Input fields para parámetros de API
- Buttons para cada endpoint
- JSON response viewer
- Auto-refresh tokens

**Endpoints a testear:**
```
POST /api/auth/login
POST /api/auth/register
GET /api/cards
GET /api/users/{playerId}/profile
GET /api/decks
POST /api/decks
GET /api/matches
POST /api/matches/{matchId}/end-turn
```

**Implementación:**
```csharp
public class ApiTestMenu : MonoBehaviour
{
    // Input fields para parámetros
    public InputField emailInput;
    public InputField passwordInput;
    public InputField userIdInput;
    
    // Buttons para cada endpoint
    public Button loginButton;
    public Button getProfileButton;
    public Button listDecksButton;
    
    // Response viewer
    public Text responseText;
    public ScrollRect responseScroll;
    
    // Manejo de requests
    private async void HandleLogin()
    {
        var authService = new AuthService();
        var result = await authService.Login(emailInput.text, passwordInput.text);
        ShowResponse($"Login result: {result}");
    }
}
```

### 2. **Match Completion Persistence** (4-6 horas)
**Ubicación:** `MatchCompletionService.cs` + `BattleScreenPresenter.cs`

**Problema:** Resultados de matches no se guardan en servidor

**Solución:**
```csharp
// En BattleScreenPresenter.HandleMatchEnd()
await matchCompletionService.CompleteMatch(
    matchId: _currentMatchId,
    winnerId: winnerId,
    loser Id: loserId,
    replayData: capturedMoves
);
```

**Endpoint:** `POST /api/matches/{matchId}/end` (crear si no existe)

### 3. **UI Integration Issues** (2-3 horas)
- [ ] MainMenuScreen no referenciada en escenas
- [ ] Transición Login → Game funcional
- [ ] Back button behavior (logout vs cancel)
- [ ] Loading states y spinners

---

## 🟡 ALTOS (Esta semana)

### 4. **Leaderboards Screen** (4-6 horas)
**Ubicación:** Assets/Runtime/UI/LeaderboardScreen.cs

**Features:**
- Top 100 jugadores por rating
- Filtro por season
- Tu posición destacada
- Refresh automático cada 5 min

**Endpoint:** `GET /api/leaderboards/{season}?page=1&pageSize=100`

### 5. **Profile Screen** (3-4 horas)
**Ubicación:** Assets/Runtime/UI/ProfileScreen.cs

**Features:**
- Muestra stats: rating, wins/losses, win rate
- Historial de últimos 10 matches
- Avatar y bio editables
- Achievements/badges

**Endpoints:**
```
GET /api/users/{playerId}/profile
PATCH /api/users/{playerId}/profile
GET /api/matches/history/{playerId}?page=1
```

### 6. **Real Error Handling** (2-3 horas)
**Problema:** Errores de API no se muestran bien al usuario

**Solución:**
```csharp
public class ApiErrorHandler
{
    public static string GetUserFriendlyMessage(int statusCode, string error)
    {
        return statusCode switch
        {
            401 => "Sesión expirada. Por favor inicia sesión de nuevo",
            403 => "No tienes permisos para esto",
            404 => "Recurso no encontrado",
            429 => "Estás haciendo demasiadas peticiones. Espera un momento",
            500 => "Error del servidor. Intenta más tarde",
            _ => $"Error: {error}"
        };
    }
}
```

---

## 🟢 MEDIOS (Próximas 2 semanas)

### 7. **Rate Limiting Handling**
- [ ] Detectar HTTP 429
- [ ] Mostrar "retry after" al usuario
- [ ] Queue de requests automático
- [ ] Backoff client-side

### 8. **Real-time Updates (SignalR)**
- [ ] Conexión WebSocket para matchmaking
- [ ] Push notifications de match found
- [ ] Live leaderboard updates
- [ ] Chat en-game

### 9. **Social Features**
- [ ] Add Friend / Block
- [ ] Friend List Screen
- [ ] Clan/Guild System (básico)
- [ ] Private Match by Code

### 10. **Seasonal Ranking**
- [ ] Season tracker
- [ ] Seasonal reset
- [ ] Tier system (Bronce/Plata/Oro/Diamante)
- [ ] Season rewards

---

## 📋 FULL FEATURE CHECKLIST

### Autenticación & Session
- [x] Login real (POST /api/auth/login)
- [x] Register (POST /api/auth/register)
- [x] Secure token storage (Keychain/Keystore)
- [x] Auto-load session al startup
- [x] Logout
- [ ] Token refresh (24h renewal)
- [ ] Password reset flow
- [ ] Two-factor auth (futuro)

### Gameplay
- [x] Drag-and-drop cartas
- [x] Validación de slots
- [x] Multiplayer con NGO
- [x] Match state sync
- [x] Game over detection
- [ ] Replay system
- [ ] Spectate mode
- [ ] AI opponent (single player)

### Progression
- [x] Rating Elo
- [ ] Leaderboards globales
- [ ] Seasonal rankings
- [ ] Tier system
- [ ] Achievements
- [ ] Season pass / Battle pass
- [ ] Cosmetics (skins, card backs)

### Social
- [ ] Friend system
- [ ] Clan system
- [ ] Messaging / Chat
- [ ] Profiles visibles
- [ ] Match replays shareable
- [ ] Tournaments

### Content
- [x] Card catalog + cache
- [x] Deck CRUD
- [x] Match history
- [ ] Card meta statistics
- [ ] Deck guides / tips
- [ ] News / announcements

### Admin
- [ ] Ban management
- [ ] Content moderation
- [ ] Player support tools
- [ ] Analytics dashboard

---

## 📂 ARQUITECTURA - CARPETAS A CREAR

```
Assets/
├── Runtime/
│   ├── UI/
│   │   ├── MainMenuScreen.cs ✅
│   │   ├── ApiTestMenu.cs (TODO)
│   │   ├── LeaderboardScreen.cs (TODO)
│   │   ├── ProfileScreen.cs (TODO)
│   │   ├── LobbyScreen.cs (TODO)
│   │   └── SettingsScreen.cs (TODO)
│   │
│   ├── Networking/
│   │   ├── SecureTokenStorage.cs ✅
│   │   ├── AuthService.cs ✅ (actualizado)
│   │   ├── ApiErrorHandler.cs (TODO)
│   │   ├── RateLimitHandler.cs (TODO)
│   │   └── SignalRClient.cs (TODO)
│   │
│   └── Services/
│       ├── LeaderboardService.cs (TODO)
│       ├── ProfileService.cs (TODO)
│       ├── FriendsService.cs (TODO)
│       └── SocialService.cs (TODO)
│
└── Tests/
    ├── AuthServiceTests.cs (expand)
    ├── LeaderboardServiceTests.cs (TODO)
    └── ProfileServiceTests.cs (TODO)
```

---

## 🔗 API ENDPOINTS CONFIRMADOS

**Base URL:** `http://localhost:5000`

### Auth
- `POST /api/auth/login` (Email, Password) → JWT token ✅
- `POST /api/auth/register` (Email, Username, Password) → JWT token ✅
- `POST /api/auth/refresh` (refresh token) → new JWT (TODO)

### Users
- `GET /api/users/{playerId}/profile` → Profile info
- `PATCH /api/users/{playerId}/profile` → Update profile
- `GET /api/users/{playerId}/stats` → Player stats
- `GET /api/users/{playerId}/achievements` → Achievements

### Decks
- `GET /api/users/{playerId}/decks` → List decks
- `GET /api/decks/{deckId}` → Get deck
- `POST /api/decks` → Create deck
- `PATCH /api/decks/{deckId}` → Update deck
- `DELETE /api/decks/{deckId}` → Delete deck

### Matches
- `GET /api/matches` → List matches
- `GET /api/matches/{matchId}/summary` → Match summary
- `GET /api/matches/{matchId}/snapshot/{playerId}` → Current state
- `POST /api/matches/{matchId}/ready` → Set ready
- `POST /api/matches/{matchId}/play` → Play card
- `POST /api/matches/{matchId}/end-turn` → End turn
- `POST /api/matches/{matchId}/forfeit` → Surrender

### Match History
- `GET /api/matches/history/{playerId}?page=1&pageSize=20` → History

### Cards
- `GET /api/cards` → All cards
- `GET /api/cards/{cardId}` → Specific card
- `GET /api/cards/search?q=...` → Search

### Leaderboards
- `GET /api/leaderboards` → Top 100 (TODO: create endpoint)
- `GET /api/leaderboards/{season}` → Seasonal (TODO)

### Replays
- `GET /api/replays/{matchId}` → Download replay (TODO)
- `POST /api/matches/{matchId}/replay` → Upload replay (TODO)

### Tournaments (existe en API)
- `GET /api/tournaments` → List tournaments
- `POST /api/tournaments` → Create tournament
- `POST /api/tournaments/{tournamentId}/join` → Join tournament
- `GET /api/tournaments/{tournamentId}/bracket` → Bracket

---

## 🛠️ ORDEN RECOMENDADO DE IMPLEMENTACIÓN

### **Sprint 1: Polish & Testing** (3-4 días)
1. API Testing Menu para debug
2. Real error handling + user messages
3. MainMenuScreen en escena principal
4. Unit tests para AuthService
5. Manual testing end-to-end

### **Sprint 2: Progression** (3-4 días)
1. LeaderboardScreen
2. ProfileScreen
3. Match completion → server
4. Stats sync

### **Sprint 3: Real-time** (5-7 días)
1. SignalR integration
2. Live leaderboard
3. Match notifications
4. Rate limiting

### **Sprint 4: Social** (5-7 días)
1. Friends system
2. Clan system (básico)
3. Chat
4. Player profiles visibles

---

## 💡 OPTIMIZATION NOTES

### Performance
- [ ] Lazy-load screens (no cargar todo en startup)
- [ ] Prefab pooling para cards en UI
- [ ] Batch network requests
- [ ] Cache agresivo con TTL configurable

### Network
- [ ] HTTPS obligatorio en prod
- [ ] Gzip compression
- [ ] Request deduplication
- [ ] Offline queue + sync

### Security
- [ ] Validate all inputs servidor-side
- [ ] Anti-cheat básico (verify match results)
- [ ] Rate limiting servidor
- [ ] JWT secret rotation

---

## 📌 PENDIENTES INMEDIATOS

```
[ ] 1. Crear ApiTestMenu.cs (Tools/UI/Menu)
[ ] 2. Integrar MainMenuScreen en Login scene
[ ] 3. Test login real con API
[ ] 4. Implementar LeaderboardScreen
[ ] 5. Implementar ProfileScreen
[ ] 6. Fix match completion → server
[ ] 7. Add error handler global
[ ] 8. SignalR integration (phase 3)
```

---

## 📞 CONTACT & NOTES

- **API Health:** Check `/health` endpoint
- **API Logs:** See server console
- **DB:** SQLite (development), SQL Server (production)
- **Auth:** JWT 24h expiry, no refresh token yet

**Documentación API:** `/api/swagger` (si está habilitado)

---

**Estado:** Ready for Sprint 1  
**Bloqueadores:** Ninguno crítico  
**Riesgo:** BAJO (authentication funcional, endpoints confirmados)
