using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Networking;
using Flippy.CardDuelMobile.Networking;

namespace Flippy.CardDuelMobile.Tests
{
    /// <summary>
    /// Mock HTTP server para testing sin hacer requests reales.
    /// Simula respuestas del CardGameAPI.
    /// </summary>
    public sealed class FakeHttpServer
    {
        private Dictionary<string, FakeResponse> _responses;

        public FakeHttpServer()
        {
            _responses = new Dictionary<string, FakeResponse>(StringComparer.OrdinalIgnoreCase);
            SetupDefaultResponses();
        }

        /// <summary>
        /// Registra una respuesta mock para un endpoint específico.
        /// </summary>
        public void RegisterResponse(string method, string path, int statusCode, string responseBody)
        {
            var key = $"{method.ToUpper()} {path}";
            _responses[key] = new FakeResponse { StatusCode = statusCode, Body = responseBody };
        }

        /// <summary>
        /// Simula un request GET.
        /// </summary>
        public FakeResponse HandleGet(string path)
        {
            var key = $"GET {path}";
            return _responses.TryGetValue(key, out var response) ? response : new FakeResponse { StatusCode = 404, Body = "" };
        }

        /// <summary>
        /// Simula un request POST.
        /// </summary>
        public FakeResponse HandlePost(string path, string body)
        {
            var key = $"POST {path}";
            return _responses.TryGetValue(key, out var response) ? response : new FakeResponse { StatusCode = 404, Body = "" };
        }

        /// <summary>
        /// Simula un request PATCH.
        /// </summary>
        public FakeResponse HandlePatch(string path, string body)
        {
            var key = $"PATCH {path}";
            return _responses.TryGetValue(key, out var response) ? response : new FakeResponse { StatusCode = 404, Body = "" };
        }

        /// <summary>
        /// Simula un request DELETE.
        /// </summary>
        public FakeResponse HandleDelete(string path)
        {
            var key = $"DELETE {path}";
            return _responses.TryGetValue(key, out var response) ? response : new FakeResponse { StatusCode = 404, Body = "" };
        }

        /// <summary>
        /// Limpia todas las respuestas.
        /// </summary>
        public void Clear()
        {
            _responses.Clear();
            SetupDefaultResponses();
        }

        private void SetupDefaultResponses()
        {
            // Cartas
            RegisterResponse("GET", "/api/cards", 200, @"[
                {""CardId"":""card1"",""DisplayName"":""Fireball"",""ManaCost"":3,""Attack"":4,""Health"":0,""Armor"":0,""AllowedRow"":2,""DefaultAttackSelector"":0,""Abilities"":null},
                {""CardId"":""card2"",""DisplayName"":""Shield"",""ManaCost"":2,""Attack"":0,""Health"":5,""Armor"":5,""AllowedRow"":2,""DefaultAttackSelector"":0,""Abilities"":null}
            ]");

            RegisterResponse("GET", "/api/cards/card1", 200, @"{""CardId"":""card1"",""DisplayName"":""Fireball"",""ManaCost"":3,""Attack"":4,""Health"":0}");
            RegisterResponse("GET", "/api/cards/stats", 200, @"{""totalCards"":2,""manaCostAvg"":2.5,""attackAvg"":2.0,""healthAvg"":2.5,""cardsWithAbilities"":0}");

            // Autenticación
            RegisterResponse("POST", "/api/auth/login", 200, @"{""token"":""fake_jwt_token"",""refreshToken"":""fake_refresh"",""expiresIn"":3600}");
            RegisterResponse("POST", "/api/auth/refresh", 200, @"{""token"":""new_jwt_token"",""expiresIn"":3600}");
            RegisterResponse("POST", "/api/auth/logout", 200, "");

            // Perfil
            RegisterResponse("GET", "/api/users/player1/profile", 200, @"{""PlayerId"":""player1"",""DisplayName"":""TestPlayer"",""Level"":10,""Experience"":5000,""FactionPreference"":""Fire"",""CreatedAt"":1681234567,""LastSeenAt"":1681234567,""IsPremium"":false}");
            RegisterResponse("GET", "/api/users/player1/stats", 200, @"{""PlayerId"":""player1"",""CurrentRating"":1500,""HighestRating"":1800,""TotalMatches"":50,""Wins"":30,""Losses"":20,""WinRate"":0.6,""RankedRating"":1400,""RankedDivision"":3,""CasualRating"":1600}");

            // Logros
            RegisterResponse("GET", "/api/users/player1/achievements", 200, @"{""achievements"":[{""AchievementId"":""ach1"",""Title"":""First Win"",""Description"":""Win your first match"",""RewardExp"":100,""IsUnlocked"":true,""UnlockedAt"":1681234567}]}");

            // Mazos
            RegisterResponse("GET", "/api/users/player1/decks", 200, @"{""decks"":[{""DeckId"":""deck1"",""PlayerId"":""player1"",""Name"":""My Deck"",""Description"":""Test"",""CardIds"":[""card1"",""card2""],""CreatedAt"":1681234567,""UpdatedAt"":1681234567,""WinRate"":55,""Matches"":10}]}");
            RegisterResponse("GET", "/api/decks/deck1", 200, @"{""DeckId"":""deck1"",""PlayerId"":""player1"",""Name"":""My Deck"",""Description"":""Test"",""CardIds"":[""card1"",""card2""],""CreatedAt"":1681234567,""UpdatedAt"":1681234567,""WinRate"":55,""Matches"":10}");
            RegisterResponse("POST", "/api/decks", 200, @"{""DeckId"":""deck2"",""PlayerId"":""player1"",""Name"":""New Deck"",""Description"":"""",""CardIds"":[""card1"",""card2""],""CreatedAt"":1681234567,""UpdatedAt"":1681234567}");

            // Matchmaking
            RegisterResponse("POST", "/api/matchmaking/queue", 200, "");
            RegisterResponse("GET", "/api/matchmaking/status", 200, @"{""IsSearching"":true,""QueueMode"":0,""TimeInQueueSeconds"":30,""EstimatedWaitSeconds"":45,""PlayersInQueue"":150,""OpponentId"":null,""MatchId"":null}");
            RegisterResponse("DELETE", "/api/matchmaking/queue", 200, "");

            // Historial de matches
            RegisterResponse("GET", "/api/matches/history/player1", 200, @"{""Page"":1,""PageSize"":20,""TotalCount"":5,""Entries"":[{""MatchId"":""match1"",""OpponentId"":""player2"",""PlayerRatingBefore"":1500,""PlayerRatingAfter"":1532,""Duration"":600,""PlayedAt"":1681234567,""Result"":""Win""}]}");
        }

        /// <summary>
        /// Respuesta simulada.
        /// </summary>
        public sealed class FakeResponse
        {
            public int StatusCode { get; set; }
            public string Body { get; set; }
        }
    }

    /// <summary>
    /// Extensión para interceptar UnityWebRequest y usar fake server.
    /// (En un proyecto real, usaría dependency injection o mocking framework)
    /// </summary>
    public static class FakeHttpServerExtensions
    {
        private static FakeHttpServer _fakeServer;

        public static void InitializeFakeServer()
        {
            _fakeServer = new FakeHttpServer();
        }

        public static FakeHttpServer.FakeResponse GetFakeResponse(string method, string url, string body = "")
        {
            if (_fakeServer == null)
            {
                InitializeFakeServer();
            }

            // Extraer path del URL
            var uri = new System.Uri(url);
            var path = uri.PathAndQuery;

            return method.ToUpper() switch
            {
                "GET" => _fakeServer.HandleGet(path),
                "POST" => _fakeServer.HandlePost(path, body),
                "PATCH" => _fakeServer.HandlePatch(path, body),
                "DELETE" => _fakeServer.HandleDelete(path),
                _ => new FakeHttpServer.FakeResponse { StatusCode = 405, Body = "" }
            };
        }

        public static void SetupFakeServer(Action<FakeHttpServer> setup)
        {
            if (_fakeServer == null)
            {
                InitializeFakeServer();
            }
            setup(_fakeServer);
        }

        public static void ClearFakeServer()
        {
            _fakeServer?.Clear();
        }
    }
}
