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
    /// Validation follows the backend: 20-30 cards, max 3 copies per cardId.
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
        private PlayerCardCollectionService _collectionService;
        private DeckDto _editingDeck;
        private bool _isNewDeck;

        // Working copy: cardId → count
        private readonly Dictionary<string, int> _cardCounts = new();
        private readonly Dictionary<string, int> _ownedCounts = new();
        private readonly Dictionary<string, string> _cardNames = new();

        public event Action OnSaved;

        private void Awake()
        {
            ApplyTextScale();
            ApplyKenneySkin();

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

        /// <summary>Temporary Kenney art skin (code-side, no prefab wiring). Null-safe.</summary>
        private void ApplyKenneySkin()
        {
            if (!KenneyUiSkin.Available) return;
            KenneyUiSkin.EnsureWindowBackdrop(this);
            if (deckCardsContainer != null)
            {
                var viewport = deckCardsContainer.parent;
                if (viewport != null)
                {
                    var vimg = viewport.GetComponent<Image>();
                    if (vimg != null) KenneyUiSkin.SkinInsetImage(vimg);
                    else KenneyUiSkin.EnsureInsetBackdrop(viewport);
                    KenneyUiSkin.SkinScrollbarsUnder(viewport);
                }
            }
            if (deckNameInput != null) KenneyUiSkin.SkinInputImage(deckNameInput.GetComponent<Image>());
            KenneyUiSkin.SkinButtonWithLabel(closeButton, KenneyUiSkin.ButtonStyle.Icon, "X");
            KenneyUiSkin.SkinButtonWithLabel(addCardsButton, KenneyUiSkin.ButtonStyle.Primary, "Add Cards");
            KenneyUiSkin.SkinButtonWithLabel(saveButton, KenneyUiSkin.ButtonStyle.Primary, "Save");
            KenneyUiSkin.ApplyFontUnder(this);
        }

        private void ApplyTextScale()
        {
            DeckBuilderTextScale.Apply(titleText, DeckBuilderTextScale.Role.Header);
            DeckBuilderTextScale.Apply(cardCountText, DeckBuilderTextScale.Role.Label);
            DeckBuilderTextScale.ApplyAutoSize(validationText, DeckBuilderTextScale.Role.Status);
            DeckBuilderTextScale.ApplyAutoSize(statusText, DeckBuilderTextScale.Role.Status);
        }

        public void OpenForCreate()
        {
            _isNewDeck = true;
            _editingDeck = null;
            _cardCounts.Clear();

            if (titleText != null) titleText.text = "New Deck";
            if (deckNameInput != null) deckNameInput.text = string.Empty;

            gameObject.SetActive(true);
            LoadOwnedCardsAsync();
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
            LoadOwnedCardsAsync();
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
            if (string.IsNullOrWhiteSpace(card.cardId))
            {
                ShowValidation("Selected card has no cardId from server.");
                return;
            }

            _cardCounts.TryGetValue(card.cardId, out var current);
            _ownedCounts.TryGetValue(card.cardId, out var owned);
            if (_ownedCounts.Count > 0 && owned <= 0)
            {
                ShowValidation($"You do not own '{card.displayName ?? card.cardId}'. Craft it first.");
                return;
            }

            if (_ownedCounts.Count > 0 && current >= owned)
            {
                ShowValidation($"Only {owned} owned copies of '{card.displayName ?? card.cardId}' available.");
                return;
            }

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

            // Ensure deck rows stack vertically with spacing (no overlap). Idempotent.
            KenneyUiSkin.EnsureVerticalList(deckCardsContainer, spacing: 10f, padding: 12);

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
            KenneyUiSkin.EnsureRowHeight(go.GetComponent<RectTransform>(), 84f);

            var texts = go.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (texts.Length > 0)
            {
                texts[0].text = _cardNames.TryGetValue(cardId, out var name) && !string.IsNullOrWhiteSpace(name)
                    ? name
                    : cardId;
                DeckBuilderTextScale.ApplyAutoSize(texts[0], DeckBuilderTextScale.Role.CardName);
            }
            if (texts.Length > 1)
            {
                texts[1].text = $"x{count}";
                DeckBuilderTextScale.Apply(texts[1], DeckBuilderTextScale.Role.Label);
            }

            var buttons = go.GetComponentsInChildren<Button>(true);
            if (buttons.Length > 0)
            {
                var capturedId = cardId;
                buttons[0].onClick.AddListener(() => RemoveCard(capturedId));
                if (KenneyUiSkin.Available) KenneyUiSkin.SkinButton(buttons[0], KenneyUiSkin.ButtonStyle.Icon);
            }
        }

        private void ValidateDeck()
        {
            int total = _cardCounts.Values.Sum();
            bool valid = _deckService?.ValidateCardList(BuildCardIdList(), out _) ?? (total >= DeckManagementService.MinCards && total <= DeckManagementService.MaxCards);
            var hasOwnershipError = !ValidateOwnedCopies(out var ownershipMessage);
            if (hasOwnershipError)
            {
                valid = false;
            }

            if (saveButton != null) saveButton.interactable = valid;

            if (hasOwnershipError)
                ShowValidation(ownershipMessage);
            else if (total < DeckManagementService.MinCards)
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

        private bool ValidateOwnedCopies(out string message)
        {
            message = string.Empty;
            if (_ownedCounts.Count == 0)
            {
                return true;
            }

            foreach (var kvp in _cardCounts)
            {
                _ownedCounts.TryGetValue(kvp.Key, out var owned);
                if (kvp.Value <= owned)
                {
                    continue;
                }

                var label = _cardNames.TryGetValue(kvp.Key, out var name) && !string.IsNullOrWhiteSpace(name)
                    ? name
                    : kvp.Key;
                message = $"Deck uses {kvp.Value} copies of '{label}', but you only own {owned}.";
                return false;
            }

            return true;
        }

        private async void LoadOwnedCardsAsync()
        {
            ServiceLocator.TryResolve<PlayerCardCollectionService>(out _collectionService);
            if (_collectionService == null)
            {
                return;
            }

            try
            {
                var summary = await _collectionService.GetSummaryAsync();
                _ownedCounts.Clear();
                _cardNames.Clear();

                if (summary?.cards != null)
                {
                    foreach (var entry in summary.cards)
                    {
                        if (entry == null || string.IsNullOrWhiteSpace(entry.cardId))
                        {
                            continue;
                        }

                        _ownedCounts[entry.cardId] = Mathf.Max(0, entry.ownedCopies);
                        _cardNames[entry.cardId] = entry.displayName ?? entry.cardId;
                    }
                }

                RebuildCardList();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DeckEdit] Unable to load owned card counts: {ex.Message}");
            }
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

            if (!ValidateOwnedCopies(out var ownershipMsg))
            {
                ShowValidation(ownershipMsg);
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
