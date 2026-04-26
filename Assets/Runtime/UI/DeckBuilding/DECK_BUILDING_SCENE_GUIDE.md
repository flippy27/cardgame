# DeckBuilding Scene - Unity Setup Guide

This scene is client-display only. Card ownership, inventory, crafting and upgrades are owned by the backend. Do not add ScriptableObject configs for gameplay costs or upgrade rules.

## Scene

Add `DeckBuildingScene` to Build Settings and keep `SceneBootstrap.DeckBuildingSceneName = "DeckBuildingScene"`.

Root setup:

```text
DeckBuildingScene
Canvas
  DeckBuildingRoot       -> CardCollectionScreen
    Header
    FilterBar
    CollectionScrollView
    Pagination
    ActionBar
    StatusText
    LoadingOverlay
    CraftingPanel        -> CraftingPanel, starts inactive
    CardDetailPanel      -> CardDetailPanel, starts inactive
```

## CardCollectionScreen

Wire these references:

```text
dustAmountText       Header/DustBadge/DustAmountText
backButton           Header/BackButton
searchField          FilterBar/SearchField
rarityDropdown       FilterBar/RarityDropdown
factionDropdown      FilterBar/FactionDropdown
clearFiltersButton   FilterBar/ClearFiltersButton
cardGridContent      CollectionScrollView/Viewport/Content
cardItemPrefab       CardCollectionItem_Prefab
prevPageButton       Pagination/PrevButton
nextPageButton       Pagination/NextButton
pageLabel            Pagination/PageLabel
craftCardsButton     ActionBar/CraftCardsButton
statusText           StatusText
loadingOverlay       LoadingOverlay
craftingPanel        CraftingPanel component
cardDetailPanel      CardDetailPanel component
```

Data used here:

```text
GET /api/v1/players/{userId}/cards/summary
GET /api/v1/players/{userId}/inventory
```

`userId` comes from `AuthService.CurrentPlayerId`, not from scene assumptions.

## Card Visuals

Deck building uses the same runtime visual path as battle cards:

```text
CardSurfaceVisualRenderer -> CardVisualAssetResolver -> backend visualProfiles/layers/assetRef
```

For collection/crafting prefabs:

```text
CardCollectionItem.cardArtImage      optional fallback Image binding
CardCollectionItem.visualRenderer    optional explicit renderer
CraftingRecipeItem.cardArtImage      optional fallback Image binding
CraftingRecipeItem.visualRenderer    optional explicit renderer
```

If the backend sends an `assetRef`, Unity resolves that exact string. If it is missing or not registered in `CardVisualAssetResolver`, the card shows the magenta missing-asset placeholder. Do not infer paths like `icons/skills/...` in client code.

## CraftingPanel

Wire:

```text
dustAmountText          Header/DustBadge/DustAmountText
closeButton             Header/CloseButton
affordableOnlyToggle    FilterRow/AffordableOnlyToggle
recipeListContainer     RecipeScrollView/Viewport/Content
recipeItemPrefab        CraftingRecipeItem_Prefab
statusText              StatusText
loadingOverlay          LoadingOverlay
```

Data used:

```text
GET  /api/v1/crafting/cards
POST /api/v1/crafting/cards/{cardId}
GET  /api/v1/players/{userId}/inventory
```

Crafting is atomic on the backend. The client only displays affordability and sends the craft request.

## CardDetailPanel

Wire:

```text
cardNameText
levelText
closeButton
visualRenderer          optional, surface "detail"
cardArtImage            optional fallback Image binding
attackText
healthText
armorText
rarityText
factionText
upgradeHistoryContainer
upgradeHistoryRowPrefab
upgradeOptionsContainer
upgradeOptionItemPrefab optional placeholder row
statusText
loadingOverlay
```

Data used:

```text
GET /api/v1/players/{userId}/cards/{playerCardId}
GET /api/v1/players/{userId}/cards/{playerCardId}/upgrades
```

Upgrade options are intentionally disabled in the client until the backend exposes available upgrade options plus atomic costs. There is no `UpgradeConfig` ScriptableObject anymore.

## MainMenu Button

Add a button to `MainMenu` and assign it to:

```text
MatchmakingPanelController.deckBuildingButton
```

The button calls `SceneBootstrap.LoadDeckBuilding()` only when the player is authenticated and hidden while the player is in an active session.

## Backend Contract Summary

```text
GET  /api/v1/items
GET  /api/v1/players/{userId}/inventory
GET  /api/v1/players/{userId}/cards
GET  /api/v1/players/{userId}/cards/summary
GET  /api/v1/players/{userId}/cards/{playerCardId}
GET  /api/v1/crafting/cards
POST /api/v1/crafting/cards/{cardId}
```

Future backend work needed for upgrades:

```text
GET  /api/v1/players/{userId}/cards/{playerCardId}/upgrade-options
POST /api/v1/players/{userId}/cards/{playerCardId}/upgrade-options/{optionId}/apply
```

The apply endpoint should validate ownership, affordability, deduct inventory and mutate the player card in one transaction.
