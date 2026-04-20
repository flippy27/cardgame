# Plan de Acción - Integración API CardDuel Game

**Fecha:** 2026-04-20  
**Estado:** Compilación casi lista, necesita debugging y refinamiento  
**Responsable:** Claude Code + User

---

## 📋 Tabla de Contenidos

1. [Estado Actual](#estado-actual)
2. [Problemas Identificados](#problemas-identificados)
3. [Plan de Acción (Prioridad)](#plan-de-acción-prioridad)
4. [Checklist de Implementación](#checklist-de-implementación)

---

## Estado Actual

### ✅ Lo que YA está implementado en Unity

| Componente | Estado | Ubicación |
|-----------|--------|-----------|
| **HTTP Client centralizado** | ✅ | HttpClientHelper.cs |
| **Autenticación JWT** | ✅ | AuthService.cs + SecureTokenStorage.cs |
| **Token Bearer headers** | ✅ | HttpClientHelper (todas las requests) |
| **HTTP polling coordinator** | ✅ | MatchHttpCoordinator.cs |
| **Snapshot converter** | ✅ | SnapshotConverter.cs |
| **Compilación C#** | ⚠️ 95% | Enums + tipos corregidos |
| **BattleSnapshotBus** | ✅ | BattleSnapshotBus.cs |
| **GameplayPresenter3D integration** | ✅ | GameplayPresenter3D.cs |
| **Battle system 3D** | ✅ | Completo con skills/abilities |

### ❌ Lo que FALTA o NECESITA TRABAJO

| Aspecto | Problema | Prioridad |
|--------|----------|-----------|
| **Token validation (401 error)** | Server rechaza token válido | 🔴 CRÍTICO |
| **SignalR WebSocket** | Usando HTTP polling (lento) | 🟡 IMPORTANTE |
| **Match start flow** | No está probado end-to-end | 🟡 IMPORTANTE |
| **UI feedback** | Sin mensajes sobre estado servidor | 🟡 IMPORTANTE |
| **Error handling** | 401 no muestra causa raíz | 🔴 CRÍTICO |
| **Deck selection** | No integrado con matchmaking | 🟡 IMPORTANTE |
| **Reconnection logic** | No implementado | 🟢 BAJA |

---

## Problemas Identificados

### 1. 🔴 **Token 401 Unauthorized (CRÍTICO)**

**Síntoma:** Token se envía correctamente (`Bearer eyJ...`) pero servidor rechaza con 401.

**Causa probable:**
- Token se genera en servidor login, pero no se valida correctamente en otros endpoints
- Formato del token es incorrecto
- Token no está siendo actualizado/refrescado
- Servidor espera header diferente (ej: `X-Auth-Token` en lugar de `Authorization`)
- Validación en servidor falla silenciosamente

**Impacto:** Ningún endpoint protegido funciona (decks, profile, etc.)

**Acción requerida:**
1. ✅ HECHO: Agregamos logging en HttpClientHelper para verificar que el token se envía
2. TODO: Revisar logs del servidor para ver qué rechaza
3. TODO: Verificar formato JWT en server-side (encode/decode)
4. TODO: Verificar if server está usando middleware correcto para Bearer validation

---

### 2. 🟡 **HTTP Polling vs WebSocket (IMPORTANTE)**

**Síntoma:** Gameplay usa polling cada 0.5s en lugar de WebSocket real-time.

**Problema:**
- MatchHttpCoordinator.cs hace GET cada 0.5s (ineficiente)
- API probablemente espera SignalR Hub para gameplay
- Latencia innecesaria (0.5s delay entre acciones)
- No aprovecha capacidades real-time del servidor

**Impacto:** Gameplay se siente lento, no escalable para muchos matches.

**Solución:**
- La guía menciona SignalR Hub en `/hubs/match`
- Endpoints SignalR: ConnectToMatch, SetReady, PlayCard, EndTurn, Forfeit
- Mejor que HTTP para gameplay (sin delay)

---

### 3. 🟡 **End-to-End Flow no probado**

**Síntoma:** Compilación OK, pero nunca hicimos un match completo.

**Checklist necesario:**
- [ ] Login → token guardado
- [ ] Get decks → deberían aparecer
- [ ] Create/join match
- [ ] Set ready → game starts
- [ ] Play card → card appears
- [ ] End turn → opponent's turn
- [ ] Combat resolution automático
- [ ] Win condition → game ends

---

## Plan de Acción (Prioridad)

### FASE 1: Debug Token 401 (Hoy)

**Objetivo:** Que todos los endpoints protegidos devuelvan 200, no 401.

#### 1.1 Verificar respuesta de login
```
[ ] Ver si el token de la respuesta /auth/login es válido (check JWT decode)
[ ] Ver si el servidor está firmando correctamente el JWT
[ ] Ver fecha de expiración del token (¿ya expiró?)
```

#### 1.2 Agregar logging server-side
```
[ ] En server: agregar logging de Authorization header recibido
[ ] En server: agregar logging de JWT validation (accept/reject reason)
[ ] En server: verificar que middleware está correctamente configurado
```

#### 1.3 Test manual
```
[ ] Usar Postman/curl para reproducir:
     POST http://localhost/api/v1/auth/login
     { "email": "test@test.com", "password": "..." }
     
[ ] Copiar token de respuesta
[ ] GET http://localhost/api/v1/decks
     Authorization: Bearer <token>
     
[ ] Si 401: revisar server logs
[ ] Si 200: problema es en Unity, no en servidor
```

#### 1.4 Fix potencial
- Si servidor espera header diferente: cambiar HttpClientHelper
- Si token expira rápido: aumentar duración en servidor
- Si validación falla: revisar secret key en servidor

---

### FASE 2: Implementar SignalR (Esta semana)

**Objetivo:** Reemplazar HTTP polling con WebSocket real-time para gameplay.

#### 2.1 Crear MatchSignalRCoordinator
```csharp
Assets/Runtime/Networking/MatchSignalRCoordinator.cs

Métodos:
- ConnectAsync(matchId, playerId, token)
- SetReadyAsync(isReady)
- PlayCardAsync(cardKey, slotIndex)
- EndTurnAsync()
- ForfeitAsync()
- OnMatchSnapshot(callback)
- DisconnectAsync()

Usa: SignalR client NuGet package (Microsoft.AspNetCore.SignalR.Client)
```

#### 2.2 Integrar con GameplayPresenter3D
```
[ ] Detectar si SignalR disponible vs fallback HTTP
[ ] Llamar SignalR en lugar de HTTP para gameplay
[ ] Descartar MatchHttpCoordinator (o mantener como fallback)
```

#### 2.3 Manejar reconexión
```
[ ] Si pierde conexión → intentar reconectar
[ ] Si falla → fallback a HTTP polling
[ ] Guardar estado local mientras desconectado
```

---

### FASE 3: Integrar Deck Selection (Esta semana)

**Objetivo:** Permitir que jugador seleccione deck antes de matchmaking.

#### 3.1 Crear UI para deck selection
```
[ ] Mostrar lista de decks disponibles (GET /api/v1/decks)
[ ] Permitir crear nuevo deck
[ ] Permitir seleccionar deck actual
```

#### 3.2 Integrar con MatchmakingService
```csharp
// Actualmente:
var reservation = await _matchmakingService.QueueCasual(testDeckId);

// Debería permitir:
var selectedDeckId = userSelectedDeck.id;
var reservation = await _matchmakingService.QueueCasual(selectedDeckId);
```

#### 3.3 Pasar deck a coordinator
```
[ ] MatchHttpCoordinator debe saber qué deck está usando
[ ] Necesario para `/api/v1/matches/{id}/join` endpoint
```

---

### FASE 4: Testing End-to-End (Esta semana)

**Objetivo:** Hacer un match completo y verificar que todo funciona.

#### 4.1 Test en local
```
[ ] Iniciar servidor (backend)
[ ] Iniciar Unity client
[ ] Login → verfcar token guardado
[ ] Get cards → verificar catálogo carga
[ ] Get decks → verificar decks muestran
[ ] Create match → verificar matchId
[ ] Connect to match → verificar snapshot llega
[ ] Set ready → game inicia
[ ] Play cards → verificar aparecen en tablero
[ ] End turn → verificar turno cambia
[ ] Combat → verificar daño se resuelve
[ ] Win → verificar match termina
```

#### 4.2 Crear test script
```csharp
Assets/Runtime/Examples/FullGameFlowTest.cs

Pasos:
1. Auth complete flow
2. Matchmaking flow
3. Gameplay loop
4. Game end

Loguea cada paso para debugging
```

---

### FASE 5: Pulir UX (Siguiente semana)

**Objetivo:** Mejorar experiencia del usuario.

#### 5.1 Mensajes y feedback
```
[ ] Mostrar estado de conexión (conectando, conectado, error)
[ ] Mostrar mensajes de error claros (por qué falló login, etc)
[ ] Mostrar spinner mientras se carga match
[ ] Mostrar timeout warning si se pierde conexión
```

#### 5.2 Error handling mejorado
```
[ ] Catch 401 → mostrar "Token inválido, reconecta"
[ ] Catch timeout → mostrar "Servidor no responde"
[ ] Catch game errors → mostrar "Error en servidor, intenta de nuevo"
```

#### 5.3 Persistencia de estado
```
[ ] Si pierde conexión, mantener estado local
[ ] Al reconectar, sincronizar con servidor
[ ] Notificar usuario si hay desincronización
```

---

## Checklist de Implementación

### ✅ Estado Actual (COMPLETADO)

- [x] HTTP client centralizado (HttpClientHelper)
- [x] SecureTokenStorage para tokens
- [x] AuthService login/register
- [x] MatchHttpCoordinator (HTTP polling)
- [x] SnapshotConverter (API → DuelSnapshotDto)
- [x] BattleSnapshotBus (evento de snapshots)
- [x] GameplayPresenter3D (responde a snapshots)
- [x] Compilación C# (enums corregidos)
- [x] **NUEVA:** Logging mejorado en HttpClientHelper (401 diagnostics)
- [x] **NUEVA:** DiagnosticFlowTest.cs (prueba auth → cards → decks → profile)
- [x] **NUEVA:** MatchSignalRCoordinator.cs (WebSocket real-time)
- [x] **NUEVA:** MatchCoordinatorFactory.cs (factory con fallback)
- [x] **NUEVA:** GameplayPresenter3D updated (usa factory)
- [x] **NUEVA:** GameflowWithSignalRExample.cs (SignalR example)
- [x] **NUEVA:** DeckSelectionPanel.cs (UI para seleccionar deck)
- [x] **NUEVA:** FullGameFlowTest.cs (test end-to-end)

### 🔴 CRÍTICO - Hacer AHORA

- [x] **Debug 401 Unauthorized** ✅ IMPLEMENTADO
  - [x] Logueo completo de respuestas HTTP en HttpClientHelper
  - [x] DiagnosticFlowTest prueba cada step
  - [ ] Ejecutar DiagnosticFlowTest y revisar logs del servidor
- [ ] Verificar token en servidor (revisar logs de server)

### 🟡 IMPORTANTE - Esta Semana

- [x] Implementar SignalR WebSocket (FASE 2) ✅ COMPLETADO
  - [x] MatchSignalRCoordinator.cs
  - [x] MatchCoordinatorFactory.cs
  - [x] GameplayPresenter3D integración
  - [ ] Testear con FullGameFlowTest.cs
  
- [x] Integrar deck selection (FASE 3) ✅ IMPLEMENTADO
  - [x] DeckSelectionPanel.cs (UI para cargar y seleccionar decks)
  - [ ] Conectar con matchmaking flow
  
- [x] Testing end-to-end completo (FASE 4) ✅ IMPLEMENTADO
  - [x] FullGameFlowTest.cs (completo auth → game → win)
  - [ ] Ejecutar y verificar cada paso

### 🟢 BAJA - Próximas Semanas

- [ ] UI polishing (mensajes, spinners)
- [ ] Error handling mejorado
- [ ] Persistencia de estado
- [ ] Reconnection logic robusto
- [ ] Leaderboard display
- [ ] Match history UI
- [ ] Replay viewer

---

## Comandos Útiles

### Server (Backend)

```bash
# Iniciar servidor local
cd /Users/idhemax/proyects/_MINE/cardgameapi
dotnet run

# Logs en tiempo real
tail -f logs/app.log

# Test endpoint con token
curl -H "Authorization: Bearer <token>" \
     http://192.168.1.84:5000/api/v1/decks
```

### Unity Client

```bash
# Build para testing
Edit → Player Settings → Disable IPv6 (si tiene problemas)

# Console logs
Window → General → Console

# Debugging network
Wireshark o Fiddler para ver requests
```

---

## Próximos Pasos Inmediatos

1. **HOY:** Ejecutar FASE 1 completa (debug 401)
   - Revisar logs del servidor
   - Verificar que token es válido
   - Arreglar validación en servidor

2. **MAÑANA:** Completar FASE 2 (SignalR)
   - Crear MatchSignalRCoordinator
   - Integrar con GameplayPresenter3D

3. **ESTA SEMANA:** FASE 3 + 4 (Decks + Testing)
   - Deck selection UI
   - Test flow completo

---

## Referencias

- **GAME_INTEGRATION_GUIDE.md** - Especificación completa del API
- **MatchHttpCoordinator.cs** - Coordinator actual (HTTP)
- **SnapshotConverter.cs** - Convertidor de datos
- **HttpClientHelper.cs** - Cliente HTTP centralizado
- **GameplayPresenter3D.cs** - Renderizador 3D

---

**Última actualización:** 2026-04-20  
**Próxima revisión:** Después de completar FASE 1
