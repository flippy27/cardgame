# CardGame Code Improvements

## Critical (Fix Now)

| Issue | Impact | Fix |
|-------|--------|-----|
| **Circuit Breaker Missing** | Server down = cascading failures | Add after 5 failed requests, wait 30s before retry. Track failure count per endpoint. |
| **No Request Validation** | Bad data sent upstream | Validate all DTOs before send. Min/max lengths, enum bounds, null checks. |
| **Hard Timeouts** | Mobile network = failures | Use exponential backoff: 500ms→1s→2s→4s. Increase timeout for slow networks. |
| **No Offline Support** | Can't play offline | Cache catalog locally (PlayerPrefs). Queue actions, sync on reconnect. |
| **Unhandled Exceptions** | Crashes w/o reason | Wrap all async ops in try-catch at service level. Log full stack. |

## High Priority (This Week)

| Area | Current | Target |
|------|---------|--------|
| **Logging** | Scattered Debug.Log | Centralized ILogger. Levels: Debug/Info/Warn/Error. File + console. |
| **Error Codes** | Generic exceptions | Typed errors: `ApiException`, `AuthException`, `NetworkException`. Codes: AUTH_001, NET_404, etc. |
| **Request Signing** | None (CORS only) | Add request signature (HMAC-SHA256). Prevent replay attacks. |
| **Config Management** | ApiConfig class | Add `appsettings.json` equivalent. Load from Resources/Addressables. |
| **Health Checks** | None | Periodic ping to `/health`. If fails 3x, show "Connection Lost" UI. |
| **Token Refresh** | 1hr expiry | Auto-refresh 5min before expiry. Silent retry on 401. |

## Medium Priority (Modular + Robust)

**Dependency Injection**
- Current: `new CardGameApiClient()` scattered
- Target: DI container (Zenject or custom). Register at bootstrap.

**Service Layer**
- Current: Controllers → API client directly
- Target: `IGameService`, `IUserService`, `IMatchService`. Mock-friendly.

**Event System**
- Current: Direct callbacks
- Target: Observer pattern. Dispatch: `OnConnected`, `OnDisconnected`, `OnError`, `OnAuthFailed`.

**Retry Strategy**
- Current: Exponential backoff only
- Target: Add jitter. Fail fast on 4xx. Circuit breaker on 5xx.

**State Management**
- Current: PlayerPrefs scattered
- Target: Single `GameState` class. Serialize/deserialize all data. Immutable updates.

**Testing**
- Current: None
- Target: Unit tests for `ApiClient`, `AuthService`. Mock `UnityWebRequest`.

## Architectural (Long-term)

**Serialization**
- Current: `JsonUtility` (limited)
- Target: Newtonsoft.Json (polymorphism, custom converters). Or Odin for more control.

**Networking**
- Current: REST only
- Target: WebSocket fallback. SignalR for real-time updates.

**Caching**
- Current: UserService 5min cache
- Target: `ICache<T>` interface. Memory + disk. TTL support. Invalidation on auth.

**Security**
- Current: Bearer token only
- Target: Certificate pinning (production). Encrypt sensitive data at rest.

**Performance**
- Current: Full card catalog on startup
- Target: Lazy load. Pagination. Client-side filtering (sort/search in cache).

**Observability**
- Current: Logs only
- Target: Metrics (request count, latency histograms). Sentry/Datadog integration.

---

## Summary

**Robustness** = +circuit breaker, +validation, +logging, +offline support, +error codes  
**Modularity** = DI, service layer, event system, immutable state  
**Flexibility** = config abstraction, caching strategy, transport abstraction  
**Indestructible** = retry strategy, graceful degradation, health checks, observability

Priority: Fix circuit breaker + validation first. Then DI + services. Then caching + offline.
