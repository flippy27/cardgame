# UI Changes To Wire In Unity

## MainMenu

Add a button for deck building and assign it in:

```text
MatchmakingPanelController.deckBuildingButton
```

The button is hidden while in a match/session and loads `DeckBuildingScene` only after login.

## DeckBuildingScene

`CardCollectionItem` and `CraftingRecipeItem` can now use `CardSurfaceVisualRenderer` exactly like battle cards. Either:

```text
assign visualRenderer manually
```

or assign only:

```text
cardArtImage
```

and the script will create a default image binding at runtime.

Recommended surfaces:

```text
collection  collection grid and crafting recipe cards
detail      CardDetailPanel
```

## CardDetailPanel

Remove any `UpgradeConfig` asset/reference. The client no longer owns upgrade costs. The panel will show a placeholder in `upgradeOptionsContainer` until the backend exposes server upgrade options.

Optional visual setup:

```text
CardDetailPanel.visualRenderer
CardDetailPanel.cardArtImage
CardDetailPanel.visualSurface = detail
```

## MainGame Destroy Drop Zone

Create a trash/delete UI element or world object and add:

```text
BoardCardDestroyDropZone
```

If it is UI, it can be a normal `RectTransform` under the Canvas. If it is world-space, add a Collider.

Defaults:

```text
acceptedPlayerIndex = 0
```

Flow:

```text
hold/drag a local played card -> move actual card visually -> release over trash zone -> client calls server DestroyCard
release elsewhere -> card returns to original slot
```

This is server-authoritative. Unity does not remove the card until the next server snapshot/event confirms it.
