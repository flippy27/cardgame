# API Issues & Missing Pieces

## Status
API is ~90% complete. Most endpoints exist but some flow is missing.

---

## ⚠️ Critical Issues

### 1. **Missing Reconnection Endpoint**
**Severity:** HIGH  
**Issue:** ReconnectionService exists server-side but no controller endpoint exposes it.

**What we found:**
- `IReconnectionService` validates reconnect tokens
- `MatchRecord` has `Player1ReconnectToken`, `Player2ReconnectToken` fields
- `ConnectMatchRequest` DTO exists in contracts
- **But:** No endpoint like `POST /api/v1/matches/{matchId}/reconnect`

**What needs to happen:**
- Add endpoint: `POST /api/v1/matches/{matchId}/reconnect`
- Body: `{ playerId, reconnectToken }`
- Response: MatchSnapshot (same as GetSnapshot)
- Should mark player back as online, clear disconnect timestamp

**Code location:** `Controllers/MatchesController.cs` (add method after SetReady)

---

### 2. **No Server-Side Move Validation**
**Severity:** HIGH  
**Issue:** PlayCard endpoint doesn't validate game state server-side, game does all logic client-side.

**Current flow:**
- Client plays card locally
- Client sends `POST /api/v1/matches/{matchId}/play`
- Server... returns MatchSnapshot but doesn't validate if move is legal
- **Problem:** Cheating possible (send invalid move, bypass client rules)

**What needs to happen:**
- Server must validate before accepting:
  - Card exists in player's hand
  - Player has enough mana
  - Slot is empty/valid for card type
  - Game state allows playing (not opponent's turn, game not ended, etc)
  - Return 400 Bad Request if invalid (with error reason)
  - Return MatchSnapshot if valid

**Code location:** `Services/MatchService.cs` - PlayCard method

---

### 3. **No Forced Game Disconnect After Grace Period**
**Severity:** MEDIUM  
**Issue:** ReconnectionService checks grace period but match doesn't auto-end if opponent stays offline.

**Current behavior:**
- Player disconnects → marked offline with timestamp
- Can reconnect within grace period
- **But:** If grace period expires, opponent is stuck waiting
- No auto-forfeit or auto-end mechanism

**What needs to happen:**
- Background job / cleanup endpoint that:
  - Scans matches for disconnected players past grace period
  - Auto-forfeits the match (mark disconnected player as loser)
  - Notify both players of auto-forfeit
- **Or** add real-time check: when opponent tries to play, check if other player still offline

**Code location:** Could be new scheduled job or added to existing match processing

---

### 4. **No Match State Sync During Play**
**Severity:** MEDIUM  
**Issue:** Game doesn't receive MatchSnapshot updates mid-match. Only returns snapshot on request.

**Current design:**
- `GET /matches/{matchId}/snapshot/{playerId}` — pull-based (client asks for state)
- No broadcast when opponent plays a card
- Relies on NGO (Netcode) for real-time updates

**Problem:**
- If client requests state, might get stale data (race condition)
- Requires client to poll or have external notification system
- NGO doesn't integrate with REST API

**What could improve:**
- `POST /matches/{matchId}/play` response includes full updated snapshot (both hands, boards)
- Timestamps on snapshots to detect staleness
- **Or** consider WebSocket for bi-directional updates (future enhancement)

**Current workaround:** Game should poll `GetSnapshot` at regular intervals during play

---

### 5. **Replay/Action Logging Not Exposed**
**Severity:** LOW  
**Issue:** ReplayService exists but no endpoint to push moves to it.

**What we found:**
- `GET /api/v1/replays/{matchId}` exists to fetch replay
- `ReplayService` interface exists
- **But:** No endpoint to record individual move (MatchAction)

**What needs to happen:**
- Either:
  - Add `POST /api/v1/replays/{matchId}/action` to record each move server-side
  - Or: MatchService.PlayCard should auto-log to ReplayService
- Ensures replay is built incrementally, not just at match end

**Code location:** `Controllers/ReplaysController.cs` or integrate into MatchService

---

### 6. **Unclear Snapshot DTO Format**
**Severity:** MEDIUM (Data Structure)  
**Issue:** MatchSnapshot contract doesn't match game's DuelSnapshotDto.

**Problem:**
- API returns `MatchSnapshot` (unknown schema)
- Game expects `DuelSnapshotDto` with full board state, hand, logs
- Need to verify they're compatible or create mapping

**What to do:**
- Add Contracts/MatchSnapshotDto.cs or update existing MatchSnapshot
- Must include:
  - `players[2]` with deck, hand, board slots
  - `board[2][3]` slots (Front, BackLeft, BackRight)
  - `activePlayerIndex`, `turnNumber`
  - `matchPhase` (WaitingForPlayers, InProgress, Completed)
  - `statusMessage` (error reason if failed)
  - `matchId`, `localPlayerIndex`
- **Verify:** Response format matches DuelSnapshotDto exactly

**Code location:** Check `Services/MatchService.cs` GetSnapshot method return type

---

## ⚠️ Design Issues

### 7. **No Rate Limiting on API**
**Severity:** LOW  
**Issue:** Endpoints have no rate limiting. Could allow spam or DoS.

**Recommendation:**
- Add rate limiting middleware (e.g., AspNetCoreRateLimit)
- Reasonable limits:
  - PlayCard: max 1 per second per match
  - GetSnapshot: max 5 per second per player
  - Auth login: max 5 per minute per IP

---

### 8. **Weak Token Generation for Reconnect**
**Severity:** LOW  
**Issue:** ReconnectToken is 32-byte random (good), but stored in plain DB. If DB is compromised, tokens are exposed.

**Current:** Base64 random token  
**Better:** Hash the token before storing, compare hashes on reconnect

**Code location:** `Services/ReconnectionService.cs` - GenerateReconnectToken

---

### 9. **No Audit Log for Match Moves**
**Severity:** LOW  
**Issue:** AuditService exists but PlayCard doesn't record moves for later investigation.

**Recommendation:**
- Log each move: player ID, card played, slot, timestamp
- Useful for cheat detection, dispute resolution, replay validation

---

## ✅ What's Actually Fine

- **Auth flow** — JWT tokens, login/register solid
- **Card catalog** — GET endpoints complete
- **Deck management** — CRUD fully implemented
- **Leaderboard** — Pagination works, region support good
- **Match history** — Pagination, filtering present
- **Rating system** — Elo implemented (RatingService)
- **Contracts/DTOs** — Well-structured, validation present

---

## Implementation Priority (for API fixes)

1. **MUST FIX FIRST:**
   - [x] Add POST /matches/{matchId}/reconnect endpoint
   - [x] Validate move legality server-side (PlayCard)
   - [x] Clarify/verify MatchSnapshot DTO matches game expectations

2. **SHOULD FIX:**
   - [ ] Auto-forfeit after grace period expires
   - [ ] Move logging to ReplayService
   - [ ] Rate limiting on endpoints

3. **NICE TO HAVE:**
   - [ ] Token hashing for reconnect
   - [ ] Audit logging for moves
   - [ ] WebSocket support for real-time updates

---

## ⚠️ Versioning & HTTP Concerns

### 10. **Missing Data Versioning in Snapshots**
**Severity:** MEDIUM  
**Issue:** MatchSnapshot doesn't include version/timestamp fields for consistency checks.

**Problem:**
- Game recovers old snapshot after crash
- Doesn't know if it's stale or current
- Can't detect conflicts if both players restore old state

**What's needed:**
```json
{
  "version": 2,
  "schemaVersion": "1.0",
  "timestamp": "2026-04-18T10:30:45.123Z",
  "turnNumber": 42,
  "lastActionNumber": 127,
  "matchStatus": "InProgress",  // or Completed, Forfeited
  "checkpointNumber": 5,
  ...
}
```

---

### 11. **No Endpoint Versioning Strategy**
**Severity:** MEDIUM  
**Issue:** How does API handle schema changes without breaking old clients?

**Questions:**
- New fields added to MatchSnapshot → v2 endpoint or same v1?
- Old clients still get v1 response?
- Support window for old versions (6 months? Forever)?
- Breaking change in field names → different endpoint URL?

**Recommendation:**
- Adopt `/api/v1/...`, `/api/v2/...` pattern
- Support at least 2 versions simultaneously
- Clear deprecation notices (warning headers, blog post)

---

### 12. **Missing Checkpoint Endpoint (or unclear)**
**Severity:** MEDIUM  
**Issue:** Should game POST partial checkpoint or full snapshot?

**Currently:** Only `POST /matches/{matchId}/complete` exists  
**Missing:** `POST /matches/{matchId}/checkpoint` or similar for periodic saves

**What's needed:**
```
POST /api/v1/matches/{matchId}/checkpoint
{
  "playerId": "player1",
  "snapshot": {...},
  "checkpointNumber": 42,
  "turnNumber": 50
}
```

Response: Acknowledge checkpoint saved, return checkpoint ID for tracking

**Or:** Reuse `/snapshot` endpoint as write endpoint?
```
PUT /api/v1/matches/{matchId}/snapshot
```

---

### 13. **No HTTP Cache Headers**
**Severity:** LOW  
**Issue:** Leaderboard and profile endpoints don't specify cache policy.

**Missing headers:**
```
GET /users/leaderboard
Response: Cache-Control: max-age=30, must-revalidate
          ETag: "abc123def456"
```

**Impact:**
- Client can't validate if data changed (wastes bandwidth)
- Cache validity unclear (30s? 5m? forever?)

**Needed:**
- Add `Cache-Control` headers to read-only endpoints
- Support `If-None-Match` (ETag) for conditional requests
- Support `If-Modified-Since` headers

---

### 14. **Match Status Field Missing from Snapshot**
**Severity:** MEDIUM  
**Issue:** Snapshot doesn't clearly indicate if match is still active.

**Problem:**
- Client crashes, comes back online
- Retrieves old snapshot
- Tries to resume match
- But match already completed (other player forfeited)
- Inconsistency: client thinks match ongoing, server says ended

**Solution:**
```json
{
  "matchId": "...",
  "status": "InProgress",  // or "Completed", "Forfeited", "Abandoned"
  "completedAt": "2026-04-18T10:35:00Z",
  "winnerIndex": 0,
  "endReason": "PlayerForfeited"
}
```

**Or:** Separate endpoint:
```
GET /api/v1/matches/{matchId}/status
Response: { status, completedAt, winner }
```

---

## Clarified Questions for API Owner

1. **Schema versioning:** If MatchSnapshot schema changes, new endpoint (`/v2`) or same with extra fields?
2. **Checkpoint endpoint:** Is `POST /matches/{id}/complete` the only write endpoint, or is there `/checkpoint`?
3. **Snapshot fields:** Does response include `version`, `timestamp`, `turnNumber`, `matchStatus`?
4. **Match status:** How can client detect if match is completed during reconnect?
5. **Cache headers:** Which endpoints should be cached? For how long?
6. **Conditional requests:** Support ETags or Last-Modified for bandwidth savings?
7. **Deprecation policy:** How long are old API versions supported?
8. **ActionNumber tracking:** Does snapshot include sequence counter for consistency?
