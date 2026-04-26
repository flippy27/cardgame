using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.Networking
{
    public sealed class CardGameApiClient
    {
        public string BaseUrl { get; private set; }

        private readonly CardApiClient _cardApi;
        private readonly DeckApiClient _deckApi;
        private AuthService _authService;

        public CardGameApiClient(string baseUrl = null)
        {
            BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? ApiConfig.BaseUrl : baseUrl.TrimEnd('/');

            if (string.IsNullOrWhiteSpace(BaseUrl))
                throw new ValidationException("BaseUrl cannot be empty. Set via ApiConfig or constructor.");

            HttpClientHelper.TimeoutSeconds = ApiConfig.TimeoutSeconds;
            HttpClientHelper.MaxRetries = ApiConfig.MaxRetries;
            HttpClientHelper.RetryDelayMs = ApiConfig.RetryDelayMs;

            _cardApi = new CardApiClient(BaseUrl);
            _deckApi = new DeckApiClient(BaseUrl);
        }

        public void SetAuthService(AuthService authService)
        {
            _authService = authService;
        }

        public async Task<List<ServerCardDefinition>> FetchAllCards()
        {
            return await _cardApi.FetchAllCards();
        }

        public async Task<ServerCardDefinition> FetchCard(string cardId)
        {
            return await _cardApi.FetchCard(cardId);
        }

        public async Task<List<ServerCardDefinition>> SearchCards(string query)
        {
            return await _cardApi.SearchCards(query);
        }

        public async Task<List<DeckDto>> FetchDecksCatalogAsync()
        {
            return await _deckApi.FetchCatalog();
        }

        public async Task<List<DeckDto>> FetchPlayerDecksAsync(string playerId)
        {
            return await _deckApi.FetchPlayerDecks(playerId);
        }

        public async Task<DeckDto> GetPlayerDeckAsync(string playerId, string deckId)
        {
            return await _deckApi.GetPlayerDeck(playerId, deckId);
        }

        public async Task<DeckDto> UpsertDeckAsync(string playerId, string deckId, string displayName, List<string> cardIds)
        {
            return await _deckApi.UpsertDeck(playerId, deckId, displayName, cardIds);
        }

        public async Task DeleteDeckAsync(string playerId, string deckId)
        {
            // No bulk-delete endpoint; delete all cards is handled via upsert with empty list
            await UpsertDeckAsync(playerId, deckId, null, new List<string>());
        }

        public async Task AddCardToDeckAsync(string playerId, string deckId, string cardId)
        {
            await _deckApi.AddCardToDeck(playerId, deckId, cardId);
        }

        public async Task RemoveCardFromDeckAsync(string playerId, string deckId, string entryId)
        {
            await _deckApi.RemoveCardFromDeck(playerId, deckId, entryId);
        }

        public async Task<List<ServerCardDefinition>> FetchCardsByDeckAsync(string playerId, string deckId)
        {
            return await _cardApi.FetchCardsByDeck(playerId, deckId);
        }

        // Legacy overload kept for compatibility
        public async Task<bool> UpsertDeckLegacyAsync(string playerId, string deckId, string displayName, List<string> cardIds)
        {
            await _deckApi.UpsertDeck(playerId, deckId, displayName, cardIds);
            return true;
        }

        public async Task<bool> RecordMatchActionAsync(string matchId, object action)
        {
            var matchApi = new MatchApiClient(BaseUrl);
            await matchApi.RecordMatchAction(matchId, action);
            return true;
        }

        public async Task<bool> CompleteMatchAsync(string matchId, object result)
        {
            var matchApi = new MatchApiClient(BaseUrl);
            await matchApi.CompleteMatch(matchId, result);
            return true;
        }

        public async Task<bool> CompleteMatchAsync(string matchId, string playerId, string opponentId, bool playerWon, int durationSeconds)
        {
            var result = new
            {
                matchId,
                playerId,
                opponentId,
                won = playerWon,
                durationSeconds
            };
            return await CompleteMatchAsync(matchId, result);
        }

        public async Task<LeaderboardPageDto> GetLeaderboardAsync(int page = 1, int pageSize = 100)
        {
            var items = await _cardApi.FetchLeaderboard();
            var converted = new List<LeaderboardDto>();
            if (items != null)
            {
                foreach (var item in items)
                {
                    converted.Add(new LeaderboardDto
                    {
                        userId = item.userId,
                        username = item.username,
                        rank = item.rank,
                        wins = item.wins
                    });
                }
            }
            return new LeaderboardPageDto
            {
                page = page,
                pageSize = pageSize,
                totalCount = converted?.Count ?? 0,
                entries = converted
            };
        }

        public async Task<UserProfileDto> GetUserProfileAsync()
        {
            var userApi = new UserApiClient(BaseUrl);
            return await userApi.GetProfile(_authService?.CurrentPlayerId);
        }

        public async Task<UserProfileDto> GetUserProfileAsync(string playerId)
        {
            var userApi = new UserApiClient(BaseUrl);
            return await userApi.GetProfile(playerId);
        }

        public async Task<UserStatsDto> GetUserStatsAsync()
        {
            var userApi = new UserApiClient(BaseUrl);
            return await userApi.GetStats(_authService?.CurrentPlayerId);
        }

        public async Task<UserStatsDto> GetUserStatsAsync(string playerId)
        {
            var userApi = new UserApiClient(BaseUrl);
            return await userApi.GetStats(playerId);
        }

        public async Task PostActionsAsync(string matchId, System.Collections.IEnumerable actions)
        {
            var matchApi = new MatchApiClient(BaseUrl);
            foreach (var action in actions)
            {
                await matchApi.RecordMatchAction(matchId, action);
            }
        }

        public async Task PostActionsAsync(string matchId, System.Collections.IEnumerable actions, int sequence)
        {
            var matchApi = new MatchApiClient(BaseUrl);
            foreach (var action in actions)
            {
                await matchApi.RecordMatchAction(matchId, action);
            }
        }
    }

    public sealed class DeckApiClient
    {
        private readonly string _baseUrl;

        public DeckApiClient(string baseUrl = null)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? ApiConfig.BaseUrl : baseUrl.TrimEnd('/');
        }

        // GET /api/v1/decks/catalog
        public async Task<List<DeckDto>> FetchCatalog()
        {
            var response = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/decks/catalog");
            return ParseDeckList(response, "FetchCatalog");
        }

        // GET /api/v1/decks/{playerId}
        public async Task<List<DeckDto>> FetchPlayerDecks(string playerId)
        {
            var response = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/decks/{playerId}");
            return ParseDeckList(response, "FetchPlayerDecks");
        }

        // GET /api/v1/decks/{playerId}/{deckId}
        public async Task<DeckDto> GetPlayerDeck(string playerId, string deckId)
        {
            var response = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/decks/{playerId}/{deckId}");
            return string.IsNullOrWhiteSpace(response) ? null : JsonUtility.FromJson<DeckDto>(response);
        }

        // PUT /api/v1/decks — upsert (create or replace)
        public async Task<DeckDto> UpsertDeck(string playerId, string deckId, string displayName, List<string> cardIds)
        {
            var req = new DeckUpsertReqDto { playerId = playerId, deckId = deckId, displayName = displayName, cardIds = cardIds };
            var json = JsonUtility.ToJson(req);
            var response = await HttpClientHelper.PutAsync($"{_baseUrl}/api/v1/decks", json);
            return string.IsNullOrWhiteSpace(response) ? null : JsonUtility.FromJson<DeckDto>(response);
        }

        // POST /api/v1/decks/{playerId}/{deckId}/cards
        public async Task AddCardToDeck(string playerId, string deckId, string cardId)
        {
            var req = new AddCardReqDto { cardId = cardId };
            var json = JsonUtility.ToJson(req);
            await HttpClientHelper.PostAsync($"{_baseUrl}/api/v1/decks/{playerId}/{deckId}/cards", json);
        }

        // DELETE /api/v1/decks/{playerId}/{deckId}/cards/{entryId}
        public async Task RemoveCardFromDeck(string playerId, string deckId, string entryId)
        {
            await HttpClientHelper.DeleteAsync($"{_baseUrl}/api/v1/decks/{playerId}/{deckId}/cards/{entryId}");
        }

        private List<DeckDto> ParseDeckList(string response, string caller)
        {
            if (string.IsNullOrWhiteSpace(response) || response == "[]")
                return new List<DeckDto>();
            try
            {
                var w = JsonUtility.FromJson<DeckListWrapper>($"{{\"items\":{response}}}");
                return w?.items?.ToList() ?? new List<DeckDto>();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DeckApi] {caller} parse error: {ex.Message}");
                throw;
            }
        }

        [Serializable] private sealed class DeckListWrapper { public DeckDto[] items; }
        [Serializable] private sealed class DeckUpsertReqDto { public string playerId; public string deckId; public string displayName; public List<string> cardIds; }
        [Serializable] private sealed class AddCardReqDto { public string cardId; }
    }
}
