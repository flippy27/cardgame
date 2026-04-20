# API Integration Plan - Card Duel Mobile

**Status:** Current game works locally, API endpoints exist but are underutilized  
**Goal:** Integrate REST API for persistent state, reconnection, leaderboards, and match history

---

## API Summary (Server Already Has)

### Authentication & Users
- `POST /api/v1/auth/register` — Register account
- `POST /api/v1/auth/login` — Login (returns JWT token)
- `GET /api/v1/users/{playerId}/profile` — Get player profile
- `GET /api/v1/users/{playerId}/stats` — Get player stats (wins, losses, rating)
- `GET /api/v1/users/leaderboard?page=1&pageSize=100&region=global` — Paginated leaderboard

### Cards & Decks
- `GET /api/v1/cards` — Get all cards (AllowAnonymous)
- `GET /api/v1/cards/{cardId}` — Get specific card (AllowAnonymous)
- `GET /api/v1/cards/search?q=name` — Search cards (AllowAnonymous)
- `GET /api/v1/cards/stats` — Card catalog stats (AllowAnonymous)
- `GET /api/v1/decks/catalog` — Get card catalog for deck building
- `GET /api/v1/decks/{playerId}` — Get player's decks
- `PUT /api/v1/decks` — Create/update deck

### Matches & Gameplay
- `POST /api/v1/matchmaking/private` — Create private match (returns matchId)
- `POST /api/v1/matchmaking/private/join` — Join private match with room code
- `POST /api/v1/matchmaking/queue` — Queue for ranked matchmaking
- `GET /api/v1/matches` — List active matches
- `GET /api/v1/matches/{matchId}/summary` — Get match summary
- `GET /api/v1/matches/{matchId}/snapshot/{playerId}` — **Get full match state (for reconnection!)**
- `POST /api/v1/matches/{matchId}/ready` — Signal ready
- `POST /api/v1/matches/{matchId}/play` — Play a card (server validates)
- `POST /api/v1/matches/{matchId}/end-turn` — End turn
- `POST /api/v1/matches/{matchId}/forfeit` — Forfeit match
- `POST /api/v1/matches/{matchId}/complete` — Mark match complete + update rating

### Match History & Replays
- `GET /api/v1/matches/history/{playerId}?page=1&pageSize=20` — Paginated match history
- `GET /api/v1/replays/{matchId}` — Get full replay logs
- `GET /api/v1/replays/{matchId}/validate` — Validate replay integrity

---

## Current Game State

### What's Already Built
✅ AuthService — Login/register exists (needs token persistence)  
✅ CardCatalogCache — Card loading works  
✅ DeckService — Deck CRUD exists  
✅ MatchHistoryService — Match history fetching exists  
✅ UserService — Profile/stats/leaderboard exists  
✅ CardGameApiClient — HTTP client with retry/circuit breaker  
✅ LocalCacheService — Local fallback caching  

### What's NOT Integrated
❌ **Match state persistence** — Game plays locally in memory, never sent to server
❌ **Matchmaking** — No queue implementation, no server-side match creation
❌ **Reconnection** — No snapshot restoration after disconnect
❌ **Crash recovery** — No state save/restore mechanism
❌ **Server-side validation** — Game allows any move locally, no server checks
❌ **Rating updates** — Match complete endpoint exists but not called
❌ **Replay recording** — Moves aren't logged to server
❌ **Leaderboard UI** — API exists but no UI to display

---

## What Needs to Happen

### Phase 1: Match Lifecycle Integration
1. **Create match server-side**
   - Call `POST /api/v1/matchmaking/private` or `/queue` when starting
   - Store matchId locally
   - Return with opponent ID & initial snapshot

2. **Send every move to server**
   - Before local TryPlayCard, call `POST /api/v1/matches/{matchId}/play`
   - Server validates card cost, legality, state
   - If rejected, show error
   - If accepted, apply to local state

3. **Send turn end to server**
   - Call `POST /api/v1/matches/{matchId}/end-turn` 
   - Server advances turn, broadcasts next state

4. **Handle match completion**
   - When game ends, call `POST /api/v1/matches/{matchId}/complete`
   - Server updates rating, returns new rating + rewards
   - Display in UI

### Phase 2: Crash Recovery & Reconnection
1. **Save match ID locally when match starts**
   - Use LocalCacheService to persist matchId

2. **On app restart, check for in-progress match**
   - Load matchId from cache
   - Call `GET /api/v1/matches/{matchId}/snapshot/{playerId}`
   - Restore full board state, hand, turn number
   - Resume gameplay from where you left off

3. **Handle mid-game disconnect**
   - Monitor network health
   - If connection lost, pause UI + show "Reconnecting..."
   - Poll snapshot endpoint with exponential backoff
   - Auto-resume when reconnected

### Phase 3: UI & Features
1. **Leaderboard Screen**
   - Call `GET /api/v1/users/leaderboard`
   - Display top 100 with pagination
   - Show player rank, wins, losses, rating

2. **Match History**
   - Already have MatchHistoryService
   - Add UI to display paginated history
   - Show opponent, result, rating change, date

3. **Profile Screen**
   - Call `GET /api/v1/users/{playerId}/stats`
   - Display rating, total games, win rate, region

4. **Replay Viewer (Optional)**
   - Call `GET /api/v1/replays/{matchId}`
   - Replay all moves in sequence
   - Allow pause/resume/seek

---

## Files to Create/Modify

### Create
- `Assets/Runtime/Networking/MatchStateService.cs` — Sync moves to server
- `Assets/Runtime/Networking/ReconnectionService.cs` — Snapshot restore on crash
- `Assets/Runtime/Networking/LeaderboardService.cs` — Leaderboard fetching
- `Assets/Runtime/UI/LeaderboardScreen.cs` — Leaderboard display
- `Assets/Runtime/UI/ProfileScreen.cs` — Player profile display

### Modify
- `CardGameApiClient.cs` — Add methods for all match endpoints (play, end-turn, complete, snapshot, etc.)
- `GameService.cs` — Add MatchStateService, ReconnectionService, LeaderboardService references
- `CardDuelNetworkCoordinator.cs` — Call API on every move + match complete
- `BattleScreenPresenter.cs` — Call MatchStateService on card play / turn end
- `MainMenuScreen.cs` — Add Leaderboard + Profile buttons
- `LocalCacheService.cs` — Persist matchId, last snapshot

---

## Dependency on API Issues?

### Missing from API (potential blockers)
None found that are critical. API looks complete for a first-pass integration.

### Unclear API behavior
1. **Snapshot endpoint format** — Does `GET /matches/{matchId}/snapshot/{playerId}` return the same MatchSnapshot DTO that's broadcast in-game? Need to verify schema matches DuelSnapshotDto.
2. **Server-side validation** — How strict is PlayCard validation? Does it validate:
   - Card exists in hand?
   - Player has enough mana?
   - Slot is empty/valid for card type?
   - Card can be played in current game state?
3. **Rating calculation** — Is Elo calculation automatic on Complete, or does caller provide new rating?
4. **Replay format** — How are MatchAction logs stored? JSON array or binary?

---

## Task List (In Order)

### Priority 1 (Essential)
- [ ] Verify Snapshot DTO schema matches API response
- [ ] Update CardGameApiClient: add SetReady, PlayCard, EndTurn, Forfeit, GetSnapshot methods
- [ ] Create MatchStateService: wraps above + handles retry/error logic
- [ ] Modify CardDuelNetworkCoordinator: call MatchStateService.PlayCard before TryPlayCard
- [ ] Modify CardDuelNetworkCoordinator: call MatchStateService.CompleteMatch on win
- [ ] Test: local game → play card → call API → verify move recorded server-side

### Priority 2 (Robustness)
- [ ] Create ReconnectionService: poll snapshot on network error
- [ ] Modify LocalCacheService: persist matchId + last snapshot
- [ ] Modify GameBootstrap: on start, check for in-progress match, restore if exists
- [ ] Test: kill app mid-match → restart → resume from saved state

### Priority 3 (Features)
- [ ] Create LeaderboardService
- [ ] Add LeaderboardScreen UI
- [ ] Modify MainMenuScreen: add Leaderboard button
- [ ] Test: load leaderboard, scroll, verify pagination

### Priority 4 (Polish)
- [ ] Add profile viewer (stats screen)
- [ ] Add match history viewer with details
- [ ] Add replay viewer (if time permits)
- [ ] Add in-game disconnect indicator + reconnect banner

---

## Known Issues to Address

1. **JWT Token expiry** — Token from login is 1 hour. Need to:
   - Store token in SecureTokenStorage
   - Refresh before expiry or handle 401 gracefully
   - Prompt re-login if refresh fails

2. **Offline play** — Currently game runs offline. Need to decide:
   - Require online for ranked
   - Allow offline practice with local storage sync on reconnect
   - Show warning banner if offline

3. **Server validation lag** — Playing locally then waiting for server response might feel sluggish:
   - Show move optimistically
   - Revert if server rejects
   - Consider read-your-writes consistency strategy

4. **Deck validation** — API validates deck on save. Game should:
   - Pre-validate before sending to server
   - Show validation errors in UI

5. **Rating system** — API uses Elo. Verify:
   - Rating persists after match
   - Proper win/loss recording
   - Region separation (if used)

---

## Success Criteria

- [x] All API endpoints documented
- [ ] CardGameApiClient covers all match endpoints
- [ ] Game syncs every card play to API
- [ ] Match completion sends result to API + updates rating
- [ ] Leaderboard displays top 100 players
- [ ] Crash during match → restart game → resume from saved state
- [ ] No local-only game state (all persisted to server)
- [ ] JWT token refresh works transparently
