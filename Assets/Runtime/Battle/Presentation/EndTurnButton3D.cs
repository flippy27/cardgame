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

        private void Start()
        {
            _button = GetComponent<UnityEngine.UI.Button>();
            _buttonText = GetComponentInChildren<TextMeshProUGUI>();

            if (_button != null)
            {
                _button.onClick.AddListener(OnEndTurnClicked);
            }

            SetEnabled(false);
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(OnEndTurnClicked);
            }
        }

        private void OnEndTurnClicked()
        {
            if (presenter != null)
            {
                presenter.RequestEndTurn();
            }
        }

        public void SetEnabled(bool enabled)
        {
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
    }
}
