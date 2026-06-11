using UnityEngine;
using System.Collections.Generic;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Gestiona 6 slots 3D en el board (3 por lado).
    /// Posiciona: enemy arriba, local abajo.
    /// Slots: Front (melee), BackLeft (ranged), BackRight (ranged)
    /// </summary>
    public class Board3DManager : MonoBehaviour
    {
        [Header("Slots - Drag from hierarchy")]
        [SerializeField] public Board3DSlot slotEnemyFront;
        [SerializeField] public Board3DSlot slotEnemyBackLeft;
        [SerializeField] public Board3DSlot slotEnemyBackRight;
        [SerializeField] public Board3DSlot slotLocalFront;
        [SerializeField] public Board3DSlot slotLocalBackLeft;
        [SerializeField] public Board3DSlot slotLocalBackRight;

        [Header("Card sizing")]
        [Tooltip("Local scale applied to a card when placed in a slot. Bump up to make played cards fill more of the slot.")]
        [SerializeField] private float boardCardScale = 1.6f;

        private Dictionary<(int playerIndex, BoardSlot slot), Board3DSlot> _slots = new();
        private Dictionary<(int playerIndex, BoardSlot slot), ICardDisplay> _cardViews = new();
        private bool _initialized = false;

        public void Initialize()
        {
            if (_initialized)
                return;

            RegisterAllSlots();
            _initialized = true;
        }

        private void RegisterAllSlots()
        {
            // Register enemy board (player 1)
            RegisterSlot(1, BoardSlot.Front, slotEnemyFront);
            RegisterSlot(1, BoardSlot.BackLeft, slotEnemyBackLeft);
            RegisterSlot(1, BoardSlot.BackRight, slotEnemyBackRight);

            // Register local board (player 0)
            RegisterSlot(0, BoardSlot.Front, slotLocalFront);
            RegisterSlot(0, BoardSlot.BackLeft, slotLocalBackLeft);
            RegisterSlot(0, BoardSlot.BackRight, slotLocalBackRight);

            Debug.Log("[Board3DManager] Registered 6 board slots");
        }

        private void RegisterSlot(int playerIndex, BoardSlot slot, Board3DSlot slotComponent)
        {
            if (slotComponent == null)
            {
                Debug.LogError($"[Board3DManager] Slot P{playerIndex} {slot} is null! Assign in inspector.");
                return;
            }

            var key = (playerIndex, slot);
            _slots[key] = slotComponent;

            // Verify slot component is set up
            slotComponent.PlayerIndex = playerIndex;
            slotComponent.Slot = slot;
        }

        public Board3DSlot GetSlot(int playerIndex, BoardSlot slot)
        {
            _slots.TryGetValue((playerIndex, slot), out var slotComponent);
            return slotComponent;
        }

        public void SetCardInSlot(int playerIndex, BoardSlot slot, ICardDisplay cardView)
        {
            var key = (playerIndex, slot);

            // Remove card from any other slots it might be in
            if (IsAlive(cardView))
            {
                var slots = System.Enum.GetValues(typeof(BoardSlot)) as BoardSlot[];
                foreach (var otherSlot in slots)
                {
                    var otherKey = (playerIndex, otherSlot);
                    if (otherKey != key && _cardViews.TryGetValue(otherKey, out var existingCard))
                    {
                        if (!IsAlive(existingCard) || existingCard == cardView)
                        {
                            _cardViews.Remove(otherKey);
                        }
                    }
                }
            }

            if (_cardViews.TryGetValue(key, out var targetOccupant))
            {
                if (!IsAlive(targetOccupant) || targetOccupant != cardView)
                {
                    DetachCardFromSlot(targetOccupant);
                    _cardViews.Remove(key);
                }
            }

            if (!IsAlive(cardView))
            {
                _cardViews.Remove(key);
                return;
            }

            _cardViews[key] = cardView;

            if (cardView != null)
            {
                if (!_slots.TryGetValue(key, out var slotComponent))
                {
                    Debug.LogError($"[Board3DManager] Slot not found for P{playerIndex} {slot}");
                    return;
                }

                var cardTransform = cardView.GetTransform();
                if (cardTransform == null)
                {
                    _cardViews.Remove(key);
                    return;
                }

                cardTransform.SetParent(slotComponent.transform);
                cardTransform.localPosition = Vector3.zero;
                cardTransform.localRotation = Quaternion.identity;
                cardTransform.localScale = Vector3.one * boardCardScale;

                Debug.Log($"[Board3DManager] Card placed in slot P{playerIndex} {slot}");
            }
        }

        public ICardDisplay GetCardInSlot(int playerIndex, BoardSlot slot)
        {
            var key = (playerIndex, slot);
            _cardViews.TryGetValue(key, out var card);
            if (!IsAlive(card))
            {
                _cardViews.Remove(key);
                return null;
            }

            return card;
        }

        public void ClearSlot(int playerIndex, BoardSlot slot)
        {
            var key = (playerIndex, slot);
            if (_cardViews.ContainsKey(key))
            {
                if (_cardViews[key] != null)
                    Destroy(_cardViews[key].GetGameObject());
                _cardViews.Remove(key);
            }
        }

        public void RemoveCardReference(int playerIndex, BoardSlot slot)
        {
            _cardViews.Remove((playerIndex, slot));
        }

        public void RemoveCardReferenceForView(int playerIndex, ICardDisplay cardView)
        {
            if (cardView == null)
            {
                return;
            }

            var slots = System.Enum.GetValues(typeof(BoardSlot)) as BoardSlot[];
            foreach (var slot in slots)
            {
                var key = (playerIndex, slot);
                if (_cardViews.TryGetValue(key, out var existing) && existing == cardView)
                {
                    _cardViews.Remove(key);
                }
            }
        }

        public void DetachCardFromSlot(ICardDisplay cardView)
        {
            var cardTransform = cardView.GetTransform();
            if (cardTransform != null)
            {
                cardTransform.SetParent(transform, worldPositionStays: true);
            }
        }

        public void MoveCardBetweenSlots(int playerIndex, BoardSlot fromSlot, BoardSlot toSlot, ICardDisplay cardView)
        {
            if (cardView == null)
                return;

            var toSlotComponent = GetSlot(playerIndex, toSlot);
            if (toSlotComponent == null)
            {
                Debug.LogError($"[Board3DManager] Target slot not found P{playerIndex} {toSlot}");
                return;
            }

            // Remove cardView from ALL slots (don't assume it's at fromSlot)
            var slots = System.Enum.GetValues(typeof(BoardSlot)) as BoardSlot[];
            foreach (var slot in slots)
            {
                var key = (playerIndex, slot);
                if (_cardViews.TryGetValue(key, out var existing) && existing == cardView)
                {
                    _cardViews.Remove(key);
                    break;
                }
            }

            var toKey = (playerIndex, toSlot);
            if (_cardViews.TryGetValue(toKey, out var targetOccupant) && targetOccupant != cardView)
            {
                DetachCardFromSlot(targetOccupant);
                _cardViews.Remove(toKey);
            }

            var cardTransform = cardView.GetTransform();
            if (cardTransform == null)
            {
                return;
            }

            cardTransform.SetParent(toSlotComponent.transform, worldPositionStays: true);
            _cardViews[toKey] = cardView;
        }

        public void HighlightSlot(int playerIndex, BoardSlot slot, bool highlight)
        {
            var slotComponent = GetSlot(playerIndex, slot);
            if (slotComponent != null)
            {
                slotComponent.SetHighlight(highlight);
            }
        }

        private static bool IsAlive(ICardDisplay card)
        {
            return card is MonoBehaviour behaviour && behaviour != null;
        }
    }
}
