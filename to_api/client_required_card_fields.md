# Client Required Card Fields

Unity should not infer card identity or attack type from board position.

## Required In Card Catalog / Card By Id / Cards By Deck

Each card returned by:

- `GET /api/v1/cards`
- `GET /api/v1/cards/{cardId}`
- `GET /api/v1/cards/by-deck?playerid=...&deckid=...`

Should include:

```json
{
  "cardId": "ember_0032",
  "displayName": "Ember Dragon Slayer",
  "manaCost": 4,
  "attack": 5,
  "health": 4,
  "armor": 0,
  "cardType": "Unit",
  "unitType": 0,
  "battlePresentation": {
    "attackDeliveryType": "melee",
    "attackMotionLevel": 3,
    "attackShakeLevel": 3
  }
}
```

`unitType` must be stable and come from the card definition:

- `0`: Melee. Attacks only from Front unless a skill changes it.
- `1`: Ranged. Attacks only from BackLeft/BackRight unless a skill changes it.
- `2`: Magic. Attacks only from BackLeft/BackRight unless a skill changes it.

`attackDeliveryType` is optional if `unitType` is present:

- `0` resolves to `melee`.
- `1` resolves to `projectile`.
- `2` resolves to `beam`.

## Required In Match Snapshots

For hand cards and board occupants, include `unitType`.

```json
{
  "cardId": "tidal_0024",
  "displayName": "Tidal Inferno Beast",
  "unitType": 1,
  "attackDeliveryType": "projectile"
}
```

The client will no longer change attack type based on slot. If `unitType` is missing, Unity can only fall back to catalog data. If both snapshot and catalog are missing `unitType`, Unity falls back to melee and logs/visuals may be wrong for ranged or magic cards.

Unity now logs a warning like:

```text
[SnapshotConverter] Backend card data missing 'unitType' for card '...'.
```

If this appears, the backend should add `unitType` to either the match snapshot card/occupant or to the catalog/card-by-id/cards-by-deck payload.
