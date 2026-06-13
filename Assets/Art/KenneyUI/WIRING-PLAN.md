# Deck Builder — Kenney UI Wiring Plan (Task #10, TEMPORARY skin)

This plan re-skins `DeckBuildingScene.unity` and its panels with the CC0 Kenney
sprites in this folder. It does **not** change any C# logic — only Image
sprites, TMP font sizes, and (optionally) the TMP font asset.

> Every step that touches the scene, prefabs, or sprite import settings must be
> done **inside the Unity Editor** — the build/editor cannot be run from this
> environment. Those steps are flagged **[EDITOR]**. The sprite files themselves
> are already downloaded and extracted.

---

## 0. Current UI summary (what we are fixing)

Scripts (`Assets/Runtime/UI/DeckBuilding/`):
- `CardCollectionScreen.cs` — root controller. Header (dust badge + back btn),
  FilterBar (search `TMP_InputField`, rarity + faction `TMP_Dropdown`,
  clear-filters btn), `CollectionScrollView` (GridLayoutGroup of
  `CardCollectionItem` prefabs), Pagination (prev/next btn + page label),
  ActionBar (craft / decks / catalog buttons), StatusText, LoadingOverlay, and
  child panels (Crafting / CardDetail / DeckList / CardCatalog).
- `DeckListPanel.cs` — list of decks, create button, rows (`deckRowPrefab`).
- `DeckEditPanel.cs` — deck name input, scrollable card rows, add-cards / save
  buttons, validation + status text.
- `CardCollectionItem.cs` — grid cell: bg Image + Button, card art, name,
  copies badge, rarity bar, faction icon. Rarity/faction colors set in code.
- `CraftingPanel.cs`, `CardDetailPanel.cs`, `CardCatalogPanel.cs` — sibling panels.

Observed problems in `DeckBuildingScene.unity`:
- **Inconsistent / tiny text.** Auto-sizing is OFF (`m_enableAutoSizing: 0`) on
  every label, so the literal `m_fontSize` is what renders. Several important
  labels are **14 pt** (StatusText, validation/feedback, dropdown item labels,
  CardDetail status) — far too small for mobile. Others are wildly inconsistent
  (24, 36, 47.5, 52.9, 56, 57.7, 67.8, 86.5, 87) because they were left at
  whatever the auto-fit produced. No deliberate type scale.
- **No theme.** Buttons/panels/inputs use Unity's default white UISprite; there
  is no visual identity, and panels are flat white rectangles.
- **Overlap / cramped layout risk.** Mixed huge (80+ pt) and 14 pt text in the
  same panels means labels collide or clip against fixed-size rects.

Goal of this task: apply a consistent Kenney skin + a sane TMP type scale.

---

## 1. [EDITOR] Import the sprites

For every `.png` under `Assets/Art/KenneyUI/PNG/**` and
`Assets/Art/KenneyUI/RPGExpansion/PNG/**` that you intend to use:

1. Select the texture(s) in the Project window.
2. Inspector → **Texture Type = Sprite (2D and UI)**.
3. **Sprite Mode = Single**, **Mesh Type = Full Rect**.
4. Set **Pixels Per Unit** to taste (default 100 is fine for UGUI Image).
5. For sprites used as stretchable backgrounds (panels, buttons, inputs,
   slider tracks), open the **Sprite Editor** and set the **9-slice border**
   (drag the green guides in ~12–20 px from each edge). This stops corners from
   stretching. Set those Image components to **Image Type = Sliced**.
6. Apply.

Tip: import the whole folder once with a default Sprite preset, then only set
borders on the handful of 9-sliced sprites listed below.

9-slice these (the rest can stay simple sprites):
- `RPGExpansion/PNG/panel_brown.png`, `panelInset_beige.png`
- `PNG/<Color>/Default/button_rectangle_depth_*` (any you use)
- `PNG/Extra/Default/input_rectangle.png`
- `PNG/Grey/Default/slide_horizontal_grey.png`, `slide_vertical_grey.png`

---

## 2. [EDITOR] Sprite → component mapping

Assign via the **Image.sprite** field of each element (Inspector). Set
`Image Type = Sliced` on the 9-sliced ones.

| Scene element (component / object) | Kenney sprite | Image Type |
|---|---|---|
| Panel windows: `CraftingPanel`, `CardDetailPanel`, `DeckListPanel`, `DeckEditPanel`, `CardCatalogPanel` root bg Image | `RPGExpansion/PNG/panel_brown.png` | Sliced |
| ScrollView background (CollectionScrollView, deck/recipe lists) | `RPGExpansion/PNG/panelInset_beige.png` | Sliced |
| `DeckBuildingRoot` / Header strip bg (optional) | `RPGExpansion/PNG/panel_beigeLight.png` | Sliced |
| Primary buttons — Save, Create Deck, Craft, Add Cards | `PNG/Blue/Default/button_rectangle_depth_gloss.png` | Sliced |
| Secondary/nav buttons — Decks, Catalog, Back | `PNG/Grey/Default/button_rectangle_depth_flat.png` | Sliced |
| Destructive — Delete (currently disabled in `DeckListPanel`) | `PNG/Red/Default/button_rectangle_depth_flat.png` | Sliced |
| Icon buttons — CloseButton, prev/next page, remove-card (`x`) | `PNG/Grey/Default/button_square_depth_flat.png` | Sliced |
| Pagination arrow glyphs (child Image of prev/next) | `arrow_basic_w.png` / `arrow_basic_e.png` (Grey/Default) | Simple |
| Text fields — `deckNameInput`, `searchField` (TMP_InputField bg) | `PNG/Extra/Default/input_rectangle.png` | Sliced |
| Dropdown bg — `rarityDropdown`, `factionDropdown` (the dropdown Image) | `PNG/Extra/Default/input_rectangle.png` | Sliced |
| Dropdown arrow (child Image of the dropdown) | `PNG/Extra/Default/icon_arrow_down_dark.png` | Simple |
| Dropdown Template / Item Background | `PNG/Grey/Default/button_rectangle_flat.png` | Sliced |
| Dropdown Item Checkmark | `PNG/Grey/Default/icon_checkmark.png` | Simple |
| Toggle — `affordableOnlyToggle` (CraftingPanel) Background | `PNG/Grey/Default/check_square_grey.png` | Simple |
| Toggle Checkmark child | `PNG/Grey/Default/check_square_color_checkmark.png` | Simple |
| Scrollbar/slider track | `PNG/Grey/Default/slide_vertical_grey.png` | Sliced |
| Scrollbar/slider handle | `PNG/Grey/Default/slide_hangle.png` | Sliced |
| `CardCollectionItem` cell bg Image | `PNG/Grey/Default/button_square_flat.png` | Sliced |
| `CardCollectionItem` rarity bar (`rarityBar`) | leave white sprite — code tints `Image.color` per rarity (do NOT replace with a colored Kenney sprite, the color is set in `CardCollectionItem.cs`) | Simple |
| `CardCollectionItem` faction icon (`factionIcon`) | `PNG/Grey/Default/icon_circle.png` (code tints color) | Simple |
| Copies badge bg | `PNG/Grey/Default/icon_square.png` or `button_round_flat.png` | Sliced |
| Legendary / rating star (optional decoration) | `PNG/Yellow/Default/star.png` | Simple |
| LoadingOverlay bg (dim) | `PNG/Grey/Default/button_square_flat.png` tinted dark + alpha | Sliced |

Notes
- `CardCollectionItem.cs` sets `rarityBar.color` and `factionIcon.color`
  programmatically (RarityColors / FactionColors arrays). Keep those Images on a
  plain white/neutral sprite so the tint reads correctly; do not bake color in.
- Button "pressed" states: optionally wire `button_*_pressed` (RPG expansion) or
  the non-`depth` variant into the Button component's **Sprite Swap** transition
  (`Pressed Sprite` / `Highlighted Sprite`). [EDITOR]

---

## 3. [EDITOR] TMP text type scale

Auto-sizing is currently disabled and sizes are arbitrary. Apply a consistent
scale. Two options:

A. Simplest: turn **Enable Auto Sizing = ON** on each TMP label and set
   sensible min/max so text fits its rect (e.g. Min 18 / Max = header value
   below). The scene already has `m_fontSizeMin: 18` everywhere.

B. Preferred (deterministic): set fixed `Font Size` per role:

| Role | Components | Size (pt) |
|---|---|---|
| Screen / panel headers | TitleText (DeckEdit/DeckList/Crafting), "My Collection" | **36–48** |
| Section labels, button text, dropdown labels, deck/card-count, page label | most `*Text`, button captions, `cardCountText`, `pageLabel`, dropdown item label | **24–28** |
| Card names / row labels | `CardCollectionItem.cardNameText`, deck row name, recipe name | **20–24** |
| Status / validation / hint | `statusText`, `validationText`, dust amount caption | **22–26** (these are the 14 pt offenders — bump up) |
| Copies badge ("x2"), small icons | `copiesText` | **18–22** |

Concrete fixes for the worst offenders (currently `m_fontSize: 14`): StatusText,
validation/feedback labels, and dropdown item labels → raise to **24**.

C. Optional themed font: create a **TMP Font Asset** from
   `Assets/Art/KenneyUI/Font/Kenney Future Narrow.ttf`
   (Window → TextMeshPro → Font Asset Creator), then assign it as the TMP
   default or per-label. "Kenney Future Narrow" fits more text per line; plain
   "Kenney Future" for headers. [EDITOR]

---

## 4. [EDITOR] Layout sanity pass (while skinning)

- Give panels a `VerticalLayoutGroup` / `HorizontalLayoutGroup` + padding where
  elements were absolutely positioned, so the new sprite paddings don't cause
  overlap.
- Add a few px **padding** inside 9-sliced buttons so text isn't flush to the
  border.
- Confirm the CardCollectionItem grid cell size in the `GridLayoutGroup`
  accommodates the new `button_square` background + 20–24 pt card name.

---

## 5. What is already done vs. needs the Editor

DONE (no editor needed):
- Downloaded + extracted both CC0 packs into `Assets/Art/KenneyUI/`.
- Inventoried sprites, wrote `README.txt` (source + CC0 + temporary note).
- Produced this mapping + TMP size plan.

NEEDS UNITY EDITOR ([EDITOR]):
- Import textures as Sprite (2D and UI) + set 9-slice borders (step 1).
- Assign sprites to Image components per the table (step 2).
- Set Button sprite-swap transition states (step 2).
- Apply TMP font sizes / auto-size / themed font asset (step 3).
- Layout padding pass (step 4).
- Generate `.meta` files (Unity does this automatically on first import).

Because the scene/prefab/.cs files must not be edited from here and the editor
can't run in this environment, steps 1–4 are handed off to be performed in Unity.

---

## 6. Code-side skin (IMPLEMENTED — no prefab wiring)

Instead of assigning sprites by hand on every Image in the scene/prefabs, the
deck-builder screens now skin themselves **in code**, mirroring the self-contained
pattern in `Assets/Runtime/UI/SinglePlayerDeckSelectOverlay.cs`.

New helper: **`Assets/Runtime/UI/DeckBuilding/KenneyUiSkin.cs`**
- Loads the handful of Kenney sprites via `Resources.Load<Sprite>` (cached).
- `SkinPanelWindow(this)` → `panel_brown` (Sliced) on a panel root's Image.
- `SkinInsetImage` / `SkinPanelInset` → `panelInset_beige` (Sliced) on scroll-view
  viewport backgrounds.
- `SkinInputImage` → `input_rectangle` (Sliced) on TMP_InputField backgrounds.
- `SkinButton(btn, style)` → button sprite (Sliced) + sprite-swap pressed/highlight
  transition. Styles: `Primary` (blue gloss), `Nav` (grey flat), `Icon` (grey square).
- Everything is null-safe and gated on `KenneyUiSkin.Available`, so if the sprites
  aren't present the UI is simply left unskinned — nothing breaks.

Screens call it from their existing `Awake`/`Start`:
`CardCollectionScreen`, `DeckListPanel`, `DeckEditPanel`, `CardCatalogPanel`,
`CraftingPanel`, `CardDetailPanel`, plus per-row/cell buttons in `CardCollectionItem`,
`CraftingRecipeItem`, `UpgradeOptionItem`, and the runtime-spawned deck/catalog rows.
The rarity bar, faction icon and card-art Images are deliberately **not** skinned
(they are code-tinted / hold card art — see step 2 notes).

### REMAINING MANUAL STEP (one-time, [EDITOR])

`Resources.Load` only sees assets under a `Resources/` folder. The raw pack at
`Assets/Art/KenneyUI/` is NOT loadable at runtime. Copy these 9 PNGs into
`Assets/Resources/Art/KenneyUI/`, **preserving the sub-paths**, then import each as
**Sprite (2D and UI)** and set 9-slice borders on the panel/button/input ones:

```
Assets/Resources/Art/KenneyUI/RPGExpansion/PNG/panel_brown.png
Assets/Resources/Art/KenneyUI/RPGExpansion/PNG/panelInset_beige.png
Assets/Resources/Art/KenneyUI/PNG/Blue/Default/button_rectangle_depth_gloss.png
Assets/Resources/Art/KenneyUI/PNG/Blue/Default/button_rectangle_gloss.png
Assets/Resources/Art/KenneyUI/PNG/Grey/Default/button_rectangle_depth_flat.png
Assets/Resources/Art/KenneyUI/PNG/Grey/Default/button_rectangle_flat.png
Assets/Resources/Art/KenneyUI/PNG/Grey/Default/button_square_depth_flat.png
Assets/Resources/Art/KenneyUI/PNG/Grey/Default/button_square_flat.png
Assets/Resources/Art/KenneyUI/PNG/Extra/Default/input_rectangle.png
```

(`KenneyUiSkin.ResourceRoots` also accepts a flatter `Assets/Resources/KenneyUI/...`
layout if you prefer to drop the `Art/` segment.) No `.cs` or scene/prefab edits are
needed after copying — the skin applies on play.
