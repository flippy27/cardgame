using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.UI.DeckBuilding
{
    /// <summary>
    /// Lists the player's decks with create / edit / delete actions.
    ///
    /// Hierarchy (DeckListPanel — starts inactive):
    ///   DeckListPanel
    ///   ├── Header
    ///   │   ├── TitleText              "My Decks"
    ///   │   └── CloseButton
    ///   ├── CreateDeckButton           opens name-entry and DeckEditPanel
    ///   ├── DeckScrollView
    ///   │   └── Viewport → Content     → deckListContainer
    ///   │       └── [DeckRowItem × N]  → deckRowPrefab
    ///   ├── StatusText
    ///   └── LoadingOverlay
    /// </summary>
    public sealed class DeckListPanel : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private Button closeButton;

        [Header("Actions")]
        [SerializeField] private Button createDeckButton;

        [Header("List")]
        [SerializeField] private Transform deckListContainer;
        [SerializeField] private GameObject deckRowPrefab;

        [Header("Panels")]
        [SerializeField] private DeckEditPanel deckEditPanel;

        [Header("Feedback")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject loadingOverlay;

        private DeckManagementService _deckService;

        public event Action OnClose;

        private void Awake()
        {
            DeckBuilderTextScale.ApplyAutoSize(statusText, DeckBuilderTextScale.Role.Status);

            ApplyKenneySkin();

            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (createDeckButton != null) createDeckButton.onClick.AddListener(OnCreateClicked);

            if (deckEditPanel != null)
            {
                deckEditPanel.Hide();
                deckEditPanel.OnSaved += OnDeckSaved;
            }
        }

        private void OnDestroy()
        {
            if (deckEditPanel != null) deckEditPanel.OnSaved -= OnDeckSaved;
        }

        /// <summary>Temporary Kenney art skin (code-side, no prefab wiring). Null-safe.</summary>
        private void ApplyKenneySkin()
        {
            if (!KenneyUiSkin.Available) return;
            KenneyUiSkin.EnsureWindowBackdrop(this);
            if (deckListContainer != null)
            {
                var viewport = deckListContainer.parent;
                if (viewport != null)
                {
                    var vimg = viewport.GetComponent<Image>();
                    if (vimg != null) KenneyUiSkin.SkinInsetImage(vimg);
                    else KenneyUiSkin.EnsureInsetBackdrop(viewport);
                    KenneyUiSkin.SkinScrollbarsUnder(viewport);
                }
            }
            KenneyUiSkin.SkinButtonWithLabel(closeButton, KenneyUiSkin.ButtonStyle.Icon, "X");
            KenneyUiSkin.SkinButtonWithLabel(createDeckButton, KenneyUiSkin.ButtonStyle.Primary, "New Deck");
            KenneyUiSkin.ApplyFontUnder(this);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            LoadAsync();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            OnClose?.Invoke();
        }

        /// <summary>
        /// Deep-link: open this panel and immediately open the deck editor for the given deckId
        /// (used by the Salvage screen when a card is locked in a deck). Null/empty id just shows
        /// the list. The deck is resolved from the player's decks (cached/fetched).
        /// </summary>
        public async void OpenDeckById(string deckId)
        {
            Show();
            if (string.IsNullOrWhiteSpace(deckId) || deckEditPanel == null) return;

            ServiceLocator.TryResolve<DeckManagementService>(out _deckService);
            if (_deckService == null) return;
            try
            {
                var decks = await _deckService.GetPlayerDecksAsync();
                var deck = decks?.Find(d => (d.deckId ?? d.id) == deckId);
                if (deck != null) deckEditPanel.OpenForEdit(deck);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DeckList] OpenDeckById({deckId}) failed: {ex}");
            }
        }

        private async void LoadAsync()
        {
            SetLoading(true);
            ShowStatus(string.Empty);

            ServiceLocator.TryResolve<DeckManagementService>(out _deckService);
            if (_deckService == null)
            {
                ShowStatus("Deck service unavailable.");
                SetLoading(false);
                return;
            }

            try
            {
                var decks = await _deckService.GetPlayerDecksAsync();
                RebuildList(decks);
            }
            catch (Exception ex)
            {
                ShowStatus($"Error: {ex.Message}");
                Debug.LogError($"[DeckList] {ex}");
            }
            finally { SetLoading(false); }
        }

        private void RebuildList(List<DeckDto> decks)
        {
            if (deckListContainer == null) return;

            // Ensure deck rows stack vertically with spacing (no overlap). Idempotent.
            KenneyUiSkin.EnsureVerticalList(deckListContainer, spacing: 12f, padding: 12);

            foreach (Transform child in deckListContainer) Destroy(child.gameObject);

            if (decks == null || decks.Count == 0)
            {
                ShowStatus("No decks yet. Create one!");
                return;
            }

            foreach (var deck in decks)
            {
                SpawnRow(deck);
            }

            ShowStatus(string.Empty);
        }

        private void SpawnRow(DeckDto deck)
        {
            if (deckRowPrefab == null || deckListContainer == null) return;

            var go = Instantiate(deckRowPrefab, deckListContainer);
            KenneyUiSkin.EnsureRowHeight(go.GetComponent<RectTransform>(), 96f);

            // Try to find text labels in the row
            var texts = go.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (texts.Length > 0)
            {
                texts[0].text = deck.Name;
                DeckBuilderTextScale.ApplyAutoSize(texts[0], DeckBuilderTextScale.Role.CardName);
            }
            if (texts.Length > 1)
            {
                texts[1].text = $"{deck.CardCount} cards";
                DeckBuilderTextScale.Apply(texts[1], DeckBuilderTextScale.Role.Label);
            }

            // Edit button (first button in row)
            var buttons = go.GetComponentsInChildren<Button>(true);
            if (buttons.Length > 0)
            {
                var capturedDeck = deck;
                buttons[0].onClick.AddListener(() => OnEditClicked(capturedDeck));
                if (KenneyUiSkin.Available) KenneyUiSkin.SkinButton(buttons[0], KenneyUiSkin.ButtonStyle.Nav);
            }
            // Delete button (second button if present)
            if (buttons.Length > 1)
            {
                // The current backend has no deck-level delete endpoint.
                buttons[1].interactable = false;
                var label = buttons[1].GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                {
                    label.text = "No delete API";
                    DeckBuilderTextScale.Apply(label, DeckBuilderTextScale.Role.Button);
                }
            }
        }

        private void OnCreateClicked()
        {
            if (deckEditPanel != null)
            {
                deckEditPanel.OpenForCreate();
            }
        }

        private void OnEditClicked(DeckDto deck)
        {
            if (deckEditPanel != null)
            {
                deckEditPanel.OpenForEdit(deck);
            }
        }

        private async void OnDeleteClicked(DeckDto deck)
        {
            if (_deckService == null) return;

            SetLoading(true);
            ShowStatus("Deleting...");
            try
            {
                var ok = await _deckService.DeleteDeckAsync(deck.deckId ?? deck.id);
                if (ok)
                {
                    ShowStatus("Deck deleted.");
                    var decks = await _deckService.GetPlayerDecksAsync(forceRefresh: true);
                    RebuildList(decks);
                }
                else
                {
                    ShowStatus("Delete failed.");
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Error: {ex.Message}");
            }
            finally { SetLoading(false); }
        }

        private void OnDeckSaved()
        {
            _deckService?.InvalidateCache();
            LoadAsync();
        }

        private void ShowStatus(string msg) { if (statusText != null) statusText.text = msg; }
        private void SetLoading(bool on) { if (loadingOverlay != null) loadingOverlay.SetActive(on); }
    }
}
