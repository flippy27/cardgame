# Testing Guide - Integración API

**Última actualización:** 2026-04-20  
**Estado:** Listo para testing

---

## 🚀 Testing Rápido

### Prerequisitos

1. ✅ Servidor ejecutándose: `cd cardgameapi && dotnet run`
2. ✅ Base de datos sincronizada con seed data
3. ✅ Token válido en SecureTokenStorage (o auto-login)

---

## Test 1: Diagnóstico Completo (CRÍTICO)

**Objetivo:** Verificar que el 401 se está reportando correctamente.

**Pasos:**

1. Crea un GameObject vacío en la escena
2. Agrega script `DiagnosticFlowTest` al GameObject
3. En Inspector, configura:
   - testEmail: `playerone@flippy.com`
   - testPassword: `password123`
4. Ejecuta en Play Mode
5. Llama `StartDiagnosticFlow()` desde Debug o OnEnable
6. **Lee console logs línea por línea:**

```
✅ STEP 1: LOGIN
✅ LOGIN SUCCESSFUL → Token guardado

✅ STEP 2: TOKEN STORAGE  
✅ TOKEN STORED → token presente en SecureTokenStorage

✅ STEP 3: GET CARDS (Protected)
   ❌ SI FALLA CON 401 → Problema es token en servidor
   ✅ SI FUNCIONA → Problema es en decks/profile endpoint

✅ STEP 4: GET DECKS (Protected)
   ❌ SI FALLA CON 401 → Verificar logs del servidor

✅ STEP 5: GET PROFILE (Protected)
   ❌ SI FALLA CON 401 → Verificar logs del servidor
```

**Qué significa cada resultado:**

| Resultado | Causa | Acción |
|-----------|-------|--------|
| LOGIN OK, CARDS FAIL 401 | Token no se valida en servidor | Revisar JWT en servidor |
| LOGIN OK, CARDS OK, DECKS FAIL | Endpoint específico rechaza | Revisar logs del servidor |
| TODO OK | ✅ Sin problemas 401 | Continuar con SignalR test |

---

## Test 2: SignalR Real-Time

**Objetivo:** Verificar que WebSocket funciona para gameplay.

**Pasos:**

1. Crea GameObject: `MatchSignalRCoordinator_Test`
2. Agrega script `GameflowWithSignalRExample`
3. Llama `StartGameFlow()` desde button o OnEnable
4. **Monitorea console:**

```
✅ [GameFlow] Authenticating...
✅ [GameFlow] Authenticated as <player-id>

✅ [GameFlow] Queueing for casual match...
✅ [GameFlow] Got match reservation: match-uuid

✅ [GameFlow] Initializing SignalR coordinator...
✅ [GameFlow] Connecting to match via SignalR...
✅ [GameFlow] SignalR connection established!

✅ [GameFlow] Match waiting for ready. Auto-readying...
✅ [GameFlow] Snapshot: phase=InProgress, turn=1
```

**Qué significa cada log:**

- `ConnectionEstablished!` → WebSocket conectado ✅
- `Snapshot` → Servidor envía datos en tiempo real ✅
- `Error:` → Problema en SignalR (revisar server logs)

---

## Test 3: Full Game Flow (End-to-End)

**Objetivo:** Simular un match completo desde login hasta fin de juego.

**Pasos:**

1. Crea GameObject: `GameTest`
2. Agrega script `FullGameFlowTest`
3. En Inspector, configura:
   - testEmail: `player1@flippy.com`
   - testPassword: `password123`
   - testDeckId: `starter-deck`
   - testDurationSeconds: `30` (tiempo de espera para game end)
4. Llama `StartFullGameFlowTest()` desde button o OnEnable
5. **Espera mientras ejecuta cada step:**

```
STEP 1: Authentication
  ✅ Authenticated. Player ID: <id>

STEP 2: Load Cards Catalog
  ✅ Loaded 200+ cards

STEP 3: Load Player Decks
  ✅ Loaded 3 player decks

STEP 4: Queue for Casual Match
  ✅ Queued for match. Match ID: <match-uuid>

STEP 5: Connect to Match (SignalR)
  ✅ Connected to match via SignalR

STEP 6: Set Ready & Wait for Game Start
  ✅ Set ready = true
  📸 Snapshot: phase=InProgress, turn=1

STEP 7: Play a Card
  ⚠️  PlayCard would be called here

STEP 8: End Turn
  ✅ End turn sent
  📸 Snapshot: turn=2

STEP 9: Wait for Game to End
  ⏳ Waiting for game to end...
  ✅ Game ended! Winner: Seat 0
```

**Éxito:** Todos los steps completados sin errores 401 ✅

---

## Test 4: Deck Selection UI

**Objetivo:** Verificar que UI carga y selecciona decks.

**Pasos:**

1. Crea Canvas con DeckSelectionPanel prefab
2. Configura referencias (deck list, buttons, text, etc.)
3. Llama `await deckSelectionPanel.LoadDecksAsync(authService)`
4. **Verifica:**
   - [ ] Panel carga sin errores
   - [ ] Lista muestra todos los decks del jugador
   - [ ] Al hacer click en deck, se selecciona
   - [ ] Play button solo habilitado cuando hay deck seleccionado
   - [ ] Back button retorna a pantalla anterior

---

## Test 5: Detector de Modo (HTTP vs SignalR)

**Objetivo:** Verificar que MatchCoordinatorFactory elige el modo correcto.

**Código test:**

```csharp
// En cualquier script:
var coordinator = MatchCoordinatorFactory.Instance.GetCoordinator();

if (MatchCoordinatorFactory.Instance.CurrentType == MatchCoordinatorFactory.CoordinatorType.SignalR)
{
    Debug.Log("✅ Usando SignalR (WebSocket)");
}
else
{
    Debug.Log("⚠️  Usando HTTP polling (fallback)");
}
```

**Esperado:**
- Primero intenta SignalR
- Si falla, cae a HTTP
- Ambos métodos funcionan

---

## Troubleshooting

### 401 Unauthorized en todoslos endpoints

**Causa:** Token no se recupera o es inválido

**Debug:**
```csharp
var token = SecureTokenStorage.GetToken();
Debug.Log($"Token: {token}");
Debug.Log($"Token vacío: {string.IsNullOrEmpty(token)}");
```

**Solución:**
1. Verificar que `SecureTokenStorage.SaveToken()` se ejecutó
2. Verificar que `PlayerPrefs.Save()` se llamó
3. En servidor: verificar logs de validación JWT

---

### SignalR no conecta

**Causa:** URL incorrecta o token inválido

**Debug logs esperados:**
```
[HTTP] Authorization header added: Bearer eyJ...
[SignalR] Connecting to http://192.168.1.84/hubs/match?matchId=...
```

**Solución:**
1. Verificar API URL en ConfigManager
2. Verificar token es válido (test GET /cards primero)
3. Verificar que server tiene endpoint `/hubs/match`

---

### Timeout esperando game start

**Causa:** Segundo jugador no se conectó o no está ready

**Debug:**
1. Abrir dos instancias del juego simultáneamente
2. Player 1 queue → Player 2 queue
3. Ambos click "Play"
4. Uno debería ver "Match started"

---

## Checklist de Testing

### Antes de llamar esto "COMPLETO"

- [ ] DiagnosticFlowTest pasa sin 401
- [ ] GameflowWithSignalRExample conecta y recibe snapshots
- [ ] FullGameFlowTest completa todos los steps
- [ ] DeckSelectionPanel carga decks
- [ ] MatchCoordinatorFactory usa SignalR (o HTTP fallback)
- [ ] GameplayPresenter3D usa factory sin errores
- [ ] Compilación sin warnings

### Antes de ir a PRODUCCIÓN

- [ ] Error handling para desconexiones
- [ ] Mensajes al usuario (spinners, estados)
- [ ] Timeout handling (15s para GameStart, 30s para GameEnd)
- [ ] Reconnection logic
- [ ] Persist estado en caso de crash

---

## Logs Importantes para Revisar

### Console Unity
```
[HTTP] Authorization header added: Bearer ... ✅
[SignalR] Connected to match hub ✅
✅ Authenticated. Player ID: ... ✅
📸 Snapshot: phase=InProgress ✅
```

### Server Logs (Backend)
```
JWT Validation: Success
Bearer Token: Validated
Endpoint: /api/v1/cards → 200 OK
SignalR Hub: Client connected
Match: Started (2 players ready)
```

---

## Próximos Pasos

1. **HOY:** Ejecutar DiagnosticFlowTest y compartir logs
2. **MAÑANA:** Si DiagnosticFlowTest OK, ejecutar GameflowWithSignalRExample
3. **MAÑANA:** Si GameflowWithSignalRExample OK, ejecutar FullGameFlowTest
4. **ESTA SEMANA:** Integrar DeckSelectionPanel en main flow

---

**Si todo passa estos tests, la integración API está LISTA para gameplay.**
