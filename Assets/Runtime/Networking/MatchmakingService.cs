using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Service for starting/joining matches via API.
    /// Handles matchmaking and initializes MatchHttpCoordinator.
    /// </summary>
    public sealed class MatchmakingService
    {
        private readonly MatchmakingApiClient _apiClient;
        private readonly AuthService _authService;

        public MatchmakingService(MatchmakingApiClient apiClient, AuthService authService)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        /// <summary>
        /// Queue for casual match.
        /// </summary>
        public async Task<MatchReservation> QueueCasual(string deckId)
        {
            if (!_authService.IsAuthenticated)
            {
                throw new InvalidOperationException("Not authenticated");
            }

            try
            {
                GameLogger.Info("Matchmaking", "Queuing for casual match");
                var response = await _apiClient.QueueForMatch(
                    _authService.CurrentPlayerId,
                    deckId,
                    MatchmakingApiClient.QueueMode.Casual,
                    1000 // default rating
                );

                return MapDto(response);
            }
            catch (Exception ex)
            {
                GameLogger.Error("Matchmaking", $"QueueCasual failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Create private match.
        /// </summary>
        public async Task<MatchReservation> CreatePrivate(string deckId, string matchName)
        {
            if (!_authService.IsAuthenticated)
            {
                throw new InvalidOperationException("Not authenticated");
            }

            try
            {
                GameLogger.Info("Matchmaking", $"Creating private match: {matchName}");
                var response = await _apiClient.CreatePrivateMatch(
                    _authService.CurrentPlayerId,
                    deckId,
                    matchName
                );

                return MapDto(response);
            }
            catch (Exception ex)
            {
                GameLogger.Error("Matchmaking", $"CreatePrivate failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Join private match by room code.
        /// </summary>
        public async Task<MatchReservation> JoinPrivate(string deckId, string roomCode)
        {
            if (!_authService.IsAuthenticated)
            {
                throw new InvalidOperationException("Not authenticated");
            }

            try
            {
                GameLogger.Info("Matchmaking", $"Joining private match: {roomCode}");
                var response = await _apiClient.JoinPrivateMatch(
                    _authService.CurrentPlayerId,
                    deckId,
                    roomCode
                );

                return MapDto(response);
            }
            catch (Exception ex)
            {
                GameLogger.Error("Matchmaking", $"JoinPrivate failed: {ex.Message}");
                throw;
            }
        }

        private MatchReservation MapDto(MatchmakingApiClient.MatchReservationDto dto)
        {
            return new MatchReservation
            {
                MatchId = dto.matchId,
                RoomCode = dto.roomCode,
                ReconnectToken = dto.reconnectToken,
                SeatIndex = dto.seatIndex,
                Mode = (MatchmakingApiClient.QueueMode)dto.mode,
                WaitingForOpponent = dto.waitingForOpponent,
                Status = dto.status
            };
        }
    }

    public sealed class MatchReservation
    {
        public string MatchId { get; set; }
        public string RoomCode { get; set; }
        public string ReconnectToken { get; set; }
        public int SeatIndex { get; set; }
        public MatchmakingApiClient.QueueMode Mode { get; set; }
        public bool WaitingForOpponent { get; set; }
        public string Status { get; set; }
    }
}
