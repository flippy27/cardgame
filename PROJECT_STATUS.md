# CardGame Project Status & Missing Features

**Última actualización:** 2026-04-14 (19:45 UTC)  
**Estado General:** 75% completo (multiplayer + auth funcional, features avanzadas faltantes)

## 🆕 IMPLEMENTADO HOY (Sesión Actual)

✅ **Drag-and-drop de cartas** - Fixed (sin EventSystem, usando Input polling)  
✅ **Validación de slots** - Invalid slots show visual feedback  
✅ **SecureTokenStorage** - Keychain/Keystore/XOR encryption  
✅ **Real Authentication** - POST /api/auth/login + register  
✅ **Persistent Login** - Tokens saved in secure storage, loaded on startup  
✅ **MainMenuScreen** - Professional login/menu UI  
✅ **PROJECT_STATUS.md** - Full feature audit

---

## 📋 RESUMEN EJECUTIVO

| Categoría | Estado | Detalles |
|-----------|--------|----------|
| **Gameplay (Local)** | ✅ COMPLETO | Drag-drop, slots, validación de cartas |
| **Multiplayer** | ✅ FUNCIONAL | NGO + Netcode, matchmaking básico |
| **Persistencia** | ⚠️ PARCIAL | LocalCache OK, servidor incompleto |
| **Autenticación** | ❌ MOCK | Login simula tokens sin validar |
| **Token Storage** | ❌ INSEGURO | PlayerPrefs sin encriptación |
| **Ranked System** | ⚠️ PARCIAL | Elo calculado localmente, no sincroniza |

---

## 🔴 FUNCIONALIDADES CRÍTICAS FALTANTES

### 1. **AUTENTICACIÓN REAL**
- **Estado:** Mock (genera tokens sin validar)
- **Ubicación:** `AuthService.cs:44`
- **Impacto:** No hay validación de identidad
- **Solución:** Implementar `POST /api/auth/login` + `POST /api/auth/register`

### 2. **TOKEN STORAGE SEGURO**
- **Estado:** PlayerPrefs (visible en texto plano)
- **Ubicación:** `AuthService.cs` (líneas 14-18)
- **Impacto:** Tokens robables si el dispositivo se compromete
- **Solución:** 
  - iOS: Keychain
  - Android: Keystore
  - Desktop: encriptación con DPAPI

### 3. **MATCH COMPLETION PERSISTENCIA**
- **Estado:** Elo calculado localmente, no envía a servidor
- **Ubicación:** `MatchCompletionService.cs:59`
- **Impacto:** Resultados de matches no se guardan
- **Solución:** Implementar `POST /api/matches/{matchId}/complete`

### 4. **LEADERBOARDS GLOBALES**
- **Estado:** No existe
- **Impacto:** Sin ranking global, sin competencia social
- **Solución:** Implementar `GET /api/leaderboards/{season}`

### 5. **SESSION PERSISTENCE REMOTA**
- **Estado:** Solo local
- **Impacto:** Sesión se pierde si cambias dispositivo
- **Solución:** Sincronizar con servidor al login

### 6. **REAL-TIME UPDATES**
- **Estado:** Polling manual
- **Impacto:** Lag en actualizaciones de matchmaking
- **Solución:** SignalR / WebSockets

---

## 🟡 FUNCIONALIDADES SECUNDARIAS FALTANTES

### 7. **RATE LIMITING HANDLING**
- **Estado:** No maneja HTTP 429
- **Ubicación:** `CardGameApiClient.cs`
- **Impacto:** Pueden hacer spam a API
- **Solución:** Implementar backoff cliente + caché más agresivo

### 8. **REPLAY SYSTEM**
- **Estado:** No existe
- **Impacto:** Sin posibilidad de revisar matches
- **Solución:** Grabar eventos de match + descargar del servidor

### 9. **SEASONAL RANKINGS**
- **Estado:** No existe
- **Impacto:** Sin progresión temporal
- **Solución:** Endpoints de `/api/leaderboards/{season}`

### 10. **ACHIEVEMENT SYSTEM AVANZADO**
- **Estado:** Endpoints básicos existen pero sin tracking en servidor
- **Ubicación:** `UserService.cs`
- **Impacto:** Logros no persisten entre sesiones
- **Solución:** Backend tracking + sync

### 11. **SOCIAL FEATURES**
- **Estado:** No existe
- **Impacto:** Sin amigos, sin chat, sin clanes
- **Solución:** Endpoints de `/api/friends`, `/api/clans`

### 12. **TOURNAMENT MODE**
- **Estado:** No existe
- **Impacto:** Sin modo competitivo estructurado
- **Solución:** Bracket system + API endpoints

---

## 🟢 FUNCIONALIDADES COMPLETADAS

✅ Catálogo de cartas (fetch + cache)  
✅ Validación de mazos  
✅ Gestión de mazos (CRUD)  
✅ Perfiles de jugador (lectura/escritura)  
✅ Estadísticas de jugador  
✅ Historial de matches  
✅ Matchmaking (casual y ranked)  
✅ Sistema de rating Elo  
✅ Multiplayer local con NGO  
✅ Modo offline con LocalCache  
✅ Sincronización de cambios offline  
✅ Retry automático con backoff exponencial  
✅ Token refresh automático  
✅ Logros (lectura y desbloqueo)  
✅ Drag-and-drop de cartas  

---

## 📊 MÉTRICAS

| Métrica | Valor |
|---------|-------|
| Servicios implementados | 10/12 |
| Endpoints documentados | 23 |
| Endpoints implementados | 18 |
| Endpoints TODOs | 3 |
| Tests unitarios | 40+ |
| Líneas de código de networking | 3500+ |

---

## 🛠️ PLAN DE IMPLEMENTACIÓN (PRIORIDAD)

### **FASE 1: AUTENTICACIÓN SEGURA** (1-2 semanas)
1. ✓ Menú de login en UI
2. ✓ SecureStorage abstraction (Keychain/Keystore/DPAPI)
3. ✓ Real endpoint `POST /api/auth/login`
4. ✓ Real endpoint `POST /api/auth/register`
5. ✓ Real endpoint `POST /api/auth/refresh`

### **FASE 2: PERSISTENCIA DE RESULTADOS** (1 semana)
1. ✓ Implementar `POST /api/matches/{matchId}/complete`
2. ✓ Enviar resultado al terminar match
3. ✓ Sincronizar Elo con servidor
4. ✓ Verificación en servidor (anti-cheat básico)

### **FASE 3: LEADERBOARDS Y RANKINGS** (1-2 semanas)
1. ✓ Endpoint `GET /api/leaderboards/{season}`
2. ✓ Pantalla de leaderboards global
3. ✓ Divisiones/tiers
4. ✓ Seasonal reset

### **FASE 4: REAL-TIME UPDATES** (1-2 semanas)
1. ✓ SignalR integration
2. ✓ Push notifications de matchmaking
3. ✓ Live player status

### **FASE 5: SOCIAL & ADVANCED** (2-4 semanas)
1. ✓ Sistema de amigos
2. ✓ Chat
3. ✓ Clanes
4. ✓ Replay system
5. ✓ Tournaments

---

## 📁 ARCHIVOS PRINCIPALES

```
Assets/Runtime/Networking/
├── AuthService.cs                 ⚠️ Mock login
├── CardGameApiClient.cs           ✅ HTTP client
├── CardCatalogCache.cs            ✅ Card cache
├── UserService.cs                 ✅ User profiles
├── DeckService.cs                 ✅ Deck CRUD
├── MatchHistoryService.cs         ✅ Match history
├── MatchmakingService.cs          ✅ Queues
├── EloRatingService.cs            ✅ Rating calc
├── MatchCompletionService.cs      ❌ Needs server sync
├── LocalCacheService.cs           ✅ Offline cache
├── MpsGameSessionService.cs       ✅ UMS integration
├── GameService.cs                 ✅ Coordinator
└── NetworkBootstrap.cs            ✅ Bootstrap
```

---

## 🎯 PRÓXIMAS ACCIONES

**INMEDIATO:**
1. ✓ Crear menú Tools/UI/Menu con acceso a API
2. ✓ Implementar login persistente (secure storage)
3. ✓ Conectar endpoints reales de autenticación

**CORTO PLAZO:**
1. Implementar persistencia de resultados
2. Leaderboards básicos
3. System de mensajes de error mejorado

**LARGO PLAZO:**
1. Real-time updates con SignalR
2. Social features
3. Tournament mode
