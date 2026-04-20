using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking.ApiClients
{
    public sealed class CardApiClient
    {
        private readonly string _baseUrl;

        public CardApiClient(string baseUrl = null)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? ApiConfig.BaseUrl : baseUrl.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(_baseUrl))
                throw new ValidationException("BaseUrl cannot be empty. Set via ApiConfig or constructor.");
        }

        public async Task<List<ServerCardDefinition>> FetchAllCards()
        {
            var response = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/cards");
            if (string.IsNullOrWhiteSpace(response) || response == "[]")
            {
                Debug.LogWarning("[API] Empty card catalog from server");
                return new List<ServerCardDefinition>();
            }
            try
            {
                var dtos = JsonUtility.FromJson<CardListDto>($"{{\"items\":{response}}}");
                return dtos?.items?.ToList() ?? new List<ServerCardDefinition>();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[API] Parse error: {ex.Message}. Raw: {response.Substring(0, Math.Min(100, response.Length))}");
                throw new InvalidOperationException($"Failed to parse cards: {ex.Message}", ex);
            }
        }

        public async Task<ServerCardDefinition> FetchCard(string cardId)
        {
            try
            {
                var json = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/cards/{cardId}");
                return JsonUtility.FromJson<ServerCardDefinition>(json);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("404"))
            {
                throw new CardNotFoundException(cardId);
            }
        }

        public async Task<List<ServerCardDefinition>> SearchCards(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                throw new ValidationException("Search query must be at least 2 characters.");

            var encodedQuery = UnityWebRequest.EscapeURL(query);
            var response = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/cards/search?q={encodedQuery}");
            if (string.IsNullOrWhiteSpace(response) || response == "[]")
                return new List<ServerCardDefinition>();

            try
            {
                var dtos = JsonUtility.FromJson<CardListDto>($"{{\"items\":{response}}}");
                return dtos?.items?.ToList() ?? new List<ServerCardDefinition>();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[API] Search parse error: {ex.Message}");
                throw;
            }
        }

        public async Task<List<DeckDto>> FetchDecksCatalog()
        {
            var response = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/decks/catalog");
            try
            {
                var dtos = JsonUtility.FromJson<DeckListDto>($"{{\"items\":{response}}}");
                return dtos?.items?.ToList() ?? new List<DeckDto>();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[API] Decks catalog parse error: {ex.Message}");
                throw;
            }
        }

        public async Task<List<DeckDto>> FetchPlayerDecks(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                throw new ValidationException("PlayerId cannot be empty");

            var response = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/decks/{playerId}");
            if (string.IsNullOrWhiteSpace(response) || response == "[]")
                return new List<DeckDto>();

            try
            {
                var dtos = JsonUtility.FromJson<DeckListDto>($"{{\"items\":{response}}}");
                return dtos?.items?.ToList() ?? new List<DeckDto>();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[API] Player decks parse error: {ex.Message}");
                throw;
            }
        }

        public async Task<DeckDto> UpsertDeck(string playerId, DeckDto deck)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                throw new ValidationException("PlayerId cannot be empty");

            var request = new DeckUpsertRequestDto
            {
                playerId = playerId,
                deckId = deck.deckId,
                displayName = string.IsNullOrWhiteSpace(deck.displayName) ? deck.deckName : deck.displayName,
                cardIds = deck.cardIds ?? new List<string>()
            };

            var json = JsonUtility.ToJson(request);
            await HttpClientHelper.PutAsync($"{_baseUrl}/api/v1/decks", json);
            return deck;
        }

        public async Task<List<LeaderboardDto>> FetchLeaderboard()
        {
            var response = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/users/leaderboard");
            try
            {
                var page = JsonUtility.FromJson<LeaderboardPageDto>(response);
                return page?.entries ?? new List<LeaderboardDto>();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[API] Leaderboard parse error: {ex.Message}");
                throw;
            }
        }

        [System.Serializable]
        private sealed class CardListDto
        {
            public ServerCardDefinition[] items;
        }

        [System.Serializable]
        private sealed class DeckListDto
        {
            public DeckDto[] items;
        }

    }
}
