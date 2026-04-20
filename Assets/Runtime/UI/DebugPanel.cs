#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Debug menu for modifying game state at runtime.
    /// Toggle with Ctrl+D
    /// </summary>
    public sealed class DebugPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Button closeButton;
        [SerializeField] private Transform contentRoot;

        private BattleScreenPresenter _presenter;
        private DuelRuntime _duelRuntime;
        private DuelState _duelState;
        private bool _isVisible;

        private void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Toggle);
            }

            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.D) && UnityEngine.Input.GetKey(KeyCode.LeftControl))
            {
                Toggle();
            }
        }

        public void Initialize(BattleScreenPresenter presenter, DuelRuntime duelRuntime, DuelState duelState)
        {
            _presenter = presenter;
            _duelRuntime = duelRuntime;
            _duelState = duelState;
            gameObject.SetActive(true);
        }

        public void Toggle()
        {
            _isVisible = !_isVisible;
            canvasGroup.alpha = _isVisible ? 1f : 0f;
            canvasGroup.blocksRaycasts = _isVisible;
            canvasGroup.interactable = _isVisible;
        }

        public void ModifyPlayerHP(int playerIndex, int amount)
        {
            if (_duelState == null) return;
            var player = _duelState.GetPlayer(playerIndex);
            if (player != null)
            {
                player.HeroHealth += amount;
                player.HeroHealth = Mathf.Max(0, player.HeroHealth);
                Debug.Log($"[DEBUG] Player {playerIndex} HP: {player.HeroHealth}");
            }
        }

        public void ModifyPlayerMana(int playerIndex, int amount)
        {
            if (_duelState == null) return;
            var player = _duelState.GetPlayer(playerIndex);
            if (player != null)
            {
                player.Mana += amount;
                player.Mana = Mathf.Max(0, player.Mana);
                Debug.Log($"[DEBUG] Player {playerIndex} Mana: {player.Mana}");
            }
        }

        public void ModifyPlayerMaxMana(int playerIndex, int amount)
        {
            if (_duelState == null) return;
            var player = _duelState.GetPlayer(playerIndex);
            if (player != null)
            {
                player.MaxMana += amount;
                player.MaxMana = Mathf.Max(1, player.MaxMana);
                player.Mana = Mathf.Min(player.Mana, player.MaxMana);
                Debug.Log($"[DEBUG] Player {playerIndex} MaxMana: {player.MaxMana}");
            }
        }

        public void ModifyCardHealth(int playerIndex, BoardSlot slot, int amount)
        {
            if (_duelState == null) return;
            var player = _duelState.GetPlayer(playerIndex);
            if (player != null)
            {
                var card = player.FindOccupant(slot);
                if (card != null)
                {
                    card.CurrentHealth += amount;
                    card.CurrentHealth = Mathf.Max(0, card.CurrentHealth);
                    Debug.Log($"[DEBUG] Card {card.DisplayName} Health: {card.CurrentHealth}");
                }
            }
        }

        public void ModifyCardAttack(int playerIndex, BoardSlot slot, int amount)
        {
            if (_duelState == null) return;
            var player = _duelState.GetPlayer(playerIndex);
            if (player != null)
            {
                var card = player.FindOccupant(slot);
                if (card != null)
                {
                    card.Attack += amount;
                    card.Attack = Mathf.Max(0, card.Attack);
                    Debug.Log($"[DEBUG] Card {card.DisplayName} Attack: {card.Attack}");
                }
            }
        }

        public void ModifyCardArmor(int playerIndex, BoardSlot slot, int amount)
        {
            if (_duelState == null) return;
            var player = _duelState.GetPlayer(playerIndex);
            if (player != null)
            {
                var card = player.FindOccupant(slot);
                if (card != null)
                {
                    card.Armor += amount;
                    card.Armor = Mathf.Max(0, card.Armor);
                    Debug.Log($"[DEBUG] Card {card.DisplayName} Armor: {card.Armor}");
                }
            }
        }

        public void ApplyPoisonToCard(int playerIndex, BoardSlot slot, int stacks)
        {
            if (_duelState == null) return;
            var player = _duelState.GetPlayer(playerIndex);
            if (player != null)
            {
                var card = player.FindOccupant(slot);
                if (card != null)
                {
                    card.PoisonStacks = stacks;
                    Debug.Log($"[DEBUG] Card {card.DisplayName} Poison: {stacks} stacks");
                }
            }
        }

        public void ApplyStunToCard(int playerIndex, BoardSlot slot, bool stunned)
        {
            if (_duelState == null) return;
            var player = _duelState.GetPlayer(playerIndex);
            if (player != null)
            {
                var card = player.FindOccupant(slot);
                if (card != null)
                {
                    card.Stunned = stunned;
                    Debug.Log($"[DEBUG] Card {card.DisplayName} Stunned: {stunned}");
                }
            }
        }

        public void ApplyShieldToCard(int playerIndex, BoardSlot slot, bool shield)
        {
            if (_duelState == null) return;
            var player = _duelState.GetPlayer(playerIndex);
            if (player != null)
            {
                var card = player.FindOccupant(slot);
                if (card != null)
                {
                    card.HasShield = shield;
                    Debug.Log($"[DEBUG] Card {card.DisplayName} Shield: {shield}");
                }
            }
        }

        public void KillCard(int playerIndex, BoardSlot slot)
        {
            if (_duelState == null) return;
            var player = _duelState.GetPlayer(playerIndex);
            if (player != null)
            {
                var card = player.FindOccupant(slot);
                if (card != null)
                {
                    card.CurrentHealth = 0;
                    Debug.Log($"[DEBUG] Card {card.DisplayName} killed");
                }
            }
        }

        public void ClearBoard(int playerIndex)
        {
            if (_duelState == null) return;
            var player = _duelState.GetPlayer(playerIndex);
            if (player != null)
            {
                foreach (var slot in player.Board)
                {
                    if (slot.Occupant != null)
                    {
                        slot.Occupant.CurrentHealth = 0;
                    }
                }
                Debug.Log($"[DEBUG] Player {playerIndex} board cleared");
            }
        }

        public void EndTurnForPlayer(int playerIndex)
        {
            if (_duelRuntime == null) return;
            if (_duelState.ActivePlayerIndex == playerIndex)
            {
                _duelRuntime.TryEndTurn(playerIndex);
                Debug.Log($"[DEBUG] Player {playerIndex} ended turn");
            }
        }

        public void PrintGameState()
        {
            if (_duelState == null) return;

            Debug.Log("=== GAME STATE ===");
            Debug.Log($"Turn: {_duelState.TurnNumber}");
            Debug.Log($"Active Player: {_duelState.ActivePlayerIndex}");

            for (int i = 0; i < 2; i++)
            {
                var player = _duelState.GetPlayer(i);
                if (player != null)
                {
                    Debug.Log($"\n--- Player {i} ---");
                    Debug.Log($"Hero HP: {player.HeroHealth}");
                    Debug.Log($"Mana: {player.Mana}/{player.MaxMana}");
                    Debug.Log($"Hand Cards: {player.Hand.Count}");
                    Debug.Log($"Deck Cards: {player.Deck.Count}");

                    foreach (var slot in player.Board)
                    {
                        if (slot.Occupant != null)
                        {
                            var card = slot.Occupant;
                            Debug.Log($"  {slot.Slot}: {card.DisplayName} (HP: {card.CurrentHealth}/{card.MaxHealth}, ATK: {card.Attack}, ARM: {card.Armor})");
                            if (card.PoisonStacks > 0) Debug.Log($"    - Poison: {card.PoisonStacks}");
                            if (card.Stunned) Debug.Log($"    - Stunned");
                            if (card.HasShield) Debug.Log($"    - Shield");
                        }
                        else
                        {
                            Debug.Log($"  {slot.Slot}: Empty");
                        }
                    }
                }
            }
        }
    }
}
#endif
