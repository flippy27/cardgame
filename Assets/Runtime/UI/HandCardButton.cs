using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using System.Collections;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Carta de la mano con click y drag and drop.
    /// </summary>
    public sealed class HandCardButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerUpHandler
    {
        public Button button;
        public Image backgroundImage;
        public CanvasGroup canvasGroup;
        public CardViewWidget cardView;
        public LayoutElement layoutElement;

        private static HandCardButton _activeDetailViewCard;

        [Header("Detail View")]
        public float detailViewHoldTime = 0.5f;
        public float detailViewScale = 1.8f;
        public float detailViewAnimSpeed = 0.15f;
        public float dragUpThreshold = 30f;

        [Header("Drag")]
        public float dragMinDistance = 15f;

        public CardInHandDto _dto;
        private BattleScreenPresenter _presenter;
        private bool _canDrag;
        private bool _isDragging;
        private Vector2 _lastDragPosition;
        private Vector3 _originalScale;
        private Coroutine _scaleCoroutine;
        private const float HoverScale = 1.25f;
        private const float ScaleAnimSpeed = 0.15f;

        private float _holdTime;
        private bool _inDetailView;
        private bool _pointerOverThisCard;
        private Vector2 _holdStartPosition;
        private bool _hasMovedEnoughToDrag;

        public void Bind(CardInHandDto dto, BattleScreenPresenter presenter, bool isSelected, bool canAfford, bool isLocalTurn)
        {
            _dto = dto;
            _presenter = presenter;
            _canDrag = isLocalTurn && canAfford;
            _originalScale = transform.localScale;

            if (layoutElement == null)
            {
                layoutElement = GetComponent<LayoutElement>();
            }

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

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_inDetailView)
            {
                HideDetailView();
            }
        }

        private void Update()
        {
            if (_presenter == null)
            {
                return;
            }

            Vector2 inputPos = Vector2.zero;
            bool inputPressed = false;
            bool inputPressedThisFrame = false;

            // Check touch input first (mobile priority)
            var touchscreen = Touchscreen.current;
            var touchCount = touchscreen != null ? touchscreen.touches.Count : 0;

            if (touchscreen != null && touchscreen.touches.Count > 0)
            {
                var touch = touchscreen.touches[0];
                inputPos = touch.position.ReadValue();
                inputPressed = true;
                inputPressedThisFrame = inputPressed && !_isDragging;
                if (_inDetailView)
                    Debug.Log($"[HandCardButton] Touch active, count={touchCount}");
            }
            else
            {
                // Check mouse input
                var mouse = Mouse.current;
                if (mouse != null)
                {
                    inputPos = mouse.position.ReadValue();
                    inputPressed = mouse.leftButton.isPressed;
                    inputPressedThisFrame = mouse.leftButton.wasPressedThisFrame;
                    // if (_inDetailView)
                    //     Debug.Log($"[HandCardButton] Mouse, isPressed={inputPressed}");
                }
            }

            var canvas = GetComponentInParent<Canvas>();
            var cam = canvas != null ? canvas.worldCamera : Camera.main;

            // HOLD TIME tracking
            if (_pointerOverThisCard && inputPressed && !_isDragging)
            {
                if (inputPressedThisFrame)
                {
                    _holdTime = 0f;
                    _holdStartPosition = inputPos;
                    _hasMovedEnoughToDrag = false;
                }
                else
                {
                    _holdTime += Time.deltaTime;
                }

                float dragDistance = Vector2.Distance(inputPos, _holdStartPosition);

                // Enter detail view if held long enough
                if (_holdTime >= detailViewHoldTime && !_inDetailView && dragDistance < dragMinDistance)
                {
                    ShowDetailView();
                }

                // Start drag if moved enough before detail threshold
                if (dragDistance >= dragMinDistance && _holdTime < detailViewHoldTime)
                {
                    if (!_canDrag)
                        return;

                    StartDrag(inputPos, cam);
                    _hasMovedEnoughToDrag = true;
                }
            }

            // DETAIL VIEW + DRAG UP conversion
            if (_inDetailView && inputPressed && _hasMovedEnoughToDrag)
            {
                float dragDeltaY = inputPos.y - _holdStartPosition.y;
                if (dragDeltaY > dragUpThreshold)
                {
                    HideDetailView();
                    if (_canDrag && !_isDragging)
                    {
                        StartDrag(inputPos, cam);
                    }
                }
            }

            // Update drag position
            if (_isDragging)
            {
                if (inputPressed)
                {
                    if (inputPos != _lastDragPosition)
                    {
                        _lastDragPosition = inputPos;
                        _presenter.UpdateDrag(inputPos);
                    }
                }
                else
                {
                    EndDrag();
                }
            }

            // Release detail view
            if (_inDetailView)
            {

                if (!inputPressed)
                {

                    HideDetailView();
                    _holdTime = 0f;
                }
            }
        }

        private void StartDrag(Vector2 inputPos, Camera cam)
        {
            HideDetailView();

            Debug.Log($"[HandCardButton] Card {_dto.displayName} drag started");
            _isDragging = true;
            _lastDragPosition = inputPos;

            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.alpha = 0.50f;
            }

            _presenter.BeginDrag(_dto, this, inputPos);
        }

        private void EndDrag()
        {
            _isDragging = false;
            _holdTime = 0f;

            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = true;
                canvasGroup.alpha = 1f;
            }

            _presenter.EndDrag();
        }

        private void ShowDetailView()
        {
            if (_inDetailView)
                return;

            _inDetailView = true;
            _activeDetailViewCard = this;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            _presenter?.ShowDetailView(_dto);
        }

        private void HideDetailView()
        {
            _inDetailView = false;
            _presenter?.HideDetailView();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }



        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerOverThisCard = true;
            if (_scaleCoroutine != null)
                StopCoroutine(_scaleCoroutine);
            _scaleCoroutine = StartCoroutine(ScaleTo(Vector3.one * HoverScale));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerOverThisCard = false;
            if (_scaleCoroutine != null)
                StopCoroutine(_scaleCoroutine);
            _scaleCoroutine = StartCoroutine(ScaleTo(_originalScale));

            if (_inDetailView)
            {
                Debug.Log($"[HandCardButton] Pointer exited while in detail view, closing");
                HideDetailView();
            }
        }

        private IEnumerator ScaleTo(Vector3 targetScale)
        {
            float speed = _inDetailView ? detailViewAnimSpeed : ScaleAnimSpeed;
            while (Vector3.Distance(transform.localScale, targetScale) > 0.01f)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, targetScale, speed);
                yield return null;
            }
            transform.localScale = targetScale;
        }
    }
}
