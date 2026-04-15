using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Carta de la mano con click y drag and drop.
    /// </summary>
    public sealed class HandCardButton : MonoBehaviour, IPointerClickHandler
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
            if (!_isDragging)
            {
                _presenter?.NotifyCardClicked(_dto);
            }
        }

        private void Update()
        {
            if (!_canDrag || _presenter == null)
            {
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            var mousePos = mouse.position.ReadValue();

            // Start drag when mouse pressed over this card
            if (!_isDragging && mouse.leftButton.wasPressedThisFrame)
            {
                if (IsMouseOverThisCard(mousePos))
                {
                    _isDragging = true;
                    _lastDragPosition = mousePos;

                    Debug.Log($"[Hand] Drag start: {_dto.displayName}");

                    if (canvasGroup != null)
                    {
                        canvasGroup.blocksRaycasts = false;
                        canvasGroup.alpha = 0.50f;
                    }

                    _presenter.BeginDrag(_dto, this, mousePos);
                }
            }

            // Update drag position while mouse held
            if (_isDragging && mouse.leftButton.isPressed)
            {
                if (mousePos != _lastDragPosition)
                {
                    _lastDragPosition = mousePos;
                    _presenter.UpdateDrag(mousePos);
                }
            }

            // End drag when mouse released
            if (_isDragging && mouse.leftButton.wasReleasedThisFrame)
            {
                _isDragging = false;

                Debug.Log("[Hand] Drag end");

                if (canvasGroup != null)
                {
                    canvasGroup.blocksRaycasts = true;
                    canvasGroup.alpha = 1f;
                }

                _presenter.EndDrag();
            }
        }

        private bool IsMouseOverThisCard(Vector2 mousePos)
        {
            var rect = transform as RectTransform;
            if (rect == null)
            {
                return false;
            }

            return RectTransformUtility.RectangleContainsScreenPoint(rect, mousePos);
        }
    }
}
