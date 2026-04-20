using System;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.Networking
{
    public sealed class MatchCheckpointService : MonoBehaviour
    {
        [SerializeField] private float checkpointIntervalSeconds = 60f;

        private string _matchId = string.Empty;
        private string _playerId = string.Empty;
        private int _localPlayerIndex = 0;
        private MatchApiClient _apiClient;
        private DuelRuntime _gameRuntime;

        private float _timeSinceCheckpoint = 0f;
        private bool _isActive = false;
        private int _checkpointNumber = 0;

        public static MatchCheckpointService Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (GameService.Instance?.IsReady == true)
            {
                _apiClient = new MatchApiClient();
            }
        }

        private void Update()
        {
            if (!_isActive || _gameRuntime == null) return;

            _timeSinceCheckpoint += Time.deltaTime;
            if (_timeSinceCheckpoint >= checkpointIntervalSeconds)
            {
                _ = SaveCheckpointAsync();
                _timeSinceCheckpoint = 0f;
            }
        }

        public void InitializeMatch(string matchId, string playerId, DuelRuntime gameRuntime, int localPlayerIndex = 0)
        {
            _matchId = matchId;
            _playerId = playerId;
            _gameRuntime = gameRuntime;
            _localPlayerIndex = localPlayerIndex;
            _checkpointNumber = 0;
            _timeSinceCheckpoint = 0f;
            _isActive = true;

            Debug.Log($"[Checkpoint] Initialized for match {matchId} (player index {localPlayerIndex})");
        }

        private async Task SaveCheckpointAsync()
        {
            if (string.IsNullOrEmpty(_matchId) || _apiClient == null || _gameRuntime == null) return;

            try
            {
                var snapshot = _gameRuntime.CreateSnapshot(_localPlayerIndex);
                _checkpointNumber++;
                _ = PostCheckpointAsync(_matchId, snapshot, _checkpointNumber, SequenceTracker.CurrentSequence());
                Debug.Log($"[Checkpoint] #{_checkpointNumber} saved (turn {snapshot.turnNumber})");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Checkpoint] Failed (non-critical): {ex.Message}");
            }
        }

        private async Task PostCheckpointAsync(string matchId, object snapshot, int checkpointNumber, int sequence)
        {
            try
            {
                var request = new MatchApiClient.PostCheckpointRequestDto
                {
                    matchId = matchId,
                    playerId = _playerId,
                    snapshot = snapshot,
                    checkpointNumber = checkpointNumber,
                    sequence = sequence,
                    timestamp = DateTimeOffset.UtcNow.ToString("O")
                };

                await _apiClient.PostCheckpoint(matchId, request);
                Debug.Log($"[Checkpoint] Uploaded checkpoint {checkpointNumber}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Checkpoint] Failed to upload: {ex.Message}");
            }
        }

        public async Task FinalizMatchAsync()
        {
            if (!_isActive || _gameRuntime == null) return;

            try
            {
                var snapshot = _gameRuntime.CreateSnapshot(_localPlayerIndex);
                _checkpointNumber++;
                await PostCheckpointAsync(_matchId, snapshot, _checkpointNumber, SequenceTracker.CurrentSequence());
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Checkpoint] Final checkpoint failed: {ex.Message}");
            }

            _isActive = false;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
