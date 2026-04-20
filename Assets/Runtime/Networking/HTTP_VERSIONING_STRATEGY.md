# HTTP Versioning & Polling Strategy

**Audience:** Game client + API team  
**Purpose:** Ensure consistency, handle schema evolution, optimize bandwidth

---

## 1. API Endpoint Versioning

### Current State
- API uses `/api/v1/...` pattern ✅
- No evidence of v2 plan ❓

### What Happens When API Changes

**Scenario:** Game ships v1.0, API ships v1.0  
New card type added (e.g., "Artifact" vs current "Unit/Spell")

**Version 1.0:**
```json
{
  "cardType": "Unit",  // enum: Unit, Spell
  "unitType": "Melee"  // enum: Melee, Ranged
}
```

**Version 1.1 (backward compatible):**
```json
{
  "cardType": "Unit",    // still Unit, Spell
  "unitType": "Melee",   // still Melee, Ranged
  "isArtifact": false    // NEW field (optional)
}
```
Game v1.0 client: ignores `isArtifact`, works fine ✅

**Version 2.0 (breaking change):**
```json
{
  "cardType": "Unit | Spell | Artifact",  // ENUM CHANGED
  "unitType": "Melee"
}
```
Game v1.0 client: receives "Artifact", crashes (enum not recognized) ❌

### Solution: Versioning Strategy

**Option A: URL Versioning (recommended)**
```
/api/v1/matches/{id}/snapshot  → old schema
/api/v2/matches/{id}/snapshot  → new schema
Support v1 for 6 months after v2 released
```

**Option B: Content Negotiation**
```
GET /api/matches/{id}/snapshot
Accept: application/vnd.api+json; version=1

Response:
Content-Type: application/vnd.api+json; version=1
```

**Option C: Query Parameter**
```
GET /api/v1/matches/{id}/snapshot?schema=v1.1
```

**Recommendation:** Use Option A (URL versioning) — clearest, most REST-ful.

---

## 2. Data Versioning (Schema Evolution)

### MatchSnapshot Versioning

**Required fields in every MatchSnapshot response:**

```json
{
  "_schemaVersion": "1.0",      // Increment if fields added/removed
  "timestamp": "2026-04-18T10:30:45.123Z",  // When snapshot was created
  "checkpointNumber": 42,        // Sequence counter (1, 2, 3...)
  "turnNumber": 50,              // Game turn (0-indexed or 1-indexed?)
  "lastActionNumber": 127,       // Total actions taken this match
  "matchPhase": "InProgress",    // InProgress, Completed, Forfeited, etc
  
  // ... rest of snapshot
  "players": [...],
  "board": [...]
}
```

### Why Each Field

| Field | Purpose | Example |
|-------|---------|---------|
| `_schemaVersion` | Detect schema breaking changes | "1.0" → "2.0" |
| `timestamp` | Detect staleness | Is checkpoint >5m old? |
| `checkpointNumber` | Sequence consistency | Did we miss checkpoints? |
| `turnNumber` | Game progress | Are we at turn 42? |
| `lastActionNumber` | Full action count | 200 total actions so far |
| `matchPhase` | Match state | Resumed? Or already ended? |

### Compatibility Table

```
Game v1.0 receives _schemaVersion "1.0" → Use it ✅
Game v1.0 receives _schemaVersion "1.1" (new field added) → Use it ✅ (ignore new field)
Game v1.0 receives _schemaVersion "2.0" (breaking change) → Error ❌
```

**Client logic:**
```csharp
if (snapshot._schemaVersion.StartsWith("1."))
{
    // Compatible with v1 parser
    ParseV1Snapshot(snapshot);
}
else if (snapshot._schemaVersion.StartsWith("2."))
{
    // Incompatible, need new parser
    ShowErrorDialog("Game version outdated, please update");
}
```

---

## 3. Polling Strategy

### Leaderboard Polling

**Endpoint:** `GET /api/v1/users/leaderboard?page=1&pageSize=100&region=global`

**Polling interval:** 30 seconds (while leaderboard screen open)

**HTTP Optimization:**
```
GET /users/leaderboard?page=1
ETag: "abc123def456"

Response: 200 OK
ETag: "abc123def456"
Cache-Control: max-age=30, public

// 10 seconds later, player still viewing
GET /users/leaderboard?page=1
If-None-Match: "abc123def456"

Response: 304 Not Modified  ← No body, saves bandwidth
ETag: "abc123def456"
```

**Bandwidth optimization:**
- Without ETags: 100KB every 30s = 200KB/min = 12MB/hour
- With ETags + 304: ~100 bytes every 30s = 0.2KB/min = 12KB/hour (100x reduction)

**Questions for API:**
- [ ] Does `/users/leaderboard` return ETag header?
- [ ] Does it support If-None-Match for 304 responses?
- [ ] What's recommended Cache-Control value?

### Player Stats Polling

**Endpoint:** `GET /api/v1/users/{playerId}/stats`

**Polling interval:** On-demand (cache 5 minutes locally)

**Cache implementation:**
```csharp
private async Task<UserStats> GetPlayerStatsAsync(string playerId)
{
    var cached = LocalCache.GetStats(playerId);
    var cacheAge = DateTime.UtcNow - cached.CachedAt;
    
    if (cacheAge < TimeSpan.FromMinutes(5))
    {
        return cached.Data;  // Return from local cache
    }
    
    var fresh = await apiClient.GetPlayerStatsAsync(playerId);
    LocalCache.SaveStats(playerId, fresh);
    return fresh;
}
```

### Match Snapshot Polling (For Reconnection)

**Endpoint:** `GET /api/v1/matches/{matchId}/snapshot/{playerId}`

**Used only after crash/disconnect, with exponential backoff:**

```csharp
private async Task PollSnapshotWithBackoff(string matchId)
{
    int attempt = 0;
    while (attempt < maxAttempts)
    {
        try
        {
            var snapshot = await apiClient.GetSnapshotAsync(matchId, playerId);
            
            // Verify snapshot is fresh
            var age = DateTime.UtcNow - snapshot.timestamp;
            if (age > TimeSpan.FromMinutes(5))
            {
                Debug.LogWarning("Snapshot too old, risky to use");
                // Maybe reject, or use with warning?
            }
            
            return snapshot;  // Success
        }
        catch (Exception ex)
        {
            attempt++;
            int delayMs = (int)Math.Pow(2, attempt) * 1000;  // 2s, 4s, 8s...
            await Task.Delay(Math.Min(delayMs, 30000));  // Cap at 30s
        }
    }
    
    throw new TimeoutException("Failed to restore match state");
}
```

**Backoff sequence:**
- Attempt 1: immediate
- Attempt 2: wait 2s
- Attempt 3: wait 4s
- Attempt 4: wait 8s
- Attempt 5: wait 16s
- Attempt 6: wait 30s (capped)
- After 6 attempts (total ~60s): give up, offer forfeit

---

## 4. Match State Checkpoint Versioning

### Checkpoint Payload

```json
{
  "checkpointId": "cp-abc123",
  "matchId": "match-xyz",
  "playerId": "player1",
  "checkpointNumber": 5,
  "turnNumber": 42,
  "timestamp": "2026-04-18T10:30:45.123Z",
  "snapshot": {
    // Full DuelSnapshotDto
    "_schemaVersion": "1.0",
    "players": [...],
    "board": [...],
    ...
  }
}
```

**Verify checkpoints are sequential:**

Game should track:
```csharp
private int _lastCheckpointNumber = -1;

void SaveCheckpoint(MatchCheckpoint checkpoint)
{
    if (checkpoint.checkpointNumber != _lastCheckpointNumber + 1)
    {
        Debug.LogWarning($"Checkpoint gap: expected {_lastCheckpointNumber + 1}, got {checkpoint.checkpointNumber}");
        // Continue anyway, or reject?
    }
    
    _lastCheckpointNumber = checkpoint.checkpointNumber;
    ApplyCheckpoint(checkpoint);
}
```

---

## 5. Handling Stale Data

### Stale Snapshot Detection

When recovering from crash:

```csharp
var snapshot = await GetSnapshotAsync(matchId);
var age = DateTime.UtcNow - snapshot.timestamp;

if (age > TimeSpan.FromMinutes(5))
{
    // Checkpoint is >5 min old, game may have progressed significantly
    ShowDialog("Match data is outdated. Continue or forfeit?");
    
    // If continue: risk of inconsistency with NGO (other player may be ahead)
    // If forfeit: safe but player loses
}
else if (age > TimeSpan.FromSeconds(30))
{
    // Moderately fresh (safe to use, but log warning)
    Debug.LogWarning($"Using checkpoint {age.TotalSeconds:F0}s old");
}
else
{
    // Fresh checkpoint (good to use)
}
```

### Turn Number Validation

When restoring from checkpoint:

```csharp
// Game says we're at turn 50
// Checkpoint says turn 48

if (checkpoint.turnNumber > currentTurnNumber)
{
    // Checkpoint is ahead, use it (other player was ahead)
    LoadFromCheckpoint(checkpoint);
}
else if (checkpoint.turnNumber == currentTurnNumber)
{
    // Same turn, use checkpoint (authority from API)
    LoadFromCheckpoint(checkpoint);
}
else
{
    // Checkpoint is behind us (we're ahead)
    // This shouldn't happen unless we cached an old checkpoint
    Debug.LogError($"Checkpoint behind us (we're at {currentTurnNumber}, checkpoint at {checkpoint.turnNumber})");
    RejectCheckpoint();
}
```

---

## 6. Questions for API Team

### Versioning
- [ ] What's the versioning strategy for breaking changes? (URL `/v2` vs header vs query param)
- [ ] If schema changes, how many API versions are supported simultaneously? (v1 and v2? Or drop v1?)
- [ ] What's the deprecation timeline? (support v1 for 6 months after v2?)

### MatchSnapshot Schema
- [ ] Does response include `_schemaVersion` field?
- [ ] Does response include `timestamp` (when snapshot was created)?
- [ ] Does response include `checkpointNumber` (sequence counter)?
- [ ] Does response include `turnNumber`?
- [ ] Does response include `lastActionNumber` (total actions)?
- [ ] Does response include `matchPhase` or `matchStatus`?

### HTTP Caching
- [ ] Do endpoints return `ETag` header?
- [ ] Do endpoints support `If-None-Match` (for 304 Not Modified)?
- [ ] What's recommended `Cache-Control` for:
  - Leaderboard? (30s? 5m?)
  - Player stats? (5m? 1h?)
  - Card catalog? (1h? 24h?)

### Checkpoint Endpoint
- [ ] Is there a dedicated `/checkpoint` endpoint or reuse `/snapshot`?
- [ ] Does checkpoint response include `checkpointId` and `checkpointNumber`?
- [ ] Can game query "latest checkpoint" or always query by ID?

---

## 7. Checklist: Before Shipping

- [ ] All snapshots include `_schemaVersion`
- [ ] All snapshots include `timestamp`
- [ ] Leaderboard/stats endpoints return ETags
- [ ] GET endpoints support `If-None-Match`
- [ ] `/api/v1` is used consistently
- [ ] Deprecation policy documented
- [ ] No enum changes in v1 (only add new enums in v2)
- [ ] Cache headers set correctly (Cache-Control, max-age)
- [ ] 30-second checkpoint interval is acceptable for data loss (if crash happens, lose <30s of gameplay)

---

## Summary

| Aspect | Strategy | Rationale |
|--------|----------|-----------|
| API versions | `/v1`, `/v2`, etc. | Clear, REST-ful, easy to route |
| Schema versioning | `_schemaVersion` field in responses | Detect breaking changes client-side |
| Stale detection | `timestamp` in snapshot | Know age, decide if usable |
| Consistency | `checkpointNumber`, `turnNumber`, `lastActionNumber` | Detect gaps/reorders |
| Bandwidth | ETags + 304 Not Modified | Leaderboard: 100x reduction |
| Polling interval | 30s (leaderboard), 5m (stats), exponential backoff (reconnect) | Balance freshness vs. server load |
