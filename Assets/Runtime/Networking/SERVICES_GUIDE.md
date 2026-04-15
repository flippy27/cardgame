# Card Game Services Integration Guide

## Overview

All network communication and API integration is centralized through `GameService` singleton. This simplifies management of auth, card catalog, and match history.

## Architecture

```
GameService (singleton)
├── CardGameApiClient (HTTP client)
├── AuthService (JWT tokens)
├── CardCatalogCache (card definitions + validation)
├── MatchHistoryService (match history tracking)
└── [User adds] MatchCompletionService (match result handling)
```

## Usage

### 1. Bootstrap on App Start

```csharp
// Attach GameService prefab to scene, or create dynamically
public class GameBootstrap : MonoBehaviour
{
    private async void Start()
    {
        var gameService = GameService.Instance;
        var ready = await gameService.Bootstrap();
        
        if (!ready)
        {
            Debug.LogError("Failed to bootstrap");
            return;
        }
        
        // Card catalog is now loaded
        var stats = gameService.GetCardStats();
        Debug.Log($"Loaded {stats.totalCards} cards");
    }
}
```

### 2. Player Login

```csharp
var success = await GameService.Instance.Login("player_id", "password");
if (success)
{
    // Player is authenticated, can now:
    // - Load match history
    // - Create/join matches
    // - Send actions to server
}
else
{
    Debug.LogError("Login failed");
}
```

### 3. Load Match History

```csharp
var page = await GameService.Instance.LoadMatchHistory(page: 1, pageSize: 20);

foreach (var match in page.matches)
{
    var result = match.result; // "win", "loss", "draw"
    var ratingDelta = match.ratingDelta;
    Debug.Log($"{match.opponentId}: {result} ({ratingDelta:+#;-#;0})");
}
```

### 4. Validate Deck

```csharp
var cardIds = new[] { "card1", "card2", "card3", ... };
var validation = GameService.Instance.ValidateDeck(cardIds);

if (!validation.IsValid)
{
    foreach (var error in validation.Errors)
    {
        Debug.LogError(error);
    }
}
```

### 5. Complete Match

```csharp
var completionService = new MatchCompletionService(GameService.Instance);

var result = await completionService.CompleteMatch(
    matchId: "match_123",
    playerId: "player_1",
    opponentId: "player_2",
    playerRatingBefore: 1600,
    opponentRatingBefore: 1580,
    playerWon: true,
    durationSeconds: 180
);

Debug.Log($"Rating: {result.ratingBefore} → {result.ratingAfter}");
```

### 6. Estimate Rating Change

```csharp
var completionService = new MatchCompletionService(GameService.Instance);
var (winDelta, lossDelta) = completionService.GetExpectedRatingChange(1600, 1580);

Debug.Log($"Win: +{winDelta}, Loss: {lossDelta}");
```

## Services Details

### CardGameApiClient

HTTP client for:
- `GET /api/cards` - all cards
- `GET /api/cards/{id}` - single card
- `GET /api/cards/search?q=...` - search by name
- `GET /api/cards/stats` - catalog statistics

### AuthService

Manages JWT tokens:
- Login: generate/store token
- Logout: clear token
- Refresh: auto-refresh if expiring soon
- GetAuthorizationHeader: bearer token for API calls

Token storage: PlayerPrefs (upgrade to secure storage in production)

### CardCatalogCache

Local cache of card definitions:
- LoadCatalog: async fetch from API
- GetCard(id): lookup by ID
- GetAll(): all cards
- ValidateDeck(cardIds): validate against catalog rules
- GetStats(): card count, abilities count

### MatchHistoryService

Paginated match history:
- FetchHistory(playerId, page, pageSize): fetch from /api/matches/history
- GetWinRateFromCache(playerId): win/loss count from cache
- ClearCache(): invalidate cache

### EloRatingService

Elo rating calculations (synced with API):
- K=32, floor=100, ceiling=4000
- CalculateEloChange(r1, r2, won): new ratings
- CalculateDelta(rating, opponentRating, won): delta only
- GetExpectedWinRate(r1, r2): probability 0-1

Formula: `expectedScore = 1 / (1 + 10^((opponent_rating - your_rating) / 400))`

### MatchCompletionService

Handle match result:
- CompleteMatch(...): calculate rating, send to server, invalidate cache
- GetExpectedRatingChange(rating, opponentRating): estimated deltas
- GetExpectedWinProbability(rating, opponentRating): win probability

## Configuration

### API Base URL

Default: `http://localhost:5000`

Change in GameService inspector:
```csharp
[SerializeField] private string apiBaseUrl = "http://your-api.com";
```

Or programmatically:
```csharp
var apiClient = new CardGameApiClient("https://production-api.com");
```

## Error Handling

All services throw `GameException` subclasses:

```csharp
try
{
    await gameService.Bootstrap();
}
catch (ValidationException ex)
{
    Debug.LogError($"Invalid input: {ex.Message}");
}
catch (InvalidGameStateException ex)
{
    Debug.LogError($"State error: {ex.Message}");
}
catch (CardNotFoundException ex)
{
    Debug.LogError($"Card not found: {ex.CardId}");
}
catch (InsufficientResourcesException ex)
{
    Debug.LogError($"Need {ex.Required} {ex.ResourceType}, have {ex.Available}");
}
catch (GameException ex)
{
    Debug.LogError($"Game error: {ex.Message}");
}
```

## Testing

Unit tests for all services in `Assets/Tests/Battle/`:
- CardGameApiClientTests
- AuthServiceTests
- MatchHistoryServiceTests
- CardCatalogCacheTests
- EloRatingServiceTests
- MatchCompletionServiceTests

Run in Unity Test Runner window: Window → General → Test Runner

## Known Limitations / TODO

- [ ] Token storage: use secure storage instead of PlayerPrefs
- [ ] HTTP mocking: add mock server support for offline testing
- [ ] Retry logic: exponential backoff for failed requests
- [ ] Timeout handling: configurable request timeouts
- [ ] Offline mode: cache all data and sync on reconnect
- [ ] Rate limiting: handle 429 responses from API
