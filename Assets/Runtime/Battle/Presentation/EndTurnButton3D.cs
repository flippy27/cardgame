using UnityEngine;
using TMPro;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Botón End Turn en 3D/UI.
    /// Se actualiza según si es turno del jugador local.
    /// </summary>
    public class EndTurnButton3D : MonoBehaviour
    {
        [SerializeField] private GameplayPresenter3D presenter;

        public GameplayPresenter3D Presenter
        {
            get => presenter;
            set => presenter = value;
        }

        private TextMeshProUGUI _buttonText;
        private UnityEngine.UI.Button _button;

        private void Awake()
        {
            EnsureReferences();
        }

        private void Start()
        {
            EnsureReferences();

            if (_button != null)
            {
                _button.onClick.AddListener(OnEndTurnClicked);
            }

            SyncStateFromLatestSnapshot();
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(OnEndTurnClicked);
            }
        }

        public void OnEndTurnClicked()
        {
            EnsureReferences();

            Debug.Log("[EndTurnButton3D] Clicked");

            if (presenter != null)
            {
                presenter.RequestEndTurn();
            }
            else
            {
                Debug.LogError("[EndTurnButton3D] GameplayPresenter3D reference is missing.");
            }
        }

        public void SetEnabled(bool enabled)
        {
            EnsureReferences();

            if (_button != null)
            {
                _button.interactable = enabled;
            }

            if (_buttonText != null)
            {
                _buttonText.text = enabled ? "END TURN" : "OPPONENT'S TURN";
                _buttonText.color = enabled ? Color.white : Color.gray;
            }
        }

        private void EnsureReferences()
        {
            if (_button == null)
            {
                _button = GetComponent<UnityEngine.UI.Button>();
            }

            if (_buttonText == null)
            {
                _buttonText = GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (presenter == null)
            {
                presenter = GameplayPresenter3D.Instance ?? FindFirstObjectByType<GameplayPresenter3D>();
            }
        }

        private void SyncStateFromLatestSnapshot()
        {
            var snapshot = GameplayPresenter3D.GetLatestSnapshot();
            if (snapshot == null)
            {
                SetEnabled(false);
                return;
            }

            var isInProgress = snapshot.matchPhase == Flippy.CardDuelMobile.Core.MatchPhase.InProgress && !snapshot.duelEnded;
            var isLocalTurn = isInProgress &&
                              (snapshot.isLocalPlayersTurn ||
                               snapshot.activePlayerIndex == snapshot.localPlayerIndex);
            SetEnabled(isLocalTurn);
        }
    }
}
