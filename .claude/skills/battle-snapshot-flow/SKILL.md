---
name: battle-snapshot-flow
description: How live match state flows from the server into the Unity battle presentation, and the rules for animating battle events. Use when working on battle rendering, the SignalR/HTTP coordinators, SnapshotConverter, or battle-event animation/dedup.
---

# Battle snapshot & event flow

The server is **authoritative**. The client renders what the snapshot says; it does
not compute combat for multiplayer.

## Pipeline

1. **Transport** — `MatchSignalRCoordinator` (preferred, WebSocket hub
   `/hubs/match`) or `MatchHttpCoordinator` (polling fallback) receives a
   `MatchSnapshot`.
   - Client invokes `ConnectToMatch`/`SetReady`/`PlayCard`/`EndTurn`/`DestroyCard`/
     `Forfeit`; the server returns + broadcasts the new `MatchSnapshot`
     (server→client event name: `"MatchSnapshot"`).
2. **Convert** — `SnapshotConverter` turns the wire `MatchSnapshot` into client
   battle DTOs.
3. **Publish** — snapshots go onto the battle bus; presenters (`Battle/Presentation`)
   subscribe and animate.

## Battle-event rules (`battleEvents[]`)

- Process in **ascending `sequence`**. Sequence is strictly increasing per match and
  never reused; **dedupe by sequence** (snapshots may re-send earlier events).
- `card_attack` = intent/windup (no health mutation). Animate the real mutation from
  `card_damage` / `card_counterattack`. `death` = removal. `hero_damage` = hero HP.
- Enums in snapshot DTOs arrive as **ints** (e.g. `phase`, `slot`, `kind`,
  `effectKind`) — map to `Core` enums.
- Board slots: `Front=0, BackLeft=1, BackRight=2`. Match phases:
  `WaitingForPlayers=0, WaitingForReady=1, InProgress=2, Completed=3, Abandoned=4`.

## Guardrails

- A player may act only when `phase==InProgress` and their seat == `activeSeatIndex`.
- Don't add client-side combat resolution for multiplayer — it desyncs from the
  server. Single-player AI uses `LocalSinglePlayerCoordinator` + `DuelRuntime`
  intentionally offline.
- Contract details: `../CardDuel.ServerApi/GAME_INTEGRATION_GUIDE.md`.
