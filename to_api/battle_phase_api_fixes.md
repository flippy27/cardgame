# Battle Phase API Fixes

## Why this exists

The Unity client can now play battle phase events slowly and readably, but multiplayer still depends on the snapshot payload being consistent per recipient.

Two problem areas remain likely on the API/backend side:

- turn ownership can still arrive ambiguous or inconsistent
- battle phase playback in multiplayer depends on raw `logs`, which is brittle

## 1. Turn ownership must be authoritative and self-consistent

For each snapshot sent to a given client, these fields should all agree:

```json
{
  "localSeatIndex": 0,
  "activeSeatIndex": 0,
  "activePlayerId": "player-uuid",
  "isLocalPlayersTurn": true
}
```

### Required guarantees

- `localSeatIndex` must be correct for the receiving client
- `activeSeatIndex` must point to the seat whose turn it currently is
- `activePlayerId` must be the real player id of the active seat
- `isLocalPlayersTurn` must be computed server-side for the receiving client, not globally

### Why this matters

If any one of those fields is wrong, both clients can end up believing:

- "it is the opponent's turn"

even when one of them should be able to act.

### Strong recommendation

Add player ids to each seat in the snapshot:

```json
{
  "seats": [
    {
      "seatIndex": 0,
      "playerId": "uuid-a"
    },
    {
      "seatIndex": 1,
      "playerId": "uuid-b"
    }
  ]
}
```

That removes the need for client inference from hidden hands or reconnect state.

## 2. Battle phase playback should not rely only on raw log strings

Right now multiplayer animation depends on parsing `logs[]` text like:

```text
[P0] Card A (ATK 3) -> [P1] Card B: 5->2HP
Direct attack to Player 1 Hero: 2 damage dealt. 20->18HP
Card B died.
```

This works, but it is fragile because:

- string formatting changes break parsing
- encoding issues (`->`, `→`, mojibake variants) break parsing
- clients need a large enough rolling log window to reconstruct a full phase

## Recommended fix

Expose structured battle events in snapshot order:

```json
{
  "battleEvents": [
    {
      "eventId": "evt-001",
      "kind": "card_attack",
      "sourceSeatIndex": 0,
      "sourceRuntimeId": "card-a",
      "targetSeatIndex": 1,
      "targetRuntimeId": "card-b",
      "damage": 3,
      "hpBefore": 5,
      "hpAfter": 2,
      "armorBlocked": 0
    },
    {
      "eventId": "evt-002",
      "kind": "hero_attack",
      "sourceSeatIndex": 0,
      "sourceRuntimeId": "card-c",
      "targetSeatIndex": 1,
      "damage": 2,
      "hpBefore": 20,
      "hpAfter": 18
    },
    {
      "eventId": "evt-003",
      "kind": "death",
      "targetSeatIndex": 1,
      "targetRuntimeId": "card-b"
    }
  ]
}
```

## Minimum requirement if `battleEvents` is not added yet

If the API keeps using `logs[]`, then:

- send a rolling window large enough for a full battle phase
- keep exact order stable
- keep message format stable
- keep hero attack logs with `hpBefore` and `hpAfter`
- keep death logs explicit

Recommended rolling window size:

- at least `40` entries

## 3. Hero attacks should remain individually visible

For good UX the client updates hero HP hit by hit during battle phase.

To support that reliably, each direct hero attack event/log must include:

- target seat/player
- damage
- hero HP before
- hero HP after

Without that, the UI can only jump to the final HP after the whole battle phase.

## 4. Best long-term contract

Best outcome:

- snapshots stay server-authoritative
- snapshots include stable turn ownership fields
- seats include `playerId`
- battle phase includes structured `battleEvents`
- final board state still arrives as the resolved snapshot

That gives the client enough information to animate everything without guessing.
