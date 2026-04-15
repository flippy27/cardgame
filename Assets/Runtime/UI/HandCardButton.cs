using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Carta de la mano con click y drag and drop.
    /// </summary>
    public sealed class HandCardButton : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        public Button button;
        public Image backgroundImage;
        public CanvasGroup canvasGroup;
        public CardViewWidget cardView;

        private CardInHandDto _dto;
        private BattleScreenPresenter _presenter;
        private bool _canDrag;
        private bool _isDragging;
        private Vector2 _lastDragPosition;

        public void Bind(CardInHandDto dto, BattleScreenPresenter presenter, bool isSelected, bool canAfford, bool isLocalTurn)
        {
            _dto = dto;
            _presenter = presenter;
            _canDrag = isLocalTurn && canAfford;

            if (cardView != null)
            {
                cardView.Bind(dto);
            }

            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (backgroundImage == null && button != null)
            {
                backgroundImage = button.targetGraphic as Image;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (button != null)
            {
                button.interactable = true;
            }

            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = true;
                canvasGroup.alpha = canAfford ? 1f : 0.62f;
            }

            if (backgroundImage != null)
            {
                if (!isLocalTurn)
                {
                    backgroundImage.color = new Color(0.14f, 0.14f, 0.16f, 0.78f);
                }
                else if (!canAfford)
                {
                    backgroundImage.color = new Color(0.30f, 0.30f, 0.32f, 0.92f);
                }
                else if (isSelected)
                {
                    backgroundImage.color = new Color(0.88f, 0.72f, 0.18f, 0.98f);
                }
                else
                {
                    backgroundImage.color = new Color(0.16f, 0.17f, 0.20f, 0.94f);
                }
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _presenter?.NotifyCardClicked(_dto);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_canDrag || _presenter == null)
            {
                return;
            }

            _isDragging = true;
            _lastDragPosition = eventData.position;

            Debug.Log($"[Hand] Drag start: {_dto.displayName}");

            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.alpha = 0.50f;
            }

            _presenter.BeginDrag(_dto, this, eventData.position);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_isDragging)
            {
                return;
            }

            _isDragging = false;

            Debug.Log("[Hand] Drag end");

            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = true;
                canvasGroup.alpha = 1f;
            }

            if (_presenter != null)
            {
                _presenter.EndDrag();
            }
        }

        private void Update()
        {
            if (_isDragging && _presenter != null)
            {
                var mousePos = (Vector2)UnityEngine.Input.mousePosition;
                if (mousePos != _lastDragPosition)
                {
                    _lastDragPosition = mousePos;
                    _presenter.UpdateDrag(mousePos);
                }
            }
        }
    }
}
