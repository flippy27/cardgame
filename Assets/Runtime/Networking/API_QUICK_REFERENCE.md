# API Integration - Quick Reference

**Status:** Game works via Netcode. API exists but not integrated.  
**Goal:** Use API for persistence, recovery, leaderboards. Keep Netcode for real-time gameplay.  
**Architecture:** Netcode (real-time) + HTTP API (durable storage + recovery)

---

## Data Flow

```
Player plays card → Netcode broadcasts (real-time) → all see it instantly
                ↓ (every 30 seconds)
         Checkpoint saved to API (async)
                ↓
     Match ends → final result POSTed to API
                ↓
     Ratings updated, match archived
         
App crashes → Restart → Get latest checkpoint from API → Resume game
```

---

## What You Have (Server)

### Match Endpoints
✅ `POST /matchmaking/private` → Create match  
✅ `POST /matchmaking/queue` → Queue ranked  
✅ `GET /matches/{id}/snapshot/{playerId}` → Get full state (for reconnection)  
✅ `POST /matches/{id}/checkpoint` — **NEEDS CLARIFICATION** (or use `/complete`?)  
❌ `POST /matches/{id}/reconnect` — **MISSING** (need to add)  
❌ `POST /matches/{id}/play` — **NOT USED** (Netcode handles real-time moves)  
❌ `POST /matches/{id}/end-turn` — **NOT USED** (Netcode handles turns)  
❌ `POST /matches/{id}/forfeit` — **NOT USED** (Netcode handles forfeit)  
✅ `POST /matches/{id}/complete` → Record final result + update rating  

### User Endpoints
✅ `GET /users/{id}/stats` → rating, wins, losses, winRate  
✅ `GET /users/leaderboard` → top 100 paginated  
✅ `GET /auth/register` → create account  
✅ `GET /auth/login` → JWT token  

### Card Endpoints
✅ `GET /cards` → all cards  
✅ `GET /cards/search?q=...` → search  
✅ `GET /decks/{playerId}` → get player's decks  
✅ `PUT /decks` → create/update deck  

---

## What You Need to Fix (API)

### Critical (blocking reconnection)
1. **Add `POST /matches/{id}/reconnect`** — so players can resume after crash
2. **Add snapshot versioning** — include `version`, `timestamp`, `turnNumber`, `matchStatus` fields
3. **Clarify checkpoint endpoint** — new `POST /checkpoint` or reuse `/complete`?

### Important (data consistency)
4. **Verify MatchSnapshot DTO** — must include `matchStatus` to detect if match ended
5. **Add versioning strategy** — how does API handle schema changes (v1 → v2)?
6. Auto-forfeit disconnected players after grace period (5+ min)

### Nice-to-have (performance)
7. Add HTTP cache headers (ETag, Cache-Control) for leaderboard/profile
8. Support conditional requests (If-None-Match) to save bandwidth

**See:** `api_stuff/missing.md` for details.

**Note:** Game does NOT sync real-time moves to API. Netcode handles that. API only stores periodic checkpoints.

---

## What You Need to Build (Game)

### Immediate (this week)
1. **MatchCheckpointService** — save snapshot to API every 30 seconds (fire-and-forget)
2. **Update CardGameApiClient** — add 3 new methods:
   - `GetSnapshotAsync()` for reconnection
   - `PostSnapshotCheckpointAsync()` for periodic saves
   - `CompleteMatchAsync()` already exists, just verify it's called
3. **Update LocalCacheService** — persist matchId + latest snapshot locally
4. **Test:** Start match → 30s passes → HTTP checkpoint logged

### Next (week 2-3)
5. **ReconnectionService** — restore from checkpoint on app crash/restart
6. **Modify GameBootstrap** — check for in-progress match, restore on startup
7. **Test:** Kill app mid-match → restart → game resumes from checkpoint

### Later (week 3-4)
8. **LeaderboardService** — fetch top 100 (polling every 30s)
9. **LeaderboardScreen** — display leaderboard UI
10. **ProfileScreen** — show player stats
11. **Token refresh** — handle JWT expiry gracefully

**Key:** Netcode stays as-is. No API calls during real-time gameplay.

---

## Quick Flow Diagram

```
┌─ Game Start ──┐
│               ├─ Check LocalCache for matchId
│               └─ If found → Restore from API snapshot
│
├─ Player plays card
│   └─ Call API: POST /matches/{id}/play
│       ├─ If succeeds → update local game
│       └─ If fails → show error, don't move
│
├─ Player ends turn
│   └─ Call API: POST /matches/{id}/end-turn
│
├─ Match ends
│   └─ Call API: POST /matches/{id}/complete
│       └─ Server updates rating
│
└─ App crashes
    └─ Next launch → restore from saved snapshot
```

---

## File Structure

```
Assets/Runtime/Networking/
├── API_INTEGRATION_PLAN.md ← Full details, 5 phases
├── IMPLEMENTATION_TASKS.md ← Detailed step-by-step tasks
├── API_QUICK_REFERENCE.md ← This file
├── api_stuff/
│   ├── missing.md ← What API needs to fix
│   └── ...
├── CardGameApiClient.cs ← Add 6 methods
├── MatchStateService.cs ← NEW: API call wrapper
├── ReconnectionService.cs ← NEW: crash recovery
├── LeaderboardService.cs ← NEW: leaderboard fetching
└── [modify existing services]
```

---

## Top 5 Things to Start With

1. **Read API_INTEGRATION_PLAN.md** — understand full scope
2. **Send api_stuff/missing.md to backend team** — request fixes
3. **Extend CardGameApiClient** — add GetSnapshot, PlayCard methods
4. **Create MatchStateService** — wrapper with error handling
5. **Hook BattleScreenPresenter** — call API on card play, test with Postman

---

## Estimated Effort

| Phase | Effort | Timeline |
|-------|--------|----------|
| API fixes (backend) | 3-5 days | Week 1 |
| Foundation (MatchCheckpointService) | 2-3 days | Week 1 |
| Reconnection (ReconnectionService) | 2-3 days | Week 2 |
| Leaderboards + UI | 3-4 days | Week 2-3 |
| Polish & testing | 4-5 days | Week 3 |
| **Total** | **14-20 days** | **3-4 weeks** |

Simpler than initially thought because Netcode already handles real-time gameplay.

---

## Success = ✅

- ✅ Netcode handles real-time gameplay (no change)
- ✅ Every 30s, checkpoint saved to API (fire-and-forget)
- ✅ App crash mid-match → restart → resume from checkpoint
- ✅ Match state persisted in DB (queryable for history)
- ✅ Leaderboard shows 100+ players (polled)
- ✅ Profile shows stats (cached 5 min)
- ✅ Rating updates properly after match completes
- ✅ No API latency affecting gameplay (Netcode is authoritative)

---

## Important Gotchas

1. **JWT token expires** — implement refresh logic
2. **Network latency** — add timeouts, don't make players wait forever
3. **Server validation** — must validate OR client won't trust it
4. **Snapshot staleness** — cache can get out of sync, needs version/timestamp
5. **Grace period** — disconnected player leaves opponent hanging unless auto-forfeit
6. **Offline mode** — decide: require internet always, or allow offline + sync later?

---

## Contacts/Links

- **API:** `/Users/idhemax/proyects/_MINE/cardgameapi`
- **Game:** `/Users/idhemax/proyects/_MINE/cardgame`
- **API Controllers:** `Controllers/*.cs` (MatchesController, UsersController, etc)
- **Game Networking:** `Assets/Runtime/Networking/`
- **Docs:** API_INTEGRATION_PLAN.md (full details)
