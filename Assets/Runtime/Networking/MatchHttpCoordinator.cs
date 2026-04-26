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
        private string _lastPublishedSnapshotJson;
        private bool _isPolling;
        private bool _rulesSyncInFlight;

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
                HandleGameplayActionError("SetReady", ex);
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
                HandleGameplayActionError("PlayCard", ex);
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
                HandleGameplayActionError("EndTurn", ex);
            }
        }

        /// <summary>
        /// Request live board-card destruction. The server owns validation and final state.
        /// </summary>
        public async void RequestDestroyCard(string runtimeCardId)
        {
            try
            {
                GameLogger.Info("MatchHttp", $"Requesting DestroyCard: {runtimeCardId}");
                var snapshot = await _apiClient.DestroyCard(matchId, playerId, runtimeCardId);
                ProcessSnapshot(snapshot);
                GameLogger.Info("MatchHttp", "DestroyCard success");
            }
            catch (Exception ex)
            {
                GameLogger.Error("MatchHttp", $"DestroyCard failed: {ex.Message}");
                HandleGameplayActionError("DestroyCard", ex);
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
                HandleGameplayActionError("Forfeit", ex);
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
            _rulesSyncInFlight = false;
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
            SyncRulesState(snapshot);
            SyncMatchIdentity(snapshot);
            var resolvedSeatIndex = SnapshotConverter.ResolveLocalSeatIndex(snapshot, seatIndex);
            if (resolvedSeatIndex is 0 or 1)
            {
                seatIndex = resolvedSeatIndex;
            }
            var snapshotJson = JsonUtility.ToJson(snapshot);
            var hasMeaningfulSnapshotChange = !string.Equals(_lastPublishedSnapshotJson, snapshotJson, StringComparison.Ordinal);

            // Detect changes and notify
            if (_lastProcessedSnapshot == null ||
                _lastProcessedSnapshot.phase != snapshot.phase ||
                _lastProcessedSnapshot.turnNumber != snapshot.turnNumber ||
                _lastProcessedSnapshot.activeSeatIndex != snapshot.activeSeatIndex ||
                !string.Equals(_lastProcessedSnapshot.activePlayerId, snapshot.activePlayerId, StringComparison.Ordinal) ||
                _lastProcessedSnapshot.isLocalPlayersTurn != snapshot.isLocalPlayersTurn)
            {
                GameLogger.Info("MatchHttp", $"Snapshot changed: phase={snapshot.phase}, turn={snapshot.turnNumber}, active={snapshot.activeSeatIndex}");
                SnapshotChanged?.Invoke(snapshot);
                _lastProcessedSnapshot = snapshot;
            }

            if (!hasMeaningfulSnapshotChange)
            {
                return;
            }

            _lastPublishedSnapshotJson = snapshotJson;

            // Convert to DuelSnapshotDto and broadcast to BattleSnapshotBus
            var duelSnapshot = SnapshotConverter.Convert(snapshot, seatIndex);
            var json = JsonUtility.ToJson(duelSnapshot);
            BattleSnapshotBus.Publish(json);

            if (snapshot.rules == null && !_rulesSyncInFlight)
            {
                _ = TryFetchPersistedRulesAsync(snapshot.matchId);
            }
        }

        private void SyncRulesState(MatchSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            var resolvedRulesetId = !string.IsNullOrWhiteSpace(snapshot.rulesetId)
                ? snapshot.rulesetId
                : snapshot.rules?.rulesetId;

            if (!string.IsNullOrWhiteSpace(resolvedRulesetId) || snapshot.rules != null)
            {
                GamePlayStateManager.Instance?.SetMatchRules(resolvedRulesetId, snapshot.rules);
            }
        }

        private void SyncMatchIdentity(MatchSnapshot snapshot)
        {
            if (snapshot == null || GamePlayStateManager.Instance == null)
            {
                return;
            }

            var resolvedSeatIndex = SnapshotConverter.ResolveLocalSeatIndex(snapshot, seatIndex);
            var localPlayerId = SnapshotConverter.ResolveLocalPlayerId(snapshot, resolvedSeatIndex) ?? playerId;
            var remotePlayerId = SnapshotConverter.ResolveRemotePlayerId(snapshot, resolvedSeatIndex);
            GamePlayStateManager.Instance.SetMatchInfo(snapshot.matchId, localPlayerId, remotePlayerId);
        }

        private async System.Threading.Tasks.Task TryFetchPersistedRulesAsync(string snapshotMatchId)
        {
            if (_rulesSyncInFlight || string.IsNullOrWhiteSpace(snapshotMatchId))
            {
                return;
            }

            _rulesSyncInFlight = true;
            try
            {
                var rules = await _apiClient.GetMatchRules(snapshotMatchId, playerId);
                if (rules == null || _currentSnapshot == null || !string.Equals(_currentSnapshot.matchId, snapshotMatchId, StringComparison.Ordinal))
                {
                    return;
                }

                _currentSnapshot.rules = rules;
                if (string.IsNullOrWhiteSpace(_currentSnapshot.rulesetId))
                {
                    _currentSnapshot.rulesetId = rules.rulesetId;
                }

                SyncRulesState(_currentSnapshot);
                var duelSnapshot = SnapshotConverter.Convert(_currentSnapshot, seatIndex);
                BattleSnapshotBus.Publish(JsonUtility.ToJson(duelSnapshot));
                SnapshotChanged?.Invoke(_currentSnapshot);
            }
            catch (Exception ex)
            {
                GameLogger.Warning("MatchHttp", $"Could not fetch persisted match rules: {ex.Message}");
            }
            finally
            {
                _rulesSyncInFlight = false;
            }
        }

        private void HandleGameplayActionError(string actionName, Exception ex)
        {
            if (GameplayActionErrorParser.ShouldRefreshSnapshot(ex))
            {
                _ = TryRefreshSnapshotAfterGameplayErrorAsync(actionName);
            }

            ErrorOccurred?.Invoke(GameplayActionErrorParser.ToUserMessage(ex, actionName));
        }

        private async System.Threading.Tasks.Task TryRefreshSnapshotAfterGameplayErrorAsync(string actionName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(matchId) || string.IsNullOrWhiteSpace(playerId))
                {
                    return;
                }

                var snapshot = await _apiClient.GetSnapshot(matchId, playerId);
                ProcessSnapshot(snapshot);
                GameLogger.Info("MatchHttp", $"{actionName}: snapshot refreshed after gameplay error.");
            }
            catch (Exception refreshException)
            {
                GameLogger.Warning("MatchHttp", $"{actionName}: could not refresh snapshot after gameplay error: {refreshException.Message}");
            }
        }
    }
}
