# Card Game Networking Layer

Complete networking and service layer for the Card Game client. Handles authentication, card catalog management, player profiles, deck management, matchmaking, and offline support.

## Quick Start

### 1. Add GameService to Scene
- Create empty GameObject
- Add `GameService` component (in Assets/Runtime/Networking)
- Set API Base URL in inspector (default: `http://localhost:5000`)
- Configure timeout and retry settings if needed

### 2. Bootstrap on Startup
```csharp
var gameService = GameService.Instance;
var success = await gameService.Bootstrap(); // Loads card catalog
```

### 3. Login
```csharp
var loggedIn = await gameService.Login("player_id", "password");
if (loggedIn)
{
    // Now authenticated
}
```

## Architecture

```
┌─────────────────────────────────────────────────────┐
│                   GameService                       │
│                  (Singleton Hub)                    │
├─────────────────────────────────────────────────────┤
│                                                     │
│  CardGameApiClient (HTTP + Retry + Timeout)       │
│  ├─ AuthService (JWT tokens)                      │
│  ├─ CardCatalogCache (card definitions)           │
│  ├─ UserService (profiles & stats)                │
│  ├─ DeckService (deck management)                 │
│  ├─ MatchHistoryService (match tracking)          │
│  ├─ MatchmakingService (queue management)         │
│  ├─ EloRatingService (rating calculations)        │
│  ├─ MatchCompletionService (match results)        │
│  ├─ LocalCacheService (offline storage)           │
│  └─ OfflineSyncService (pending changes)          │
│                                                     │
│  All auto-initialized on Awake()                  │
└─────────────────────────────────────────────────────┘
```

## Service Reference

### GameService
Central coordinator for all networking and service operations.

**Properties:**
- `IsReady`: Services initialized successfully
- `IsCatalogReady`: Card catalog loaded
- `IsAuthenticated`: Player logged in with valid token

**Methods:**
- `Bootstrap()`: Load card catalog
- `Login(playerId, password)`: Authenticate player
- `Logout()`: Clear authentication
- `LoadMatchHistory(page, pageSize)`: Get player's match history
- `GetCardStats()`: Get catalog statistics
- `ValidateDeck(cardIds)`: Validate deck against rules

### CardGameApiClient
HTTP client with built-in retry and timeout logic.

**Properties:**
- `BaseUrl`: API endpoint (configurable)
- `TimeoutSeconds`: Request timeout (default: 30s)
- `MaxRetries`: Retry attempts on failure (default: 3)

**Methods:**
- `FetchAllCards()`: GET /api/cards
- `FetchCard(cardId)`: GET /api/cards/{id}
- `SearchCards(query)`: GET /api/cards/search?q={query}
- `FetchCardStats()`: GET /api/cards/stats

**Features:**
- Exponential backoff retry (500+ errors & timeouts)
- Configurable request timeout
- Automatic bearer token injection (via AuthService)

### AuthService
JWT token management with auto-refresh.

**Properties:**
- `IsAuthenticated`: Has valid token
- `CurrentPlayerId`: Logged-in player ID
- `CurrentToken`: JWT token (from storage)

**Methods:**
- `Login(playerId, password)`: Authenticate (uses mock tokens for now)
- `Logout()`: Clear tokens
- `GetAuthorizationHeader()`: Bearer token for requests
- `RefreshTokenIfNeeded()`: Auto-refresh if expiring soon

**Storage:**
- Uses PlayerPrefs (⚠️ INSECURE - replace with Keychain/Keystore in production)
- Token expiry checks automatically

### CardCatalogCache
Local cache of card definitions from API.

**Properties:**
- `IsLoaded`: Catalog ready to use
- `IsLoading`: Currently fetching from API
- `LoadError`: Exception if load failed

**Methods:**
- `LoadCatalog()`: Fetch from API and cache
- `GetCard(cardId)`: Lookup card by ID
- `GetAll()`: All cards (returns empty dict if not loaded)
- `ValidateDeck(cardIds)`: Check deck rules
- `GetStats()`: Card count and ability count
- `Clear()`: Force reload next time

**Rules:**
- Min 20 cards, Max 30 cards per deck
- Max 3 copies of same card
- No limit on legendaries (server enforces)

### UserService
Player profile and statistics management.

**Methods:**
- `GetProfile(playerId)`: Player info (name, level, faction, premium status)
- `GetStats(playerId)`: Win/loss record, ratings, division
- `UpdateProfile(profile)`: Update player info
- `GetAchievements(playerId)`: List of achievements
- `UnlockAchievement(achievementId)`: Unlock achievement

**Features:**
- 5-minute cache for profiles
- Automatic auth check
- Token refresh before calls

### DeckService
Deck creation, editing, and management.

**Methods:**
- `LoadDecks(playerId)`: Get all decks
- `GetDeck(deckId)`: Fetch single deck
- `CreateDeck(name, description, cardIds)`: New deck with validation
- `UpdateDeck(deckId, name, description, cardIds)`: Update with validation
- `DeleteDeck(deckId)`: Remove deck
- `ValidateDeck(cardIds)`: Check rules

**Features:**
- Deck validation against catalog before API call
- Local caching of loaded decks
- Token refresh before requests

### MatchmakingService
Queue management for casual and ranked modes.

**Enums:**
```csharp
enum QueueMode { Casual = 0, Ranked = 1 }
```

**Methods:**
- `JoinQueue(mode)`: Enter matchmaking queue
- `LeaveQueue()`: Exit queue
- `GetStatus()`: Current queue status
- `CancelSearch()`: Alias for LeaveQueue

**Status Data:**
- `IsSearching`: Currently in queue
- `TimeInQueue`: Seconds waiting
- `EstimatedWait`: Estimated seconds until match
- `PlayersInQueue`: Queue population
- `OpponentId`: Found opponent (if null, still searching)

### EloRatingService
Rating calculations synced with API.

**Constants:**
- K factor: 32
- Floor: 100
- Ceiling: 4000
- Formula: expectedScore = 1 / (1 + 10^((opponent - you) / 400))

**Methods:**
- `CalculateEloChange(r1, r2, player1Won)`: New ratings
- `CalculateDelta(rating, opponentRating, won)`: Rating change only
- `GetExpectedWinRate(r1, r2)`: Win probability (0-1)

**Usage:**
```csharp
var (newYourRating, newOpponentRating) = EloRatingService.CalculateEloChange(1600, 1580, won: true);
var delta = EloRatingService.CalculateDelta(1600, 1580, won: true); // +32 or so
var winProb = EloRatingService.GetExpectedWinRate(1600, 1580); // ~0.55
```

### MatchHistoryService
Paginated match history with caching.

**Methods:**
- `FetchHistory(playerId, page, pageSize)`: Get page of matches
- `GetWinRateFromCache(playerId)`: Quick (wins, losses, total)
- `ClearCache()`: Invalidate cache

### MatchCompletionService
Handle match results and rating updates.

**Methods:**
- `CompleteMatch(matchId, playerId, opponentId, ...)`: Send match result
- `GetExpectedRatingChange(rating, opponentRating)`: Estimated deltas
- `GetExpectedWinProbability(rating, opponentRating)`: Win probability

### LocalCacheService
Offline data storage with TTL expiry.

**Methods:**
- `Set<T>(key, value, expiryHours=24)`: Cache data
- `Get<T>(key)`: Retrieve (null if expired or missing)
- `Has(key)`: Check if exists and not expired
- `Delete(key)`: Remove entry
- `Clear()`: Clear all cache
- `GetStats()`: Returns (totalKeys, expiredKeys)

**Usage:**
```csharp
// Cache player profile
gameService.LocalCache.Set("profile", profileData, expiryHours: 24);

// Later, retrieve
var cached = gameService.LocalCache.Get<PlayerProfileDto>("profile");
if (cached != null) {
    // Use cached data
}
```

### OfflineSyncService
Tracks changes made offline, syncs when reconnected.

**Methods:**
- `MarkPending(changeId, changeData)`: Queue change for sync
- `GetPendingChanges()`: List of pending changes
- `MarkSynced(changeId)`: Remove from pending
- `SetOnlineStatus(isOnline)`: Update connection state

**Properties:**
- `IsOnline`: Current connection state
- `PendingChanges`: Count of unsynced changes

**Usage:**
```csharp
// When deck is updated while offline
gameService.OfflineSync.MarkPending("deck_update_1", deckData);

// Later, when online
if (gameService.OfflineSync.IsOnline) {
    var pending = gameService.OfflineSync.GetPendingChanges();
    foreach (var (id, data) in pending) {
        await SendToServer(data);
        gameService.OfflineSync.MarkSynced(id);
    }
}
```

## Testing

### Unit Tests
All services have comprehensive unit # API Integration Documentation

**Last Updated:** 2026-04-18  
**Scope:** Integrate HTTP REST API with Netcode-based card game  
**Status:** Planning phase, docs ready for implementation

---

## 🚀 Quick Start

**Architecture:** Netcode (real-time) + HTTP API (persistent storage)

1. **Read first:** `ARCHITECTURE_CLARIFICATION.md` — understand separation of concerns
2. **Share with backend:** `api_stuff/missing.md` — what API needs to fix
3. **Implement:** Follow `IMPLEMENTATION_TASKS.md` Phase 1-4
4. **Reference:** `HTTP_VERSIONING_STRATEGY.md` for polling & versioning

---

## 📁 Documents Overview

### Understanding
- **ARCHITECTURE_CLARIFICATION.md** ⭐ START HERE
  - Clear separation: Netcode vs. HTTP API
  - What goes where and why
  - HTTP patterns (polling, versioning, consistency)

- **HTTP_VERSIONING_STRATEGY.md**
  - API versioning (v1 → v2)
  - Schema versioning (breaking changes)
  - Polling intervals & optimization
  - Stale data detection

### Implementation
- **IMPLEMENTATION_TASKS.md**
  - Phase 1: Checkpoint service (periodic saves every 30s)
  - Phase 2: Reconnection service (crash recovery)
  - Phase 3: Leaderboard UI
  - Phase 4: Polish & error handling
  - Detailed code examples and checklist

- **API_INTEGRATION_PLAN.md** (detailed reference)
  - Complete API endpoint inventory
  - 5-phase implementation plan
  - File-by-file changes
  - Success criteria

- **API_QUICK_REFERENCE.md** (one-page cheat sheet)
  - What API has vs. missing
  - Data flow diagram
  - Quick timeline

### For Backend Team
- **api_stuff/missing.md** 📢
  - 14 issues found (critical, medium, low)
  - What needs fixing before game can integrate
  - Questions for API owner

---

## 🎮 Architecture Summary

```
Player plays card
    ↓
Netcode broadcasts (real-time, all clients see instantly)
    ↓
(Every 30 seconds)
    ↓
MatchCheckpointService saves snapshot to API (async, fire-and-forget)
    ↓
Match ends
    ↓
POST final result + ratings to API
    ↓
API updates match history & ratings
```

### Netcode (Real-Time Authority)
✅ Card plays, board updates, turn execution  
✅ Sub-100ms latency, all clients sync instantly  
✅ Handles all gameplay logic  
✅ Authoritative source during match  

### HTTP API (Durable Storage)
✅ Periodic checkpoints (every 30s)  
✅ Match completion & ratings  
✅ State recovery after crash  
✅ Leaderboards & player profiles  
✅ Match history  

---

## 📋 Implementation Roadmap

### Week 1: Foundation
- Backend: Add reconnect endpoint, snapshot versioning
- Game: Create MatchCheckpointService, update CardGameApiClient
- **Result:** Checkpoints working

### Week 2: Recovery
- Game: Create ReconnectionService, update GameBootstrap
- **Result:** Crash recovery working

### Week 3: Features
- Game: Create LeaderboardService & UI screens
- **Result:** Leaderboards visible

### Week 4: Polish
- Game: JWT refresh, error handling, offline detection
- **Result:** Production-ready

---

## 🎯 Success Criteria

✅ Netcode controls gameplay (API never blocks)  
✅ Checkpoints fire-and-forget (no gameplay lag)  
✅ Crash recovery works (restore from checkpoint)  
✅ Leaderboards functional (top 100, polled)  
✅ No data loss (all moves logged via Netcode + API)  
✅ Versioning clear (handle schema changes)  

---

## 📝 Before Talking to Backend

1. Read `ARCHITECTURE_CLARIFICATION.md`
2. Review `api_stuff/missing.md`
3. Ask questions from `HTTP_VERSIONING_STRATEGY.md`

## 🔧 Before Starting Code

1. Backend provides: reconnect endpoint, checkpoint clarity, MatchSnapshot schema
2. Game team decides: local storage (PlayerPrefs vs. secure), offline policy, acceptable data loss

---

## 📚 Document Legend

| Doc | Length | Purpose | Read When |
|-----|--------|---------|-----------|
| ARCHITECTURE_CLARIFICATION.md | 15 min | Understand design | First thing, with backend |
| IMPLEMENTATION_TASKS.md | 20 min | Detailed tasks | Ready to code |
| HTTP_VERSIONING_STRATEGY.md | 15 min | Polling & versions | Before implementation |
| API_INTEGRATION_PLAN.md | 30 min | Full scope (detailed) | Reference, detailed questions |
| API_QUICK_REFERENCE.md | 5 min | One-page summary | Quick lookup |
| api_stuff/missing.md | 10 min | API issues | Share with backend |
| README.md (this) | 5 min | Overview | Navigation |

---

## 🚨 Critical Points

⚠️ **Netcode is real-time authority** — API never makes gameplay decisions  
⚠️ **Checkpoints are durability only** — not game-state sync  
⚠️ **API calls are async fire-and-forget** — don't wait for responses  
⚠️ **Polling has intervals** — leaderboard 30s, stats 5m, reconnect exponential backoff  
⚠️ **Schema versioning needed** — handle breaking changes without crashing  

---

## 📞 Quick Answers

**Q: Won't HTTP ruin real-time gameplay?**  
A: No. Netcode handles real-time. API only saves snapshots async (doesn't block).

**Q: What if I crash at turn 50?**  
A: Restore checkpoint from turn ~48, catch up via Netcode = consistent.

**Q: What if network drops?**  
A: Netcode drops but checkpoint still saves every 30s. Auto-forfeit after 5m offline.

**Q: How much battery does polling drain?**  
A: Only when screen visible. Leaderboard: 30s interval = minimal.

**Q: What if checkpoint is stale?**  
A: Snapshot has timestamp. Reject if >5m old, use if <30s.

---

## 📂 File Structure

```
Assets/Runtime/Networking/
├── README.md ← You are here
├── ARCHITECTURE_CLARIFICATION.md ← Start here
├── IMPLEMENTATION_TASKS.md
├── API_INTEGRATION_PLAN.md
├── API_QUICK_REFERENCE.md
├── HTTP_VERSIONING_STRATEGY.md
│
├── api_stuff/
│   └── missing.md ← Share with backend
│
├── [TO CREATE]
├── MatchCheckpointService.cs
├── ReconnectionService.cs
├── LeaderboardService.cs
│
└── [TO MODIFY]
    ├── CardGameApiClient.cs
    ├── LocalCacheService.cs
    ├── GameBootstrap.cs
    └── GameService.cs

Assets/Runtime/UI/
└── [TO CREATE]
    ├── LeaderboardScreen.cs
    └── ProfileScreen.cs
```

---

**Status:** Documentation complete, ready for implementation  
**Next:** Read ARCHITECTURE_CLARIFICATION.md
s in `Assets/Tests/Battle/`:

- `CardGameApiClientTests` (basic HTTP operations)
- `AuthServiceTests` (authentication)
- `UserServiceTests` (profile operations)
- `DeckServiceTests` (deck management)
- `MatchmakingServiceTests` (queue logic)
- `LocalCacheServiceTests` (offline storage)
- `EloRatingServiceTests` (rating calculations)
- `MatchCompletionServiceTests` (match results)

### Integration Tests
`IntegrationTests.cs` # API Integration Documentation

**Last Updated:** 2026-04-18  
**Scope:** Integrate HTTP REST API with Netcode-based card game  
**Status:** Planning phase, docs ready for implementation

---

## 🚀 Quick Start

**Architecture:** Netcode (real-time) + HTTP API (persistent storage)

1. **Read first:** `ARCHITECTURE_CLARIFICATION.md` — understand separation of concerns
2. **Share with backend:** `api_stuff/missing.md` — what API needs to fix
3. **Implement:** Follow `IMPLEMENTATION_TASKS.md` Phase 1-4
4. **Reference:** `HTTP_VERSIONING_STRATEGY.md` for polling & versioning

---

## 📁 Documents Overview

### Understanding
- **ARCHITECTURE_CLARIFICATION.md** ⭐ START HERE
  - Clear separation: Netcode vs. HTTP API
  - What goes where and why
  - HTTP patterns (polling, versioning, consistency)

- **HTTP_VERSIONING_STRATEGY.md**
  - API versioning (v1 → v2)
  - Schema versioning (breaking changes)
  - Polling intervals & optimization
  - Stale data detection

### Implementation
- **IMPLEMENTATION_TASKS.md**
  - Phase 1: Checkpoint service (periodic saves every 30s)
  - Phase 2: Reconnection service (crash recovery)
  - Phase 3: Leaderboard UI
  - Phase 4: Polish & error handling
  - Detailed code examples and checklist

- **API_INTEGRATION_PLAN.md** (detailed reference)
  - Complete API endpoint inventory
  - 5-phase implementation plan
  - File-by-file changes
  - Success criteria

- **API_QUICK_REFERENCE.md** (one-page cheat sheet)
  - What API has vs. missing
  - Data flow diagram
  - Quick timeline

### For Backend Team
- **api_stuff/missing.md** 📢
  - 14 issues found (critical, medium, low)
  - What needs fixing before game can integrate
  - Questions for API owner

---

## 🎮 Architecture Summary

```
Player plays card
    ↓
Netcode broadcasts (real-time, all clients see instantly)
    ↓
(Every 30 seconds)
    ↓
MatchCheckpointService saves snapshot to API (async, fire-and-forget)
    ↓
Match ends
    ↓
POST final result + ratings to API
    ↓
API updates match history & ratings
```

### Netcode (Real-Time Authority)
✅ Card plays, board updates, turn execution  
✅ Sub-100ms latency, all clients sync instantly  
✅ Handles all gameplay logic  
✅ Authoritative source during match  

### HTTP API (Durable Storage)
✅ Periodic checkpoints (every 30s)  
✅ Match completion & ratings  
✅ State recovery after crash  
✅ Leaderboards & player profiles  
✅ Match history  

---

## 📋 Implementation Roadmap

### Week 1: Foundation
- Backend: Add reconnect endpoint, snapshot versioning
- Game: Create MatchCheckpointService, update CardGameApiClient
- **Result:** Checkpoints working

### Week 2: Recovery
- Game: Create ReconnectionService, update GameBootstrap
- **Result:** Crash recovery working

### Week 3: Features
- Game: Create LeaderboardService & UI screens
- **Result:** Leaderboards visible

### Week 4: Polish
- Game: JWT refresh, error handling, offline detection
- **Result:** Production-ready

---

## 🎯 Success Criteria

✅ Netcode controls gameplay (API never blocks)  
✅ Checkpoints fire-and-forget (no gameplay lag)  
✅ Crash recovery works (restore from checkpoint)  
✅ Leaderboards functional (top 100, polled)  
✅ No data loss (all moves logged via Netcode + API)  
✅ Versioning clear (handle schema changes)  

---

## 📝 Before Talking to Backend

1. Read `ARCHITECTURE_CLARIFICATION.md`
2. Review `api_stuff/missing.md`
3. Ask questions from `HTTP_VERSIONING_STRATEGY.md`

## 🔧 Before Starting Code

1. Backend provides: reconnect endpoint, checkpoint clarity, MatchSnapshot schema
2. Game team decides: local storage (PlayerPrefs vs. secure), offline policy, acceptable data loss

---

## 📚 Document Legend

| Doc | Length | Purpose | Read When |
|-----|--------|---------|-----------|
| ARCHITECTURE_CLARIFICATION.md | 15 min | Understand design | First thing, with backend |
| IMPLEMENTATION_TASKS.md | 20 min | Detailed tasks | Ready to code |
| HTTP_VERSIONING_STRATEGY.md | 15 min | Polling & versions | Before implementation |
| API_INTEGRATION_PLAN.md | 30 min | Full scope (detailed) | Reference, detailed questions |
| API_QUICK_REFERENCE.md | 5 min | One-page summary | Quick lookup |
| api_stuff/missing.md | 10 min | API issues | Share with backend |
| README.md (this) | 5 min | Overview | Navigation |

---

## 🚨 Critical Points

⚠️ **Netcode is real-time authority** — API never makes gameplay decisions  
⚠️ **Checkpoints are durability only** — not game-state sync  
⚠️ **API calls are async fire-and-forget** — don't wait for responses  
⚠️ **Polling has intervals** — leaderboard 30s, stats 5m, reconnect exponential backoff  
⚠️ **Schema versioning needed** — handle breaking changes without crashing  

---

## 📞 Quick Answers

**Q: Won't HTTP ruin real-time gameplay?**  
A: No. Netcode handles real-time. API only saves snapshots async (doesn't block).

**Q: What if I crash at turn 50?**  
A: Restore checkpoint from turn ~48, catch up via Netcode = consistent.

**Q: What if network drops?**  
A: Netcode drops but checkpoint still saves every 30s. Auto-forfeit after 5m offline.

**Q: How much battery does polling drain?**  
A: Only when screen visible. Leaderboard: 30s interval = minimal.

**Q: What if checkpoint is stale?**  
A: Snapshot has timestamp. Reject if >5m old, use if <30s.

---

## 📂 File Structure

```
Assets/Runtime/Networking/
├── README.md ← You are here
├── ARCHITECTURE_CLARIFICATION.md ← Start here
├── IMPLEMENTATION_TASKS.md
├── API_INTEGRATION_PLAN.md
├── API_QUICK_REFERENCE.md
├── HTTP_VERSIONING_STRATEGY.md
│
├── api_stuff/
│   └── missing.md ← Share with backend
│
├── [TO CREATE]
├── MatchCheckpointService.cs
├── ReconnectionService.cs
├── LeaderboardService.cs
│
└── [TO MODIFY]
    ├── CardGameApiClient.cs
    ├── LocalCacheService.cs
    ├── GameBootstrap.cs
    └── GameService.cs

Assets/Runtime/UI/
└── [TO CREATE]
    ├── LeaderboardScreen.cs
    └── ProfileScreen.cs
```

---

**Status:** Documentation complete, ready for implementation  
**Next:** Read ARCHITECTURE_CLARIFICATION.md
s full workflows:
- Bootstrap → Load catalog
- Login → Authenticate
- Deck validation
- Service initialization
- Offline mode

### Mock Server
`FakeHttpServer.cs` provides mock API responses for # API Integration Documentation

**Last Updated:** 2026-04-18  
**Scope:** Integrate HTTP REST API with Netcode-based card game  
**Status:** Planning phase, docs ready for implementation

---

## 🚀 Quick Start

**Architecture:** Netcode (real-time) + HTTP API (persistent storage)

1. **Read first:** `ARCHITECTURE_CLARIFICATION.md` — understand separation of concerns
2. **Share with backend:** `api_stuff/missing.md` — what API needs to fix
3. **Implement:** Follow `IMPLEMENTATION_TASKS.md` Phase 1-4
4. **Reference:** `HTTP_VERSIONING_STRATEGY.md` for polling & versioning

---

## 📁 Documents Overview

### Understanding
- **ARCHITECTURE_CLARIFICATION.md** ⭐ START HERE
  - Clear separation: Netcode vs. HTTP API
  - What goes where and why
  - HTTP patterns (polling, versioning, consistency)

- **HTTP_VERSIONING_STRATEGY.md**
  - API versioning (v1 → v2)
  - Schema versioning (breaking changes)
  - Polling intervals & optimization
  - Stale data detection

### Implementation
- **IMPLEMENTATION_TASKS.md**
  - Phase 1: Checkpoint service (periodic saves every 30s)
  - Phase 2: Reconnection service (crash recovery)
  - Phase 3: Leaderboard UI
  - Phase 4: Polish & error handling
  - Detailed code examples and checklist

- **API_INTEGRATION_PLAN.md** (detailed reference)
  - Complete API endpoint inventory
  - 5-phase implementation plan
  - File-by-file changes
  - Success criteria

- **API_QUICK_REFERENCE.md** (one-page cheat sheet)
  - What API has vs. missing
  - Data flow diagram
  - Quick timeline

### For Backend Team
- **api_stuff/missing.md** 📢
  - 14 issues found (critical, medium, low)
  - What needs fixing before game can integrate
  - Questions for API owner

---

## 🎮 Architecture Summary

```
Player plays card
    ↓
Netcode broadcasts (real-time, all clients see instantly)
    ↓
(Every 30 seconds)
    ↓
MatchCheckpointService saves snapshot to API (async, fire-and-forget)
    ↓
Match ends
    ↓
POST final result + ratings to API
    ↓
API updates match history & ratings
```

### Netcode (Real-Time Authority)
✅ Card plays, board updates, turn execution  
✅ Sub-100ms latency, all clients sync instantly  
✅ Handles all gameplay logic  
✅ Authoritative source during match  

### HTTP API (Durable Storage)
✅ Periodic checkpoints (every 30s)  
✅ Match completion & ratings  
✅ State recovery after crash  
✅ Leaderboards & player profiles  
✅ Match history  

---

## 📋 Implementation Roadmap

### Week 1: Foundation
- Backend: Add reconnect endpoint, snapshot versioning
- Game: Create MatchCheckpointService, update CardGameApiClient
- **Result:** Checkpoints working

### Week 2: Recovery
- Game: Create ReconnectionService, update GameBootstrap
- **Result:** Crash recovery working

### Week 3: Features
- Game: Create LeaderboardService & UI screens
- **Result:** Leaderboards visible

### Week 4: Polish
- Game: JWT refresh, error handling, offline detection
- **Result:** Production-ready

---

## 🎯 Success Criteria

✅ Netcode controls gameplay (API never blocks)  
✅ Checkpoints fire-and-forget (no gameplay lag)  
✅ Crash recovery works (restore from checkpoint)  
✅ Leaderboards functional (top 100, polled)  
✅ No data loss (all moves logged via Netcode + API)  
✅ Versioning clear (handle schema changes)  

---

## 📝 Before Talking to Backend

1. Read `ARCHITECTURE_CLARIFICATION.md`
2. Review `api_stuff/missing.md`
3. Ask questions from `HTTP_VERSIONING_STRATEGY.md`

## 🔧 Before Starting Code

1. Backend provides: reconnect endpoint, checkpoint clarity, MatchSnapshot schema
2. Game team decides: local storage (PlayerPrefs vs. secure), offline policy, acceptable data loss

---

## 📚 Document Legend

| Doc | Length | Purpose | Read When |
|-----|--------|---------|-----------|
| ARCHITECTURE_CLARIFICATION.md | 15 min | Understand design | First thing, with backend |
| IMPLEMENTATION_TASKS.md | 20 min | Detailed tasks | Ready to code |
| HTTP_VERSIONING_STRATEGY.md | 15 min | Polling & versions | Before implementation |
| API_INTEGRATION_PLAN.md | 30 min | Full scope (detailed) | Reference, detailed questions |
| API_QUICK_REFERENCE.md | 5 min | One-page summary | Quick lookup |
| api_stuff/missing.md | 10 min | API issues | Share with backend |
| README.md (this) | 5 min | Overview | Navigation |

---

## 🚨 Critical Points

⚠️ **Netcode is real-time authority** — API never makes gameplay decisions  
⚠️ **Checkpoints are durability only** — not game-state sync  
⚠️ **API calls are async fire-and-forget** — don't wait for responses  
⚠️ **Polling has intervals** — leaderboard 30s, stats 5m, reconnect exponential backoff  
⚠️ **Schema versioning needed** — handle breaking changes without crashing  

---

## 📞 Quick Answers

**Q: Won't HTTP ruin real-time gameplay?**  
A: No. Netcode handles real-time. API only saves snapshots async (doesn't block).

**Q: What if I crash at turn 50?**  
A: Restore checkpoint from turn ~48, catch up via Netcode = consistent.

**Q: What if network drops?**  
A: Netcode drops but checkpoint still saves every 30s. Auto-forfeit after 5m offline.

**Q: How much battery does polling drain?**  
A: Only when screen visible. Leaderboard: 30s interval = minimal.

**Q: What if checkpoint is stale?**  
A: Snapshot has timestamp. Reject if >5m old, use if <30s.

---

## 📂 File Structure

```
Assets/Runtime/Networking/
├── README.md ← You are here
├── ARCHITECTURE_CLARIFICATION.md ← Start here
├── IMPLEMENTATION_TASKS.md
├── API_INTEGRATION_PLAN.md
├── API_QUICK_REFERENCE.md
├── HTTP_VERSIONING_STRATEGY.md
│
├── api_stuff/
│   └── missing.md ← Share with backend
│
├── [TO CREATE]
├── MatchCheckpointService.cs
├── ReconnectionService.cs
├── LeaderboardService.cs
│
└── [TO MODIFY]
    ├── CardGameApiClient.cs
    ├── LocalCacheService.cs
    ├── GameBootstrap.cs
    └── GameService.cs

Assets/Runtime/UI/
└── [TO CREATE]
    ├── LeaderboardScreen.cs
    └── ProfileScreen.cs
```

---

**Status:** Documentation complete, ready for implementation  
**Next:** Read ARCHITECTURE_CLARIFICATION.md
ing without real backend:

```csharp
// In # API Integration Documentation

**Last Updated:** 2026-04-18  
**Scope:** Integrate HTTP REST API with Netcode-based card game  
**Status:** Planning phase, docs ready for implementation

---

## 🚀 Quick Start

**Architecture:** Netcode (real-time) + HTTP API (persistent storage)

1. **Read first:** `ARCHITECTURE_CLARIFICATION.md` — understand separation of concerns
2. **Share with backend:** `api_stuff/missing.md` — what API needs to fix
3. **Implement:** Follow `IMPLEMENTATION_TASKS.md` Phase 1-4
4. **Reference:** `HTTP_VERSIONING_STRATEGY.md` for polling & versioning

---

## 📁 Documents Overview

### Understanding
- **ARCHITECTURE_CLARIFICATION.md** ⭐ START HERE
  - Clear separation: Netcode vs. HTTP API
  - What goes where and why
  - HTTP patterns (polling, versioning, consistency)

- **HTTP_VERSIONING_STRATEGY.md**
  - API versioning (v1 → v2)
  - Schema versioning (breaking changes)
  - Polling intervals & optimization
  - Stale data detection

### Implementation
- **IMPLEMENTATION_TASKS.md**
  - Phase 1: Checkpoint service (periodic saves every 30s)
  - Phase 2: Reconnection service (crash recovery)
  - Phase 3: Leaderboard UI
  - Phase 4: Polish & error handling
  - Detailed code examples and checklist

- **API_INTEGRATION_PLAN.md** (detailed reference)
  - Complete API endpoint inventory
  - 5-phase implementation plan
  - File-by-file changes
  - Success criteria

- **API_QUICK_REFERENCE.md** (one-page cheat sheet)
  - What API has vs. missing
  - Data flow diagram
  - Quick timeline

### For Backend Team
- **api_stuff/missing.md** 📢
  - 14 issues found (critical, medium, low)
  - What needs fixing before game can integrate
  - Questions for API owner

---

## 🎮 Architecture Summary

```
Player plays card
    ↓
Netcode broadcasts (real-time, all clients see instantly)
    ↓
(Every 30 seconds)
    ↓
MatchCheckpointService saves snapshot to API (async, fire-and-forget)
    ↓
Match ends
    ↓
POST final result + ratings to API
    ↓
API updates match history & ratings
```

### Netcode (Real-Time Authority)
✅ Card plays, board updates, turn execution  
✅ Sub-100ms latency, all clients sync instantly  
✅ Handles all gameplay logic  
✅ Authoritative source during match  

### HTTP API (Durable Storage)
✅ Periodic checkpoints (every 30s)  
✅ Match completion & ratings  
✅ State recovery after crash  
✅ Leaderboards & player profiles  
✅ Match history  

---

## 📋 Implementation Roadmap

### Week 1: Foundation
- Backend: Add reconnect endpoint, snapshot versioning
- Game: Create MatchCheckpointService, update CardGameApiClient
- **Result:** Checkpoints working

### Week 2: Recovery
- Game: Create ReconnectionService, update GameBootstrap
- **Result:** Crash recovery working

### Week 3: Features
- Game: Create LeaderboardService & UI screens
- **Result:** Leaderboards visible

### Week 4: Polish
- Game: JWT refresh, error handling, offline detection
- **Result:** Production-ready

---

## 🎯 Success Criteria

✅ Netcode controls gameplay (API never blocks)  
✅ Checkpoints fire-and-forget (no gameplay lag)  
✅ Crash recovery works (restore from checkpoint)  
✅ Leaderboards functional (top 100, polled)  
✅ No data loss (all moves logged via Netcode + API)  
✅ Versioning clear (handle schema changes)  

---

## 📝 Before Talking to Backend

1. Read `ARCHITECTURE_CLARIFICATION.md`
2. Review `api_stuff/missing.md`
3. Ask questions from `HTTP_VERSIONING_STRATEGY.md`

## 🔧 Before Starting Code

1. Backend provides: reconnect endpoint, checkpoint clarity, MatchSnapshot schema
2. Game team decides: local storage (PlayerPrefs vs. secure), offline policy, acceptable data loss

---

## 📚 Document Legend

| Doc | Length | Purpose | Read When |
|-----|--------|---------|-----------|
| ARCHITECTURE_CLARIFICATION.md | 15 min | Understand design | First thing, with backend |
| IMPLEMENTATION_TASKS.md | 20 min | Detailed tasks | Ready to code |
| HTTP_VERSIONING_STRATEGY.md | 15 min | Polling & versions | Before implementation |
| API_INTEGRATION_PLAN.md | 30 min | Full scope (detailed) | Reference, detailed questions |
| API_QUICK_REFERENCE.md | 5 min | One-page summary | Quick lookup |
| api_stuff/missing.md | 10 min | API issues | Share with backend |
| README.md (this) | 5 min | Overview | Navigation |

---

## 🚨 Critical Points

⚠️ **Netcode is real-time authority** — API never makes gameplay decisions  
⚠️ **Checkpoints are durability only** — not game-state sync  
⚠️ **API calls are async fire-and-forget** — don't wait for responses  
⚠️ **Polling has intervals** — leaderboard 30s, stats 5m, reconnect exponential backoff  
⚠️ **Schema versioning needed** — handle breaking changes without crashing  

---

## 📞 Quick Answers

**Q: Won't HTTP ruin real-time gameplay?**  
A: No. Netcode handles real-time. API only saves snapshots async (doesn't block).

**Q: What if I crash at turn 50?**  
A: Restore checkpoint from turn ~48, catch up via Netcode = consistent.

**Q: What if network drops?**  
A: Netcode drops but checkpoint still saves every 30s. Auto-forfeit after 5m offline.

**Q: How much battery does polling drain?**  
A: Only when screen visible. Leaderboard: 30s interval = minimal.

**Q: What if checkpoint is stale?**  
A: Snapshot has timestamp. Reject if >5m old, use if <30s.

---

## 📂 File Structure

```
Assets/Runtime/Networking/
├── README.md ← You are here
├── ARCHITECTURE_CLARIFICATION.md ← Start here
├── IMPLEMENTATION_TASKS.md
├── API_INTEGRATION_PLAN.md
├── API_QUICK_REFERENCE.md
├── HTTP_VERSIONING_STRATEGY.md
│
├── api_stuff/
│   └── missing.md ← Share with backend
│
├── [TO CREATE]
├── MatchCheckpointService.cs
├── ReconnectionService.cs
├── LeaderboardService.cs
│
└── [TO MODIFY]
    ├── CardGameApiClient.cs
    ├── LocalCacheService.cs
    ├── GameBootstrap.cs
    └── GameService.cs

Assets/Runtime/UI/
└── [TO CREATE]
    ├── LeaderboardScreen.cs
    └── ProfileScreen.cs
```

---

**Status:** Documentation complete, ready for implementation  
**Next:** Read ARCHITECTURE_CLARIFICATION.md
s
FakeHttpServerExtensions.InitializeFakeServer();
var client = new CardGameApiClient("http://localhost:5000");
// Requests automatically intercepted with fake responses
```

## Configuration

### Editor (Inspector)
- `GameService` has `apiBaseUrl` field in inspector
- `CardGameApiClient.TimeoutSeconds` (default 30)
- `CardGameApiClient.MaxRetries` (default 3)

### Code
```csharp
// Change API endpoint
var client = new CardGameApiClient("https://production-api.com");

// Configure timeout and retries
client.TimeoutSeconds = 60;
client.MaxRetries = 5;

// Cache expiry
gameService.LocalCache.Set(key, value, expiryHours: 48);
```

## Error Handling

All services throw typed exceptions:

```csharp
try {
    await gameService.Login(id, pwd);
} 
catch (ValidationException ex) {
    // Invalid input (empty password, etc)
}
catch (InvalidGameStateException ex) {
    // State error (not authenticated, catalog not loaded)
}
catch (CardNotFoundException ex) {
    // Card ID not in catalog
}
catch (InsufficientResourcesException ex) {
    // Not enough mana, cards, etc
}
catch (GameException ex) {
    // Base exception for other errors
}
```

## Offline Mode

The client supports offline play with sync on reconnect:

1. **Detection:** Monitor network status
2. **Local Operation:** Use LocalCacheService for reads, OfflineSyncService for writes
3. **Sync Queue:** OfflineSyncService.MarkPending() for offline writes
4. **Reconnection:** Detect online status change
5. **Sync:** Send pending changes in order

```csharp
void OnNetworkStatusChanged(bool isOnline) {
    gameService.OfflineSync.SetOnlineStatus(isOnline);
    if (isOnline) {
        SyncPendingChanges();
    }
}

async Task SyncPendingChanges() {
    var pending = gameService.OfflineSync.GetPendingChanges();
    foreach (var (id, data) in pending) {
        try {
            await SendToServer(data);
            gameService.OfflineSync.MarkSynced(id);
        } catch {
            // Keep in pending queue, retry later
        }
    }
}
```

## Security Considerations

⚠️ **CRITICAL ISSUES:**
1. **Token Storage:** Uses PlayerPrefs (insecure). Replace with:
   - iOS: Keychain
   - Android: Keystore
   - See SECURITY.md for details

2. **HTTPS:** Production must enforce HTTPS (currently http://localhost for dev)

3. **Token Refresh:** Currently not fully implemented. Needs real endpoint.

4. **Deck Validation:** Server must re-validate (client-side can be bypassed)

See `SECURITY.md` for complete threat model and fixes.

## Performance Tips

1. **Caching:** Use LocalCacheService for frequently-accessed data
2. **Pagination:** LoadMatchHistory with reasonable pageSize (20-50)
3. **Batch Loads:** Call GetAll() instead of GetCard() in loop
4. **Retry Tuning:** Adjust MaxRetries and TimeoutSeconds for network conditions
5. **Offline Priority:** Keep working while offline, sync when ready

## Roadmap

**Phase 3: Real Authentication**
- [ ] POST /api/auth/login (replace mock)
- [ ] POST /api/auth/refresh (auto-refresh)
- [ ] Secure token storage (Keychain/Keystore)

**Phase 4: Match Flow**
- [ ] Create match endpoint
- [ ] Join match endpoint
- [ ] SignalR real-time updates

**Phase 5: Advanced Features**
- [ ] Replay download and storage
- [ ] Tournament support
- [ ] Seasonal rankings
- [ ] Leaderboards

## Files

```
Assets/Runtime/Networking/
├── GameService.cs (coordinator)
├── CardGameApiClient.cs (HTTP + retry/timeout)
├── AuthService.cs (JWT tokens)
├── CardCatalogCache.cs (card definitions)
├── UserService.cs (player profiles)
├── DeckService.cs (deck management)
├── MatchHistoryService.cs (match tracking)
├── MatchmakingService.cs (queue management)
├── MatchCompletionService.cs (match results)
├── EloRatingService.cs (rating calculations)
├── LocalCacheService.cs (offline storage)
├── NetworkBootstrap.cs (auto-initialize)
├── GameServiceExample.cs (usage examples)
├── README.md (this file)
├── SERVICES_GUIDE.md (API reference)
├── SECURITY.md (threat model)
└── ROADMAP.md (future plans)

Assets/Tests/Battle/
├── CardGameApiClientTests.cs
├── AuthServiceTests.cs
├── UserServiceTests.cs
├── DeckServiceTests.cs
├── MatchmakingServiceTests.cs
├── LocalCacheServiceTests.cs
├── EloRatingServiceTests.cs
├── MatchCompletionServiceTests.cs
├── IntegrationTests.cs
├── FakeHttpServer.cs
└── BattleTestHelpers.cs
```

## Contributing

When adding new services:
1. Extend GameService with new property
2. Initialize in InitializeServices()
3. Create service # API Integration Documentation

**Last Updated:** 2026-04-18  
**Scope:** Integrate HTTP REST API with Netcode-based card game  
**Status:** Planning phase, docs ready for implementation

---

## 🚀 Quick Start

**Architecture:** Netcode (real-time) + HTTP API (persistent storage)

1. **Read first:** `ARCHITECTURE_CLARIFICATION.md` — understand separation of concerns
2. **Share with backend:** `api_stuff/missing.md` — what API needs to fix
3. **Implement:** Follow `IMPLEMENTATION_TASKS.md` Phase 1-4
4. **Reference:** `HTTP_VERSIONING_STRATEGY.md` for polling & versioning

---

## 📁 Documents Overview

### Understanding
- **ARCHITECTURE_CLARIFICATION.md** ⭐ START HERE
  - Clear separation: Netcode vs. HTTP API
  - What goes where and why
  - HTTP patterns (polling, versioning, consistency)

- **HTTP_VERSIONING_STRATEGY.md**
  - API versioning (v1 → v2)
  - Schema versioning (breaking changes)
  - Polling intervals & optimization
  - Stale data detection

### Implementation
- **IMPLEMENTATION_TASKS.md**
  - Phase 1: Checkpoint service (periodic saves every 30s)
  - Phase 2: Reconnection service (crash recovery)
  - Phase 3: Leaderboard UI
  - Phase 4: Polish & error handling
  - Detailed code examples and checklist

- **API_INTEGRATION_PLAN.md** (detailed reference)
  - Complete API endpoint inventory
  - 5-phase implementation plan
  - File-by-file changes
  - Success criteria

- **API_QUICK_REFERENCE.md** (one-page cheat sheet)
  - What API has vs. missing
  - Data flow diagram
  - Quick timeline

### For Backend Team
- **api_stuff/missing.md** 📢
  - 14 issues found (critical, medium, low)
  - What needs fixing before game can integrate
  - Questions for API owner

---

## 🎮 Architecture Summary

```
Player plays card
    ↓
Netcode broadcasts (real-time, all clients see instantly)
    ↓
(Every 30 seconds)
    ↓
MatchCheckpointService saves snapshot to API (async, fire-and-forget)
    ↓
Match ends
    ↓
POST final result + ratings to API
    ↓
API updates match history & ratings
```

### Netcode (Real-Time Authority)
✅ Card plays, board updates, turn execution  
✅ Sub-100ms latency, all clients sync instantly  
✅ Handles all gameplay logic  
✅ Authoritative source during match  

### HTTP API (Durable Storage)
✅ Periodic checkpoints (every 30s)  
✅ Match completion & ratings  
✅ State recovery after crash  
✅ Leaderboards & player profiles  
✅ Match history  

---

## 📋 Implementation Roadmap

### Week 1: Foundation
- Backend: Add reconnect endpoint, snapshot versioning
- Game: Create MatchCheckpointService, update CardGameApiClient
- **Result:** Checkpoints working

### Week 2: Recovery
- Game: Create ReconnectionService, update GameBootstrap
- **Result:** Crash recovery working

### Week 3: Features
- Game: Create LeaderboardService & UI screens
- **Result:** Leaderboards visible

### Week 4: Polish
- Game: JWT refresh, error handling, offline detection
- **Result:** Production-ready

---

## 🎯 Success Criteria

✅ Netcode controls gameplay (API never blocks)  
✅ Checkpoints fire-and-forget (no gameplay lag)  
✅ Crash recovery works (restore from checkpoint)  
✅ Leaderboards functional (top 100, polled)  
✅ No data loss (all moves logged via Netcode + API)  
✅ Versioning clear (handle schema changes)  

---

## 📝 Before Talking to Backend

1. Read `ARCHITECTURE_CLARIFICATION.md`
2. Review `api_stuff/missing.md`
3. Ask questions from `HTTP_VERSIONING_STRATEGY.md`

## 🔧 Before Starting Code

1. Backend provides: reconnect endpoint, checkpoint clarity, MatchSnapshot schema
2. Game team decides: local storage (PlayerPrefs vs. secure), offline policy, acceptable data loss

---

## 📚 Document Legend

| Doc | Length | Purpose | Read When |
|-----|--------|---------|-----------|
| ARCHITECTURE_CLARIFICATION.md | 15 min | Understand design | First thing, with backend |
| IMPLEMENTATION_TASKS.md | 20 min | Detailed tasks | Ready to code |
| HTTP_VERSIONING_STRATEGY.md | 15 min | Polling & versions | Before implementation |
| API_INTEGRATION_PLAN.md | 30 min | Full scope (detailed) | Reference, detailed questions |
| API_QUICK_REFERENCE.md | 5 min | One-page summary | Quick lookup |
| api_stuff/missing.md | 10 min | API issues | Share with backend |
| README.md (this) | 5 min | Overview | Navigation |

---

## 🚨 Critical Points

⚠️ **Netcode is real-time authority** — API never makes gameplay decisions  
⚠️ **Checkpoints are durability only** — not game-state sync  
⚠️ **API calls are async fire-and-forget** — don't wait for responses  
⚠️ **Polling has intervals** — leaderboard 30s, stats 5m, reconnect exponential backoff  
⚠️ **Schema versioning needed** — handle breaking changes without crashing  

---

## 📞 Quick Answers

**Q: Won't HTTP ruin real-time gameplay?**  
A: No. Netcode handles real-time. API only saves snapshots async (doesn't block).

**Q: What if I crash at turn 50?**  
A: Restore checkpoint from turn ~48, catch up via Netcode = consistent.

**Q: What if network drops?**  
A: Netcode drops but checkpoint still saves every 30s. Auto-forfeit after 5m offline.

**Q: How much battery does polling drain?**  
A: Only when screen visible. Leaderboard: 30s interval = minimal.

**Q: What if checkpoint is stale?**  
A: Snapshot has timestamp. Reject if >5m old, use if <30s.

---

## 📂 File Structure

```
Assets/Runtime/Networking/
├── README.md ← You are here
├── ARCHITECTURE_CLARIFICATION.md ← Start here
├── IMPLEMENTATION_TASKS.md
├── API_INTEGRATION_PLAN.md
├── API_QUICK_REFERENCE.md
├── HTTP_VERSIONING_STRATEGY.md
│
├── api_stuff/
│   └── missing.md ← Share with backend
│
├── [TO CREATE]
├── MatchCheckpointService.cs
├── ReconnectionService.cs
├── LeaderboardService.cs
│
└── [TO MODIFY]
    ├── CardGameApiClient.cs
    ├── LocalCacheService.cs
    ├── GameBootstrap.cs
    └── GameService.cs

Assets/Runtime/UI/
└── [TO CREATE]
    ├── LeaderboardScreen.cs
    └── ProfileScreen.cs
```

---

**Status:** Documentation complete, ready for implementation  
**Next:** Read ARCHITECTURE_CLARIFICATION.md
s
4. Update SERVICES_GUIDE.md
5. Add example in GameServiceExample.cs
6. Commit with descriptive message
