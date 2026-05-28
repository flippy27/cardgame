# Deck Building Upgrade Contract Notes

The client-side `UpgradeConfig` ScriptableObject was removed. Deck building now shows card details, applied upgrade history, and sends generic upgrade requests to the backend.

Current implemented client request:

```text
POST /api/v1/players/{userId}/cards/{playerCardId}/upgrades
```

Body:

```json
{
  "upgradeKind": "attack_bonus",
  "intValue": 1,
  "stringValue": "",
  "appliedBy": "player",
  "note": ""
}
```

The Unity UI exposes these as editable `GenericUpgradePreset` entries on `CardDetailPanel`. The client does not calculate cost, requirements, or final effects; the backend validates the request.

Optional future improvement if we want the UI to show affordability before clicking:

```text
GET  /api/v1/players/{userId}/cards/{playerCardId}/upgrade-options
```

Suggested response:

```json
{
  "playerCardId": "uuid",
  "options": [
    {
      "optionId": "attack_bonus_1",
      "displayName": "+1 ATK",
      "description": "Increase attack by 1.",
      "canAfford": true,
      "requirements": [
        {
          "itemTypeKey": "card_dust",
          "itemTypeDisplayName": "Card Dust",
          "quantityRequired": 50,
          "ownedQuantity": 120,
          "iconAssetRef": "items/card_dust"
        }
      ]
    }
  ]
}
```

This is not required for the current Unity implementation, but would let the UI display disabled/enabled upgrade buttons and exact costs before sending the generic POST.
