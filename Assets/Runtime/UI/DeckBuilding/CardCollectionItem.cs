using System;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Networking.ApiClients;
using Flippy.CardDuelMobile.UI;
using TMPro;

namespace Flippy.CardDuelMobile.UI.DeckBuilding
{
    /// <summary>
    /// Single card cell in the collection album grid.
    /// Bound to a PlayerCardSummaryEntryDto (one entry = one card type, N copies).
    ///
    /// Prefab structure (CardCollectionItem_Prefab):
    ///   CardCollectionItem  (Image bg + this component + Button)
    ///   ├── CardArtImage    (Image)          — card art
    ///   ├── CardNameText    (Text)           — displayName
    ///   ├── CopiesBadge
    ///   │   └── CopiesText  (Text)           — "×2"
    ///   ├── RarityBar       (Image)          — colored strip at bottom
    ///   └── FactionIcon     (Image)          — faction color tint (optional)
    ///
    /// Rarity colors: Common=grey, Rare=blue, Epic=purple, Legendary=gold
    /// Faction colors: Ember=orange, Tidal=cyan, Grove=green, Alloy=silver, Void=purple-dark
    /// </summary>
    public sealed class CardCollectionItem : MonoBehaviour
    {
        [SerializeField] private Image cardArtImage;
        [SerializeField] private TextMeshProUGUI cardNameText;
        [SerializeField] private TextMeshProUGUI copiesText;
        [SerializeField] private Image rarityBar;
        [SerializeField] private Image factionIcon;
        [SerializeField] private Button selectButton;
        [SerializeField] private CardSurfaceVisualRenderer visualRenderer;
        [SerializeField] private string visualSurface = "collection";

        private static readonly Color[] RarityColors =
        {
            new Color(0.65f, 0.65f, 0.65f), // Common   — grey
            new Color(0.20f, 0.50f, 1.00f), // Rare     — blue
            new Color(0.60f, 0.10f, 0.90f), // Epic     — purple
            new Color(1.00f, 0.78f, 0.10f), // Legendary — gold
        };

        private static readonly Color[] FactionColors =
        {
            new Color(1.00f, 0.45f, 0.10f), // Ember  — orange
            new Color(0.10f, 0.75f, 0.95f), // Tidal  — cyan
            new Color(0.20f, 0.75f, 0.30f), // Grove  — green
            new Color(0.70f, 0.70f, 0.75f), // Alloy  — silver
            new Color(0.45f, 0.10f, 0.70f), // Void   — dark purple
        };

        private PlayerCardsApiClient.PlayerCardSummaryEntryDto _data;
        private Action<PlayerCardsApiClient.PlayerCardSummaryEntryDto> _onSelected;

        private void Awake()
        {
            if (selectButton != null)
                selectButton.onClick.AddListener(OnClicked);
        }

        /// <summary>Bind a summary entry to this cell. Call after Instantiate.</summary>
        public void Bind(
            PlayerCardsApiClient.PlayerCardSummaryEntryDto entry,
            Action<PlayerCardsApiClient.PlayerCardSummaryEntryDto> onSelected)
        {
            _data = entry;
            _onSelected = onSelected;

            if (cardNameText != null)
                cardNameText.text = entry.displayName ?? entry.cardId;

            EnsureVisualRenderer();
            visualRenderer?.ApplyCard(entry.cardId, visualSurface);

            if (copiesText != null)
                copiesText.text = entry.ownedCopies > 1 ? $"×{entry.ownedCopies}" : string.Empty;

            // Use first instance for rarity/faction colors (all copies share the same definition)
            var first = entry.ownedInstances != null && entry.ownedInstances.Length > 0
                ? entry.ownedInstances[0]
                : null;

            if (rarityBar != null && first != null)
            {
                var idx = Mathf.Clamp(first.cardRarity, 0, RarityColors.Length - 1);
                rarityBar.color = RarityColors[idx];
            }

            if (factionIcon != null && first != null)
            {
                var idx = Mathf.Clamp(first.cardFaction, 0, FactionColors.Length - 1);
                factionIcon.color = FactionColors[idx];
            }
        }

        private void EnsureVisualRenderer()
        {
            if (visualRenderer == null)
            {
                visualRenderer = GetComponent<CardSurfaceVisualRenderer>() ?? GetComponentInChildren<CardSurfaceVisualRenderer>(true);
            }

            if (visualRenderer == null && cardArtImage != null)
            {
                visualRenderer = gameObject.AddComponent<CardSurfaceVisualRenderer>();
                visualRenderer.EnsureDefaultImageBinding(cardArtImage, visualSurface);
            }
        }

        private void OnClicked() => _onSelected?.Invoke(_data);
    }
}
