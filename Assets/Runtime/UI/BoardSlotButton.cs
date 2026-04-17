using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;
using System.Collections.Generic;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Vista de slot fija en la escena.
    /// Muestra placeholder, recibe drop y aloja la carta ocupante.
    /// </summary>
    public sealed class BoardSlotButton : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("Identity")]
        public BoardSlot slot;
        public bool isLocalSide = true;

        [Header("References")]
        public Button button;
        public Image placeholderImage;
        public TMPro.TextMeshProUGUI labelTextTMP;
        public RectTransform cardAnchor;
        public Image hoverGlowImage;

        private BattleScreenPresenter _presenter;
        private CardViewWidget _spawnedCard;
        private string _currentOccupantRuntimeId;
        private bool _isHovering;
        private bool _hasSelectedCard;
        private bool _legalForSelectedCard;
        private bool _hasDrag;
        private bool _isOccupied;
        private Coroutine _pulseRoutine;

        public RectTransform CardAnchor => cardAnchor != null ? cardAnchor : transform as RectTransform;

        public void Bind(BattleScreenPresenter presenter)
        {
            _presenter = presenter;

            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (placeholderImage == null && button != null)
            {
                placeholderImage = button.targetGraphic as Image;
            }

            if (cardAnchor == null)
            {
                cardAnchor = transform as RectTransform;
            }

            RefreshOnlyVisual(false, false, false);
        }

        public void ApplySnapshot(BoardSlotSnapshotDto snapshot, bool isLocalTurn, bool hasSelectedCard, bool legalForSelectedCard)
        {
            _hasSelectedCard = hasSelectedCard;
            _legalForSelectedCard = legalForSelectedCard;
            _isOccupied = snapshot != null && snapshot.occupied && snapshot.occupant != null;

            // NUCLEAR: destroy all children in anchor first
            if (CardAnchor != null)
            {
                var children = new List<Transform>();
                foreach (Transform child in CardAnchor)
                {
                    children.Add(child);
                }
                foreach (var child in children)
                {
                    Destroy(child.gameObject);
                }
            }
            _spawnedCard = null;
            _currentOccupantRuntimeId = null;

            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
                _pulseRoutine = null;
            }

            if (_isOccupied)
            {
                EnsureSpawnedCard();
                _spawnedCard.Bind(snapshot.occupant);
                _currentOccupantRuntimeId = snapshot.occupant.runtimeId;
                _spawnedCard.gameObject.SetActive(true);
            }
            else
            {
                // Slot is empty
            }

            var canInteract = isLocalSide && isLocalTurn && hasSelectedCard && legalForSelectedCard && !_isOccupied;
            if (button != null)
            {
                button.interactable = canInteract;
            }

            RefreshOnlyVisual(hasSelectedCard, legalForSelectedCard, _presenter != null && _presenter.HasDraggedCard);
        }

        public void RefreshOnlyVisual(bool hasSelectedCard, bool legalForSelectedCard, bool hasDrag)
        {
            _hasSelectedCard = hasSelectedCard;
            _legalForSelectedCard = legalForSelectedCard;
            _hasDrag = hasDrag;

            if (placeholderImage != null)
            {
                placeholderImage.color = ResolvePlaceholderColor();
            }

            if (labelTextTMP != null)
            {
                labelTextTMP.text = BuildLabel();
            }

            if (hoverGlowImage != null)
            {
                hoverGlowImage.enabled = isLocalSide && !_isOccupied && _isHovering && _hasDrag && _legalForSelectedCard;
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (_presenter == null || !isLocalSide)
            {
                return;
            }

            _presenter.TryPlayDraggedCardTo(slot, CardAnchor);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovering = true;
            RefreshOnlyVisual(_hasSelectedCard, _legalForSelectedCard, _hasDrag);

            // Register this slot as drop target during drag
            if (_presenter != null && _presenter.HasDraggedCard)
            {
                _presenter.SetDragOverSlot(this);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovering = false;
            RefreshOnlyVisual(_hasSelectedCard, _legalForSelectedCard, _hasDrag);

            // Unregister drop target
            if (_presenter != null)
            {
                _presenter.SetDragOverSlot(null);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_presenter == null || !isLocalSide)
            {
                return;
            }

            _presenter.TryPlaySelectedCardTo(slot, CardAnchor);
        }

        private void EnsureSpawnedCard()
        {
            // Destroy all children in anchor except our spawned card
            if (CardAnchor != null)
            {
                var children = new List<Transform>();
                foreach (Transform child in CardAnchor)
                {
                    if (_spawnedCard == null || child.gameObject != _spawnedCard.gameObject)
                    {
                        children.Add(child);
                    }
                }
                foreach (var child in children)
                {
                    Destroy(child.gameObject);
                }
            }

            if (_spawnedCard != null)
            {
                return;
            }

            if (_presenter == null || _presenter.BoardCardPrefab == null)
            {
                return;
            }

            _spawnedCard = Instantiate(_presenter.BoardCardPrefab, CardAnchor);
            var rect = _spawnedCard.transform as RectTransform;
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one;
            }
        }

        private void PlaySlideIn()
        {
            if (_spawnedCard == null)
            {
                return;
            }

            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
            }

            _pulseRoutine = StartCoroutine(SlideInRoutine(_spawnedCard.transform));
        }

        private void PlayReplace()
        {
            if (_spawnedCard == null)
            {
                return;
            }

            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
            }

            _pulseRoutine = StartCoroutine(ReplaceRoutine(_spawnedCard.transform));
        }

        private void PlayDeath(Transform target)
        {
            if (target == null)
            {
                return;
            }

            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
            }

            _pulseRoutine = StartCoroutine(DeathRoutine(target));
        }

        private IEnumerator SlideInRoutine(Transform target)
        {
            var elapsed = 0f;
            const float duration = 0.25f;
            var rect = target as RectTransform;

            Vector2 fromPos;
            if (slot == BoardSlot.Front)
            {
                fromPos = new Vector2(0, -200);
            }
            else if (slot == BoardSlot.BackLeft)
            {
                fromPos = new Vector2(-150, 100);
            }
            else
            {
                fromPos = new Vector2(150, 100);
            }

            var toPos = Vector2.zero;
            var fromScale = Vector3.one * 0.7f;
            var toScale = Vector3.one;

            if (rect != null)
            {
                rect.anchoredPosition = fromPos;
            }
            target.localScale = fromScale;

            while (elapsed < duration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                t = Mathf.SmoothStep(0, 1, t);

                if (rect != null)
                {
                    rect.anchoredPosition = Vector2.Lerp(fromPos, toPos, t);
                }
                target.localScale = Vector3.Lerp(fromScale, toScale, t);
                yield return null;
            }

            if (target != null && rect != null)
            {
                rect.anchoredPosition = toPos;
                target.localScale = toScale;
            }

            _pulseRoutine = null;
        }

        private IEnumerator ReplaceRoutine(Transform target)
        {
            var elapsed = 0f;
            const float duration = 0.15f;
            var rect = target as RectTransform;
            var startScale = target.localScale;
            var startPos = rect != null ? rect.anchoredPosition : Vector2.zero;

            // Shake and scale effect for replacement
            if (rect != null)
            {
                rect.anchoredPosition = startPos;
            }
            target.localScale = startScale;

            while (elapsed < duration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);

                // Shake effect
                float shake = Mathf.Sin(t * Mathf.PI * 4) * (1 - t) * 8f;
                if (rect != null)
                {
                    rect.anchoredPosition = startPos + new Vector2(shake, 0);
                }

                // Scale pulse
                var scale = Mathf.Lerp(1f, 1.05f, Mathf.Sin(t * Mathf.PI) * 0.5f + 0.5f);
                target.localScale = startScale * scale;
                yield return null;
            }

            if (target != null && rect != null)
            {
                rect.anchoredPosition = startPos;
                target.localScale = startScale;
            }

            _pulseRoutine = null;
        }

        private IEnumerator DeathRoutine(Transform target)
        {
            var elapsed = 0f;
            const float duration = 0.22f;
            var startScale = target.localScale;
            var startAlpha = 1f;

            var canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = target.gameObject.AddComponent<CanvasGroup>();
            }

            while (elapsed < duration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                t = Mathf.SmoothStep(0, 1, t);

                target.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0, t * t);
                yield return null;
            }

            if (target != null)
            {
                Destroy(target.gameObject);
            }

            _pulseRoutine = null;
        }

        /// <summary>
        /// Animate card from current position to new slot position.
        /// Used when cards are displaced by replacement.
        /// </summary>
        public void PlaySlotTransition(Transform target, RectTransform sourceSlotRect)
        {
            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
            }

            _pulseRoutine = StartCoroutine(SlotToSlotRoutine(target, sourceSlotRect));
        }

        public CardViewWidget GetSpawnedCard()
        {
            return _spawnedCard;
        }

        public string GetCurrentOccupantRuntimeId()
        {
            return _currentOccupantRuntimeId;
        }

        /// <summary>
        /// When a widget is animated into this slot, claim ownership of it.
        /// </summary>
        public void ClaimWidget(CardViewWidget widget)
        {
            if (widget == null)
                return;

            // Destroy any other widgets in this anchor
            var children = new List<Transform>();
            foreach (Transform child in CardAnchor)
            {
                if (child.gameObject != widget.gameObject)
                {
                    children.Add(child);
                }
            }
            foreach (var child in children)
            {
                Destroy(child.gameObject);
            }

            _spawnedCard = widget;
            // Keep the occupant ID in sync
            if (widget.TryGetComponent<CardViewWidget>(out var cardWidget))
            {
                // ID will be set by Bind, this just ensures ownership
            }
        }

        /// <summary>
        /// Animate card from world position to current position.
        /// </summary>
        public void PlayWorldPositionTransition(Transform target, Vector3 fromWorldPos, Vector3? toWorldPos = null)
        {
            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
            }

            _pulseRoutine = StartCoroutine(WorldPositionTransitionRoutine(target, fromWorldPos, toWorldPos));
        }

        private IEnumerator WorldPositionTransitionRoutine(Transform target, Vector3 fromWorldPos, Vector3? toWorldPos = null)
        {
            var elapsed = 0f;
            const float duration = 0.25f;
            var finalPos = toWorldPos ?? target.position;
            var rect = target as RectTransform;

            Debug.Log($"[BoardSlotButton] Animating {target.name} from {fromWorldPos} to {finalPos}");

            while (elapsed < duration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                t = Mathf.SmoothStep(0, 1, t);

                target.position = Vector3.Lerp(fromWorldPos, finalPos, t);
                yield return null;
            }

            if (target != null)
            {
                target.position = finalPos;
                if (rect != null)
                {
                    rect.localScale = Vector3.one;
                }
                var canvasGroup = target.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f;
                }
            }

            _pulseRoutine = null;
        }

        private IEnumerator SlotToSlotRoutine(Transform target, RectTransform sourceSlotRect)
        {
            var elapsed = 0f;
            const float duration = 0.25f;
            var rect = target as RectTransform;

            if (rect == null || sourceSlotRect == null)
                yield break;

            // Current position (at source slot)
            var fromPos = sourceSlotRect.anchoredPosition;
            // Target position (at this slot, local 0,0)
            var toPos = Vector2.zero;

            var startScale = target.localScale;

            while (elapsed < duration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                t = Mathf.SmoothStep(0, 1, t);

                if (rect != null)
                {
                    rect.anchoredPosition = Vector2.Lerp(fromPos, toPos, t);
                }
                yield return null;
            }

            if (target != null && rect != null)
            {
                rect.anchoredPosition = toPos;
            }

            _pulseRoutine = null;
        }

        private Color ResolvePlaceholderColor()
        {
            var baseColor = slot == BoardSlot.Front
                ? new Color(0.70f, 0.16f, 0.16f, 0.88f)
                : new Color(0.16f, 0.72f, 0.28f, 0.88f);

            if (_isOccupied)
            {
                return new Color(baseColor.r * 0.45f, baseColor.g * 0.45f, baseColor.b * 0.45f, 0.78f);
            }

            if (_hasSelectedCard && !_legalForSelectedCard && isLocalSide)
            {
                return new Color(0.30f, 0.30f, 0.30f, 0.72f);
            }

            if (_hasSelectedCard && _legalForSelectedCard && isLocalSide)
            {
                return Color.Lerp(baseColor, Color.white, _isHovering ? 0.30f : 0.18f);
            }

            return baseColor;
        }

        private string BuildLabel()
        {
            var slotName = slot == BoardSlot.Front ? "Front / Melee" : slot == BoardSlot.BackLeft ? "Back Left / Ranged" : "Back Right / Ranged";
            var sideName = isLocalSide ? "Your" : "Enemy";

            if (_isOccupied)
            {
                return $"{sideName} {slotName}\nOccupied";
            }

            if (!isLocalSide)
            {
                return $"{sideName} {slotName}";
            }

            if (!_hasSelectedCard)
            {
                return $"{sideName} {slotName}";
            }

            return _legalForSelectedCard ? $"{sideName} {slotName}\nDrop Here" : $"{sideName} {slotName}\nInvalid";
        }
    }
}
