using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Networking;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Pantalla mínima para matchmaking.
    /// </summary>
    public sealed class MatchmakingPanelController : MonoBehaviour
    {
        public MpsGameSessionService sessionService;
        public InputField joinCodeField;
        public InputField privateMatchNameField;
        public Text statusText;
        public Text joinCodeText;
        public Button quickMatchButton;
        public Button createPrivateButton;
        public Button joinByCodeButton;
        public Button advancedQueueButton;
        public Button leaveButton;
        public Button readyButton;
        public Button copyJoinCodeButton;
        public GameObject[] hideWhenInSession;
        public GameObject[] showWhenInSession;

        private CancellationTokenSource _matchmakerCts;
        private bool _localReady;

        private void Awake()
        {
            if (sessionService == null)
            {
                sessionService = FindFirstObjectByType<MpsGameSessionService>();
            }
        }

        private void OnEnable()
        {
            if (sessionService != null)
            {
                sessionService.SessionChanged += HandleSessionChanged;
                sessionService.JoinCodeChanged += HandleJoinCodeChanged;
                sessionService.ErrorRaised += HandleErrorRaised;
            }

            AddListener(quickMatchButton, HandleQuickMatch);
            AddListener(createPrivateButton, HandleCreatePrivate);
            AddListener(joinByCodeButton, HandleJoinByCode);
            AddListener(advancedQueueButton, HandleAdvancedQueue);
            AddListener(leaveButton, HandleLeave);
            AddListener(readyButton, HandleReadyToggle);
            AddListener(copyJoinCodeButton, HandleCopyJoinCode);
            RefreshButtonVisibility();
        }

        private void OnDisable()
        {
            if (sessionService != null)
            {
                sessionService.SessionChanged -= HandleSessionChanged;
                sessionService.JoinCodeChanged -= HandleJoinCodeChanged;
                sessionService.ErrorRaised -= HandleErrorRaised;
            }

            RemoveListener(quickMatchButton, HandleQuickMatch);
            RemoveListener(createPrivateButton, HandleCreatePrivate);
            RemoveListener(joinByCodeButton, HandleJoinByCode);
            RemoveListener(advancedQueueButton, HandleAdvancedQueue);
            RemoveListener(leaveButton, HandleLeave);
            RemoveListener(readyButton, HandleReadyToggle);
            RemoveListener(copyJoinCodeButton, HandleCopyJoinCode);
        }

        private async void HandleQuickMatch()
        {
            SetStatus("Quick matching...");
            await sessionService.QuickMatchAsync();
        }

        private async void HandleCreatePrivate()
        {
            SetStatus("Creating private match...");
            await sessionService.CreatePrivateMatchAsync(privateMatchNameField != null ? privateMatchNameField.text : string.Empty);
        }

        private async void HandleJoinByCode()
        {
            SetStatus("Joining by code...");
            await sessionService.JoinByCodeAsync(joinCodeField != null ? joinCodeField.text : string.Empty);
        }

        private async void HandleAdvancedQueue()
        {
            _matchmakerCts?.Cancel();
            _matchmakerCts = new CancellationTokenSource();
            SetStatus("Searching queue...");
            await sessionService.AdvancedMatchmakerAsync(_matchmakerCts.Token);
        }

        private async void HandleLeave()
        {
            SetStatus("Leaving session...");
            _matchmakerCts?.Cancel();

            if (CardDuelNetworkCoordinator.Instance != null)
            {
                CardDuelNetworkCoordinator.Instance.SubmitLeaveIntent();
            }

            _localReady = false;
            await sessionService.LeaveAsync();
            RefreshButtonVisibility();
        }

        private void HandleReadyToggle()
        {
            if (CardDuelNetworkCoordinator.Instance == null)
            {
                SetStatus("Coordinator not available.");
                return;
            }

            _localReady = !_localReady;
            CardDuelNetworkCoordinator.Instance.SubmitReady(_localReady);
            RefreshReadyButtonText();
            SetStatus(_localReady ? "Ready submitted." : "Ready removed.");
        }

        private void HandleCopyJoinCode()
        {
            sessionService.CopyJoinCodeToClipboard();
            SetStatus("Join code copied.");
        }

        private void HandleSessionChanged(Unity.Services.Multiplayer.ISession session)
        {
            SetStatus(session == null
                ? "Not in session"
                : $"In session: {session.Name} ({session.PlayerCount}/{session.MaxPlayers})");

            if (session == null)
            {
                _localReady = false;
            }

            RefreshReadyButtonText();
            RefreshButtonVisibility();
        }

        private void HandleJoinCodeChanged(string code)
        {
            if (joinCodeText != null)
            {
                joinCodeText.text = string.IsNullOrWhiteSpace(code) ? "Join Code: -" : $"Join Code: {code}";
            }

            RefreshButtonVisibility();
        }

        private void HandleErrorRaised(string message)
        {
            SetStatus($"Error: {message}");
        }

        private void RefreshButtonVisibility()
        {
            var inSession = sessionService != null && sessionService.CurrentSession != null;
            SetActive(hideWhenInSession, !inSession);
            SetActive(showWhenInSession, inSession);

            if (quickMatchButton != null) quickMatchButton.gameObject.SetActive(!inSession);
            if (createPrivateButton != null) createPrivateButton.gameObject.SetActive(!inSession);
            if (joinByCodeButton != null) joinByCodeButton.gameObject.SetActive(!inSession);
            if (advancedQueueButton != null) advancedQueueButton.gameObject.SetActive(!inSession);
            if (leaveButton != null) leaveButton.gameObject.SetActive(inSession);
            if (readyButton != null) readyButton.gameObject.SetActive(inSession);
            if (copyJoinCodeButton != null) copyJoinCodeButton.gameObject.SetActive(inSession && !string.IsNullOrWhiteSpace(sessionService.LastJoinCode));
        }

        private void RefreshReadyButtonText()
        {
            if (readyButton == null)
            {
                return;
            }

            var label = readyButton.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = _localReady ? "Unready" : "Ready";
            }
        }

        private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.AddListener(action);
            }
        }

        private static void RemoveListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(action);
            }
        }

        private static void SetActive(GameObject[] targets, bool active)
        {
            if (targets == null)
            {
                return;
            }

            foreach (var target in targets)
            {
                if (target != null)
                {
                    target.SetActive(active);
                }
            }
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }
    }
}
