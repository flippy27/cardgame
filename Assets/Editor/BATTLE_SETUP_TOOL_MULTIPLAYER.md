# Battle Scene Setup Tool — Multiplayer Edition

## What's New

Tool now generates **complete battle scenes** that work with **both LocalMode (AI) and OnlineMode (Netcode)**:

✅ GameModeManager (switches modes)
✅ LocalSinglePlayerCoordinator (AI testing)
✅ NetworkBootstrap (Netcode multiplayer)
✅ 3D presentation layer (Board, Hand, Camera, UI)
✅ Snapshot synchronization (both modes)

## Quick Start

```
1. Menu: Tools → Battle System → Setup 3D Battle Scene
2. Click "Generate Complete Battle Scene"
3. Espera a que termine (5-10 seg)
4. Scene Assets/Scenes/MainGame.unity se abre auto
5. Click Play

LocalSinglePlayerCoordinator intenta auto-cargar decks de Resources/Decks/
Si no encuentra, asigna manualmente en inspector.
```

## Mode Switching

### LocalMode (default for testing)

```csharp
GameModeManager.Instance.SetLocalMode();
// LocalSinglePlayerCoordinator starts
// DuelRuntime runs locally with AI opponent
// Snapshots published via BattleSnapshotBus
// Fast iteration, no network needed
```

### OnlineMode (Netcode multiplayer)

```csharp
GameModeManager.Instance.SetOnlineMode();
// NetworkBootstrap initializes
// Netcode connects to session
// CardDuelNetworkCoordinator runs on server
// Snapshots broadcast to all clients via network
```

## Generated Scene Hierarchy

```
MainGame/
├── Main Camera (Camera3DController)
├── Board3DContainer (Board3DManager - enemy + local boards)
├── Hand3DContainer (Hand3DManager - card arc animation)
├── Canvas (HUD3D with HP/Mana/TurnInfo)
├── GameplayPresenter3D (subscribes to BattleSnapshotBus)
├── AudioManager (sound effects)
├── GameModeManager ⭐ (mode switching)
├── NetworkBootstrap ⭐ (Netcode setup)
├── LocalSinglePlayerCoordinator ⭐ (AI coordinator)
└── MatchCompletionScreen (victory/defeat)
```

## Data Flow

### LocalMode
```
LocalSinglePlayerCoordinator
  ↓ (runs)
DuelRuntime (battle logic)
  ↓ (publishes)
BattleSnapshotBus.Publish(JSON)
  ↓ (receives)
GameplayPresenter3D.HandleSnapshot(json)
  ↓ (updates)
Board3D, Hand3D, HUD3D (3D visuals)
```

### OnlineMode
```
Player 1 (Client)
  ↓ (inputs)
CardDuelNetworkCoordinator.SubmitAction() [RPC]
  ↓
CardDuelNetworkCoordinator (Server)
  ↓ (runs)
DuelRuntime (authoritative)
  ↓ (broadcasts)
BattleSnapshotBus via Network
  ↓
All Clients receive JSON
  ↓
GameplayPresenter3D on each client (identical UI)
```

## Key Features

### ✅ Mode Detection
- Automatic activation of right coordinator
- GameModeManager.IsLocalMode property
- Can switch at runtime (though rarely needed)

### ✅ Snapshot-Based Sync
- Both modes use identical JSON schema (DuelSnapshotDto)
- Deterministic gameplay (same inputs = same output)
- No RNG, all randomness is seeded

### ✅ Animator Integration
- Card placement animations (slide-up)
- Attack effects (line + flash)
- Repositioning on death (shift animation)
- Tooltips on hover

### ✅ Hand Card Arc
- Cards spawn in arc formation
- Smooth ease-in animation
- Interactive drag-and-drop to board

## For Testing LocalMode

1. **Add test decks:**
   - LocalSinglePlayerCoordinator.localPlayerDeck (your deck)
   - LocalSinglePlayerCoordinator.enemyDeck (AI deck)
   - Use CardDefinition assets from GameService

2. **Configure AI:**
   - aiDifficulty (Easy/Medium/Hard)
   - aiFirstActionDelay (seconds before first action)
   - aiActionDelay (seconds between actions)
   - maxAiActionsPerTurn

3. **Run battle:**
   - Press Play in Unity
   - LocalSinglePlayerCoordinator auto-starts (if autoStartOnStart=true)
   - Or call manually: LocalSinglePlayerCoordinator.Instance.StartMatch()

## For Testing OnlineMode

1. **Setup Netcode:**
   - NetworkManager prefab must exist (Netcode for GameObjects)
   - MpsGameSessionService configured (matchmaking)
   - NetworkBootstrap will initialize

2. **Run multiplayer:**
   - Start host client (listen server)
   - Start guest client (connect to host)
   - Both receive BattleSnapshotBus updates via network

3. **Verify sync:**
   - Both clients should show identical board
   - Cards move at same time
   - Damage applied synchronously

## Architecture

### GameModeManager
- **Job:** Switch between LocalMode and OnlineMode
- **Method:** Activates/deactivates coordinators
- **Persistent:** DontDestroyOnLoad (survives scene transitions)

### LocalSinglePlayerCoordinator
- **Job:** Run DuelRuntime locally with AI
- **Publishes:** BattleSnapshotBus with local snapshots
- **AI:** Uses SimpleCardAiAgent (basic card play + attacks)

### NetworkBootstrap
- **Job:** Initialize Netcode and GameService
- **Loads:** Card catalog from API (CardRegistry)
- **Manages:** NetworkManager lifecycle

### GameplayPresenter3D
- **Job:** Subscribe to BattleSnapshotBus
- **Receives:** JSON from LocalMode or network (OnlineMode)
- **Updates:** Board3D, Hand3D, HUD3D with animations

## Troubleshooting

### "LocalSinglePlayerCoordinator not found"
- Tool didn't create it or it's inactive
- Check MainGame hierarchy, should be under root
- Verify GameModeManager is active

### "Board is frozen / won't animate"
- LocalSinglePlayerCoordinator might not be running
- Check: LocalSinglePlayerCoordinator.IsActive
- Call: StartMatch() if needed

### "Network snapshots not arriving"
- NetworkBootstrap might not have started
- Check: GameModeManager.IsLocalMode should be false
- Verify NetworkManager exists and is spawned

### "AI not taking turns"
- LocalSinglePlayerCoordinator might be inactive (OnlineMode set)
- Call: GameModeManager.Instance.SetLocalMode()
- Verify: LocalSinglePlayerCoordinator.autoStartOnStart is true

## Next Steps

1. ✅ Scene generation (tool handles it)
2. ✅ 3D visualization (GameplayPresenter3D + animations)
3. ⏳ Card definitions (from GameService/API)
4. ⏳ Ability effects (already implemented, just needs integration)
5. ⏳ Spell/Equipment execution (CardType.Equipment/Spell not yet executed)

## Files Modified

- **BattleSceneSetupTool.cs** - Added GameModeManager, LocalSinglePlayerCoordinator, NetworkBootstrap creation
- **GameModeManager.cs** - Enhanced with ApplyMode() to activate/deactivate coordinators
- **Tool help text** - Updated to show multiplayer features

## Determinism Guarantee

Both LocalMode and OnlineMode produce **identical snapshots** because:
1. DuelRuntime is deterministic (no floating point, no threading)
2. All randomness is seeded (predictable)
3. Both modes use same battle logic (BattleContext, skills, abilities)
4. Snapshots are identical JSON schema

Result: **All clients see the same board**, no desyncs, no rollback needed.
