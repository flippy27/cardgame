using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.UI.DeckBuilding
{
    /// <summary>
    /// Create or edit a deck. Cards are referenced by their catalog cardId.
    /// Validation: 20-60 cards, max 3 copies per cardId.
    ///
    /// Hierarchy (DeckEditPanel — starts inactive):
    ///   DeckEditPanel
    ///   ├── Header
    ///   │   ├── TitleText          "New Deck" / "Edit Deck"
    ///   │   └── CloseButton
    ///   ├── NameRow
    ///   │   └── DeckNameInput      (TMP_InputField)    → deckNameInput
    ///   ├── DeckScrollView
    ///   │   └── Viewport → Content → deckCardsContainer
    ///   │       └── [DeckCardRow × N]  → deckCardRowPrefab
    ///   ├── AddCardsButton         opens CardCatalogPanel in "add to deck" mode
    ///   ├── SaveButton
    ///   ├── ValidationText         → validationText
    ///   ├── StatusText             → statusText
    ///   └── LoadingOverlay
    /// </summary>
    public sealed class DeckEditPanel : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Button closeButton;

        [Header("Deck Name")]
        [SerializeField] private TMP_InputField deckNameInput;

        [Header("Card List")]
        [SerializeField] private Transform deckCardsContainer;
        [SerializeField] private GameObject deckCardRowPrefab;
        [SerializeField] private TextMeshProUGUI cardCountText;

        [Header("Actions")]
        [SerializeField] private Button addCardsButton;
        [SerializeField] private Button saveButton;

        [Header("Catalog for adding cards")]
        [SerializeField] private CardCatalogPanel cardCatalogPanel;

        [Header("Feedback")]
        [SerializeField] private TextMeshProUGUI validationText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject loadingOverlay;

        private DeckManagementService _deckService;
        private DeckDto _editingDeck;
        private bool _isNewDeck;

        // Working copy: cardId → count
        private readonly Dictionary<string, int> _cardCounts = new();

        public event Action OnSaved;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (saveButton != null) saveButton.onClick.AddListener(OnSaveClicked);
            if (addCardsButton != null) addCardsButton.onClick.AddListener(OnAddCardsClicked);

            if (cardCatalogPanel != null)
            {
                cardCatalogPanel.Hide();
                cardCatalogPanel.OnCardSelectedForDeck += OnCatalogCardSelected;
            }
        }

        private void OnDestroy()
        {
            if (cardCatalogPanel != null) cardCatalogPanel.OnCardSelectedForDeck -= OnCatalogCardSelected;
        }

        public void OpenForCreate()
        {
            _isNewDeck = true;
            _editingDeck = null;
            _cardCounts.Clear();

            if (titleText != null) titleText.text = "New Deck";
            if (deckNameInput != null) deckNameInput.text = string.Empty;

            gameObject.SetActive(true);
            RebuildCardList();
            ShowStatus(string.Empty);
        }

        public void OpenForEdit(DeckDto deck)
        {
            _isNewDeck = false;
            _editingDeck = deck;
            _cardCounts.Clear();

            if (titleText != null) titleText.text = "Edit Deck";
            if (deckNameInput != null) deckNameInput.text = deck.Name ?? string.Empty;

            // Build working copy from deck.cardIds
            if (deck.cardIds != null)
            {
                foreach (var id in deck.cardIds)
                {
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    _cardCounts.TryGetValue(id, out var n);
                    _cardCounts[id] = n + 1;
                }
            }

            gameObject.SetActive(true);
            RebuildCardList();
            ShowStatus(string.Empty);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // Called by CardCatalogPanel when user picks a card to add
        private void OnCatalogCardSelected(ServerCardDefinition card)
        {
            if (card == null) return;

            _cardCounts.TryGetValue(card.cardId, out var current);
            if (current >= DeckManagementService.MaxCopiesPerCard)
            {
                ShowValidation($"Max {DeckManagementService.MaxCopiesPerCard} copies of '{card.displayName ?? card.cardId}'");
                return;
            }
            _cardCounts[card.cardId] = current + 1;
            RebuildCardList();
        }

        private void RemoveCard(string cardId)
        {
            if (!_cardCounts.ContainsKey(cardId)) return;
            _cardCounts[cardId]--;
            if (_cardCounts[cardId] <= 0) _cardCounts.Remove(cardId);
            RebuildCardList();
        }

        private void RebuildCardList()
        {
            if (deckCardsContainer == null) return;
            foreach (Transform child in deckCardsContainer) Destroy(child.gameObject);

            int total = _cardCounts.Values.Sum();

            foreach (var kvp in _cardCounts)
            {
                SpawnCardRow(kvp.Key, kvp.Value);
            }

            if (cardCountText != null)
                cardCountText.text = $"{total} / {DeckManagementService.MaxCards} cards";

            ValidateDeck();
        }

        private void SpawnCardRow(string cardId, int count)
        {
            if (deckCardRowPrefab == null || deckCardsContainer == null) return;

            var go = Instantiate(deckCardRowPrefab, deckCardsContainer);

            var texts = go.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (texts.Length > 0) texts[0].text = cardId;
            if (texts.Length > 1) texts[1].text = $"x{count}";

            var buttons = go.GetComponentsInChildren<Button>(true);
            if (buttons.Length > 0)
            {
                var capturedId = cardId;
                buttons[0].onClick.AddListener(() => RemoveCard(capturedId));
            }
        }

        private void ValidateDeck()
        {
            int total = _cardCounts.Values.Sum();
            bool valid = _deckService?.ValidateCardList(BuildCardIdList(), out _) ?? (total >= DeckManagementService.MinCards && total <= DeckManagementService.MaxCards);

            if (saveButton != null) saveButton.interactable = valid;

            if (total < DeckManagementService.MinCards)
                ShowValidation($"Need at least {DeckManagementService.MinCards} cards ({DeckManagementService.MinCards - total} more)");
            else if (total > DeckManagementService.MaxCards)
                ShowValidation($"Too many cards (remove {total - DeckManagementService.MaxCards})");
            else
                ShowValidation(string.Empty);
        }

        private List<string> BuildCardIdList()
        {
            var result = new List<string>();
            foreach (var kvp in _cardCounts)
                for (int i = 0; i < kvp.Value; i++)
                    result.Add(kvp.Key);
            return result;
        }

        private async void OnSaveClicked()
        {
            ServiceLocator.TryResolve<DeckManagementService>(out _deckService);
            if (_deckService == null)
            {
                ShowStatus("Service unavailable.");
                return;
            }

            var name = deckNameInput != null ? deckNameInput.text.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                ShowValidation("Deck name required.");
                return;
            }

            var cardList = BuildCardIdList();
            if (!_deckService.ValidateCardList(cardList, out var validMsg))
            {
                ShowValidation(validMsg);
                return;
            }

            SetLoading(true);
            ShowStatus(_isNewDeck ? "Creating deck..." : "Saving deck...");
            try
            {
                DeckDto result;
                if (_isNewDeck)
                {
                    result = await _deckService.CreateDeckAsync(name, cardList);
                }
                else
                {
                    var deckId = _editingDeck?.deckId ?? _editingDeck?.id;
                    result = await _deckService.UpdateDeckAsync(deckId, name, cardList);
                }

                if (result != null)
                {
                    ShowStatus("Saved!");
                    OnSaved?.Invoke();
                    await Task.Delay(600);
                    Hide();
                }
                else
                {
                    ShowStatus("Save failed — server returned no response.");
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Error: {ex.Message}");
                Debug.LogError($"[DeckEdit] Save failed: {ex}");
            }
            finally { SetLoading(false); }
        }

        private void OnAddCardsClicked()
        {
            if (cardCatalogPanel != null)
            {
                cardCatalogPanel.ShowForDeckEdit();
            }
        }

        private void ShowStatus(string msg) { if (statusText != null) statusText.text = msg; }
        private void ShowValidation(string msg) { if (validationText != null) validationText.text = msg; }
        private void SetLoading(bool on) { if (loadingOverlay != null) loadingOverlay.SetActive(on); }
    }
}
