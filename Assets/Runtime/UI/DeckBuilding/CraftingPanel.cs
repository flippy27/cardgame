using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.UI.DeckBuilding
{
    /// <summary>
    /// Crafting overlay panel. Shows craftable cards, their item requirements, and the craft button.
    ///
    /// Hierarchy (CraftingPanel — starts inactive):
    ///   CraftingPanel
    ///   ├── Header
    ///   │   ├── TitleText           "Crafting Workshop"
    ///   │   ├── DustBadge
    ///   │   │   ├── DustIcon        (Image)
    ///   │   │   └── DustAmountText  (Text) — card_dust balance
    ///   │   └── CloseButton         (Button)
    ///   ├── FilterRow
    ///   │   └── AffordableOnlyToggle (Toggle)
    ///   ├── RecipeScrollView
    ///   │   └── Viewport → Content  (VerticalLayoutGroup + ContentSizeFitter)
    ///   │       └── [CraftingRecipeItem × N — spawned at runtime]
    ///   ├── StatusText              (Text)
    ///   └── LoadingOverlay          (GameObject)
    /// </summary>
    public sealed class CraftingPanel : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private Text dustAmountText;
        [SerializeField] private Button closeButton;

        [Header("Filters")]
        [SerializeField] private Toggle affordableOnlyToggle;

        [Header("Recipe List")]
        [SerializeField] private Transform recipeListContainer;
        [SerializeField] private GameObject recipeItemPrefab;

        [Header("Feedback")]
        [SerializeField] private Text statusText;
        [SerializeField] private GameObject loadingOverlay;

        private CraftingService _craftingService;
        private InventoryService _inventoryService;
        private PlayerCardCollectionService _collectionService;

        private List<CraftingApiClient.CraftableCardDto> _craftableCards = new();
        private Dictionary<string, InventoryApiClient.PlayerItemDto> _inventory = new();
        private bool _affordableOnly;

        /// <summary>Fired after a successful craft — CardCollectionScreen subscribes to refresh.</summary>
        public event Action OnCraftSuccess;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (affordableOnlyToggle != null)
                affordableOnlyToggle.onValueChanged.AddListener(v => { _affordableOnly = v; RebuildList(); });
        }

        public void Show()
        {
            gameObject.SetActive(true);
            LoadDataAsync();
        }

        public void Hide() => gameObject.SetActive(false);

        private async void LoadDataAsync()
        {
            SetLoading(true);
            ShowStatus(string.Empty);

            ServiceLocator.TryResolve<CraftingService>(out _craftingService);
            ServiceLocator.TryResolve<InventoryService>(out _inventoryService);
            ServiceLocator.TryResolve<PlayerCardCollectionService>(out _collectionService);

            if (_craftingService == null || _inventoryService == null)
            {
                ShowStatus("Services unavailable.");
                SetLoading(false);
                return;
            }

            try
            {
                var cardsTask = _craftingService.GetCraftableCardsAsync();
                var invTask = _inventoryService.GetInventoryAsync();
                await Task.WhenAll(cardsTask, invTask);

                _craftableCards = cardsTask.Result;
                _inventory = invTask.Result;

                RefreshDustDisplay();
                RebuildList();
            }
            catch (Exception ex)
            {
                ShowStatus($"Error loading crafting data: {ex.Message}");
                Debug.LogError($"[CraftingPanel] {ex}");
            }
            finally { SetLoading(false); }
        }

        private void RebuildList()
        {
            if (recipeListContainer == null || recipeItemPrefab == null) return;

            foreach (Transform child in recipeListContainer) Destroy(child.gameObject);

            var toShow = _affordableOnly
                ? _craftableCards.FindAll(c => _inventoryService?.CanAffordCraft(c.requirements, _inventory) == true)
                : _craftableCards;

            foreach (var card in toShow)
            {
                var go = Instantiate(recipeItemPrefab, recipeListContainer);
                var item = go.GetComponent<CraftingRecipeItem>();
                if (item == null) continue;
                bool canAfford = _inventoryService?.CanAffordCraft(card.requirements, _inventory) == true;
                item.Bind(card, _inventory, canAfford, OnCraftRequested);
            }

            if (toShow.Count == 0)
                ShowStatus(_affordableOnly ? "No affordable recipes." : "No craftable cards available.");
        }

        private async void OnCraftRequested(string cardId)
        {
            if (_craftingService == null) return;
            SetLoading(true);
            ShowStatus("Crafting...");

            try
            {
                var result = await _craftingService.CraftCardAsync(cardId);
                if (result?.success == true)
                {
                    // Update cached inventory from response
                    if (result.updatedInventory != null)
                        _inventoryService?.ApplyPartialUpdate(result.updatedInventory);

                    // Invalidate collection cache — new card was added
                    _collectionService?.InvalidateSummaryCache();

                    ShowStatus($"Crafted {result.playerCard?.displayName ?? cardId}!");
                    _inventory = await _inventoryService.GetInventoryAsync();
                    RefreshDustDisplay();
                    RebuildList();
                    OnCraftSuccess?.Invoke();
                }
                else
                {
                    // 409 or other failure — message from server
                    ShowStatus(result?.message ?? "Craft failed.");
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Error: {ex.Message}");
                Debug.LogError($"[CraftingPanel] Craft failed: {ex}");
            }
            finally { SetLoading(false); }
        }

        private void RefreshDustDisplay()
        {
            if (dustAmountText == null) return;
            _inventory.TryGetValue(InventoryService.CardDustKey, out var dust);
            dustAmountText.text = (dust?.quantity ?? 0).ToString("N0");
        }

        private void ShowStatus(string msg) { if (statusText != null) statusText.text = msg; }
        private void SetLoading(bool on) { if (loadingOverlay != null) loadingOverlay.SetActive(on); }
    }
}
