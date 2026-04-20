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

        private Dictionary<(int playerIndex, BoardSlot slot), Board3DSlot> _slots = new();
        private Dictionary<(int playerIndex, BoardSlot slot), Card3DView> _cardViews = new();
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

        public void SetCardInSlot(int playerIndex, BoardSlot slot, Card3DView cardView)
        {
            var key = (playerIndex, slot);
            _cardViews[key] = cardView;

            if (cardView != null)
            {
                if (!_slots.TryGetValue(key, out var slotComponent))
                {
                    Debug.LogError($"[Board3DManager] Slot not found for P{playerIndex} {slot}");
                    return;
                }

                cardView.transform.SetParent(slotComponent.transform);
                cardView.transform.localPosition = Vector3.zero;
                cardView.transform.localRotation = Quaternion.identity;
                cardView.transform.localScale = Vector3.one;

                Debug.Log($"[Board3DManager] Card {cardView.CardData.displayName} placed in slot P{playerIndex} {slot}");
            }
        }

        public Card3DView GetCardInSlot(int playerIndex, BoardSlot slot)
        {
            _cardViews.TryGetValue((playerIndex, slot), out var card);
            return card;
        }

        public void ClearSlot(int playerIndex, BoardSlot slot)
        {
            var key = (playerIndex, slot);
            if (_cardViews.ContainsKey(key))
            {
                if (_cardViews[key] != null)
                    Destroy(_cardViews[key].gameObject);
                _cardViews.Remove(key);
            }
        }

        public void HighlightSlot(int playerIndex, BoardSlot slot, bool highlight)
        {
            var slotComponent = GetSlot(playerIndex, slot);
            if (slotComponent != null)
            {
                slotComponent.SetHighlight(highlight);
            }
        }
    }
}
