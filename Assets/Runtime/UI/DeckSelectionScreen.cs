using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Networking.ApiClients;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Pantalla de selección de deck antes de empezar a jugar.
    /// Muestra decks disponibles, permite seleccionar uno.
    /// Guarda la selección para usar en el match.
    /// </summary>
    public sealed class DeckSelectionScreen : MonoBehaviour
    {
        [SerializeField] private Transform deckListContainer;
        [SerializeField] private GameObject deckItemPrefab;
        [SerializeField] private Button playButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Text statusText;
        [SerializeField] private Button createDeckButton;

        private DeckManagementService _deckService;
        private string _selectedDeckId;
        private List<DeckDto> _playerDecks = new();
        private List<DeckItemUI> _deckItems = new();

        // Static reference for passing selected deck to next scene
        public static string SelectedDeckId { get; private set; }

        private void OnEnable()
        {
            LoadDecks();
        }

        private void Start()
        {
            if (playButton != null)
                playButton.onClick.AddListener(OnPlayButtonClicked);

            if (backButton != null)
                backButton.onClick.AddListener(OnBackButtonClicked);

            if (createDeckButton != null)
                createDeckButton.onClick.AddListener(OnCreateDeckButtonClicked);
        }

        private async void LoadDecks()
        {
            _deckService = ServiceLocator.Get<DeckManagementService>();
            if (_deckService == null)
            {
                ShowStatus("Error: Deck service not available", isError: true);
                return;
            }

            ShowStatus("Loading decks...");

            try
            {
                _playerDecks = await _deckService.GetPlayerDecksAsync();

                if (_playerDecks.Count == 0)
                {
                    ShowStatus("No decks found. Create a new deck to play.", isError: true);
                    if (playButton != null)
                        playButton.interactable = false;
                    return;
                }

                DisplayDecks();
                ShowStatus("");
            }
            catch (System.Exception ex)
            {
                ShowStatus($"Error loading decks: {ex.Message}", isError: true);
                Debug.LogError($"[DeckSelection] Failed to load decks: {ex}");
            }
        }

        private void DisplayDecks()
        {
            // Clear existing items
            foreach (Transform child in deckListContainer)
            {
                Destroy(child.gameObject);
            }
            _deckItems.Clear();

            // Create deck items
            foreach (var deck in _playerDecks)
            {
                var itemGo = Instantiate(deckItemPrefab, deckListContainer);
                var itemUI = itemGo.GetComponent<DeckItemUI>();

                if (itemUI != null)
                {
                    itemUI.Initialize(deck.displayName, deck.cardIds.Count, deck.deckId);
                    itemUI.OnSelected += OnDeckSelected;
                    _deckItems.Add(itemUI);
                }
            }

            // Select first deck by default
            if (_deckItems.Count > 0)
            {
                _deckItems[0].Select();
            }
        }

        private void OnDeckSelected(string deckId)
        {
            _selectedDeckId = deckId;

            // Deselect all others
            foreach (var item in _deckItems)
            {
                if (item.DeckId != deckId)
                    item.Deselect();
            }

            if (playButton != null)
                playButton.interactable = true;

            ShowStatus($"Selected: {_playerDecks.Find(d => d.deckId == deckId)?.displayName}");
        }

        private void OnPlayButtonClicked()
        {
            if (string.IsNullOrWhiteSpace(_selectedDeckId))
            {
                ShowStatus("Please select a deck", isError: true);
                return;
            }

            SelectedDeckId = _selectedDeckId;
            GameLogger.Info("DeckSelection", $"Selected deck: {_selectedDeckId}");

            // Load game scene or trigger matchmaking
            // This depends on your game flow - you might load a matchmaking scene
            // or directly start the game
            // For now, just log it
            ShowStatus("Starting game with selected deck...");
        }

        private void OnBackButtonClicked()
        {
            // Go back to main menu or previous screen
            gameObject.SetActive(false);
        }

        private void OnCreateDeckButtonClicked()
        {
            // TODO: Open deck builder screen
            ShowStatus("Deck builder not yet implemented", isError: false);
        }

        private void ShowStatus(string message, bool isError = false)
        {
            if (statusText != null)
            {
                statusText.text = message;
                statusText.color = isError ? Color.red : Color.white;
            }
        }

        private void OnDestroy()
        {
            if (playButton != null)
                playButton.onClick.RemoveListener(OnPlayButtonClicked);

            if (backButton != null)
                backButton.onClick.RemoveListener(OnBackButtonClicked);

            if (createDeckButton != null)
                createDeckButton.onClick.RemoveListener(OnCreateDeckButtonClicked);

            foreach (var item in _deckItems)
            {
                item.OnSelected -= OnDeckSelected;
            }
        }
    }

    /// <summary>
    /// Item de UI para mostrar un deck en la lista.
    /// </summary>
    public sealed class DeckItemUI : MonoBehaviour
    {
        [SerializeField] private Text deckNameText;
        [SerializeField] private Text cardCountText;
        [SerializeField] private Button selectButton;
        [SerializeField] private Image selectionIndicator;

        public string DeckId { get; private set; }
        public event System.Action<string> OnSelected;

        public void Initialize(string deckName, int cardCount, string deckId)
        {
            DeckId = deckId;

            if (deckNameText != null)
                deckNameText.text = deckName;

            if (cardCountText != null)
                cardCountText.text = $"{cardCount} cards";

            if (selectButton != null)
                selectButton.onClick.AddListener(OnSelectClicked);

            Deselect();
        }

        public void Select()
        {
            if (selectionIndicator != null)
                selectionIndicator.gameObject.SetActive(true);
        }

        public void Deselect()
        {
            if (selectionIndicator != null)
                selectionIndicator.gameObject.SetActive(false);
        }

        private void OnSelectClicked()
        {
            Select();
            OnSelected?.Invoke(DeckId);
        }

        private void OnDestroy()
        {
            if (selectButton != null)
                selectButton.onClick.RemoveListener(OnSelectClicked);
        }
    }
}
