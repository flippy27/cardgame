# API Integration Implementation Tasks

**Owner:** You (Unity game)  
**Blocker on:** API fixes (see api_stuff/missing.md)  
**Timeline:** 4-6 weeks for full integration

---

## Phase 1: Foundation (Week 1-2)
*Get core checkpoint system working, not real-time move sync.*

### API Fixes (Wait for these first)
- [ ] **[API TEAM] Add POST /matches/{matchId}/reconnect endpoint** 
  - Unblocks: Reconnection feature
  - Dependency: ConnectMatchRequest contract exists, just needs controller method

- [ ] **[API TEAM] Add snapshot versioning fields**
  - Unblocks: Consistency checks, staleness detection
  - Fields needed: `version`, `timestamp`, `turnNumber`, `matchStatus`, `checkpointNumber`
  - See: ARCHITECTURE_CLARIFICATION.md

- [ ] **[API TEAM] Add/clarify checkpoint endpoint**
  - Option A: New `POST /matches/{matchId}/checkpoint` endpoint
  - Option B: Clarify if `POST /matches/{matchId}/complete` serves this purpose
  - Unblocks: Periodic state saves

- [ ] **[API TEAM] Clarify MatchSnapshot DTO schema**
  - Unblocks: Snapshot parsing, reconnection restore
  - Request: Exact schema or link to definition (must include status field)

### Game: Update CardGameApiClient
- [ ] Add method: `async Task<MatchSnapshot> GetSnapshotAsync(string matchId, string playerId)`
  - Endpoint: `GET /api/v1/matches/{matchId}/snapshot/{playerId}`
  - Returns: Full match state for reconnection
  - Supports: polling with exponential backoff

- [ ] Add method: `async Task PostSnapshotCheckpointAsync(string matchId, DuelSnapshotDto snapshot)`
  - Endpoint: `POST /api/v1/matches/{matchId}/checkpoint` (or `/complete` if that's used)
  - Payload: snapshot + metadata
  - Returns: Checkpoint ID or acknowledgment
  - Fire-and-forget: don't wait for response, don't block gameplay

- [ ] Add method: `async Task<MatchCompletionResponse> CompleteMatchAsync(...)`
  - Endpoint: `POST /api/v1/matches/{matchId}/complete`
  - Called only at match end, not during play
  - Already exists, just verify it's used correctly

- [ ] Add method: `async Task<LeaderboardPage> GetLeaderboardAsync(int page, string region)`
  - Endpoint: `GET /api/v1/users/leaderboard`
  - Supports: polling every 30s

- [ ] Add method: `async Task<UserStats> GetPlayerStatsAsync(string playerId)`
  - Endpoint: `GET /api/v1/users/{playerId}/stats`
  - Supports: caching 5 minutes

### Game: Create MatchCheckpointService
**File:** `Assets/Runtime/Networking/MatchCheckpointService.cs`

```csharp
public class MatchCheckpointService : MonoBehaviour
{
    private CardGameApiClient _apiClient;
    private DuelRuntime _gameRuntime;
    private string _matchId;
    
    private float _timeSinceCheckpoint = 0f;
    private const float CHECKPOINT_INTERVAL = 30f;
    
    private void Update()
    {
        if (!IsInMatch) return;
        
        _timeSinceCheckpoint += Time.deltaTime;
        if (_timeSinceCheckpoint >= CHECKPOINT_INTERVAL)
        {
            // Fire-and-forget: don't await, don't block gameplay
            _ = SaveCheckpointAsync();
            _timeSinceCheckpoint = 0f;
        }
    }
    
    private async Task SaveCheckpointAsync()
    {
        try
        {
            var snapshot = _gameRuntime.BuildSnapshot();
            await _apiClient.PostSnapshotCheckpointAsync(_matchId, snapshot);
            Debug.Log($"[Checkpoint] Saved (turn {_gameRuntime.TurnNumber})");
        }
        catch (Exception ex)
        {
            // Non-critical: log but don't crash
            Debug.LogWarning($"[Checkpoint] Failed: {ex.Message}");
        }
    }
}
```

- [ ] Implement periodic checkpointing (every 30 seconds)
- [ ] Fire-and-forget pattern (don't await, don't block NGO)
- [ ] Graceful failure (log warning, keep playing)
- [ ] Log checkpoint metadata (turn number, action count)

### Game: Keep CardDuelNetworkCoordinator as-is
✅ **NO CHANGES** — Netcode handles all real-time gameplay  
✅ Real-time card plays, board updates stay on NGO  
✅ API only used for checkpoints + final result  

### Testing
- [ ] Manual test: Start match, 30s passes, checkpoint HTTP request logged
- [ ] Manual test: Kill network during gameplay, NGO continues (not API-dependent)
- [ ] Manual test: Match completes, final score POSTed to API

---

## Phase 2: Reconnection (Week 2-3)
*Survive app crash and restore game state from checkpoint.*

**Key:** Checkpoints saved every 30s, game restores from API snapshot on restart.

### Game: Create ReconnectionService
**File:** `Assets/Runtime/Networking/ReconnectionService.cs`

```csharp
public class ReconnectionService : MonoBehaviour
{
    // On app startup, check if user was in a match
    // If yes: try to restore state from checkpoint
    public async Task<bool> TryRestoreMatch()
    {
        var savedMatchId = LocalCache.GetCurrentMatchId();
        if (string.IsNullOrEmpty(savedMatchId)) return false;
        
        try
        {
            var snapshot = await _apiClient.GetSnapshotAsync(savedMatchId, _authService.CurrentPlayerId);
            
            // Verify match is still active
            if (snapshot.matchPhase != MatchPhase.InProgress)
            {
                Debug.Log($"Match {savedMatchId} already ended, clearing cache");
                LocalCache.ClearMatchState();
                return false; // Match ended while offline
            }
            
            // Verify snapshot is not too stale
            var age = DateTimeOffset.UtcNow - snapshot.timestamp;
            if (age.TotalMinutes > 30)
            {
                Debug.LogWarning($"Checkpoint too old ({age.TotalMinutes:F0}m), may be inconsistent");
            }
            
            LocalCache.SaveSnapshot(snapshot);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to restore match: {ex.Message}");
            return false;
        }
    }
}
```

- [ ] On app start (GameBootstrap), check LocalCache for in-progress matchId
- [ ] If found: call ReconnectionService.TryRestoreMatch()
- [ ] If restored: load DuelRuntime from checkpoint snapshot instead of creating new
- [ ] If failed or match ended: clear cache, proceed to main menu
- [ ] Verify snapshot status (InProgress vs Completed) before restoring

### Game: Modify LocalCacheService
- [ ] Add method: `SaveCurrentMatchId(string matchId)` — called when match starts
- [ ] Add method: `SaveLatestSnapshot(DuelSnapshotDto snapshot)` — called by MatchCheckpointService
- [ ] Add method: `GetCurrentMatchId() -> string` — called on app startup
- [ ] Add method: `GetLatestSnapshot() -> DuelSnapshotDto` — used for restore
- [ ] Add method: `ClearMatchState()` — called when match ends or rejected
- [ ] **Important:** Store in secure location (PlayerPrefs or secure storage)

### Game: Modify GameBootstrap
- [ ] On startup, **before** loading main menu:
  ```csharp
  var restored = await ReconnectionService.TryRestoreMatch();
  if (restored)
  {
      LoadBattleScene(LocalCache.GetLatestSnapshot());
      return;
  }
  ```
- [ ] If restored: load battle scene with restored snapshot
- [ ] If not: proceed to normal main menu

### Game: Network Disconnect Handler (Optional, for graceful degradation)
**During match, if NGO connection drops:**

- [ ] Monitor Netcode disconnection event
- [ ] Show "Disconnected" banner (but don't pause game)
- [ ] Checkpoint is already saved periodically (no special action needed)
- [ ] If offline >5 minutes: offer to forfeit or retry
- [ ] When reconnected: NGO re-syncs, checkpoint is still valid backup

**Don't poll API during gameplay** — NGO is real-time authority.  
**Only use API snapshot on app restart** or permanent disconnect.

### Testing
- [ ] Manual test: Mid-game (turn 25) → kill app → restart → game loads turn 24-25 checkpoint
- [ ] Manual test: Verify checkpoint is max ~30s old (not from turn 1)
- [ ] Manual test: Opponent forfeits while P1 offline → restart shows match ended
- [ ] Manual test: Offline >5m, then reconnect → match should be auto-forfeit by server
- [ ] Edge case: Get stale checkpoint, try to resume while NGO says different — verify consistency

---

## Phase 3: Leaderboards & Profile (Week 3-4)
*Display competitive features.*

### Game: Create LeaderboardService
**File:** `Assets/Runtime/Networking/LeaderboardService.cs`

- [ ] Method: `async Task<LeaderboardPage> FetchLeaderboard(int page = 1, string region = "global")`
  - Endpoint: `GET /api/v1/users/leaderboard?page=1&pageSize=100&region=global`
  - Handle pagination (prev/next)
  - Cache for 5 minutes

- [ ] Method: `async Task<UserStats> FetchPlayerStats(string playerId)`
  - Endpoint: `GET /api/v1/users/{playerId}/stats`
  - Returns: rating, wins, losses, winRate

### Game: Create LeaderboardScreen.cs (UI)
**File:** `Assets/Runtime/UI/LeaderboardScreen.cs`

- [ ] Display: Top 100 players in a list
- [ ] Columns: Rank, Username, Rating, Wins, Losses, Win Rate
- [ ] Pagination: Prev/Next buttons (or scroll)
- [ ] Search: Filter by username
- [ ] Refresh: Button to reload
- [ ] Show player's own rank highlighted

### Game: Create ProfileScreen.cs (UI)
**File:** `Assets/Runtime/UI/ProfileScreen.cs`

- [ ] Display: Your stats (rating, total games, win rate, region)
- [ ] Show: Match history (delegate to MatchHistoryService)
- [ ] Button: View leaderboard
- [ ] Button: Edit decks

### Game: Update MainMenuScreen
- [ ] Add "Leaderboard" button → opens LeaderboardScreen
- [ ] Add "Profile" button → opens ProfileScreen
- [ ] Move existing "Match History" into Profile

### Testing
- [ ] Manual test: Open leaderboard, see top 10
- [ ] Manual test: Scroll pagination
- [ ] Manual test: Search for a player
- [ ] Manual test: Profile shows correct stats
- [ ] Manual test: Match history visible in profile

---

## Phase 4: Polish & Robustness (Week 4-6)
*Handle edge cases, improve UX.*

### JWT Token Management
- [ ] Token expires after 1 hour (from API)
- [ ] Before each API call: check token expiry
- [ ] If expired: refresh (or re-login)
- [ ] On 401 response: force logout + go to login screen
- [ ] Use SecureTokenStorage for token persistence

### Error Handling & UX
- [ ] Network error → show "Connection Lost" banner with retry
- [ ] API error 400 → show specific error message to user
- [ ] API error 500 → show generic "Server error, try again"
- [ ] Timeout → auto-retry with exponential backoff
- [ ] Match ended unexpectedly → show results, cleanup local state

### Offline Support
- [ ] Game requires online for:
  - Ranked mode
  - Leaderboards
  - Match history
  - Deck uploads
- [ ] Game allows offline:
  - Practice mode (local)
  - Deck building (cached cards)
- [ ] When online: auto-sync cached changes
- [ ] Show "Offline Mode" indicator in UI

### Security
- [ ] Never log sensitive data (tokens, passwords)
- [ ] Use HTTPS only (no HTTP)
- [ ] Validate server certificates
- [ ] Encrypt token storage (SecureTokenStorage already does this)
- [ ] Don't store player data in local JSON (use Preferences, SecureStorage)

### Monitoring & Logging
- [ ] Log all API calls with timestamp, status, duration
- [ ] Log errors with full exception + context
- [ ] Upload logs to server (optional, for debugging)
- [ ] Add metrics: API latency, error rates, match duration

### Testing
- [ ] Automated: Unit test MatchStateService
- [ ] Automated: Unit test ReconnectionService
- [ ] Manual: 10 full matches start-to-finish
- [ ] Manual: Network errors mid-match
- [ ] Manual: Concurrent players (if multiplayer)
- [ ] Load test: Can server handle 100 concurrent matches?

---

## Phase 5: Future Enhancements (Post-MVP)
*Nice-to-have features for later.*

- [ ] Spectate matches (read-only)
- [ ] Tournaments
- [ ] Clans / Teams
- [ ] Achievements / Badges
- [ ] Trading cards (if applicable)
- [ ] Daily quests / Seasonal rewards
- [ ] Replay viewer with playback controls
- [ ] WebSocket for real-time board state sync
- [ ] Voice/text chat in lobby
- [ ] Mobile push notifications (match ready, opponent forfeited, etc.)

---

## Blockers & Dependencies

| Task | Blocked By | Status |
|------|-----------|--------|
| MatchStateService | CardGameApiClient methods + API fixes | ⏳ Waiting |
| ReconnectionService | MatchSnapshot DTO clarification | ⏳ Waiting |
| Gameplay move sync | PlayCard server-side validation | ⏳ Waiting |
| Full integration | Reconnect endpoint | ⏳ Waiting |

---

## Success Criteria

- [ ] Every move is sent to API before applied locally
- [ ] App crash mid-match → restart → resume from saved state
- [ ] Match state persists in DB (queryable via GetSnapshot)
- [ ] Leaderboard works with 100+ players
- [ ] No critical bugs after 10 full test matches
- [ ] Network latency <500ms doesn't break gameplay
- [ ] All JWT tokens refresh transparently
- [ ] No unhandled exceptions in production

---

## Timeline Estimate

| Phase | Tasks | Estimated Days |
|-------|-------|-----------------|
| 1: Foundation | API fixes (3-5 days) + MatchStateService | 10-15 |
| 2: Reconnection | LocalCache, GameBootstrap, disconnect handler | 8-10 |
| 3: Leaderboard | LeaderboardService + UI screens | 5-7 |
| 4: Polish | Token mgmt, error handling, security | 7-10 |
| 5: Testing | Full QA, load testing, edge cases | 5-7 |
| **Total** | | **35-49 days (~8 weeks)** |

---

## Questions Before Starting

1. Should gameplay be **server-authoritative** (server decides all outcomes) or **client-optimistic** (client predicts, server validates)?
2. Should matches be **player-to-player** (direct socket) or **server-relayed** (all moves through API)?
3. Is **Netcode (NGO)** the intended real-time layer, or switch to REST-only?
4. Should **offline mode** be supported, or require internet always?
5. What's the **grace period** for reconnection (currently 20 seconds)?

---

## File Checklist

```
Assets/Runtime/Networking/
├── [✏️] CardGameApiClient.cs (add 6 new methods)
├── [✏️] GameService.cs (add MatchStateService reference)
├── [✏️] CardDuelNetworkCoordinator.cs (call MatchStateService)
├── [✏️] LocalCacheService.cs (add match persistence)
├── [✏️] BattleScreenPresenter.cs (call MatchStateService on play/turn)
├── [✏️] GameBootstrap.cs (restore match on startup)
├── [🆕] MatchStateService.cs
├── [🆕] ReconnectionService.cs
├── [🆕] LeaderboardService.cs
├── [🆕] api_stuff/missing.md
└── [🆕] api_stuff/IMPLEMENTATION_TASKS.md (this file)

Assets/Runtime/UI/
├── [🆕] LeaderboardScreen.cs
├── [🆕] ProfileScreen.cs
└── [✏️] MainMenuScreen.cs (add buttons)
```

---

## Next Steps

1. **Read api_stuff/missing.md** → Identify which API fixes you must request
2. **Request API fixes from backend team** → Provide them with missing.md
3. **Start Phase 1** → Update CardGameApiClient + MatchStateService
4. **Test locally** → Verify API calls are working
5. **Move to Phase 2** → Add reconnection
6. **Iterate** → Test, find bugs, fix
