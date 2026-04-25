# DeckBuilding Scene — Unity Setup Guide
*Aligned to CLIENT_CONTRACT_PLAYER_OWNERSHIP.md v1.0*

---

## 1. Create the Scene

1. **File → New Scene** → save as `Assets/Scenes/DeckBuildingScene.unity`
2. **File → Build Settings** → add `DeckBuildingScene` to build list (index 2+)

---

## 2. Root Hierarchy

```
DeckBuildingScene
└── Canvas (Screen Space – Overlay, Scale With Screen Size 1080×1920)
    └── DeckBuildingRoot      ← attach CardCollectionScreen here
        ├── Header
        ├── FilterBar
        ├── CollectionScrollView
        ├── Pagination
        ├── ActionBar
        ├── StatusText
        ├── LoadingOverlay
        ├── CraftingPanel         ← starts INACTIVE — attach CraftingPanel component
        └── CardDetailPanel       ← starts INACTIVE — attach CardDetailPanel component
```

---

## 3. CardCollectionScreen Inspector Wiring

Attach `CardCollectionScreen` to `DeckBuildingRoot`.

| Field                | Object path                                     |
|----------------------|-------------------------------------------------|
| dustAmountText       | Header/DustBadge/DustAmountText                 |
| backButton           | Header/BackButton                               |
| searchField          | FilterBar/SearchField                           |
| rarityDropdown       | FilterBar/RarityDropdown                        |
| factionDropdown      | FilterBar/FactionDropdown                       |
| clearFiltersButton   | FilterBar/ClearFiltersButton                    |
| cardGridContent      | CollectionScrollView/Viewport/Content           |
| cardItemPrefab       | Prefabs/CardCollectionItem_Prefab               |
| pageSize             | 12 (adjust to grid columns × rows)              |
| prevPageButton       | Pagination/PrevButton                           |
| nextPageButton       | Pagination/NextButton                           |
| pageLabel            | Pagination/PageLabel                            |
| craftCardsButton     | ActionBar/CraftCardsButton                      |
| statusText           | StatusText                                      |
| loadingOverlay       | LoadingOverlay                                  |
| craftingPanel        | CraftingPanel (component on CraftingPanel GO)   |
| cardDetailPanel      | CardDetailPanel (component on CardDetailPanel GO)|

---

## 4. CollectionScrollView → Content

GridLayoutGroup settings:
- Cell Size: `200 × 260`
- Spacing: `10 × 10`
- Constraint: Fixed Column Count = 3

Content needs `ContentSizeFitter` (Vertical Fit = Preferred Size).

---

## 5. CardCollectionItem_Prefab

```
CardCollectionItem  (Image bg, CardCollectionItem + Button)
├── CardArtImage    (Image — art placeholder)
├── CardNameText    (Text — displayName, bottom area)
├── CopiesBadge     (Image — top-right chip)
│   └── CopiesText  (Text — "×2")
├── RarityBar       (Image — thin strip at bottom, color set by script)
└── FactionIcon     (Image — small faction color tint, optional)
```

Inspector wiring on component:
- `cardArtImage` → CardArtImage
- `cardNameText` → CardNameText
- `copiesText`   → CopiesBadge/CopiesText
- `rarityBar`    → RarityBar
- `factionIcon`  → FactionIcon
- `selectButton` → root Button

---

## 6. CraftingPanel Hierarchy & Wiring

```
CraftingPanel  (Image bg, CraftingPanel component, starts INACTIVE)
├── Header
│   ├── TitleText           "Crafting Workshop"
│   ├── DustBadge/DustAmountText   ← dustAmountText
│   └── CloseButton                ← closeButton
├── FilterRow
│   └── AffordableOnlyToggle       ← affordableOnlyToggle
├── RecipeScrollView
│   └── Viewport → Content (VerticalLayout + ContentSizeFitter)
│                              ← recipeListContainer
├── StatusText                 ← statusText
└── LoadingOverlay             ← loadingOverlay
```

`recipeItemPrefab` → Prefabs/CraftingRecipeItem_Prefab

---

## 7. CraftingRecipeItem_Prefab

```
CraftingRecipeItem  (CraftingRecipeItem component)
├── CardNameText        (Text)
├── RarityText          (Text — optional)
├── CostContainer       (HorizontalLayoutGroup)
│   └── [CostChip × N — spawned, assign costChipPrefab]
│       └── CostLabel   (Text)
├── AffordabilityText   (Text)
└── CraftButton         (Button)
    └── CraftButtonText (Text)
```

`CostChip_Prefab`: simple Image + child Text.

---

## 8. CardDetailPanel Hierarchy & Wiring

```
CardDetailPanel  (Image bg, CardDetailPanel component, starts INACTIVE)
├── Header
│   ├── CardNameText            ← cardNameText
│   ├── LevelText               ← levelText
│   └── CloseButton             ← closeButton
├── StatsSection
│   ├── AttackText              ← attackText   "ATK: 3 → 5"
│   ├── HealthText              ← healthText
│   ├── ArmorText               ← armorText
│   ├── RarityText              ← rarityText
│   └── FactionText             ← factionText
├── UpgradesAppliedSection
│   ├── Title "Upgrades Applied"
│   └── UpgradeHistoryContainer (VertLayout)   ← upgradeHistoryContainer
│       └── [Text rows — spawned, assign upgradeHistoryRowPrefab]
├── UpgradeOptionsSection
│   ├── Title "Available Upgrades"
│   └── UpgradeOptionsContainer (VertLayout)   ← upgradeOptionsContainer
│       └── [UpgradeOptionItem × N — spawned, assign upgradeOptionItemPrefab]
├── StatusText                  ← statusText
└── LoadingOverlay              ← loadingOverlay
```

Also assign `upgradeConfig` → Resources/UpgradeConfig.asset (see §10).

---

## 9. UpgradeOptionItem_Prefab

```
UpgradeOptionItem  (UpgradeOptionItem component)
├── UpgradeNameText     (Text — "+2 ATK")
├── DescriptionText     (Text — optional)
├── CostContainer       (HorizontalLayoutGroup)
│   └── [CostChip × N — spawned, same CostChip_Prefab]
├── AffordabilityText   (Text)
└── ApplyButton         (Button)
    └── ApplyButtonText (Text)
```

---

## 10. UpgradeConfig ScriptableObject

Create: **Assets → Create → CardDuel → UpgradeConfig** → save at `Assets/Resources/UpgradeConfig.asset`

Example configuration:

| upgradeKind    | displayName | intValue | costs                                           |
|----------------|-------------|----------|--------------------------------------------------|
| `attack_bonus` | +2 ATK      | 2        | upgrade_stone ×1, card_dust ×50                 |
| `health_bonus` | +2 HP       | 2        | upgrade_stone ×1, card_dust ×50                 |
| `armor_bonus`  | +1 ARM      | 1        | upgrade_stone ×1, card_dust ×30                 |
| `level_up`     | Level Up    | 0        | upgrade_stone ×2, card_dust ×100                |
| `added_ability`| Add Ability | 0        | ability_tome ×1, card_dust ×200 (set stringValue to ability_id) |

Item type keys (from contract §1):
`card_dust`, `arcane_shard`, `essence_of_void`, `upgrade_stone`, `ability_tome`,
`faction_ember`, `faction_tidal`, `faction_grove`, `faction_alloy`, `faction_void`

---

## 11. Backend Contract Summary

| Use case                        | Endpoint                                               |
|---------------------------------|--------------------------------------------------------|
| Player card collection (album)  | `GET /api/v1/players/{userId}/cards/summary`           |
| Card detail + upgrades          | `GET /api/v1/players/{userId}/cards/{playerCardId}`    |
| Apply upgrade                   | `POST /api/v1/players/{userId}/cards/{playerCardId}/upgrades` |
| Inventory balances              | `GET /api/v1/players/{userId}/inventory`               |
| Single item balance             | `GET /api/v1/players/{userId}/inventory/{itemTypeKey}` |
| Craftable cards + requirements  | `GET /api/v1/crafting/cards`                           |
| Craft a card                    | `POST /api/v1/crafting/cards/{cardId}` (no body)       |
| Item type catalog               | `GET /api/v1/items`                                    |

### Upgrade flow (non-atomic — see contract §4)
```
Client verifies balance (InventoryService.CanAfford)
→ POST /players/{userId}/cards/{playerCardId}/upgrades   ← server applies upgrade (NO item deduction)
→ InventoryService.InvalidateCache()                      ← force fresh fetch next access
```

> When the backend adds an atomic upgrade+consume endpoint, update
> `PlayerCardCollectionService.ApplyUpgradeAsync()` to call it instead.

### Craft flow (atomic — server handles deduction)
```
POST /api/v1/crafting/cards/{cardId}
→ server checks inventory, deducts items, creates player_card in one transaction
→ response includes updatedInventory[] for local cache update
```

---

## 12. Navigation

- No deck → `DeckSelectionScreen` auto-redirects to `DeckBuildingScene`
- "Build Deck" button → `SceneBootstrap.LoadDeckBuilding()`
- Back button in screen → `SceneBootstrap.LoadMenu()`
- Scene name constant: `SceneBootstrap.DeckBuildingSceneName = "DeckBuildingScene"`

---

## 13. Extension Points

| Where                           | How                                                    |
|---------------------------------|--------------------------------------------------------|
| Card tap → multi-copy picker    | If `ownedCopies > 1`, show instance list before opening CardDetailPanel |
| Faction filter                  | Already wired — data present in `PlayerCardSummaryEntryDto.ownedInstances[0].cardFaction` |
| Add card to deck                | Add deck-select button in CardDetailPanel, call `DeckManagementService` |
| Disenchant / sell card          | `DELETE /players/{userId}/cards/{playerCardId}` + `POST /inventory/grant` |
| Match reward grant              | `POST /players/{userId}/inventory/grant` with `reason: "match_win"` |
| Dynamic upgrade costs from API  | Add `GET /crafting/upgrades` endpoint; replace UpgradeConfig with that response |
