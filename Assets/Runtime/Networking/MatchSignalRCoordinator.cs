using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// SignalR-based match coordinator for real-time gameplay.
    /// Currently uses HTTP polling as fallback. To enable WebSocket:
    /// 1. Add NuGet: Microsoft.AspNetCore.SignalR.Client
    /// 2. Replace implementation with HubConnectionBuilder
    ///
    /// For now, delegates to MatchHttpCoordinator for compatibility.
    /// </summary>
    public sealed class MatchSignalRCoordinator : MonoBehaviour, IMatchCoordinator
    {
        [Header("Match")]
        public string matchId;
        public string playerId;

        private MatchHttpCoordinator _httpCoordinator;
        private bool _isConnected;

        public static MatchSignalRCoordinator Instance { get; private set; }
        public bool IsConnected => _isConnected;

        public event Action<MatchSnapshot> SnapshotChanged;
        public event Action<string> ErrorOccurred;
        public event Action ConnectionEstablished;
        public event Action ConnectionLost;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            _ = DisconnectAsync();
        }

        /// <summary>
        /// Initialize and connect to match (using HTTP polling as fallback).
        /// </summary>
        public async Task<bool> ConnectAsync(string matchId, string playerId)
        {
            this.matchId = matchId;
            this.playerId = playerId;

            try
            {
                GameLogger.Info("SignalR", $"Initializing coordinator for match {matchId}");

                // Create HTTP coordinator as fallback
                var coordinatorGo = new GameObject($"HttpCoordinator_{matchId}");
                _httpCoordinator = coordinatorGo.AddComponent<MatchHttpCoordinator>();

                // Subscribe to events
                _httpCoordinator.SnapshotChanged += OnHttpSnapshotChanged;
                _httpCoordinator.ErrorOccurred += OnHttpError;

                // Initialize with match details
                _httpCoordinator.Initialize(matchId, playerId, 0);

                _isConnected = true;

                GameLogger.Info("SignalR", "Coordinator initialized (using HTTP fallback)");
                ConnectionEstablished?.Invoke();

                return true;
            }
            catch (Exception ex)
            {
                GameLogger.Error("SignalR", $"Connection failed: {ex.Message}");
                ErrorOccurred?.Invoke(ex.Message);
                _isConnected = false;
                return false;
            }
        }

        /// <summary>
        /// Set ready status for game start.
        /// </summary>
        public async Task SetReadyAsync(bool isReady)
        {
            try
            {
                if (!_isConnected || _httpCoordinator == null)
                {
                    ErrorOccurred?.Invoke("Not connected to match");
                    return;
                }

                _httpCoordinator.RequestSetReady(isReady);
                await Task.Delay(100); // Brief delay for request to process
            }
            catch (Exception ex)
            {
                GameLogger.Error("SignalR", $"SetReady failed: {ex.Message}");
                ErrorOccurred?.Invoke($"SetReady error: {ex.Message}");
            }
        }

        /// <summary>
        /// Play a card from hand.
        /// </summary>
        public async Task PlayCardAsync(string runtimeHandKey, int slotIndex)
        {
            try
            {
                if (!_isConnected || _httpCoordinator == null)
                {
                    ErrorOccurred?.Invoke("Not connected to match");
                    return;
                }

                GameLogger.Info("SignalR", $"Playing card: {runtimeHandKey} to slot {slotIndex}");

                _httpCoordinator.RequestPlayCard(runtimeHandKey, slotIndex);
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                GameLogger.Error("SignalR", $"PlayCard failed: {ex.Message}");
                ErrorOccurred?.Invoke($"PlayCard error: {ex.Message}");
            }
        }

        /// <summary>
        /// End current turn.
        /// </summary>
        public async Task EndTurnAsync()
        {
            try
            {
                if (!_isConnected || _httpCoordinator == null)
                {
                    ErrorOccurred?.Invoke("Not connected to match");
                    return;
                }

                GameLogger.Info("SignalR", "Ending turn");

                _httpCoordinator.RequestEndTurn();
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                GameLogger.Error("SignalR", $"EndTurn failed: {ex.Message}");
                ErrorOccurred?.Invoke($"EndTurn error: {ex.Message}");
            }
        }

        /// <summary>
        /// Forfeit the match.
        /// </summary>
        public async Task ForfeitAsync()
        {
            try
            {
                if (!_isConnected || _httpCoordinator == null)
                {
                    ErrorOccurred?.Invoke("Not connected to match");
                    return;
                }

                GameLogger.Info("SignalR", "Forfeiting match");

                _httpCoordinator.RequestForfeit();
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                GameLogger.Error("SignalR", $"Forfeit failed: {ex.Message}");
                ErrorOccurred?.Invoke($"Forfeit error: {ex.Message}");
            }
        }

        /// <summary>
        /// Disconnect from match.
        /// </summary>
        public async Task DisconnectAsync()
        {
            try
            {
                if (_httpCoordinator != null)
                {
                    _httpCoordinator.StopPolling();
                    Destroy(_httpCoordinator.gameObject);
                    _httpCoordinator = null;
                    _isConnected = false;
                    GameLogger.Info("SignalR", "Disconnected from match");
                }
            }
            catch (Exception ex)
            {
                GameLogger.Error("SignalR", $"Disconnect error: {ex.Message}");
            }
        }

        private void OnHttpSnapshotChanged(MatchSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            GameLogger.Info("SignalR", $"Snapshot received: turn={snapshot.turnNumber}, phase={snapshot.phase}");

            // Convert and broadcast
            var duelSnapshot = SnapshotConverter.Convert(snapshot, 0);
            var json = JsonUtility.ToJson(duelSnapshot);
            BattleSnapshotBus.Publish(json);

            SnapshotChanged?.Invoke(snapshot);
        }

        private void OnHttpError(string message)
        {
            GameLogger.Error("SignalR", $"HTTP error: {message}");
            ErrorOccurred?.Invoke(message);
        }

        // IMatchCoordinator implementation
        void IMatchCoordinator.RequestPlayCard(string runtimeCardKey, int slotIndex)
        {
            _ = PlayCardAsync(runtimeCardKey, slotIndex);
        }

        void IMatchCoordinator.RequestEndTurn()
        {
            _ = EndTurnAsync();
        }

        void IMatchCoordinator.RequestSetReady(bool isReady)
        {
            _ = SetReadyAsync(isReady);
        }

        void IMatchCoordinator.RequestForfeit()
        {
            _ = ForfeitAsync();
        }
    }
}
