using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Networking.ApiClients;
using Flippy.CardDuelMobile.UI;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// HTTP-based match coordinator (replaces Netcode).
    /// Polls API for game state updates and sends commands via HTTP.
    /// </summary>
    public sealed class MatchHttpCoordinator : MonoBehaviour, IMatchCoordinator
    {
        [Header("Match")]
        public string matchId;
        public string playerId;
        public int seatIndex;

        [Header("Polling")]
        public float pollIntervalSeconds = 0.5f;
        public float readyPollIntervalSeconds = 1f;

        private MatchplayApiClient _apiClient;
        private MatchSnapshot _currentSnapshot;
        private MatchSnapshot _lastProcessedSnapshot;
        private bool _isPolling;

        public static MatchHttpCoordinator Instance { get; private set; }
        public MatchSnapshot CurrentSnapshot => _currentSnapshot;

        public event Action<MatchSnapshot> SnapshotChanged;
        public event Action<string> ErrorOccurred;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            InitializeClient();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            StopPolling();
        }

        /// <summary>
        /// Initialize with match details and start polling (token from SecureTokenStorage).
        /// </summary>
        public void Initialize(string matchId, string playerId, int seatIndex)
        {
            this.matchId = matchId;
            this.playerId = playerId;
            this.seatIndex = seatIndex;

            GameLogger.Info("MatchHttp", $"Initialized: matchId={matchId}, playerId={playerId}, seatIndex={seatIndex}");
            InitializeClient();
            StartPolling();
        }

        private void InitializeClient()
        {
            var apiBaseUrl = ConfigManager.GetApiBaseUrl();
            _apiClient = new MatchplayApiClient(apiBaseUrl);
            GameLogger.Info("MatchHttp", $"API client initialized: {apiBaseUrl}");
        }

        /// <summary>
        /// Request ready status change.
        /// </summary>
        public async void RequestSetReady(bool isReady)
        {
            try
            {
                GameLogger.Info("MatchHttp", $"Requesting SetReady: {isReady}");
                var snapshot = await _apiClient.SetReady(matchId, playerId, isReady);
                ProcessSnapshot(snapshot);
                GameLogger.Info("MatchHttp", $"SetReady success");
            }
            catch (Exception ex)
            {
                GameLogger.Error("MatchHttp", $"SetReady failed: {ex.Message}");
                ErrorOccurred?.Invoke(ex.Message);
            }
        }

        /// <summary>
        /// Request play card.
        /// </summary>
        public async void RequestPlayCard(string runtimeHandKey, int slotIndex)
        {
            try
            {
                GameLogger.Info("MatchHttp", $"Requesting PlayCard: {runtimeHandKey} -> slot {slotIndex}");
                var snapshot = await _apiClient.PlayCard(matchId, playerId, runtimeHandKey, slotIndex);
                ProcessSnapshot(snapshot);
                GameLogger.Info("MatchHttp", $"PlayCard success");
            }
            catch (Exception ex)
            {
                GameLogger.Error("MatchHttp", $"PlayCard failed: {ex.Message}");
                ErrorOccurred?.Invoke(ex.Message);
            }
        }

        /// <summary>
        /// Request end turn.
        /// </summary>
        public async void RequestEndTurn()
        {
            try
            {
                GameLogger.Info("MatchHttp", $"Requesting EndTurn");
                var snapshot = await _apiClient.EndTurn(matchId, playerId);
                ProcessSnapshot(snapshot);
                GameLogger.Info("MatchHttp", $"EndTurn success");
            }
            catch (Exception ex)
            {
                GameLogger.Error("MatchHttp", $"EndTurn failed: {ex.Message}");
                ErrorOccurred?.Invoke(ex.Message);
            }
        }

        /// <summary>
        /// Request forfeit.
        /// </summary>
        public async void RequestForfeit()
        {
            try
            {
                GameLogger.Info("MatchHttp", $"Requesting Forfeit");
                var snapshot = await _apiClient.Forfeit(matchId, playerId);
                ProcessSnapshot(snapshot);
                GameLogger.Info("MatchHttp", $"Forfeit success");
            }
            catch (Exception ex)
            {
                GameLogger.Error("MatchHttp", $"Forfeit failed: {ex.Message}");
                ErrorOccurred?.Invoke(ex.Message);
            }
        }

        private void StartPolling()
        {
            _isPolling = true;
            PollSnapshotsAsync();
            GameLogger.Info("MatchHttp", "Started polling");
        }

        public void StopPolling()
        {
            _isPolling = false;
            GameLogger.Info("MatchHttp", "Stopped polling");
        }

        private async void PollSnapshotsAsync()
        {
            while (_isPolling)
            {
                await System.Threading.Tasks.Task.Delay((int)(pollIntervalSeconds * 1000));

                try
                {
                    if (string.IsNullOrEmpty(matchId) || string.IsNullOrEmpty(playerId))
                    {
                        continue;
                    }

                    var snapshot = await _apiClient.GetSnapshot(matchId, playerId);
                    ProcessSnapshot(snapshot);
                }
                catch (Exception ex)
                {
                    GameLogger.Error("MatchHttp", $"Poll failed: {ex.Message}");
                }
            }
        }

        private void ProcessSnapshot(MatchSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            _currentSnapshot = snapshot;

            // Detect changes and notify
            if (_lastProcessedSnapshot == null ||
                _lastProcessedSnapshot.phase != snapshot.phase ||
                _lastProcessedSnapshot.turnNumber != snapshot.turnNumber ||
                _lastProcessedSnapshot.activeSeatIndex != snapshot.activeSeatIndex)
            {
                GameLogger.Info("MatchHttp", $"Snapshot changed: phase={snapshot.phase}, turn={snapshot.turnNumber}, active={snapshot.activeSeatIndex}");
                SnapshotChanged?.Invoke(snapshot);
                _lastProcessedSnapshot = snapshot;

                // Convert to DuelSnapshotDto and broadcast to BattleSnapshotBus
                var duelSnapshot = SnapshotConverter.Convert(snapshot, seatIndex);
                var json = JsonUtility.ToJson(duelSnapshot);
                BattleSnapshotBus.Publish(json);
            }
        }
    }
}
