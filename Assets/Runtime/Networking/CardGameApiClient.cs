using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
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
        public string BaseUrl { get; private set; }
        public int TimeoutSeconds { get; set; }
        public int MaxRetries { get; set; }
        public int RetryDelayMs { get; set; }
        private readonly CircuitBreaker _circuitBreaker = new CircuitBreaker(failureThreshold: 5, resetTimeoutSeconds: 60);

        public CardGameApiClient(string baseUrl = null)
        {
            BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? ApiConfig.BaseUrl : baseUrl.TrimEnd('/');
            TimeoutSeconds = ApiConfig.TimeoutSeconds;
            MaxRetries = ApiConfig.MaxRetries;
            RetryDelayMs = ApiConfig.RetryDelayMs;

            if (string.IsNullOrWhiteSpace(BaseUrl))
                throw new ValidationException("BaseUrl cannot be empty. Set via ApiConfig or constructor.");
        }

        /// <summary>
        /// Descarga todas las cartas disponibles desde el API (con retry).
        /// </summary>
        public async Task<List<ServerCardDefinition>> FetchAllCards()
        {
            var response = await ExecuteWithRetry($"{BaseUrl}/api/v1/cards");
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

        /// <summary>
        /// Obtiene una carta específica por ID.
        /// </summary>
        public async Task<ServerCardDefinition> FetchCard(string cardId)
        {
            try
            {
                var json = await ExecuteWithRetry($"{BaseUrl}/api/v1/cards/{cardId}");
                return JsonUtility.FromJson<ServerCardDefinition>(json);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("404"))
            {
                throw new CardNotFoundException(cardId);
            }
        }

        /// <summary>
        /// Busca cartas por nombre o ID (con retry).
        /// </summary>
        public async Task<List<ServerCardDefinition>> SearchCards(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                throw new ValidationException("Search query must be at least 2 characters.");
            }

            var encodedQuery = UnityWebRequest.EscapeURL(query);
            var response = await ExecuteWithRetry($"{BaseUrl}/api/v1/cards/search?q={encodedQuery}");
            if (string.IsNullOrWhiteSpace(response) || response == "[]")
            {
                return new List<ServerCardDefinition>();
            }
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

        /// <summary>
        /// Obtiene estadísticas del catálogo (con retry).
        /// </summary>
        public async Task<CardStatsDto> FetchCardStats()
        {
            var json = await ExecuteWithRetry($"{BaseUrl}/api/v1/cards/stats");
            return JsonUtility.FromJson<CardStatsDto>(json);
        }

        /// <summary>
        /// Completa un match y registra resultado en servidor.
        /// </summary>
        public async Task<string> CompleteMatchAsync(string matchId, string playerId, string opponentId, bool playerWon, int durationSeconds)
        {
            var request = new MatchCompletionRequestDto
            {
                playerId = playerId,
                opponentId = opponentId,
                playerWon = playerWon,
                durationSeconds = durationSeconds
            };

            var json = JsonUtility.ToJson(request);
            return await ExecutePostWithRetry($"{BaseUrl}/api/v1/matches/{matchId}/complete", json);
        }

        /// <summary>
        /// Ejecuta request POST con retry exponencial y timeout.
        /// </summary>
        private async Task<string> ExecutePostWithRetry(string url, string jsonBody, int retryAttempt = 0)
        {
            try
            {
                using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
                request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonBody));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = TimeoutSeconds;

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Delay(10);
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    var error = $"HTTP {request.responseCode}: {request.error}";
                    if (retryAttempt < MaxRetries && request.responseCode >= 500)
                    {
                        var delay = (int)Math.Pow(2, retryAttempt) * 1000;
                        await Task.Delay(delay);
                        return await ExecutePostWithRetry(url, jsonBody, retryAttempt + 1);
                    }

                    throw new InvalidOperationException($"Request failed: {error}");
                }

                return request.downloadHandler.text;
            }
            catch (TaskCanceledException)
            {
                if (retryAttempt < MaxRetries)
                {
                    var delay = (int)Math.Pow(2, retryAttempt) * 1000;
                    await Task.Delay(delay);
                    return await ExecutePostWithRetry(url, jsonBody, retryAttempt + 1);
                }
                throw new InvalidOperationException($"Request timeout after {MaxRetries} retries");
            }
        }

        private async Task<string> ExecuteWithRetry(string url, int retryAttempt = 0)
        {
            if (_circuitBreaker.IsOpen())
            {
                throw new InvalidOperationException($"Circuit breaker OPEN. Server unavailable ({_circuitBreaker.FailureCount} failures)");
            }

            var startTime = UnityEngine.Time.realtimeSinceStartup;
            try
            {
                Debug.Log($"[API] GET {url} (attempt {retryAttempt + 1}/{MaxRetries + 1})");

                using var request = UnityWebRequest.Get(url);
                request.timeout = TimeoutSeconds;
                request.downloadHandler = new DownloadHandlerBuffer();

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Delay(10);
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    var statusCode = request.responseCode;
                    var errorMsg = request.error ?? "Unknown error";
                    var duration = (UnityEngine.Time.realtimeSinceStartup - startTime) * 1000f;
                    Debug.LogWarning($"[API] Request failed: HTTP {statusCode} - {errorMsg} ({duration:F2}ms)");
                    MetricsCollector.RecordRequest(url, "GET", (int)statusCode, duration);
                    _circuitBreaker.RecordFailure();

                    if (retryAttempt < MaxRetries && statusCode >= 500)
                    {
                        var delay = (int)Math.Pow(2, retryAttempt) * RetryDelayMs;
                        Debug.Log($"[API] Retrying in {delay}ms...");
                        await Task.Delay(delay);
                        return await ExecuteWithRetry(url, retryAttempt + 1);
                    }

                    if (statusCode == 404)
                        throw new InvalidOperationException($"Resource not found: {url}");

                    if (statusCode == 401 || statusCode == 403)
                        throw new UnauthorizedAccessException($"Unauthorized: {url}");

                    throw new InvalidOperationException($"HTTP {statusCode}: {errorMsg}");
                }

                var durationMs = (UnityEngine.Time.realtimeSinceStartup - startTime) * 1000f;
                Debug.Log($"[API] Success: {url} ({request.downloadHandler.text.Length} bytes) in {durationMs:F2}ms");
                _circuitBreaker.RecordSuccess();
                MetricsCollector.RecordRequest(url, "GET", (int)request.responseCode, durationMs);
                return request.downloadHandler.text;
            }
            catch (TaskCanceledException)
            {
                Debug.LogError($"[API] Timeout on {url}");
                _circuitBreaker.RecordFailure();
                if (retryAttempt < MaxRetries)
                {
                    var delay = (int)Math.Pow(2, retryAttempt) * RetryDelayMs;
                    await Task.Delay(delay);
                    return await ExecuteWithRetry(url, retryAttempt + 1);
                }
                throw new InvalidOperationException($"Request timeout after {MaxRetries} retries: {url}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[API] Unexpected error: {ex.GetType().Name} - {ex.Message}");
                throw;
            }
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

    [System.Serializable]
    public sealed class MatchCompletionRequestDto
    {
        public string playerId;
        public string opponentId;
        public bool playerWon;
        public int durationSeconds;
    }
}
