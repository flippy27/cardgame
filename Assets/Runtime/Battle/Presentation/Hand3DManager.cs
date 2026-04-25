using System.Collections.Generic;
using UnityEngine;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Data;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Manages the local player's hand as a 3D arc.
    /// </summary>
    public class Hand3DManager : MonoBehaviour
    {
        [Header("Hand Arc")]
        public float arcRadius = 5f;
        public float arcHeight = -2f;
        public float arcAngle = 60f;
        public float arcDepth = 1f;
        public float cardSpacing = 0.5f;
        public float hoverLiftY = 0.6f;
        public float hoverSlerpSpeed = 18f;

        [Header("Debug")]
        public bool debugArcMode = false;

        [Header("Prefab")]
        [SerializeField] private GameObject handCardPrefab;

        private readonly List<Card3DView> _handCards = new();
        private readonly List<Vector3> _targetLocalPositions = new();
        private readonly List<Quaternion> _targetLocalRotations = new();
        private Card3DView _hoveredCard;
        private bool _initialized;
        private float _lastArcRadius;
        private float _lastArcHeight;
        private float _lastArcAngle;
        private float _lastArcDepth;
        private float _lastCardSpacing;

        private void Update()
        {
            if (debugArcMode && _handCards.Count > 0)
            {
                if (!Mathf.Approximately(arcRadius, _lastArcRadius) ||
                    !Mathf.Approximately(arcHeight, _lastArcHeight) ||
                    !Mathf.Approximately(arcAngle, _lastArcAngle) ||
                    !Mathf.Approximately(arcDepth, _lastArcDepth) ||
                    !Mathf.Approximately(cardSpacing, _lastCardSpacing))
                {
                    _lastArcRadius = arcRadius;
                    _lastArcHeight = arcHeight;
                    _lastArcAngle = arcAngle;
                    _lastArcDepth = arcDepth;
                    _lastCardSpacing = cardSpacing;
                    RepositionCards(false);
                }
            }

            AnimateCards();
        }

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            foreach (var card in _handCards)
            {
                if (card != null)
                {
                    Destroy(card.gameObject);
                }
            }

            _handCards.Clear();
            _targetLocalPositions.Clear();
            _targetLocalRotations.Clear();

            _lastArcRadius = arcRadius;
            _lastArcHeight = arcHeight;
            _lastArcAngle = arcAngle;
            _lastArcDepth = arcDepth;
            _lastCardSpacing = cardSpacing;

            _initialized = true;
        }

        public void RefreshHand(CardInHandDto[] handDtos)
        {
            if (handDtos == null || handDtos.Length == 0)
            {
                foreach (var card in _handCards)
                {
                    if (card != null)
                    {
                        Destroy(card.gameObject);
                    }
                }

                _handCards.Clear();
                _targetLocalPositions.Clear();
                _targetLocalRotations.Clear();
                return;
            }

            var newCardIds = new HashSet<string>();
            foreach (var dto in handDtos)
            {
                newCardIds.Add(dto.runtimeCardKey);
            }

            for (var i = _handCards.Count - 1; i >= 0; i--)
            {
                var existingCard = _handCards[i];
                if (existingCard != null && !newCardIds.Contains(existingCard.CardData.runtimeId))
                {
                    Destroy(existingCard.gameObject);
                    _handCards.RemoveAt(i);
                }
            }

            for (var i = 0; i < handDtos.Length; i++)
            {
                var dto = handDtos[i];
                var existing = _handCards.Find(c => c != null && c.CardData.runtimeId == dto.runtimeCardKey);
                if (existing != null)
                {
                    existing.Initialize(BuildHandCard(dto), 0);
                    continue;
                }

                var cardGo = Instantiate(handCardPrefab, transform);
                cardGo.name = $"HandCard_{i}_{dto.displayName}";

                var cardView = cardGo.GetComponent<Card3DView>();
                if (cardView == null)
                {
                    cardView = cardGo.AddComponent<Card3DView>();
                }

                var boardCard = BuildHandCard(dto);

                cardView.Initialize(boardCard, 0);
                _handCards.Add(cardView);
            }

            RepositionCards(true);
        }

        public void SetHoveredCard(Card3DView cardView)
        {
            if (_hoveredCard == cardView)
            {
                return;
            }

            _hoveredCard = cardView;
            RepositionCards(false);
        }

        public void ClearHoveredCard()
        {
            SetHoveredCard(null);
        }

        public Card3DView GetCardAt(int index)
        {
            return index >= 0 && index < _handCards.Count ? _handCards[index] : null;
        }

        public int GetHandCount()
        {
            return _handCards.Count;
        }

        private static BoardCardDto BuildHandCard(CardInHandDto dto)
        {
            var definition = CardRegistry.GetCard(dto.cardId);
            return new BoardCardDto
            {
                displayName = dto.displayName,
                manaCost = dto.manaCost,
                attack = dto.attack,
                maxHealth = dto.health,
                currentHealth = dto.health,
                runtimeId = dto.runtimeCardKey,
                cardId = dto.cardId,
                ownerIndex = 0,
                armor = dto.armor,
                slot = BoardSlot.Front,
                canAttack = false,
                unitType = dto.unitType,
                turnsUntilCanAttack = 0,
                attackMotionLevel = definition?.attackMotionLevel ?? 0,
                attackShakeLevel = definition?.attackShakeLevel ?? 0,
                attackDeliveryType = !string.IsNullOrWhiteSpace(dto.attackDeliveryType)
                    ? dto.attackDeliveryType
                    : definition?.attackDeliveryType,
                abilities = dto.abilities
            };
        }

        private void RepositionCards(bool snapImmediately)
        {
            var count = _handCards.Count;
            var anglePerCard = count > 1 ? arcAngle / (count - 1) : 0f;
            var absRadius = Mathf.Abs(arcRadius);
            var radiusNegative = arcRadius < 0f;
            EnsureTargetCapacity(count);

            for (var i = 0; i < count; i++)
            {
                var card = _handCards[i];
                if (card == null)
                {
                    continue;
                }

                var angle = (-(arcAngle / 2f) + (i * anglePerCard)) * Mathf.Deg2Rad;
                var x = Mathf.Sin(angle) * absRadius;
                var y = arcHeight + Mathf.Cos(angle) * arcDepth;
                if (card == _hoveredCard)
                {
                    y += hoverLiftY;
                }

                var z = -(i - (count - 1) / 2f) * cardSpacing;
                if (radiusNegative)
                {
                    x = -x;
                }

                _targetLocalPositions[i] = new Vector3(x, y, z);
                var rotZ = -angle * Mathf.Rad2Deg * 0.2f;
                _targetLocalRotations[i] = Quaternion.Euler(0f, 0f, rotZ);

                if (snapImmediately)
                {
                    card.transform.localPosition = _targetLocalPositions[i];
                    card.transform.localRotation = _targetLocalRotations[i];
                    card.SetStatsOverlayRotation(Quaternion.Euler(0f, 0f, -rotZ));
                }
            }
        }

        private void AnimateCards()
        {
            var t = Mathf.Clamp01(Time.deltaTime * hoverSlerpSpeed);
            for (var i = 0; i < _handCards.Count; i++)
            {
                var card = _handCards[i];
                if (card == null || i >= _targetLocalPositions.Count || i >= _targetLocalRotations.Count)
                {
                    continue;
                }

                card.transform.localPosition = Vector3.Slerp(card.transform.localPosition, _targetLocalPositions[i], t);
                card.transform.localRotation = Quaternion.Slerp(card.transform.localRotation, _targetLocalRotations[i], t);
                card.SetStatsOverlayRotation(Quaternion.Euler(0f, 0f, -card.transform.localEulerAngles.z));
            }
        }

        private void EnsureTargetCapacity(int count)
        {
            while (_targetLocalPositions.Count < count)
            {
                _targetLocalPositions.Add(Vector3.zero);
            }

            while (_targetLocalRotations.Count < count)
            {
                _targetLocalRotations.Add(Quaternion.identity);
            }

            if (_targetLocalPositions.Count > count)
            {
                _targetLocalPositions.RemoveRange(count, _targetLocalPositions.Count - count);
            }

            if (_targetLocalRotations.Count > count)
            {
                _targetLocalRotations.RemoveRange(count, _targetLocalRotations.Count - count);
            }
        }
    }
}
