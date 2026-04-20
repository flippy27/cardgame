using UnityEngine;
using System.Collections.Generic;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Gestiona cartas en mano del jugador local en arco 3D.
    /// </summary>
    public class Hand3DManager : MonoBehaviour
    {
        [Header("Hand Arc")]
        public float arcRadius = 5f;
        public float arcHeight = -2f;
        public float cardSpacing = 0.5f;

        private List<Card3DView> _handCards = new();
        private bool _initialized = false;

        public void Initialize()
        {
            if (_initialized)
                return;

            // Limpio cartas existentes
            foreach (var card in _handCards)
            {
                if (card != null)
                    Destroy(card.gameObject);
            }
            _handCards.Clear();
            _initialized = true;
        }

        public void RefreshHand(CardInHandDto[] handDtos)
        {
            if (handDtos == null || handDtos.Length == 0)
            {
                // Limpiar mano
                foreach (var card in _handCards)
                {
                    if (card != null)
                        Destroy(card.gameObject);
                }
                _handCards.Clear();
                return;
            }

            // Detectar cartas nuevas vs viejas
            var newCardIds = new System.Collections.Generic.HashSet<string>();
            foreach (var dto in handDtos)
            {
                newCardIds.Add(dto.runtimeCardKey);
            }

            var oldCardIds = new System.Collections.Generic.HashSet<string>();
            foreach (var card in _handCards)
            {
                if (card != null && card.CardData != null)
                    oldCardIds.Add(card.CardData.runtimeId);
            }

            // Remover cartas que ya no están
            for (int i = _handCards.Count - 1; i >= 0; i--)
            {
                if (_handCards[i] != null && !newCardIds.Contains(_handCards[i].CardData.runtimeId))
                {
                    Destroy(_handCards[i].gameObject);
                    _handCards.RemoveAt(i);
                }
            }

            // Agregar cartas nuevas
            for (int i = 0; i < handDtos.Length; i++)
            {
                var dto = handDtos[i];

                // Si ya existe, skip
                var existing = _handCards.Find(c => c != null && c.CardData.runtimeId == dto.runtimeCardKey);
                if (existing != null)
                    continue;

                var cardGo = new GameObject($"HandCard_{i}_{dto.displayName}");
                cardGo.transform.SetParent(transform);

                var cardView = cardGo.AddComponent<Card3DView>();
                cardGo.AddComponent<CardTooltip>();

                var boardCard = new BoardCardDto
                {
                    displayName = dto.displayName,
                    attack = dto.attack,
                    maxHealth = dto.health,
                    currentHealth = dto.health,
                    runtimeId = dto.runtimeCardKey,
                    cardId = dto.cardId,
                    ownerIndex = 0,
                    armor = 0,
                    slot = BoardSlot.Front,
                    canAttack = false,
                    unitType = dto.unitType,
                    turnsUntilCanAttack = 0
                };

                cardView.Initialize(boardCard, 0);
                _handCards.Add(cardView);
            }

            RepositionCards();
        }

        private void RepositionCards()
        {
            int count = _handCards.Count;
            float anglePerCard = count > 1 ? 60f / (count - 1) : 0f;

            for (int i = 0; i < _handCards.Count; i++)
            {
                float angle = -30f + (i * anglePerCard);
                angle *= Mathf.Deg2Rad;

                float x = Mathf.Sin(angle) * arcRadius;
                float z = Mathf.Cos(angle) * arcRadius - arcRadius;
                float y = arcHeight;

                _handCards[i].transform.localPosition = new Vector3(x, y, z);
                float rotZ = Mathf.Atan2(x, z) * Mathf.Rad2Deg;
                _handCards[i].transform.localRotation = Quaternion.Euler(0, 0, rotZ);

                // Counter-rotate stats overlay to keep text upright
                var statsOverlay = _handCards[i].transform.Find("StatsOverlay");
                if (statsOverlay != null)
                    statsOverlay.localRotation = Quaternion.Euler(0, 0, -rotZ);
            }
        }

        public Card3DView GetCardAt(int index)
        {
            if (index >= 0 && index < _handCards.Count)
                return _handCards[index];
            return null;
        }

        public int GetHandCount() => _handCards.Count;
    }
}
