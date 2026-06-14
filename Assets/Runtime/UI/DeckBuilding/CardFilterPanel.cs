using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.UI;
using TMPro;

namespace Flippy.CardDuelMobile.UI.DeckBuilding
{
    /// <summary>
    /// REUSABLE chip-based filter popup for the deck-builder grids. Built entirely in code
    /// (no prefab wiring), Kenney-skinned. Lives on / is owned by <see cref="CardGridView"/> so all
    /// three tabs (Decks / Create / Salvage) get the same filter UI from one place.
    ///
    /// The panel shows chips grouped by CATEGORY:
    ///   FACCIÓN: Ember / Tidal / Grove / Alloy / Void  (coloured dot per faction)
    ///   RAREZA:  Common / Rare / Epic / Legendary       (rarity gem badge)
    ///   TIPO:    Unit / Utility / Equipment / Spell      (best-effort stat icon)
    ///   MANA:    0-2 / 3-5 / 6-7 / 8+                     (mana stat icon)
    ///
    /// Selection rule: within a category multi-select = OR; across categories = AND. A "Clear"
    /// chip-row button resets every category. <see cref="OnChanged"/> fires whenever any chip or
    /// Clear is toggled so the host re-pages/rebuilds the grid.
    ///
    /// ICONS: faction has no sprite in the project, so each faction chip uses a generated coloured
    /// DOT tinted by the faction palette (also tints the chip). Rarity uses
    /// <see cref="CardArtLibrary.GetRarityBadgeSprite"/>; mana uses the "stat_mana" stat icon; type
    /// approximates with stat icons (Unit→attack_sword, Spell→attack_magic, Equipment→stat_armor,
    /// Utility→stat_mana). When a sprite is null the chip falls back to label-only (still toggleable).
    /// </summary>
    public sealed class CardFilterPanel : MonoBehaviour
    {
        public enum Category { Faction, Rarity, Type, Mana }

        private static readonly string[] FactionNames = { "Ember", "Tidal", "Grove", "Alloy", "Void" };
        private static readonly string[] RarityNames = { "Common", "Rare", "Epic", "Legendary" };
        // CardType enum: Unit=0, Utility=1, Equipment=2, Spell=3 — display in a friendlier order.
        private static readonly (int value, string label)[] TypeChips =
        {
            (0, "Unit"), (1, "Utility"), (2, "Equipment"), (3, "Spell"),
        };
        // Mana buckets: index 0 = [0..2], 1 = [3..5], 2 = [6..7], 3 = [8..inf].
        private static readonly (string label, int min, int max)[] ManaBuckets =
        {
            ("0-2", 0, 2), ("3-5", 3, 5), ("6-7", 6, 7), ("8+", 8, int.MaxValue),
        };

        // Faction palette (matches CardCollectionItem / CardCellView faction colours).
        private static readonly Color[] FactionColors =
        {
            new Color(1.00f, 0.45f, 0.10f), // Ember  orange
            new Color(0.10f, 0.75f, 0.95f), // Tidal  blue
            new Color(0.20f, 0.75f, 0.30f), // Grove  green
            new Color(0.70f, 0.70f, 0.75f), // Alloy  silver
            new Color(0.45f, 0.10f, 0.70f), // Void   purple
        };

        private static readonly Color ChipBgNormal = new Color(0.18f, 0.19f, 0.24f, 0.95f);
        private static readonly Color ChipBgSelected = new Color(0.20f, 0.55f, 0.85f, 1f);

        // ---- Public selection state (read by CardGridView) ----
        public readonly HashSet<int> SelectedFactions = new();
        public readonly HashSet<int> SelectedRarities = new();
        public readonly HashSet<int> SelectedTypes = new();
        public readonly HashSet<int> SelectedManaBuckets = new();

        /// <summary>Fires whenever a chip or the Clear button changes the selection.</summary>
        public event Action OnChanged;

        public bool IsOpen => _panelRoot != null && _panelRoot.gameObject.activeSelf;

        public bool AnySelected =>
            SelectedFactions.Count > 0 || SelectedRarities.Count > 0 ||
            SelectedTypes.Count > 0 || SelectedManaBuckets.Count > 0;

        private RectTransform _panelRoot;   // dim backdrop covering the grid
        private RectTransform _dialog;      // the chip dialog (positioned below the Filter button)
        private RectTransform _anchorBtn;   // the Filter button to drop down from (optional)
        private readonly List<Chip> _chips = new();
        private static Sprite _dotSprite;

        private sealed class Chip
        {
            public Category category;
            public int value;
            public Image bg;
            public bool selected;
            public Color tint; // faction tint (white for non-faction)
        }

        /// <summary>
        /// Creates the (hidden) overlay filter panel filling <paramref name="parent"/> (typically the
        /// CardGridView root, so the popup covers the grid). Call <see cref="Toggle"/> to show/hide.
        /// </summary>
        public static CardFilterPanel Create(Transform parent)
        {
            var go = new GameObject("CardFilterPanel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            KenneyUiSkin.Fill((RectTransform)go.transform);
            var panel = go.AddComponent<CardFilterPanel>();
            panel.Build();
            return panel;
        }

        private void Build()
        {
            // Dim backdrop (tap outside the dialog to close).
            var bdGo = gameObject.AddComponent<Image>();
            bdGo.color = new Color(0f, 0f, 0f, 0.55f);
            bdGo.raycastTarget = true;
            var bdBtn = gameObject.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;
            bdBtn.onClick.AddListener(Close);

            _panelRoot = (RectTransform)transform;

            // Dialog: centred card-ish panel that holds the category rows.
            var dialogGo = new GameObject("Dialog", typeof(RectTransform), typeof(Image));
            var drt = (RectTransform)dialogGo.transform;
            _dialog = drt;
            drt.SetParent(_panelRoot, false);
            // Stretch horizontally to use the screen width (chips overflowed a fixed 940 box).
            drt.anchorMin = new Vector2(0.04f, 0.5f);
            drt.anchorMax = new Vector2(0.96f, 0.5f);
            drt.pivot = new Vector2(0.5f, 0.5f);
            drt.anchoredPosition = Vector2.zero;
            drt.sizeDelta = new Vector2(0f, 760f);
            var dimg = dialogGo.GetComponent<Image>();
            dimg.color = new Color(0.14f, 0.13f, 0.17f, 1f);
            if (KenneyUiSkin.Available) KenneyUiSkin.SkinInsetImage(dimg);
            // Swallow taps inside the dialog so they don't hit the backdrop's close handler.
            var swallow = dialogGo.AddComponent<Button>();
            swallow.transition = Selectable.Transition.None;

            var title = MakeText(drt, "FilterTitle", "Filters", TextAlignmentOptions.MidlineLeft);
            KenneyUiSkin.SetRect(title, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, -16f), new Vector2(420f, 72f));
            DeckBuilderTextScale.Apply(title, DeckBuilderTextScale.Role.Header);

            var closeBtn = MakeButton(drt, "FilterClose", "Close", KenneyUiSkin.ButtonStyle.Nav);
            KenneyUiSkin.SetRect(closeBtn, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-16f, -16f), new Vector2(190f, 72f));
            closeBtn.onClick.AddListener(Close);

            var clearBtn = MakeButton(drt, "FilterClear", "Clear", KenneyUiSkin.ButtonStyle.Nav);
            KenneyUiSkin.SetRect(clearBtn, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-216f, -16f), new Vector2(190f, 72f));
            clearBtn.onClick.AddListener(() => { ClearAll(); OnChanged?.Invoke(); });

            // Category rows, stacked top→bottom under the title.
            // VISUAL TUNING: rowTop is the y-offset (negative, from the dialog's top) of the first row;
            // each row is rowHeight tall; bump these if labels/chips feel cramped.
            const float rowTop = -148f;
            const float rowHeight = 138f;

            BuildCategoryRow(drt, "FACCIÓN", Category.Faction, FactionChips(), rowTop - rowHeight * 0f, rowHeight);
            BuildCategoryRow(drt, "RAREZA", Category.Rarity, RarityChipsData(), rowTop - rowHeight * 1f, rowHeight);
            BuildCategoryRow(drt, "TIPO", Category.Type, TypeChipsData(), rowTop - rowHeight * 2f, rowHeight);
            BuildCategoryRow(drt, "MANA", Category.Mana, ManaChipsData(), rowTop - rowHeight * 3f, rowHeight);

            if (KenneyUiSkin.Available) KenneyUiSkin.ApplyFontUnder(this);
            gameObject.SetActive(false);
        }

        // ---- Chip data builders (label + value + icon sprite + tint) ----

        private struct ChipData
        {
            public int value;
            public string label;
            public Sprite icon;
            public Color tint;       // chip + dot tint (white when not faction)
            public bool useColorDot; // draw a generated coloured dot instead of an icon sprite
        }

        private List<ChipData> FactionChips()
        {
            var list = new List<ChipData>();
            for (int i = 0; i < FactionNames.Length; i++)
            {
                var factionIcon = CardArtLibrary.GetFactionIcon(i); // real emblem, else fall back to the colour dot
                list.Add(new ChipData
                {
                    value = i,
                    label = FactionNames[i],
                    icon = factionIcon,
                    tint = factionIcon != null ? Color.white : FactionColors[i],
                    useColorDot = factionIcon == null,
                });
            }
            return list;
        }

        private List<ChipData> RarityChipsData()
        {
            var list = new List<ChipData>();
            for (int i = 0; i < RarityNames.Length; i++)
                list.Add(new ChipData
                {
                    value = i,
                    label = RarityNames[i],
                    icon = CardArtLibrary.GetRarityBadgeSprite(i),
                    tint = Color.white,
                });
            return list;
        }

        private List<ChipData> TypeChipsData()
        {
            var list = new List<ChipData>();
            foreach (var (value, label) in TypeChips)
                list.Add(new ChipData
                {
                    value = value,
                    // Real type emblem; fall back to the best-available stat-icon approximation.
                    icon = CardArtLibrary.GetTypeIcon(value) ?? CardArtLibrary.GetStatIconSprite(TypeIconName(value)),
                    tint = Color.white,
                });
            return list;
        }

        private List<ChipData> ManaChipsData()
        {
            var manaIcon = CardArtLibrary.GetStatIconSprite("stat_mana");
            var list = new List<ChipData>();
            for (int i = 0; i < ManaBuckets.Length; i++)
                list.Add(new ChipData
                {
                    value = i,
                    label = ManaBuckets[i].label,
                    icon = manaIcon,
                    tint = Color.white,
                });
            return list;
        }

        // Type → best-available stat icon (approximation; see class summary).
        private static string TypeIconName(int cardType) => cardType switch
        {
            0 => "attack_sword", // Unit
            3 => "attack_magic", // Spell
            2 => "stat_armor",   // Equipment
            1 => "stat_mana",    // Utility
            _ => "stat_mana"
        };

        // ---- Row + chip construction ----

        private void BuildCategoryRow(RectTransform dialog, string heading, Category category,
            List<ChipData> chips, float yTop, float rowHeight)
        {
            var rowGo = new GameObject($"Row_{category}", typeof(RectTransform));
            var row = (RectTransform)rowGo.transform;
            row.SetParent(dialog, false);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            // This rect is stretched horizontally (anchorMin.x=0, anchorMax.x=1). Setting sizeDelta.x=0
            // (as before) OVERRODE the left/right insets and made the row span the full dialog width —
            // that's why the padding never showed. Drive every edge via offsetMin/offsetMax instead:
            //   x: left/right insets;  y: row top (yTop, below the dialog top) and bottom (yTop-rowHeight).
            row.offsetMin = new Vector2(80f, yTop - rowHeight);
            row.offsetMax = new Vector2(-72f, yTop);

            // Category heading (left column) — wider so "FACCIÓN"/"EQUIPMENT"-length labels don't wrap.
            var head = MakeText(row, "Heading", heading, TextAlignmentOptions.MidlineLeft);
            KenneyUiSkin.SetRect(head, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(0f, 0f), new Vector2(250f, 0f));
            DeckBuilderTextScale.Apply(head, DeckBuilderTextScale.Role.Label);
            head.fontStyle |= FontStyles.Bold;
            head.enableWordWrapping = false;
            head.overflowMode = TextOverflowModes.Overflow;

            // Chip strip (right of the heading) — a HorizontalLayoutGroup so chips flow left→right.
            var stripGo = new GameObject("Chips", typeof(RectTransform));
            var strip = (RectTransform)stripGo.transform;
            strip.SetParent(row, false);
            strip.anchorMin = new Vector2(0f, 0f);
            strip.anchorMax = new Vector2(1f, 1f);
            strip.pivot = new Vector2(0f, 0.5f);
            strip.offsetMin = new Vector2(268f, 14f);
            strip.offsetMax = new Vector2(0f, -14f);
            KenneyUiSkin.EnsureHorizontalStrip(strip, spacing: 12f, padding: 0);

            foreach (var data in chips) BuildChip(strip, category, data);
        }

        private void BuildChip(RectTransform parent, Category category, ChipData data)
        {
            // VISUAL TUNING: chips flex to fill the row width evenly (preferred is a floor; flexibleWidth
            // distributes the leftover so 5 faction chips fit the wide dialog). Height fixed.
            const float chipH = 88f;

            var go = new GameObject($"Chip_{category}_{data.value}", typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            KenneyUiSkin.SetLayoutElement(go.GetComponent<Image>(), preferredWidth: 120f, preferredHeight: chipH, flexibleWidth: 1f);

            var bg = go.GetComponent<Image>();
            bg.color = ChipBgNormal;
            if (KenneyUiSkin.Available) KenneyUiSkin.SkinButton(go.GetComponent<Button>(), KenneyUiSkin.ButtonStyle.Nav);

            // Icon (coloured dot for faction, sprite otherwise). Omitted when no sprite is available.
            bool hasIcon = data.useColorDot || data.icon != null;
            if (hasIcon)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                var irt = (RectTransform)iconGo.transform;
                irt.SetParent(rt, false);
                irt.anchorMin = new Vector2(0f, 0.5f);
                irt.anchorMax = new Vector2(0f, 0.5f);
                irt.pivot = new Vector2(0f, 0.5f);
                irt.anchoredPosition = new Vector2(12f, 0f);
                irt.sizeDelta = new Vector2(40f, 40f);
                var iconImg = iconGo.GetComponent<Image>();
                iconImg.raycastTarget = false;
                iconImg.preserveAspect = true;
                if (data.useColorDot)
                {
                    iconImg.sprite = DotSprite();
                    iconImg.color = data.tint;
                }
                else
                {
                    iconImg.sprite = data.icon;
                    iconImg.color = Color.white;
                }
            }

            // Label.
            var label = MakeText(rt, "Label", data.label, TextAlignmentOptions.Center);
            KenneyUiSkin.SetRect(label, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(hasIcon ? 24f : 0f, 0f), new Vector2(hasIcon ? -8f : -8f, 0f));
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            DeckBuilderTextScale.ApplyAutoSize(label, DeckBuilderTextScale.Role.Button);
            label.raycastTarget = false;

            var chip = new Chip { category = category, value = data.value, bg = bg, tint = data.tint };
            _chips.Add(chip);
            ApplyChipVisual(chip);

            go.GetComponent<Button>().onClick.AddListener(() =>
            {
                ToggleChip(chip);
                OnChanged?.Invoke();
            });
        }

        private void ToggleChip(Chip chip)
        {
            var set = SetFor(chip.category);
            if (chip.selected) set.Remove(chip.value);
            else set.Add(chip.value);
            chip.selected = !chip.selected;
            ApplyChipVisual(chip);
        }

        private HashSet<int> SetFor(Category category) => category switch
        {
            Category.Faction => SelectedFactions,
            Category.Rarity => SelectedRarities,
            Category.Type => SelectedTypes,
            _ => SelectedManaBuckets,
        };

        private void ApplyChipVisual(Chip chip)
        {
            if (chip.bg == null) return;
            // Selected chips read as "active": faction chips glow their faction colour, others blue.
            if (chip.selected)
                chip.bg.color = chip.category == Category.Faction
                    ? Color.Lerp(chip.tint, Color.white, 0.15f)
                    : ChipBgSelected;
            else
                chip.bg.color = ChipBgNormal;
        }

        // ---- Public API ----

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        /// <summary>Sets the Filter button the dialog should drop down from. Null = stay centred.</summary>
        public void SetAnchorButton(RectTransform anchor) => _anchorBtn = anchor;

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            PositionBelowAnchor();
        }

        // Drops the dialog just below the Filter button (full width), instead of centring it.
        private void PositionBelowAnchor()
        {
            if (_dialog == null || _anchorBtn == null) return;
            var canvas = GetComponentInParent<Canvas>();
            var cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

            var corners = new Vector3[4];
            _anchorBtn.GetWorldCorners(corners);              // 0=BL 1=TL 2=TR 3=BR
            var bottomWorld = (corners[0] + corners[3]) * 0.5f; // bottom-centre of the button
            var screen = RectTransformUtility.WorldToScreenPoint(cam, bottomWorld);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_panelRoot, screen, cam, out var local))
                return;

            // Anchor to the panel TOP edge, pivot top, so the dialog hangs down from the button bottom.
            _dialog.anchorMin = new Vector2(0.04f, 1f);
            _dialog.anchorMax = new Vector2(0.96f, 1f);
            _dialog.pivot = new Vector2(0.5f, 1f);
            float topEdgeLocalY = _panelRoot.rect.height * 0.5f; // panel root pivot is centre
            _dialog.anchoredPosition = new Vector2(0f, local.y - topEdgeLocalY - 10f);
        }

        public void Close() => gameObject.SetActive(false);

        public void ClearAll()
        {
            SelectedFactions.Clear();
            SelectedRarities.Clear();
            SelectedTypes.Clear();
            SelectedManaBuckets.Clear();
            foreach (var chip in _chips) { chip.selected = false; ApplyChipVisual(chip); }
        }

        /// <summary>Bucket index (0..3) for a mana cost, matching <see cref="ManaBuckets"/>.</summary>
        public static int ManaBucketFor(int manaCost)
        {
            for (int i = 0; i < ManaBuckets.Length; i++)
                if (manaCost >= ManaBuckets[i].min && manaCost <= ManaBuckets[i].max) return i;
            return ManaBuckets.Length - 1; // 8+
        }

        // ---- Generated round dot sprite (faction colour swatch) ----

        private static Sprite DotSprite()
        {
            if (_dotSprite != null) return _dotSprite;
            const int size = 64;
            var c = (size - 1) * 0.5f;
            var radius = c * 0.96f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - c) / radius;
                    float dy = (y - c) / radius;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // White interior (tinted by the Image.color), soft edge, transparent outside.
                    Color col;
                    if (d > 1f) col = new Color(0, 0, 0, 0);
                    else if (d > 0.86f) col = new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) / 0.14f));
                    else col = Color.white;
                    pixels[y * size + x] = col;
                }
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "FilterDot", wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            _dotSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            _dotSprite.name = "FilterDot";
            return _dotSprite;
        }

        // ---- Small builders (mirror CardGridView's helpers) ----

        private static TextMeshProUGUI MakeText(Transform parent, string name, string text, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.alignment = align;
            t.color = Color.white;
            return t;
        }

        private static Button MakeButton(Transform parent, string name, string label, KenneyUiSkin.ButtonStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.30f, 0.32f, 0.40f, 1f);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            var lt = labelGo.AddComponent<TextMeshProUGUI>();
            labelGo.transform.SetParent(go.transform, false);
            KenneyUiSkin.Fill(lt);
            lt.text = label;
            lt.alignment = TextAlignmentOptions.Center;
            lt.color = Color.white;
            lt.raycastTarget = false;
            DeckBuilderTextScale.ApplyAutoSize(lt, DeckBuilderTextScale.Role.Button);

            var btn = go.GetComponent<Button>();
            if (KenneyUiSkin.Available) KenneyUiSkin.SkinButtonWithLabel(btn, style, label);
            return btn;
        }
    }
}
