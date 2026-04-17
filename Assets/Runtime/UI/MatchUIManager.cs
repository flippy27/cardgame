using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Services;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Controls match UI visibility and interactions based on MatchState.
    /// - Hide lobby buttons when in-match
    /// - Show/hide Ready button
    /// - Block controls when opponent disconnected
    /// - Show Reconnect button when reconnect available
    /// </summary>
    public class MatchUIManager : MonoBehaviour
    {
        [SerializeField] private Button joinCodeButton;
        [SerializeField] private Button privateMatchButton;
        [SerializeField] private Button reconnectButton;
        [SerializeField] private CanvasGroup gameplayCanvasGroup; // Cards, End Turn, etc.
        [SerializeField] private TextMeshProUGUI statusText;

        private string _currentPlayerId;

        private void Start()
        {
            _currentPlayerId = ServiceLocator.Resolve<IUserService>().GetCurrentUserId();

            // Auto-find references if not assigned
            AutoFindReferences();

            // Subscribe to state changes (view only - MatchStateMachine updates come from server)
            MatchStateMachine.OnStateChanged += OnMatchStateChanged;
            MatchStateMachine.OnPlayerReady += OnPlayerReady;
            MatchStateMachine.OnPlayerDisconnected += OnPlayerDisconnected;
            MatchStateMachine.OnCanReconnect += OnCanReconnect;

            // Initial state
            UpdateUIForState(MatchStateMachine.CurrentState);
        }

        private void AutoFindReferences()
        {
            var matchmakingPanel = FindFirstObjectByType<MatchmakingPanelController>();
            if (matchmakingPanel != null)
            {
                if (!joinCodeButton) joinCodeButton = matchmakingPanel.quickMatchButton;
                if (!privateMatchButton) privateMatchButton = matchmakingPanel.createPrivateButton;
                if (!statusText) statusText = matchmakingPanel.statusText;
            }
            // gameplayCanvasGroup must be manually assigned in inspector to avoid disabling entire canvas
        }

        private void OnMatchStateChanged(MatchState newState)
        {
            GameLogger.Info("UI", $"State → {newState}");
            UpdateUIForState(newState);
        }

        private void UpdateUIForState(MatchState state)
        {
            // Hide lobby buttons when in match
            bool inMatch = state == MatchState.InProgress ||
                          state == MatchState.BothPlayersReady ||
                          state == MatchState.WaitingForPlayer2 ||
                          state == MatchState.PlayerDisconnected;

            if (joinCodeButton) joinCodeButton.gameObject.SetActive(!inMatch);
            if (privateMatchButton) privateMatchButton.gameObject.SetActive(!inMatch);

            // Show reconnect only when needed
            if (reconnectButton)
                reconnectButton.gameObject.SetActive(false); // Will show via OnCanReconnect

            // Block gameplay controls when opponent disconnected
            if (gameplayCanvasGroup)
            {
                gameplayCanvasGroup.interactable = state == MatchState.InProgress;
                gameplayCanvasGroup.alpha = state == MatchState.InProgress ? 1f : 0.5f;
            }

            // Update status text
            if (statusText)
            {
                statusText.text = state switch
                {
                    MatchState.WaitingForPlayer2 => "Waiting for opponent...",
                    MatchState.BothPlayersReady => "Ready to start!",
                    MatchState.InProgress => "In Battle",
                    MatchState.PlayerDisconnected => "⚠️ Opponent Disconnected",
                    _ => ""
                };
            }
        }

        private void OnPlayerReady(string playerId)
        {
            GameLogger.Info("UI", $"Player {playerId} ready");
        }

        private void OnPlayerDisconnected(string playerId)
        {
            GameLogger.Warning("UI", $"Player {playerId} disconnected - gameplay blocked");
        }

        private void OnCanReconnect(string matchId)
        {
            if (reconnectButton)
            {
                reconnectButton.gameObject.SetActive(true);
                GameLogger.Info("UI", "Reconnect available");
            }
        }


        private void OnDestroy()
        {
            MatchStateMachine.OnStateChanged -= OnMatchStateChanged;
            MatchStateMachine.OnPlayerReady -= OnPlayerReady;
            MatchStateMachine.OnPlayerDisconnected -= OnPlayerDisconnected;
            MatchStateMachine.OnCanReconnect -= OnCanReconnect;
        }
    }
}
