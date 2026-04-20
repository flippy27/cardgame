# HTTP API Integration Guide

## Overview
Unity client now communicates with CardDuel API via HTTP instead of Netcode RPC.

**Flow:**
1. User queues for match via `MatchmakingService.QueueCasual(deckId)`
2. API returns `MatchReservation` with matchId + seatIndex
3. Create `MatchHttpCoordinator` with match details
4. Coordinator polls `/api/v1/matches/{matchId}/snapshot/{playerId}` every 0.5s
5. Snapshots convert to `DuelSnapshotDto` → broadcast to `BattleSnapshotBus`
6. `GameplayPresenter3D` receives updates → renders board/hand/HUD

## Architecture

### Core Classes

**MatchmakingService** — Wraps queueing API calls
```csharp
var service = new MatchmakingService(apiClient, authService);
var reservation = await service.QueueCasual(deckId);
// reservation.MatchId, reservation.SeatIndex
```

**MatchHttpCoordinator** — Polling-based match handler
```csharp
coordinator.Initialize(matchId, playerId, seatIndex, token);
// Polls API, broadcasts snapshots to BattleSnapshotBus
coordinator.RequestPlayCard(handKey, slotIndex);
coordinator.RequestEndTurn();
```

**MatchplayApiClient** — HTTP layer for gameplay
- `GetSnapshot(matchId, playerId, token)` → `MatchSnapshot`
- `PlayCard(matchId, playerId, handKey, slotIndex, token)` → `MatchSnapshot`
- `EndTurn(matchId, playerId, token)` → `MatchSnapshot`
- `SetReady(matchId, playerId, isReady, token)` → `MatchSnapshot`

**SnapshotConverter** — Converts `MatchSnapshot` (API) ↔ `DuelSnapshotDto` (Unity)
```csharp
var duelSnapshot = SnapshotConverter.Convert(apiSnapshot, localSeatIndex);
```

### Integration Points

**GameplayPresenter3D.RequestPlayCard()**
- Checks: Local mode → LocalSinglePlayerCoordinator
- Checks: HTTP mode → MatchHttpCoordinator
- Falls back: Netcode → CardDuelNetworkCoordinator

**GameplayPresenter3D.RequestEndTurn()**
- Same precedence as RequestPlayCard

## Setup Example

```csharp
// 1. Authenticate
var authService = new AuthService(baseUrl);
await authService.Login(email, password);

// 2. Create matchmaking service
var matchmakingClient = new MatchmakingApiClient(baseUrl);
var matchmaking = new MatchmakingService(matchmakingClient, authService);

// 3. Queue for match
var reservation = await matchmaking.QueueCasual(deckId);

// 4. Initialize coordinator
var coordinator = gameObject.AddComponent<MatchHttpCoordinator>();
coordinator.Initialize(
    reservation.MatchId,
    authService.CurrentPlayerId,
    reservation.SeatIndex,
    authService.CurrentToken
);

// 5. Load battle scene
// GameplayPresenter3D.RequestPlayCard/RequestEndTurn now route to coordinator
```

## API Endpoints Used

```
POST   /api/v1/matchmaking/queue
POST   /api/v1/matchmaking/private
POST   /api/v1/matchmaking/private/join
GET    /api/v1/matches/{matchId}/snapshot/{playerId}
POST   /api/v1/matches/{matchId}/ready
POST   /api/v1/matches/{matchId}/play
POST   /api/v1/matches/{matchId}/end-turn
POST   /api/v1/matches/{matchId}/forfeit
```

## Testing Checklist

- [ ] Two clients authenticate with different users
- [ ] One client queues casual match
- [ ] Both clients receive matchId
- [ ] Match initializes in ready screen
- [ ] Both clients click ready
- [ ] Match transitions to InProgress
- [ ] Player 0 plays card → all clients update
- [ ] Player 1 plays card → all clients update
- [ ] Player 0 ends turn → board executes attacks
- [ ] Player 1's turn starts
- [ ] Match completes when one hero dies

## Known Limitations

- API doesn't expose detailed unit types (Melee/Ranged/Magic in snapshot)
- API doesn't track canAttack flag or turnsUntilCanAttack
- Opponent hand hidden (correct privacy)
- cardPile, deadCardPile counts not exposed

## Next Steps

1. Create dedicated HTTP matchmaking UI (replaces MPS UI)
2. Add ready screen with hand selection
3. Test e2e with two clients
4. Add reconnection logic for grace period
5. Implement match completion/rating updates
