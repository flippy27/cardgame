---
name: add-api-client
description: Add or extend a server API client in the Unity CardDuel client, following the ApiClients/ + JsonUtility conventions. Use when wiring a new server endpoint into the game.
---

# Add an API client call

All server HTTP lives in `Assets/Runtime/Networking/ApiClients/*` — one client per
server domain. UI and battle code must consume these, never raw HTTP.

## Steps

1. **Pick or create the client** — match the server domain (e.g. cards →
   `CardApiClient`, matchmaking → `MatchmakingApiClient`, player cards →
   `PlayerCardsApiClient`). Mirror the route from the server controller.
2. **DTOs** — add `[System.Serializable]` classes with **public camelCase fields**
   matching the server record exactly. Shared DTOs go in `CommonDtos.cs`; client-
   specific ones can be nested in the client class. Rules:
   - server number → numeric field (`int`/`long`/`float`), never `string`;
   - server enum → `int` field (map to a `Core` enum in consuming code);
   - server `DateTimeOffset` → `string` field;
   - use `= -1` sentinels where `0` is a meaningful value.
3. **Call** — use `HttpClientHelper` (it attaches the `Bearer` token from
   `SecureTokenStorage`). Deserialize with `JsonUtility.FromJson<T>`; for top-level
   arrays use the existing `{ items: T[] }` wrapper pattern.
4. **Base URL** comes from `ApiConfig.BaseUrl` — never hardcode.

## Guardrails

- JsonUtility ignores properties and `[JsonProperty]` — only public fields work.
- After adding a DTO, run the `dto-sync` agent to confirm parity with the server.
- Don't simulate authoritative game state locally for multiplayer — render from
  `MatchSnapshot` via `SnapshotConverter`.
