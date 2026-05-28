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
    DeckListPanel        -> DeckListPanel, starts inactive
      DeckEditPanel      -> DeckEditPanel, starts inactive (child of DeckListPanel)
        CardCatalogPanel -> CardCatalogPanel, starts inactive (child of DeckEditPanel)
    CardCatalogPanel2    -> CardCatalogPanel (browse mode), starts inactive
```

## CardCollectionScreen

Wire these references:

```text
dustAmountText         Header/DustBadge/DustAmountText
backButton             Header/BackButton
searchField            FilterBar/SearchField
rarityDropdown         FilterBar/RarityDropdown
factionDropdown        FilterBar/FactionDropdown
clearFiltersButton     FilterBar/ClearFiltersButton
cardGridContent        CollectionScrollView/Viewport/Content
cardItemPrefab         CardCollectionItem_Prefab
prevPageButton         Pagination/PrevButton
nextPageButton         Pagination/NextButton
pageLabel              Pagination/PageLabel
craftCardsButton       ActionBar/CraftCardsButton
deckManagementButton   ActionBar/DecksButton
cardCatalogButton      ActionBar/CatalogButton
statusText             StatusText
loadingOverlay         LoadingOverlay
craftingPanel          CraftingPanel component
cardDetailPanel        CardDetailPanel component
deckListPanel          DeckListPanel component
cardCatalogPanel       CardCatalogPanel component (browse mode)
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
upgradePresets          editable generic server POST presets
statusText
loadingOverlay
```

Data used:

```text
GET /api/v1/players/{userId}/cards/{playerCardId}
GET /api/v1/players/{userId}/cards/{playerCardId}/upgrades
POST /api/v1/players/{userId}/cards/{playerCardId}/upgrades
```

There is no `UpgradeConfig` ScriptableObject anymore. `Upgrade Presets` are generic request presets only:

```text
upgradeKind
intValue
stringValue
appliedBy
note
```

For example, `attack_bonus + intValue=1` sends the same body used by Swagger. The backend validates ownership, cost and effect; Unity refreshes the card detail after success.

## DeckListPanel / DeckEditPanel

Wire `DeckListPanel` to `CardCollectionScreen.deckListPanel` and the ActionBar deck button to `deckManagementButton`.

Deck list data:

```text
GET /api/v1/decks/{playerId}
```

Deck edit save:

```text
PUT /api/v1/decks
```

The backend currently requires a `deckId`, so Unity generates `deck_{guid}` when creating a new deck. Edits are done as a local working copy and saved with a full `cardIds` list once the deck is valid.

Validation mirrors the current backend:

```text
20-30 cards
max 3 copies per cardId
only owned cards shown for deck editing
```

Deck-level deletion is not fully wired because the current backend only exposes per-card entry deletion, not `DELETE /api/v1/decks/{playerId}/{deckId}`. See `server_fixes/deck_building_contract_findings.md`.

`DeckEditPanel` should wire:

```text
titleText
closeButton
deckNameInput
deckCardsContainer
deckCardRowPrefab
cardCountText
addCardsButton
saveButton
cardCatalogPanel
validationText
statusText
loadingOverlay
```

Deck editing uses only cards owned by the player, from:

```text
GET /api/v1/players/{userId}/cards/summary
```

`CardCatalogPanel` in deck edit mode defaults to `deckEditShowsOwnedCardsOnly = true`, so it does not let you add catalog cards that the player has not crafted/owned yet.

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
POST /api/v1/players/{userId}/cards/{playerCardId}/upgrades
GET  /api/v1/crafting/cards
POST /api/v1/crafting/cards/{cardId}
GET  /api/v1/decks/{playerId}
PUT  /api/v1/decks
```
