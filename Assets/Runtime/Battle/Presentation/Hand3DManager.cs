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
        public float arcAngle = 60f;
        public float arcDepth = 1f;
        public float cardSpacing = 0.5f;

        [Header("Debug")]
        public bool debugArcMode = false;

        [Header("Prefab")]
        [SerializeField] private GameObject handCardPrefab;

        private List<Card3DView> _handCards = new();
        private bool _initialized = false;
        private float _lastArcRadius;
        private float _lastArcHeight;
        private float _lastArcAngle;
        private float _lastArcDepth;
        private float _lastCardSpacing;

        private void Update()
        {
            if (debugArcMode && _handCards.Count > 0)
            {
                // Detectar cambios en variables del arco
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
                    RepositionCards();
                }
            }
        }

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

            // Inicializar valores de debug
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

                var cardGo = Instantiate(handCardPrefab, transform);
                cardGo.name = $"HandCard_{i}_{dto.displayName}";

                var cardView = cardGo.GetComponent<Card3DView>();
                if (cardView == null)
                    cardView = cardGo.AddComponent<Card3DView>();

                if (cardGo.GetComponent<CardTooltip>() == null)
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
            float anglePerCard = count > 1 ? arcAngle / (count - 1) : 0f;
            float absRadius = Mathf.Abs(arcRadius);
            bool radiusNegative = arcRadius < 0;

            for (int i = 0; i < _handCards.Count; i++)
            {
                float angle = -(arcAngle / 2f) + (i * anglePerCard);
                angle *= Mathf.Deg2Rad;

                // X: solo arco (sin distributed por arcAngle)
                float x = Mathf.Sin(angle) * absRadius;

                // Y: arco vertical con pivote abajo (fan/abanico)
                // Centro (angle=0) más alto, esquinas más bajas
                float y = arcHeight + Mathf.Cos(angle) * arcDepth;

                // Z: profundidad para crear efecto de apilamiento (left adelante, right atrás)
                float z = -(i - (count - 1) / 2f) * cardSpacing;

                // Negar X si arcRadius es negativo para mantener orden visual
                if (radiusNegative)
                    x = -x;

                _handCards[i].transform.localPosition = new Vector3(x, y, z);

                // Rotación MINIMAL: solo 20% del ángulo
                float rotZ = -angle * Mathf.Rad2Deg * 0.2f;
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
