#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Data;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Debug menu for modifying game state at runtime.
    /// Toggle with Cmd+T (Mac) or Ctrl+T (Windows)
    /// Entirely code-generated UI.
    /// </summary>
    public sealed class DebugPanel : MonoBehaviour
    {
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private bool _isVisible;

        private DuelRuntime _duelRuntime;
        private DuelState _duelState;
        private Hand3DManager _hand3DManager;
        private Board3DManager _board3DManager;

        private void Awake()
        {
            gameObject.name = "DebugPanel";
            gameObject.SetActive(true);
            CreateUI();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            bool isMac = Application.platform == RuntimePlatform.OSXEditor;
            bool modPressed = isMac ?
                (keyboard.leftMetaKey.isPressed || keyboard.rightMetaKey.isPressed) :
                (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);

            if (keyboard.tKey.wasPressedThisFrame && modPressed)
            {
                Toggle();
            }
        }

        public void Initialize(DuelRuntime duelRuntime, DuelState duelState, Hand3DManager hand3D = null, Board3DManager board3D = null)
        {
            _duelRuntime = duelRuntime;
            _duelState = duelState;
            _hand3DManager = hand3D;
            _board3DManager = board3D;
        }

        private void CreateUI()
        {
            // Create Canvas
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 9999;

            var canvasScaler = gameObject.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);

            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            // Create Panel background
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(transform);
            var panelRect = panelGo.AddComponent<RectTransform>();
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(400, 600);
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.zero;

            var panelImage = panelGo.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

            var panelLayout = panelGo.AddComponent<LayoutElement>();
            panelLayout.preferredWidth = 400;
            panelLayout.preferredHeight = 600;

            // Create scroll view for buttons
            var scrollGo = new GameObject("ScrollView");
            scrollGo.transform.SetParent(panelGo.transform);
            var scrollRect = scrollGo.AddComponent<RectTransform>();
            scrollRect.anchoredPosition = Vector2.zero;
            scrollRect.sizeDelta = new Vector2(-20, -60);
            scrollRect.offsetMin = new Vector2(10, 10);
            scrollRect.offsetMax = new Vector2(-10, -50);

            var scrollComp = scrollGo.AddComponent<ScrollRect>();
            scrollComp.vertical = true;
            scrollComp.horizontal = false;

            var scrollImage = scrollGo.AddComponent<Image>();
            scrollImage.color = new Color(0.05f, 0.05f, 0.05f, 1f);

            // Create content area
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(scrollGo.transform);
            var contentRect = contentGo.AddComponent<RectTransform>();
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(360, 0);

            var contentLayout = contentGo.AddComponent<VerticalLayoutGroup>();
            contentLayout.childForceExpandHeight = false;
            contentLayout.childForceExpandWidth = true;
            contentLayout.spacing = 5;
            contentLayout.padding = new RectOffset(5, 5, 5, 5);

            var contentFitter = contentGo.AddComponent<LayoutElement>();
            contentFitter.preferredWidth = 360;

            scrollComp.content = contentRect;

            // Add close button at top
            AddCloseButton(panelGo);

            // Add debug buttons
            AddButton(contentGo, "Draw Card (Local)", () => new DebugGameManager().DrawCardForLocalPlayer());
            AddButton(contentGo, "Draw Card (Enemy)", () => new DebugGameManager().DrawCardForEnemy());
            AddButton(contentGo, "Add 10 HP (Local)", () => new DebugGameManager().ModifyLocalPlayerHP(10));
            AddButton(contentGo, "Kill Local Front", () => new DebugGameManager().KillLocalCardAt(BoardSlot.Front));
            AddButton(contentGo, "Kill Enemy Front", () => new DebugGameManager().KillEnemyCardAt(BoardSlot.Front));
            AddButton(contentGo, "Clear Local Board", () => new DebugGameManager().ClearLocalBoard());
            AddButton(contentGo, "Clear Enemy Board", () => new DebugGameManager().ClearEnemyBoard());
            AddButton(contentGo, "Print State", () => new DebugGameManager().PrintGameState());
            AddButton(contentGo, "End Local Turn", () => new DebugGameManager().EndLocalPlayerTurn());

            // Rebuild layout
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        }

        private void AddCloseButton(GameObject parent)
        {
            var btnGo = new GameObject("CloseButton");
            btnGo.transform.SetParent(parent.transform);
            var btnRect = btnGo.AddComponent<RectTransform>();
            btnRect.anchoredPosition = Vector2.zero;
            btnRect.sizeDelta = new Vector2(-20, 40);
            btnRect.offsetMin = new Vector2(10, -50);
            btnRect.offsetMax = new Vector2(-10, -10);

            var btnImage = btnGo.AddComponent<Image>();
            btnImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            var btn = btnGo.AddComponent<Button>();
            btn.onClick.AddListener(Toggle);

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(btnGo.transform);
            var txtRect = txtGo.AddComponent<RectTransform>();
            txtRect.anchoredPosition = Vector2.zero;
            txtRect.sizeDelta = Vector2.zero;

            var txt = txtGo.AddComponent<TextMeshProUGUI>();
            txt.text = "Close";
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontSize = 20;
        }

        private void AddButton(GameObject parent, string label, System.Action onClick)
        {
            var btnGo = new GameObject("Btn_" + label);
            btnGo.transform.SetParent(parent.transform);
            var btnRect = btnGo.AddComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(-10, 40);

            var btnImage = btnGo.AddComponent<Image>();
            btnImage.color = new Color(0.3f, 0.3f, 0.5f, 1f);

            var btn = btnGo.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());

            var layoutElem = btnGo.AddComponent<LayoutElement>();
            layoutElem.preferredHeight = 40;

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(btnGo.transform);
            var txtRect = txtGo.AddComponent<RectTransform>();
            txtRect.anchoredPosition = Vector2.zero;
            txtRect.sizeDelta = Vector2.zero;

            var txt = txtGo.AddComponent<TextMeshProUGUI>();
            txt.text = label;
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontSize = 16;
        }

        public void Toggle()
        {
            _isVisible = !_isVisible;
            _canvasGroup.alpha = _isVisible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = _isVisible;
            _canvasGroup.interactable = _isVisible;
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

        public void DrawCardsToHand(int playerIndex, int count)
        {
            if (_duelState == null) return;
            var player = _duelState.GetPlayer(playerIndex);
            if (player == null || player.Deck.Count == 0)
            {
                Debug.Log($"[DEBUG] Player {playerIndex} deck is empty");
                return;
            }

            for (int i = 0; i < count && player.Deck.Count > 0; i++)
            {
                var cardDef = player.Deck[0];
                player.Deck.RemoveAt(0);
                player.Hand.Add(new HandCardRuntime
                {
                    RuntimeHandKey = System.Guid.NewGuid().ToString("N"),
                    Definition = cardDef
                });
                Debug.Log($"[DEBUG] Drew {cardDef.displayName} to Player {playerIndex} hand");
            }
        }

        public void RemoveCardFromHand(int playerIndex, int handIndex)
        {
            if (_duelState == null) return;
            var player = _duelState.GetPlayer(playerIndex);
            if (player == null || handIndex < 0 || handIndex >= player.Hand.Count)
            {
                Debug.Log($"[DEBUG] Invalid hand index {handIndex} for Player {playerIndex}");
                return;
            }

            var card = player.Hand[handIndex];
            player.Hand.RemoveAt(handIndex);
            Debug.Log($"[DEBUG] Removed {card.Definition.displayName} from Player {playerIndex} hand");
        }

        public void RemoveCardFromBoardWithAnimation(int playerIndex, BoardSlot slot)
        {
            if (_duelState == null) return;
            var player = _duelState.GetPlayer(playerIndex);
            if (player == null)
            {
                Debug.Log($"[DEBUG] Player {playerIndex} not found");
                return;
            }

            var card = player.FindOccupant(slot);
            if (card == null)
            {
                Debug.Log($"[DEBUG] No card at {slot} for Player {playerIndex}");
                return;
            }

            card.CurrentHealth = 0;
            Debug.Log($"[DEBUG] Killed {card.DisplayName} at {slot} with animation");
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
