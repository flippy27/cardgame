# Game Architecture: NGO + HTTP API

## Clear Separation

### Netcode for GameObjects (Real-Time)
✅ **Authoritative game state** — card plays, turn execution, board changes  
✅ **Sub-100ms latency** — direct P2P or relay  
✅ **Deterministic** — all clients see same game  
✅ **Used for:** Every gameplay action in real-time  

### HTTP REST API (Data Persistence)
✅ **Durable storage** — match history, ratings, deck definitions  
✅ **State recovery** — restore game after crash  
✅ **Periodic checkpoints** — save match state for safety  
✅ **Read-only queries** — leaderboards, profiles, catalogs  
✅ **Async operations** — not time-critical  

### What Does NOT Go to API Real-Time
❌ ~~Every card play~~ → NGO broadcasts this  
❌ ~~Every turn change~~ → NGO handles  
❌ ~~Real-time opponent state~~ → NGO syncs  

### What DOES Go to API
✅ **Periodic snapshots** — every 10-30 seconds (checkpoint)  
✅ **Match completion** — final result, winner, duration  
✅ **Rating updates** — after match ends  
✅ **Crash recovery** — restore last snapshot  
✅ **Leaderboard queries** — GET (polling)  
✅ **Player profiles** — GET (on-demand)  

---

## HTTP Patterns & Considerations

### 1. **Polling (Not WebSocket)**
Since API is HTTP:

**For leaderboards:**
```
GET /users/leaderboard?page=1&pageSize=100 (every 30s if screen open)
```
- ✅ Simple, stateless
- ✅ Works with REST
- ❌ Higher latency (30s stale data OK for leaderboard)

**For match snapshots (during crash detection):**
```
GET /matches/{matchId}/snapshot/{playerId} (every 2s when offline)
Exponential backoff: 2s → 4s → 8s → 16s (max 30s)
```
- ✅ Reconnect detection
- ❌ Battery drain if continuous

**For profile/stats:**
```
GET /users/{playerId}/stats (on-demand, cache 5 min)
```

**Key:** Polling intervals should be configurable, not 1Hz.

---

### 2. **Versioning Issues**

#### API Versioning (Endpoint versioning)
**Current:** `/api/v1/...`

**Problem if missing:**
- Backend updates `/matches/{id}/snapshot` response schema
- Game still expects old fields
- Parsing fails, match recovery broken

**Solution needed:**
```
GET /api/v1/matches/{matchId}/snapshot/{playerId}
Headers: Accept-Version: 1  (or use URL: /api/v1/...)
Response version: Content-Type: application/vnd.api+json; version=1
```

**Questions for API:**
- [ ] Is versioning strategy in place (v1, v2, etc)?
- [ ] If schema changes, will endpoint be `/api/v2/...` or version header?
- [ ] Is there a deprecation policy (support v1 for 6 months after v2 released)?

#### Data Versioning (Snapshot versioning)
**Current:** Each MatchSnapshot is immutable, but game state evolves

**Problem:**
- Game v1.0: MatchSnapshot has fields {board, hand, heroHealth}
- Game v1.1: MatchSnapshot adds {poisonStacks, stunStatus}
- Game v1.0 client receives v1.1 snapshot → missing fields, crashes

**Solution needed:**
```json
{
  "version": 2,
  "timestamp": "2026-04-18T10:30:00Z",
  "snapshotNumber": 42,
  "players": [...],
  "board": [...],
  "extra_v2_field": "..."
}
```

**Questions for API:**
- [ ] Does MatchSnapshot have a version field?
- [ ] Does it have a timestamp to detect staleness?
- [ ] How does it handle game updates that add new card types or abilities?

#### Match State Versioning
**Problem:**
- Game plays 50 turns
- Crash at turn 50
- Restore snapshot from turn 48
- What if turn 49-50 already happened on other player's NGO?
- Inconsistency: one player ahead, other behind

**Solution:**
```
MatchSnapshot must include:
- turnNumber (how many turns elapsed)
- lastActionNumber (action sequence counter)
- timestamp (when this snapshot was saved)
```

Game compares against local state:
- If snapshot is older → ignore it, keep playing
- If snapshot is newer → use it (other player was ahead, we need to catch up)

**Questions for API:**
- [ ] Does snapshot include turnNumber and actionNumber?
- [ ] Can snapshot be stale (from 5 turns ago)?
- [ ] How does server sync if one client recovered an old snapshot?

---

### 3. **Checkpoint Strategy**

**When to save snapshot to API:**

❌ **Not on every move** (too much HTTP)
```
PlayCard → [NGO] → all clients see it → NO API CALL YET
```

✅ **On turn boundary or timer**
```
Every 30 seconds (regardless of turn): POST snapshot to API for durability
If crash happens, recover to last 30-sec checkpoint
```

✅ **On match completion**
```
Match ends → POST final state + result to API → ratings update
```

**Code pattern:**
```csharp
// In DuelRuntime or BattleContext
private float _timeSinceLastCheckpoint = 0f;
private const float CHECKPOINT_INTERVAL = 30f;

void Update()
{
    _timeSinceLastCheckpoint += Time.deltaTime;
    if (_timeSinceLastCheckpoint > CHECKPOINT_INTERVAL)
    {
        SaveCheckpointToAPI();
        _timeSinceLastCheckpoint = 0f;
    }
}

async Task SaveCheckpointToAPI()
{
    var snapshot = BuildCurrentSnapshot();
    await api.PostSnapshotAsync(_matchId, snapshot);
    // Don't wait for response, fire-and-forget OK
    // (API call is async, doesn't block gameplay)
}
```

---

### 4. **HTTP Reliability**

Since HTTP is unreliable for real-time:

**Strategy: Ignore API failures for gameplay**
```csharp
try
{
    await api.PostSnapshotAsync(_matchId, snapshot);
}
catch (HttpRequestException ex)
{
    Debug.LogWarning($"Snapshot save failed (non-critical): {ex.Message}");
    // Keep playing, will retry next checkpoint
}
```

**Strategy: Retry on disconnect detection**
```csharp
if (NetworkOffline)
{
    await PollSnapshotWithBackoff(matchId);
    if (succeeds)
    {
        ResumeFromSnapshot();
    }
    else
    {
        OfferForfeit(); // After 5 min offline
    }
}
```

---

### 5. **HTTP Headers & Consistency**

**Missing from API contracts:**

Check if endpoints return these headers:
```
ETag: "abc123def456"  (for caching, detect changes)
Last-Modified: "2026-04-18T10:30:00Z"
Vary: "Accept-Encoding"
Cache-Control: "max-age=5, must-revalidate"  (for leaderboard)
```

**Conditional requests:**
```
GET /users/leaderboard
If-None-Match: "abc123"  (from ETag)
Response: 304 Not Modified (save bandwidth)
```

**Questions for API:**
- [ ] Do endpoints support ETags for caching?
- [ ] Do they support If-Modified-Since / Last-Modified?
- [ ] Are Cache-Control headers correct (not too long for leaderboard)?

---

## Updated Architecture Diagram

```
┌─────────────────────────────────────────────────┐
│              Netcode for GameObjects            │
│  (Real-time, <100ms latency, P2P or relay)     │
│                                                 │
│  ▪ Card plays                                   │
│  ▪ Turn execution                               │
│  ▪ Board updates                                │
│  ▪ Network handshakes                           │
│                                                 │
│  Clients: Authoritative game state              │
│  Server: Relay only (or optional relay)         │
└─────────────────────────────────────────────────┘
                      ▼
          ┌───────────────────────┐
          │   Match Checkpoint    │
          │   (Every 30 seconds)  │
          └───────────────────────┘
                      ▼
┌─────────────────────────────────────────────────┐
│       HTTP REST API (Async, Durable)            │
│  (For persistence, recovery, queries)           │
│                                                 │
│  ▪ POST /matches/{id}/checkpoint (async)       │
│  ▪ GET /matches/{id}/snapshot/{playerId}       │
│  ▪ POST /matches/{id}/complete (at match end)  │
│  ▪ GET /users/leaderboard (polling every 30s) │
│  ▪ GET /users/{id}/stats (on-demand)           │
│                                                 │
│  Server: Single source of truth for results    │
│  Clients: Read-only for recovery               │
└─────────────────────────────────────────────────┘
```

---

## Implementation Changes Needed

### Game-Side: New Checkpoint Service
**File:** `Assets/Runtime/Networking/MatchCheckpointService.cs`

```csharp
public class MatchCheckpointService : MonoBehaviour
{
    private const float CHECKPOINT_INTERVAL = 30f;
    private float _timeSinceCheckpoint = 0f;
    
    private CardGameApiClient _apiClient;
    private DuelRuntime _gameRuntime;
    
    private void Update()
    {
        if (!IsInMatch) return;
        
        _timeSinceCheckpoint += Time.deltaTime;
        if (_timeSinceCheckpoint >= CHECKPOINT_INTERVAL)
        {
            _ = SaveCheckpointAsync(); // Fire-and-forget
            _timeSinceCheckpoint = 0f;
        }
    }
    
    private async Task SaveCheckpointAsync()
    {
        try
        {
            var snapshot = _gameRuntime.BuildSnapshot();
            // POST without awaiting (non-blocking)
            await _apiClient.PostSnapshotCheckpointAsync(_matchId, snapshot);
            Debug.Log($"[Checkpoint] Saved at turn {_gameRuntime.CurrentTurnNumber}");
        }
        catch (Exception ex)
        {
            // Log but don't block gameplay
            Debug.LogWarning($"[Checkpoint] Failed (non-critical): {ex.Message}");
        }
    }
}
```

### API-Side: New Checkpoint Endpoint (if missing)
**Request:** ~~Don't send every move~~

**Instead:**
```
POST /api/v1/matches/{matchId}/checkpoint
{
  "playerId": "player1",
  "matchSnapshot": {...},
  "checkpointNumber": 42,
  "timestamp": "2026-04-18T10:30:00Z"
}
```

**Response:**
```json
{
  "success": true,
  "checkpointNumber": 42,
  "savedAt": "2026-04-18T10:30:00.123Z"
}
```

**Behavior:**
- ✅ Fire-and-forget (async)
- ✅ Server stores latest checkpoint
- ✅ GET /snapshot returns latest checkpoint
- ✅ Used for crash recovery only

---

## Data Sync Consistency

### NGO State vs API State

**During match (NGO authoritative):**
```
Player 1 plays card → NGO broadcasts → All clients see move
↓
(30s later) Checkpoint interval → Save to API
↓
API has checkpoint, but NGO is 30s ahead
```

**On crash + recovery:**
```
Player 1 crashes at turn 50
↓
Restart → Get last checkpoint from API (maybe turn 48)
↓
Player 1 resumes at turn 48, catches up via NGO
```

**Conflict: What if other player forfeited while P1 was offline?**
```
P2 can't play alone, match should end
On P1 reconnect:
- GET /snapshot → says match is completed
- Auto-close match, show result
- Don't try to resume
```

**Questions for API:**
- [ ] Does MatchSnapshot include `status` field (Completed, InProgress, etc)?
- [ ] Can we query: "Is this match still active?"
- [ ] What happens if we try to POST checkpoint to completed match?

---

## Versioning Checklist

Add to `api_stuff/missing.md`:

- [ ] **Endpoint versioning:** Does API support `/api/v2/...` if schema changes?
- [ ] **MatchSnapshot version:** Does snapshot include `version` field?
- [ ] **Timestamp:** Does snapshot include `timestamp` for staleness detection?
- [ ] **Turn number:** Does snapshot include `turnNumber` for consistency checks?
- [ ] **Action counter:** Does snapshot include `lastActionNumber`?
- [ ] **Match status:** Does snapshot include `status` (Active, Completed, Forfeited)?
- [ ] **Checkpoint endpoint:** Is there a dedicated `/checkpoint` endpoint or reuse `/snapshot`?
- [ ] **Cache headers:** Do endpoints return ETag, Last-Modified, Cache-Control?
- [ ] **Conditional requests:** Does API support If-None-Match, If-Modified-Since?
- [ ] **Deprecation policy:** How long are old API versions supported?

---

## Summary: What Changes

### ❌ Remove from Plan
- ~~Real-time API calls for every move~~
- ~~Server-side move validation via HTTP~~ (use Netcode validation instead)
- ~~REST API as gameplay transport~~

### ✅ Keep in Plan
- HTTP for persistent storage (match history, ratings)
- HTTP for state recovery (crash/reconnect)
- HTTP for leaderboards (polling)
- HTTP for profiles (on-demand)
- Checkpoints every 30s (fire-and-forget)
- Match completion POST

### 🆕 Add to Plan
- **MatchCheckpointService** — periodic snapshot saves
- **Versioning strategy** — handle schema evolution
- **Polling intervals** — configurable delays for leaderboard/profile
- **Offline detection** → Snapshot restore with exponential backoff
- **Consistency checks** — snapshot staleness, match status validation
