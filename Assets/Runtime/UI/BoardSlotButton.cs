using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

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

            if (_isOccupied)
            {
                EnsureSpawnedCard();
                _spawnedCard.Bind(snapshot.occupant);

                var isNewOccupant = _currentOccupantRuntimeId != snapshot.occupant.runtimeId;
                _currentOccupantRuntimeId = snapshot.occupant.runtimeId;
                _spawnedCard.gameObject.SetActive(true);

                if (isNewOccupant)
                {
                    PlaySpawnPulse();
                }
            }
            else
            {
                _currentOccupantRuntimeId = null;
                if (_spawnedCard != null)
                {
                    Destroy(_spawnedCard.gameObject);
                    _spawnedCard = null;
                }
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

        private void PlaySpawnPulse()
        {
            if (_spawnedCard == null)
            {
                return;
            }

            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
            }

            _pulseRoutine = StartCoroutine(SpawnPulseRoutine(_spawnedCard.transform));
        }

        private IEnumerator SpawnPulseRoutine(Transform target)
        {
            var elapsed = 0f;
            const float duration = 0.18f;
            var from = Vector3.one * 0.82f;
            var to = Vector3.one;
            target.localScale = from;

            while (elapsed < duration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                t = 1f - Mathf.Pow(1f - t, 3f);
                target.localScale = Vector3.LerpUnclamped(from, to, t);
                yield return null;
            }

            if (target != null)
            {
                target.localScale = Vector3.one;
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
