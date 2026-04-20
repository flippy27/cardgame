using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Data;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// UI panel for deck selection before matchmaking.
    /// Loads player's decks from API and allows selection.
    /// </summary>
    public sealed class DeckSelectionPanel : MonoBehaviour
    {
        [SerializeField] private GameObject deckItemPrefab;
        [SerializeField] private Transform deckListContent;
        [SerializeField] private Button playButton;
        [SerializeField] private Button backButton;
        [SerializeField] private TextMeshProUGUI selectedDeckNameText;
        [SerializeField] private TextMeshProUGUI cardCountText;
        [SerializeField] private CanvasGroup loadingSpinner;

        private List<DeckDefinition> _availableDecks = new();
        private DeckDefinition _selectedDeck;
        private AuthService _authService;
        private CardApiClient _cardApiClient;

        public event Action<DeckDefinition> OnDeckSelected;
        public event Action OnBackPressed;

        private void Awake()
        {
            if (playButton != null)
                playButton.onClick.AddListener(OnPlayClicked);

            if (backButton != null)
                backButton.onClick.AddListener(OnBackClicked);
        }

        public async Task LoadDecksAsync(AuthService authService)
        {
            _authService = authService;

            try
            {
                ShowLoading(true);

                var baseUrl = ConfigManager.GetApiBaseUrl();
                _cardApiClient = new CardApiClient(baseUrl);

                // Get player's decks
                var decks = await _cardApiClient.FetchPlayerDecks(authService.CurrentPlayerId);

                if (decks == null || decks.Count == 0)
                {
                    GameLogger.Warning("DeckSelection", "No decks found for player");
                    ShowError("No decks found. Create one first.");
                    return;
                }

                _availableDecks = decks;
                PopulateDeckList();

                // Auto-select first deck
                if (_availableDecks.Count > 0)
                {
                    SelectDeck(_availableDecks[0]);
                }

                GameLogger.Info("DeckSelection", $"Loaded {decks.Count} decks");
            }
            catch (Exception ex)
            {
                GameLogger.Error("DeckSelection", $"Failed to load decks: {ex.Message}");
                ShowError($"Failed to load decks: {ex.Message}");
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private void PopulateDeckList()
        {
            // Clear existing items
            foreach (Transform child in deckListContent)
            {
                Destroy(child.gameObject);
            }

            // Create deck items
            foreach (var deck in _availableDecks)
            {
                var itemGo = Instantiate(deckItemPrefab, deckListContent);
                var deckItemUI = itemGo.GetComponent<DeckSelectionItemUI>();

                if (deckItemUI != null)
                {
                    deckItemUI.Initialize(deck, () => SelectDeck(deck));
                }
            }

            GameLogger.Info("DeckSelection", $"Populated deck list with {_availableDecks.Count} items");
        }

        private void SelectDeck(DeckDefinition deck)
        {
            _selectedDeck = deck;

            // Update UI
            if (selectedDeckNameText != null)
                selectedDeckNameText.text = deck.displayName;

            if (cardCountText != null)
                cardCountText.text = $"{deck.GetTotalCards()} cards";

            // Enable play button
            if (playButton != null)
                playButton.interactable = true;

            GameLogger.Info("DeckSelection", $"Selected deck: {deck.displayName}");
            OnDeckSelected?.Invoke(deck);
        }

        private void OnPlayClicked()
        {
            if (_selectedDeck == null)
            {
                GameLogger.Warning("DeckSelection", "No deck selected");
                return;
            }

            GameLogger.Info("DeckSelection", $"Starting matchmaking with deck: {_selectedDeck.deckId}");
            OnDeckSelected?.Invoke(_selectedDeck);
        }

        private void OnBackClicked()
        {
            GameLogger.Info("DeckSelection", "Back pressed");
            OnBackPressed?.Invoke();
        }

        private void ShowLoading(bool show)
        {
            if (loadingSpinner != null)
            {
                loadingSpinner.alpha = show ? 1f : 0f;
                loadingSpinner.blocksRaycasts = show;
            }
        }

        private void ShowError(string message)
        {
            Debug.LogError($"[DeckSelection] {message}");
            // TODO: Show error dialog to user
        }

        public DeckDefinition GetSelectedDeck() => _selectedDeck;
    }

    /// <summary>
    /// UI item for a single deck in the selection list.
    /// </summary>
    public sealed class DeckSelectionItemUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI cardCountText;
        [SerializeField] private Button selectButton;
        [SerializeField] private Image highlightImage;

        private DeckDefinition _deckData;
        private Action _onSelected;

        public void Initialize(DeckDefinition deck, Action onSelected)
        {
            _deckData = deck;
            _onSelected = onSelected;

            if (nameText != null)
                nameText.text = deck.displayName;

            if (cardCountText != null)
                cardCountText.text = $"{deck.GetTotalCards()} cards";

            if (selectButton != null)
                selectButton.onClick.AddListener(OnSelectClicked);
        }

        public void SetHighlighted(bool highlighted)
        {
            if (highlightImage != null)
                highlightImage.enabled = highlighted;
        }

        private void OnSelectClicked()
        {
            GameLogger.Info("DeckItemUI", $"Selected: {_deckData.displayName}");
            _onSelected?.Invoke();
        }
    }
}
