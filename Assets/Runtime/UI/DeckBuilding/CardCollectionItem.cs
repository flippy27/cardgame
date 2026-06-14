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

        // Compact-row extras created in code (the prefab has no rarity/atk/hp/level text).
        private TMPro.TextMeshProUGUI _rarityText;
        private TMPro.TextMeshProUGUI _atkText;
        private TMPro.TextMeshProUGUI _hpText;
        private TMPro.TextMeshProUGUI _levelText;
        private Networking.CardCatalogCache _catalog;

        private static readonly string[] RarityShort = { "Common", "Rare", "Epic", "Legendary" };

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

            DeckBuilderTextScale.ApplyAutoSize(cardNameText, DeckBuilderTextScale.Role.CardName);
            DeckBuilderTextScale.Apply(copiesText, DeckBuilderTextScale.Role.Label);

            EnsureRowLayout();

            // Spawned rows aren't reached by the screen-level font pass, so apply the size floor here.
            if (KenneyUiSkin.Available) KenneyUiSkin.ApplyFontUnder(this);
        }

        // OVERHAUL: the cell is now a COMPACT HORIZONTAL ROW (the prefab parked everything in a giant
        // card-sized space). Layout L→R: [thumbnail] [name (flex)] [rarity] [ATK] [HP] [×copies], with a
        // rarity-coloured strip down the left edge and a transparent full-row tap target. The rarity/ATK/HP
        // labels don't exist in the prefab, so they're created here once.
        private void EnsureRowLayout()
        {
            KenneyUiSkin.SetLayoutElement(this, preferredHeight: 116f, flexibleWidth: 1f);
            var h = GetComponent<HorizontalLayoutGroup>() ?? gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(22, 16, 8, 8);
            h.spacing = 14f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;

            // Rarity colour strip down the left edge (out of the layout flow).
            if (rarityBar != null)
            {
                var le = rarityBar.GetComponent<LayoutElement>() ?? rarityBar.gameObject.AddComponent<LayoutElement>();
                le.ignoreLayout = true;
                KenneyUiSkin.SetRect(rarityBar, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                    new Vector2(0f, 0f), new Vector2(8f, 0f));
                rarityBar.raycastTarget = false;
            }
            if (factionIcon != null) factionIcon.gameObject.SetActive(false);

            // Create the stat labels once. Rarity is conveyed by the left colour strip, so the row only
            // needs the upgrade level ("★N") + ATK / HP (kept short so they never collide).
            if (_levelText == null) _levelText = MakeRowText("LevelText", DeckBuilderTextScale.Role.Label);
            if (_atkText == null) _atkText = MakeRowText("AtkText", DeckBuilderTextScale.Role.Status);
            if (_hpText == null) _hpText = MakeRowText("HpText", DeckBuilderTextScale.Role.Status);

            // Known children kept in the flow; everything else (e.g. an unused "Text (TMP)") is hidden.
            var known = new System.Collections.Generic.HashSet<Transform>();
            foreach (var c in new Component[] { cardArtImage, cardNameText, copiesText, rarityBar, factionIcon, selectButton, _levelText, _atkText, _hpText })
                if (c != null) known.Add(c.transform);
            foreach (Transform child in transform)
            {
                if (known.Contains(child)) continue;
                var le = child.GetComponent<LayoutElement>() ?? child.gameObject.AddComponent<LayoutElement>();
                le.ignoreLayout = true;
                child.gameObject.SetActive(false);
            }

            int idx = 0;
            void Place(Component c, float prefW, float flexW)
            {
                if (c == null) return;
                c.transform.SetSiblingIndex(idx++);
                KenneyUiSkin.SetLayoutElement(c, preferredWidth: prefW, preferredHeight: 100f, flexibleWidth: flexW);
            }
            Place(cardArtImage, 74f, -1f);
            Place(cardNameText, 200f, 1f);
            Place(_levelText, 70f, -1f);
            Place(_atkText, 104f, -1f);
            Place(_hpText, 104f, -1f);
            Place(copiesText, 72f, -1f);

            if (cardArtImage != null) { cardArtImage.preserveAspect = true; cardArtImage.raycastTarget = false; }
            if (cardNameText != null) { cardNameText.alignment = TextAlignmentOptions.MidlineLeft; cardNameText.raycastTarget = false; cardNameText.enableWordWrapping = false; cardNameText.overflowMode = TextOverflowModes.Ellipsis; }
            if (copiesText != null) { copiesText.alignment = TextAlignmentOptions.MidlineRight; copiesText.raycastTarget = false; }

            // Transparent full-row tap target on top.
            if (selectButton != null)
            {
                var le = selectButton.GetComponent<LayoutElement>() ?? selectButton.gameObject.AddComponent<LayoutElement>();
                le.ignoreLayout = true;
                KenneyUiSkin.Fill(selectButton);
                selectButton.transition = Selectable.Transition.None;
                selectButton.transform.SetAsLastSibling();
                var img = (selectButton.targetGraphic as Image) ?? selectButton.GetComponent<Image>();
                if (img != null) { img.sprite = null; img.color = new Color(1f, 1f, 1f, 0f); img.raycastTarget = true; }
                var lbl = selectButton.GetComponentInChildren<TMP_Text>(true);
                if (lbl != null) lbl.gameObject.SetActive(false);
            }
        }

        private TMPro.TextMeshProUGUI MakeRowText(string goName, DeckBuilderTextScale.Role role)
        {
            var go = new GameObject(goName, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var t = go.AddComponent<TMPro.TextMeshProUGUI>();
            t.alignment = TextAlignmentOptions.Midline;
            t.raycastTarget = false;
            t.enableWordWrapping = false;
            DeckBuilderTextScale.ApplyAutoSize(t, role);
            return t;
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

            ApplyThumbnail(entry.cardId);

            if (copiesText != null)
                copiesText.text = $"×{Mathf.Max(1, entry.ownedCopies)}";

            // Use first instance for rarity/faction (all copies share the same definition).
            var first = entry.ownedInstances != null && entry.ownedInstances.Length > 0
                ? entry.ownedInstances[0]
                : null;
            int rarity = first != null ? first.cardRarity : 0;

            // "★N" = highest upgrade level across the owned copies. Hidden when ≤ 1 (unupgraded).
            int maxLevel = 1;
            if (entry.ownedInstances != null)
                foreach (var inst in entry.ownedInstances)
                    maxLevel = Mathf.Max(maxLevel, PlayerCardLevel.Resolve(inst));
            if (_levelText != null)
            {
                bool showLevel = maxLevel > 1;
                _levelText.gameObject.SetActive(showLevel);
                if (showLevel)
                {
                    _levelText.text = $"★{maxLevel}";
                    _levelText.color = new Color(1f, 0.85f, 0.25f); // gold
                }
            }

            if (rarityBar != null)
                rarityBar.color = RarityColors[Mathf.Clamp(rarity, 0, RarityColors.Length - 1)];

            if (_rarityText != null)
            {
                _rarityText.text = RarityShort[Mathf.Clamp(rarity, 0, RarityShort.Length - 1)];
                _rarityText.color = RarityColors[Mathf.Clamp(rarity, 0, RarityColors.Length - 1)];
            }

            // ATK / HP come from the card catalog (the summary entry carries no stats).
            int atk = -1, hp = -1;
            if (_catalog == null) Core.ServiceLocator.TryResolve<Networking.CardCatalogCache>(out _catalog);
            if (_catalog != null && _catalog.TryGetCard(entry.cardId, out var def) && def != null)
            {
                atk = def.attack;
                hp = def.health;
            }
            bool isUnit = first == null || first.cardType == 0;
            if (_atkText != null)
            {
                _atkText.text = (isUnit && atk >= 0) ? $"ATK {atk}" : string.Empty;
                _atkText.color = new Color(1f, 0.55f, 0.2f);
            }
            if (_hpText != null)
            {
                _hpText.text = (isUnit && hp >= 0) ? $"HP {hp}" : string.Empty;
                _hpText.color = new Color(0.45f, 0.85f, 0.4f);
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

        // Thumbnail without the finicky composite pipeline: raw per-card illustration if present,
        // otherwise a clean faction-tinted block (never the white/magenta "missing" sprite).
        private void ApplyThumbnail(string cardId)
        {
            if (cardArtImage == null) return;
            cardArtImage.preserveAspect = true;
            if (_catalog == null) Core.ServiceLocator.TryResolve<Networking.CardCatalogCache>(out _catalog);
            Networking.ApiClients.ServerCardDefinition d = null;
            if (_catalog != null) _catalog.TryGetCard(cardId, out d);

            // SAME compositor the in-game card uses (art + ornamental frame) → one source of truth, so
            // frame/art changes show in both battle and the deck builder. Falls back to raw art, then tint.
            Sprite spr = null;
            if (d != null)
                spr = CardArtLibrary.GetCardComposite(cardId, d.cardType, d.cardRarity, d.cardFaction, d.unitType, d.armor > 0, "hand");
            if (spr == null || spr == CardArtLibrary.Missing)
                spr = CardArtLibrary.GetCardArt(cardId);

            if (spr != null && spr != CardArtLibrary.Missing)
            {
                cardArtImage.sprite = spr;
                cardArtImage.color = Color.white;
                return;
            }
            cardArtImage.sprite = null;
            cardArtImage.color = FactionColors[Mathf.Clamp(d?.cardFaction ?? 0, 0, FactionColors.Length - 1)];
        }

        private void OnClicked() => _onSelected?.Invoke(_data);
    }
}
