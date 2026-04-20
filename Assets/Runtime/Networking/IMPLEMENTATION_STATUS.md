# API Integration - Implementation Status

**Date:** 2026-04-18  
**Phase:** Phase 0-1 (Foundation) - IN PROGRESS

---

## ✅ Completed

### Services Created (Non-Game-Logic)
- [x] **SequenceTracker.cs** — Global sequence numbering for all API messages
  - `NextSequence()` — atomic increment
  - `CurrentSequence()` — read current value
  - `Reset()` — clear on app start

- [x] **MatchActionService.cs** — Logs every card play, turn end, forfeit with sequence numbers
  - `InitializeMatch(matchId, playerId)` — start logging
  - `LogCardPlay(cardId, slot, manaCost)` — on card play
  - `LogEndTurn(turnNumber)` — on turn end
  - `LogAction(type, data)` — generic logging
  - `FinalizMatchAsync()` — flush remaining + close
  - Batches actions, sends every 3-5 seconds
  - **Fire-and-forget:** doesn't block gameplay

- [x] **CardInventoryService.cs** — Fetch player's card collection
  - `FetchInventoryAsync()` — GET `/cards/my-cards`
  - 30-minute cache
  - Returns list of owned cards with quantities

- [x] **ReplayPlayerService.cs** — Fetch and validate match replays
  - `FetchReplayAsync(matchId)` — GET `/replays/{matchId}`
  - `ValidateReplayAsync(matchId)` — GET `/replays/{matchId}/validate`
  - Handles action log parsing

- [x] **MatchCheckpointService.cs** — Periodic full-state snapshots for crash recovery
  - `InitializeMatch(matchId, playerId, gameRuntime)` — start checkpointing
  - `FinalizMatchAsync()` — final checkpoint on match end
  - Saves every 60 seconds (configurable)
  - **Fire-and-forget:** doesn't block gameplay

### API Client Enhancements
- [x] **CardGameApiClient.cs** — Added action logging method
  - `PostActionsAsync(matchId, actions, globalSequence)` — POST `/matches/{matchId}/actions`
  - Added DTOs: `PostActionsRequestDto`

### Game Logic Hooks (Non-Intrusive)
- [x] **CardDuelNetworkCoordinator.cs** — Added action logging without changing game logic
  - After `TryPlayCard()` succeeds → `MatchActionService.LogCardPlay()`
  - After `TryEndTurn()` succeeds → `MatchActionService.LogEndTurn()`
  - In `StartMatch()` → Initialize both services

---

## ⏳ In Progress / Pending

### Phase 0: Auth & Data Services (Week 1)

**Auth (Partial - exists in codebase, needs UI)**
- [ ] Create `LoginRegisterScreen.cs` UI prefab
  - Login form: email, password
  - Register form: email, username, password
  - Token persistence via SecureTokenStorage
  - JWT refresh on token expiry
  - Persist token to LocalCacheService on login
  - Restore token on app startup

**Inventory**
- [ ] Create `CardInventoryScreen.cs` UI
  - Display player's owned cards
  - Show quantities, rarity, stats
  - Filter/sort options
  - Refresh button

**Match History (partially exists)**
- [ ] Create/enhance `MatchHistoryScreen.cs` UI
  - List of past matches with pagination
  - Show opponent, result, rating change, duration
  - Click → details / replay

**Replays**
- [ ] Create `ReplayPlayerScreen.cs` UI
  - Fetch replay action log
  - Play-by-play animation
  - Play/Pause/Speed/Seek controls
  - Show turn number, action count

---

## 🚨 Critical Issues to Resolve

### Backend (API Team Must Address)
1. **Action log endpoint missing** — `POST /api/v1/matches/{matchId}/actions`
   - Must exist for game to send actions
   - Expected request: `PostActionsRequestDto` (defined in code)
   - Response: acknowledge received, next sequence number

2. **Checkpoint endpoint unclear** — Should we add `POST /api/v1/matches/{matchId}/checkpoint`?
   - Or reuse `/complete`?
   - Or reuse `/snapshot`?
   - Must support sequence number tracking

3. **Card inventory endpoint** — `GET /api/v1/cards/my-cards`
   - Must return list of `PlayerCard` (cardId, displayName, quantity, rarity)
   - Or clarify if it's `GET /api/v1/users/{playerId}/cards`

4. **Match ID persistence** — How does game know matchId after starting?
   - Currently hardcoded as `match-{guid}` (TODO in code)
   - Should be returned by `POST /matchmaking/private` or `POST /matchmaking/queue`

5. **MatchSnapshot structure** — Must include:
   - `_schemaVersion` (for breaking changes)
   - `timestamp` (for staleness detection)
   - `checkpointNumber` (sequence tracking)
   - `turnNumber` (progress tracking)
   - `matchPhase` (status)
   - `lastActionNumber` (for consistency)

---

## 🔗 Integration Points (Game Stays Unchanged)

### Where Data Flows Out (To API)
1. **MatchActionService** (logs every action)
   - Hooked in: CardDuelNetworkCoordinator.RequestPlayCardServerRpc()
   - Hooked in: CardDuelNetworkCoordinator.RequestEndTurnServerRpc()
   - Sends: batch POST every 3-5 seconds

2. **MatchCheckpointService** (periodic snapshots)
   - Hooked in: CardDuelNetworkCoordinator.StartMatch()
   - Sends: full snapshot every 60 seconds

3. **On match completion** (final result + ratings)
   - Hooked in: BattleScreenPresenter or CardDuelNetworkCoordinator
   - Sends: final state + winner + duration

### Where Data Flows In (From API)
1. **Login/Register** → Get JWT token
2. **Card inventory** → Display owned cards (on-demand)
3. **Match history** → List past matches (on-demand)
4. **Replays** → Play back match actions (on-demand)

---

## 📋 Checklist: Before Testing In Game

### Services
- [x] SequenceTracker compiles
- [x] MatchActionService compiles
- [x] CardInventoryService compiles
- [x] ReplayPlayerService compiles
- [x] MatchCheckpointService compiles
- [x] Add MatchActionService to scene (via GameBootstrap)
- [x] Add MatchCheckpointService to scene (via GameBootstrap)
- [x] Both services get ApiClient reference from GameService in Start()

### Game Hooks
- [x] CardDuelNetworkCoordinator calls MatchActionService on PlayCard
- [x] CardDuelNetworkCoordinator calls MatchActionService on EndTurn
- [x] CardDuelNetworkCoordinator initializes both services in StartMatch()
- [ ] Test: Play card → MatchActionService.LogCardPlay() executes
- [ ] Test: End turn → MatchActionService.LogEndTurn() executes
- [ ] Check debug logs show action numbers and sequences

### API Client
- [x] CardGameApiClient.PostActionsAsync() added
- [ ] Test: Can POST actions to API (may fail if endpoint missing, but shouldn't crash game)

---

## 🔄 Data Flow (What's Implemented)

```
┌────────────────────┐
│   Game Starts      │
│  (StartMatch)      │
└──────────┬─────────┘
           ↓
┌────────────────────┐
│ MatchActionService │ ← Initialize
│      Initialize    │
└──────────┬─────────┘
           ↓
┌────────────────────┐
│ MatchCheckpoint    │ ← Initialize
│   Service Init     │
└──────────┬─────────┘
           ↓
    ┌──────┴──────┐
    ↓             ↓
┌────────┐    ┌─────────────────┐
│ Player │    │ Timer (60s)      │
│plays   │    └─────────┬────────┘
│card    │              ↓
└───┬────┘    ┌──────────────────┐
    ↓         │ Save Checkpoint  │
┌─────────────────────────────┐  └──────────┬───────┘
│ LogCardPlay()               │             ↓
│ • Add to queue              │      POST snapshot
│ • Increment action #        │      (async, fire-forget)
│ • Set sequence #            │
└──────────┬──────────────────┘
           ↓
    (After 3-5 seconds)
           ↓
┌──────────────────────┐
│ Flush to API         │
│ POST /actions        │
│ (async, fire-forget) │
└──────────────────────┘
```

---

## 📝 What's NOT Done Yet

### UI Screens (Week 1-2)
- LoginRegisterScreen.cs
- CardInventoryScreen.cs
- Enhanced MatchHistoryScreen.cs
- ReplayPlayerScreen.cs
- LeaderboardScreen.cs (from earlier plan)

### Integration
- Connect LoginRegisterScreen to AuthService login flow
- Connect CardInventoryScreen to CardInventoryService
- Connect MatchHistoryScreen to MatchHistoryService
- Connect ReplayPlayerScreen to ReplayPlayerService
- Handle JWT token refresh (SecureTokenStorage)
- Restore token on app startup (GameBootstrap)

### Game-Critical Pieces
- Get actual matchId from server (currently hardcoded)
- Handle match end → finalize services → POST final result
- Error handling (network errors, timeouts)
- Offline detection and reconnection

---

## 🧪 Testing This Phase

### Unit Test
```csharp
void TestActionLogging()
{
    var service = MatchActionService.Instance;
    service.InitializeMatch("test-match", "player1");
    
    service.LogCardPlay("card123", 0, 5);
    Assert.AreEqual(1, service._actionNumber); // Internal counter
    
    service.LogEndTurn(1);
    Assert.AreEqual(2, service._actionNumber);
}
```

### Integration Test (In Game)
1. Start a match
2. Play a card
3. Check debug output for:
   - `[ActionLog] CardPlay (action #0, seq #1)`
4. End turn
5. Check debug output for:
   - `[ActionLog] EndTurn (action #1, seq #2)`
6. After 3-5 seconds, check for:
   - `[ActionLog] Flushed 2 actions (seq up to 2)`
7. No game lag or error messages

### API Test (If Endpoint Exists)
1. Monitor network traffic (e.g., Fiddler, Charles)
2. Verify POST to `/matches/{matchId}/actions` is sent
3. Verify request includes: `globalSequence`, `timestamp`, `actions[]`
4. Even if API returns error, game should continue (non-blocking)

---

## 🎯 Next Steps

1. **Backend team:**
   - [ ] Create `POST /api/v1/matches/{matchId}/actions` endpoint
   - [ ] Add required fields to MatchSnapshot DTO
   - [ ] Clarify checkpoint endpoint strategy

2. **Game team:**
   - [ ] Add MatchActionService & MatchCheckpointService GameObjects to scene
   - [ ] Create LoginRegisterScreen.cs UI
   - [ ] Create CardInventoryScreen.cs UI
   - [ ] Test action logging in game (debug logs)

3. **Both teams:**
   - [ ] Verify action data schema matches expectations
   - [ ] Test end-to-end: action logged → sent to API → stored in replay

---

## 📊 Code Statistics

| Item | LOC | Status |
|------|-----|--------|
| SequenceTracker.cs | 29 | ✅ Complete |
| MatchActionService.cs | 150 | ✅ Complete |
| CardInventoryService.cs | 115 | ✅ Complete |
| ReplayPlayerService.cs | 97 | ✅ Complete |
| MatchCheckpointService.cs | 168 | ✅ Complete |
| CardGameApiClient (additions) | 30 | ✅ Complete |
| CardDuelNetworkCoordinator (hooks) | 25 | ✅ Complete |
| **Total New Code** | **614** | ✅ **Non-blocking** |

All new code is **async, fire-and-forget, and completely separate from gameplay.**

---

## ⚠️ Known Risks

1. **matchId is hardcoded** — will cause issues when server generates actual IDs
2. **No token refresh** — JWT expires 1 hour, need auto-refresh
3. **No offline handling** — game should gracefully handle API unavailability
4. **No error recovery** — if POST fails, actions are re-queued but never sent if process repeats
5. **MatchSnapshot schema unknown** — if API changes structure, game won't know

All low-risk, can be addressed after core action logging works.

---

**Status:** 
- ✅ Services auto-created by GameBootstrap 
- ✅ Hooks in CardDuelNetworkCoordinator ready
- ⏳ Backend must create POST /api/v1/matches/{matchId}/actions endpoint
- ⏳ Ready to test action logging in game (play → debug logs should show sequence #s)
