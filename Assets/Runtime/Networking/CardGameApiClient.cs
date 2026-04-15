using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine.Networking;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Data;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Cliente HTTP para CardGameAPI.
    /// Maneja: catálogo de cartas, historial de matches, validación de deck.
    /// </summary>
    public sealed class CardGameApiClient
    {
        private readonly string _baseUrl;

        public CardGameApiClient(string baseUrl = "http://localhost:5000")
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ValidationException("BaseUrl cannot be empty.");

            _baseUrl = baseUrl.TrimEnd('/');
        }

        /// <summary>
        /// Descarga todas las cartas disponibles desde el API.
        /// </summary>
        public async Task<List<ServerCardDefinition>> FetchAllCards()
        {
            using var request = UnityWebRequest.Get($"{_baseUrl}/api/cards");
            request.downloadHandler = new DownloadHandlerBuffer();

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Delay(10);
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException(
                    $"Failed to fetch cards: {request.responseCode} - {request.error}");
            }

            var json = request.downloadHandler.text;
            var dtos = JsonUtility.FromJson<CardListDto>($"{{\"items\":{json}}}");
            return dtos.items.ToList();
        }

        /// <summary>
        /// Obtiene una carta específica por ID.
        /// </summary>
        public async Task<ServerCardDefinition> FetchCard(string cardId)
        {
            using var request = UnityWebRequest.Get($"{_baseUrl}/api/cards/{cardId}");
            request.downloadHandler = new DownloadHandlerBuffer();

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Delay(10);
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                if (request.responseCode == 404)
                {
                    throw new CardNotFoundException(cardId);
                }
                throw new InvalidOperationException(
                    $"Failed to fetch card '{cardId}': {request.responseCode} - {request.error}");
            }

            var json = request.downloadHandler.text;
            return JsonUtility.FromJson<ServerCardDefinition>(json);
        }

        /// <summary>
        /// Busca cartas por nombre o ID.
        /// </summary>
        public async Task<List<ServerCardDefinition>> SearchCards(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                throw new ValidationException("Search query must be at least 2 characters.");
            }

            var encodedQuery = UnityWebRequest.EscapeURL(query);
            using var request = UnityWebRequest.Get($"{_baseUrl}/api/cards/search?q={encodedQuery}");
            request.downloadHandler = new DownloadHandlerBuffer();

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Delay(10);
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException(
                    $"Failed to search cards: {request.responseCode} - {request.error}");
            }

            var json = request.downloadHandler.text;
            var dtos = JsonUtility.FromJson<CardListDto>($"{{\"items\":{json}}}");
            return dtos.items.ToList();
        }

        /// <summary>
        /// Obtiene estadísticas del catálogo.
        /// </summary>
        public async Task<CardStatsDto> FetchCardStats()
        {
            using var request = UnityWebRequest.Get($"{_baseUrl}/api/cards/stats");
            request.downloadHandler = new DownloadHandlerBuffer();

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Delay(10);
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException(
                    $"Failed to fetch card stats: {request.responseCode} - {request.error}");
            }

            var json = request.downloadHandler.text;
            return JsonUtility.FromJson<CardStatsDto>(json);
        }

        // DTO wrappers for JSON serialization
        [System.Serializable]
        private sealed class CardListDto
        {
            public ServerCardDefinition[] items;
        }
    }

    /// <summary>
    /// DTO para estadísticas del catálogo (server response).
    /// </summary>
    [System.Serializable]
    public sealed class CardStatsDto
    {
        public int totalCards;
        public float manaCostAvg;
        public float attackAvg;
        public float healthAvg;
        public int cardsWithAbilities;
    }

    /// <summary>
    /// Definición de carta como viene del servidor.
    /// Espejo de CardDuel.ServerApi.Game.ServerCardDefinition.
    /// </summary>
    [System.Serializable]
    public sealed class ServerCardDefinition
    {
        public string CardId;
        public string DisplayName;
        public int ManaCost;
        public int Attack;
        public int Health;
        public int Armor;
        public int AllowedRow; // enum ordinal: 0=FrontOnly, 1=BackOnly, 2=Flexible
        public int DefaultAttackSelector; // enum ordinal
        public ServerAbilityDefinition[] Abilities;
    }

    [System.Serializable]
    public sealed class ServerAbilityDefinition
    {
        public string AbilityId;
        public int Trigger; // enum ordinal
        public int Selector; // enum ordinal
        public ServerEffectDefinition[] Effects;
    }

    [System.Serializable]
    public sealed class ServerEffectDefinition
    {
        public int Kind; // enum ordinal
        public int Amount;
    }
}
