# Setup MainGame Scene - 3D Battle

## Generar Escena Completa

### Paso 1: Ejecutar Tool
```
Menu: Tools → Battle System → Setup 3D Battle Scene
Click: "Generate Complete Battle Scene"
```

Espera 5-10 segundos. Tool crea:
- ✅ Assets/Scenes/MainGame.unity
- ✅ Assets/Generated/3DBattle/ (materiales + prefabs)
- ✅ Jerarquía 3D completa
- ✅ GameModeManager (persiste entre escenas)
- ✅ LocalSinglePlayerCoordinator (AI testing)
- ✅ NetworkBootstrap (Netcode multiplayer)

### Paso 2: Asignar Decks (Si Auto-load Falla)

Escena se abre automática. En Hierarchy:
```
LocalSinglePlayerCoordinator
  ├─ localPlayerDeck = [Arrastra deck aquí]
  ├─ enemyDeck = [Arrastra deck aquí]
  ├─ rulesProfile = [Arrastra DuelRulesProfile aquí]
```

**Auto-load intenta buscar en Resources/Decks/, si existen ahí carga automático.**

### Paso 3: Test LocalMode

Click Play en editor:
- GameplayPresenter3D detecta LocalMode
- LocalSinglePlayerCoordinator.StartMatch() inicia
- Snapshots publican en BattleSnapshotBus
- 3D board renderiza + anima

## Flow Completo (Local → MainGame)

```
MatchmakingScene
  ↓ Click "Local Match"
  ↓ MatchmakingPanelController.HandleLocalMatch()
    ↓ GameModeManager.SetLocalMode()
    ↓ SceneBootstrap.LoadMainGame()
  ↓
MainGame Scene
  ↓ GameplayPresenter3D.OnEnable()
    ↓ Suscribe a BattleSnapshotBus
    ↓ Start() → LocalSinglePlayerCoordinator.StartMatch()
  ↓
DuelRuntime genera snapshot
  ↓
BattleSnapshotBus.Publish(JSON)
  ↓
GameplayPresenter3D.HandleSnapshot()
  ↓
Board3D + Hand3D + HUD3D renderiza
```

## Componentes Críticos

| Componente | Función | Status |
|-----------|---------|--------|
| GameModeManager | Detecta modo + activa coordinadores | ✅ Auto wired |
| LocalSinglePlayerCoordinator | Corre DuelRuntime + AI | ✅ Auto-loaded (decks) |
| NetworkBootstrap | Netcode init para online | ✅ Created |
| GameplayPresenter3D | Suscribe a snapshots + UI | ✅ Suscrito OnEnable |
| BattleSnapshotBus | Pub/sub JSON snapshots | ✅ Wired |

## Test Checklist

- [ ] Play → Scene carga sin errores
- [ ] LocalSinglePlayerCoordinator inicia automático (si autoStartOnStart=true)
- [ ] O llama manualmente: LocalSinglePlayerCoordinator.Instance.StartMatch()
- [ ] Board 3D visible con 6 slots (3 player, 3 enemy)
- [ ] Hand 3D muestra cartas en arc
- [ ] Puedo draggear cartas de hand a board
- [ ] Snapshots llegan (Debug.Log en GameplayPresenter3D.HandleSnapshot)
- [ ] Board se actualiza al jugar cartas
- [ ] Ataques anim (linea + flash)

## Troubleshooting

**Scene carga vacía:**
- Cámara está a Z=0? Boards at Y=3/-3? Canvas at Z=5?
- Check MainGame hierarchy vs tool template

**LocalSinglePlayerCoordinator no inicia:**
- autoStartOnStart = false? Llama manualmente.
- Decks no asignados? Auto-load busca Resources/Decks/

**BattleSnapshotBus no recibe:**
- LocalSinglePlayerCoordinator no corriendo? StartMatch() debe ejecutar.
- GameplayPresenter3D no suscrito? Checa OnEnable().

**Board no anima:**
- ProcessAttackLogs llamando? GameplayPresenter3D.HandleSnapshot debe detectar attacks en logs.
- Animaciones corrutinas ejecutando? Check Hand3DManager, Board3DManager.

## Decks Location

Busca decks en:
```
Assets/Resources/Decks/
Assets/Runtime/Data/Decks/
O arrastra desde Project directamente al inspector
```

## Next: Online Mode

Una vez LocalMode funciona, para OnlineMode:
1. GameModeManager.SetOnlineMode() (lo hace MatchmakingPanel)
2. NetworkBootstrap activa
3. CardDuelNetworkCoordinator (en NetworkManager) corre DuelRuntime en server
4. Snapshots broadcast a todos clientes
5. Determinismo garantizado (same JSON = same visuals)

---

**Ready?** Tools → Battle System → Setup 3D Battle Scene
