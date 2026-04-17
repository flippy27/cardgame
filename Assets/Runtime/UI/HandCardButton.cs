using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Carta de la mano con click y drag and drop.
    /// </summary>
    public sealed class HandCardButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
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
        private Vector3 _originalScale;
        private Coroutine _scaleCoroutine;
        private const float HoverScale = 1.25f;
        private const float ScaleAnimSpeed = 0.15f;

        public void Bind(CardInHandDto dto, BattleScreenPresenter presenter, bool isSelected, bool canAfford, bool isLocalTurn)
        {
            _dto = dto;
            _presenter = presenter;
            _canDrag = isLocalTurn && canAfford;
            _originalScale = transform.localScale;

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
                Debug.LogWarning("[Hand] Mouse.current is NULL");
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

                   

                    if (canvasGroup != null)
                    {
                        canvasGroup.blocksRaycasts = false;
                        canvasGroup.alpha = 0.50f;
                    }

                    _presenter.BeginDrag(_dto, this, mousePos);
                }
            }

            // Update drag position while mouse held
            if (_isDragging)
            {
                var isPressed = mouse.leftButton.isPressed;
               

                if (isPressed)
                {
                    if (mousePos != _lastDragPosition)
                    {
                        _lastDragPosition = mousePos;
                        _presenter.UpdateDrag(mousePos);
                    }
                }
                else
                {
                    // Mouse released
                    _isDragging = false;
                   

                    if (canvasGroup != null)
                    {
                        canvasGroup.blocksRaycasts = true;
                        canvasGroup.alpha = 1f;
                    }

                    _presenter.EndDrag();
                }
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

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_scaleCoroutine != null)
                StopCoroutine(_scaleCoroutine);
            _scaleCoroutine = StartCoroutine(ScaleTo(Vector3.one * HoverScale));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_scaleCoroutine != null)
                StopCoroutine(_scaleCoroutine);
            _scaleCoroutine = StartCoroutine(ScaleTo(_originalScale));
        }

        private IEnumerator ScaleTo(Vector3 targetScale)
        {
            while (Vector3.Distance(transform.localScale, targetScale) > 0.01f)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, targetScale, ScaleAnimSpeed);
                yield return null;
            }
            transform.localScale = targetScale;
        }
    }
}
