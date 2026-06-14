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

## Card visual system (battle) — READ before touching card rendering

A card = **composite texture (art + frame)** on a 3D quad + **stat badges as overlay UI**.
- `CardArtLibrary.BuildComposite` builds ONLY art + frame (hand 512x768 2:3, board 512x512 square).
  Frames: `Resources/Art/frames3/frame_{hand|board}_{design}.png` (open ornamental, no sockets);
  `FrameDesign(cardType,faction)` maps type/faction → design. Art: `Resources/Art/cardart_type/{type}.png`
  (per-TYPE test art, precedes per-card `Resources/CardArt/{cardId}.png`).
- **Stat badges** = `CardStatBadges.cs`: under each card's world-space `StatsOverlay` Canvas it builds
  one badge per stat = circle Image + icon Image + **number TMP centred as a child** (so number never
  drifts from its circle). Cost (hand only), attack/health (units), armor (>0), rarity (hexagon+gem, no
  number). Used by `Card3DView` (hand), `Card3DPlayed` (board), `CardDetailOverlayUI` (preview) via
  `CardStatBadges.Apply(overlayRect, card, isBoard)`. Socket positions come from
  `CardArtLibrary.HandSocket()/BoardSocket()` (the single source of truth — tune there).
- **Board overlay occlusion gotcha**: the board quad is TILTED (bounds z-depth ~2) and scaled by the
  slot (~4.2x) × `boardCardScale`. `Card3DPlayed.LateUpdate` pushes the overlay
  `bounds.extents.z + margin` ALONG THE VIEW RAY toward `Camera.main` so badges clear the quad in Z
  (no parallax). Do NOT use TMP `_ZTestMode` (the Mobile/Distance Field shader ignores it) and do NOT
  use ZTest Always (it bleeds a card's numbers over other cards).
- **Combat positioning rule** (`Assets/mydocs/battle.md`): melee attacks only from Front; ranged/magic
  only from BackLeft/BackRight — **including counterattacks**. Enforced server-side
  (`MatchEngine.CanAttackFromCurrentSlot`).

## Debugging the running game
Unity writes all `Debug.Log` to `C:\Users\Flippy\AppData\Local\Unity\Editor\Editor.log`. **Read that
file** (grep) to diagnose runtime behaviour instead of asking the user to paste the console — this is
the fastest way to nail visual/positioning/timing bugs. Unity cannot be compiled from Claude Code; the
user recompiles in the editor and iterates by screenshot.
Validate C# without compiling via `mcp__ide__getDiagnostics` (per file) — but note it does NOT catch all
cross-namespace errors the Unity compiler does (e.g. a missing `using`). Input: project uses the NEW Input
System only — use `UnityEngine.InputSystem.Keyboard.current`, NEVER `UnityEngine.Input` (legacy throws).

## Systems added 2026-06-14 (see ../HANDOFF_20260614.md + memory)
- **Stat/effect badges**: `CardStatBadges` restyle — health = heart WITHOUT a circle, stat icons poke to the
  outer corner, armor raised off health; non-unit effect badges (+/- + status icon). 107 pixel-art icons
  installed under `Resources/Art/icons/{modular,status,skills,faction,rarity,type}` + `Resources/Art/items`
  (Point-filter via `CardArtImportPostprocessor`). Status sprite keys are PAST-tense (`status_poisoned`…).
- **Card icons**: skills + buffs/debuffs render via `CardIconGroup` (hand bottom-centre, board above); the
  group is created at runtime if the prefab didn't wire one. Skill icons = `Art/icons/skills/skill_{abilityId}`.
- **Progression UI**: `ProgressApiClient` → Lv/XP bar in `DeckBuilderShell` header; `PlayerProfileHud` (level
  circle = Player Profile button) in the main menu (`MatchmakingPanelController`); ★N card level in collection
  cells + preview (`CardCellView`/`CardDetailOverlayUI`). In-battle ★N is a TODO (snapshot lacks the level).
- **Card upgrade trees (client)**: "My Cards" tab in `DeckBuilderShell` + `CardUpgradeView` (leveling) +
  `CardUpgradeApiClient` (GET tree/state, POST apply-level).
- **Card effect shaders**: `Assets/Shaders/CardEffects.shader` + `CardEffectController` (Shield/Glow/Dissolve/
  Freeze/Burn/Holo/Outline). **Currently GATED OFF** (`Card3DPlayed.SurfaceEffectsEnabled=false`, summon glow
  removed) because the shader-swap blanked cards. Diagnose with the **FX Test** view (`EffectTestView`, menu
  button) which drives `CardEffectController` directly; fix the material binding before re-enabling.
- **Debug battle panel** (`DebugBattlePanel`, F9 / "DBG" button, editor/dev only): server-authoritative debug
  actions (HP/mana/draw/end-turn/status/force-attack) via `MatchSignalRCoordinator.DebugActionAsync`.
- **Match-start**: `MatchLoadingOverlay` + composite warm-up; fixed the play→board "disappear/reappear"
  (removed a renderer hide-gap in `GameplayPresenter3D.AnimateBoardCardEntry`).
- Card frames are still `frames3`; new `frames4` (title nameplate as a separate overlay, faction-themed,
  rarity-by-gem) are to be generated from the `tutorials/*.csv` manifests, then re-mapped in `CardArtLibrary`.
