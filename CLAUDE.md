# CLAUDE.md — cardsGame (CardDuel Unity client)

Unity client for **CardDuel**, a turn-based card-battle game. Talks to the
server-authoritative backend in `../CardDuel.ServerApi` (repo
`flippy27/cardgameapi`). This repo is `flippy27/cardgame`.

## Stack

- **Unity 6000.4.0f1** (Unity 6.4), 2D + 3D battle presentation.
- Runtime code: one assembly `Flippy.CardDuelMobile.Runtime` under `Assets/Runtime`.
  Tests under `Assets/Tests` (`Flippy.CardDuelMobile.Tests`).
- **`UnityEngine.JsonUtility`** for all server (de)serialization — NOT Newtonsoft.
- Unity Netcode / Transport packages present; live play uses **SignalR over
  WebSocket** to the server hub.

## Run / build

Open in Unity 6000.4.0f1. The `Tools` menu auto-generates the 3D battle scene
(see `SETUP_MAINSCENE.md`). Single-player vs AI runs without the server via
`LocalSinglePlayerCoordinator`; multiplayer needs the API + a JWT login.
Test entry points described in `TESTING_GUIDE.md` (`DiagnosticFlowTest`,
`FullGameFlowTest`, SignalR example).

Server URL: `ApiConfig.BaseUrl` — from `API_BASE_URL` env, else
`Resources/config.json` (`AppSettings.api.baseUrl`), else defaults
(`http://localhost:5000` in editor). See `Assets/Runtime/Core/ConfigManager.cs`.

## Layout (`Assets/Runtime`)

- `Core/` — config, app settings, enums (`CardType`, `CardRarity`, … mirroring the
  server), `CardDefinition` ScriptableObject (the **runtime, enum-typed** card).
- `Networking/`
  - `ApiClients/` — one client per domain (`AuthApiClient`, `CardApiClient`,
    `MatchmakingApiClient`, `MatchplayApiClient`, `PlayerCardsApiClient`,
    `InventoryApiClient`, `CraftingApiClient`, …). DTOs in `CommonDtos.cs` +
    per-client nested classes. `HttpClientHelper` adds the `Bearer` token.
  - `MatchSignalRCoordinator.cs` — live play (invoke hub, listen for
    `"MatchSnapshot"`). `MatchHttpCoordinator.cs` — HTTP-polling fallback.
  - `SnapshotConverter.cs` — server `MatchSnapshot` → client battle DTOs.
  - `CardCatalogCache.cs` — caches `ServerCardDefinition` (the **wire DTO**).
- `Battle/` — `DuelRuntime` (client-side battle model), phase managers,
  `Presentation/` (3D rendering, `CardSurfaceVisualRenderer`).
- `SinglePlayer/` — `LocalSinglePlayerCoordinator` (builds runtime decks from the
  catalog, AI opponent via `SimpleCardAiAgent`).
- `UI/` — `DeckBuilding/` (catalog, deck edit, card detail, crafting), matchmaking.
- `Data/` — deck/skill ScriptableObjects, `DeckValidator` (min 20 / max 30 /
  3 copies max).

## Two card types — don't confuse them

- **`ServerCardDefinition`** (`Networking/ApiClients/CommonDtos.cs`) — the wire DTO
  deserialized from `/api/v1/cards`. Fields are **camelCase**; enum-ish fields
  (`cardType`, `cardRarity`, `cardFaction`, `unitType`) are **ints** matching the
  server's integer enums.
- **`CardDefinition`** (`Core`) — the enum-typed runtime ScriptableObject.
  `LocalSinglePlayerCoordinator.BuildRuntimeCard` maps DTO → runtime (int → enum).

## Contract with the server — READ BEFORE CHANGING A DTO

- Server JSON is **camelCase** and **enums are integers**. Mirror field names/types
  exactly; a number-valued server field must be an `int`/numeric field here
  (`JsonUtility` silently drops a number into a `string` field — this exact bug hit
  the card catalog; see `../CardDuel.ServerApi/CONTRACT_REVIEW.md`).
- Server is authoritative: render from `MatchSnapshot`, don't simulate combat
  locally for multiplayer. Consume `battleEvents` in ascending `sequence`; treat
  `card_attack` as windup, `card_damage`/`card_counterattack` as real mutations.
- Canonical server contract: `../CardDuel.ServerApi/GAME_INTEGRATION_GUIDE.md`.
- Local design notes: `Assets/mydocs/`, `Assets/Docs/`, `Assets/Mock/`.

## Conventions

- DTO classes are `[System.Serializable]` with public camelCase fields (JsonUtility
  requirement — no properties, no `[JsonProperty]`).
- Keep server calls inside `Networking/ApiClients/*`; UI/battle code consumes the
  coordinators/converted snapshots, not raw HTTP.
