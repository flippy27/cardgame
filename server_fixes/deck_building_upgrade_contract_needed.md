# Deck Building Upgrade Contract Needed

The client-side `UpgradeConfig` ScriptableObject was removed. Deck building now shows card details and applied upgrade history from the backend, but it does not compute available upgrade options locally.

To re-enable upgrade buttons in a server-authoritative way, backend should expose an atomic upgrade option contract.

Suggested endpoints:

```text
GET  /api/v1/players/{userId}/cards/{playerCardId}/upgrade-options
POST /api/v1/players/{userId}/cards/{playerCardId}/upgrade-options/{optionId}/apply
```

The GET response should include everything the UI needs to render without guessing:

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

The POST response should return updated player card detail plus updated inventory balances, after validating ownership and deducting items in one transaction.
