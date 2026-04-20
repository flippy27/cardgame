using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Logs every in-game action (card plays, turn ends, etc.) with sequence numbers.
    /// Batches actions and sends to API every few seconds.
    /// Primary source for match replays.
    /// </summary>
    public sealed class MatchActionService : MonoBehaviour
    {
        [SerializeField] private float batchIntervalSeconds = 3f;

        private string _matchId = string.Empty;
        private string _playerId = string.Empty;
        private CardGameApiClient _apiClient;
        private AuthService _authService;

        private Queue<MatchAction> _pendingActions = new();
        private int _actionNumber = 0; // Local action counter
        private float _timeSinceLastFlush = 0f;
        private bool _isActive = false;

        public static MatchActionService Instance { get; private set; }

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
                _apiClient = GameService.Instance.ApiClient;
                _authService = GameService.Instance.AuthService;
            }
        }

        private void Update()
        {
            if (!_isActive) return;

            _timeSinceLastFlush += Time.deltaTime;
            if (_timeSinceLastFlush >= batchIntervalSeconds)
            {
                _ = FlushActionsAsync(); // Fire-and-forget
                _timeSinceLastFlush = 0f;
            }
        }

        /// <summary>
        /// Initialize for a match. Called when match starts.
        /// </summary>
        public void InitializeMatch(string matchId, string playerId)
        {
            _matchId = matchId;
            _playerId = playerId;
            _actionNumber = 0;
            _pendingActions.Clear();
            _timeSinceLastFlush = 0f;
            _isActive = true;

            Debug.Log($"[ActionLog] Initialized for match {matchId} (player {playerId})");
        }

        /// <summary>
        /// Log a card play action.
        /// </summary>
        public void LogCardPlay(string cardId, int slotIndex, int manaCost)
        {
            LogAction("CardPlay", new
            {
                cardId = cardId,
                slot = slotIndex,
                manaCost = manaCost
            });
        }

        /// <summary>
        /// Log an end turn action.
        /// </summary>
        public void LogEndTurn(int turnNumber)
        {
            LogAction("EndTurn", new { turnNumber = turnNumber });
        }

        /// <summary>
        /// Log a forfeit action.
        /// </summary>
        public void LogForfeit()
        {
            LogAction("Forfeit", null);
        }

        /// <summary>
        /// Log a generic action.
        /// </summary>
        public void LogAction(string actionType, object data)
        {
            if (!_isActive) return;

            var action = new MatchAction
            {
                actionNumber = _actionNumber++,
                sequence = SequenceTracker.NextSequence(),
                timestamp = DateTimeOffset.UtcNow,
                playerId = _playerId,
                actionType = actionType,
                data = data
            };

            _pendingActions.Enqueue(action);
            Debug.Log($"[ActionLog] {actionType} (action #{action.actionNumber}, seq #{action.sequence})");
        }

        /// <summary>
        /// Flush pending actions to API. Fire-and-forget.
        /// </summary>
        private async Task FlushActionsAsync()
        {
            if (_pendingActions.Count == 0) return;
            if (string.IsNullOrEmpty(_matchId)) return;
            if (_apiClient == null) return;

            var actions = new List<MatchAction>(_pendingActions.Count);
            while (_pendingActions.Count > 0)
            {
                actions.Add(_pendingActions.Dequeue());
            }

            try
            {
                var currentSequence = SequenceTracker.CurrentSequence();
                await _apiClient.PostActionsAsync(_matchId, actions, currentSequence);
                Debug.Log($"[ActionLog] Flushed {actions.Count} actions (seq up to {currentSequence})");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ActionLog] Failed to send: {ex.Message}. Will retry next batch.");
                // Re-queue actions for next attempt
                foreach (var action in actions)
                {
                    _pendingActions.Enqueue(action);
                }
            }
        }

        /// <summary>
        /// Finalize match. Send remaining actions and complete match.
        /// </summary>
        public async Task FinializeMatchAsync()
        {
            if (!_isActive) return;

            // Flush any remaining actions
            await FlushActionsAsync();

            _isActive = false;
            Debug.Log($"[ActionLog] Match {_matchId} finalized ({_actionNumber} total actions)");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }

    [System.Serializable]
    public class MatchAction
    {
        public int actionNumber;                    // Local action counter (0, 1, 2, ...)
        public int sequence;                        // Global sequence number (detect loss)
        public DateTimeOffset timestamp;            // When action occurred
        public string playerId;                     // Who did it
        public string actionType;                   // CardPlay, EndTurn, Forfeit, etc.
        public object data;                         // Action-specific data
    }
}
