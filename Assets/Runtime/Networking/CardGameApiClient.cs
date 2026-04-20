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

        public async Task<DeckDto> UpsertDeckAsync(string playerId, DeckDto deck)
        {
            return await _deckApi.UpsertDeck(playerId, deck);
        }

        public async Task<bool> UpsertDeckAsync(string playerId, string deckId, string displayName, List<string> cardIds)
        {
            var deck = new DeckDto
            {
                deckId = deckId,
                playerId = playerId,
                displayName = displayName,
                deckName = displayName,
                cardIds = cardIds
            };
            await _deckApi.UpsertDeck(playerId, deck);
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

        public async Task<List<DeckDto>> FetchCatalog()
        {
            var cardApi = new CardApiClient(_baseUrl);
            return await cardApi.FetchDecksCatalog();
        }

        public async Task<List<DeckDto>> FetchPlayerDecks(string playerId)
        {
            var cardApi = new CardApiClient(_baseUrl);
            return await cardApi.FetchPlayerDecks(playerId);
        }

        public async Task<DeckDto> UpsertDeck(string playerId, DeckDto deck)
        {
            var cardApi = new CardApiClient(_baseUrl);
            return await cardApi.UpsertDeck(playerId, deck);
        }
    }
}
