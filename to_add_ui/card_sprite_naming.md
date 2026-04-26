# Card Sprite Asset Refs

Unity does not invent visual asset paths anymore.

Everything visual comes from backend fields such as:

```text
visualProfiles.layers[].assetRef
ability.iconAssetRef
status.iconAssetRef
metadataJson.iconAssetRef
metadataJson.assetRef
```

The exact string sent by the backend must be registered in `CardVisualAssetResolver`.

## Resolver Lists

Use the categorized lists only to keep the inspector tidy:

```text
Frame Assets
Art Assets
Attack Icon Assets
Skill Icon Assets
Status Icon Assets
Misc Assets
Legacy Asset Entries
```

All lists are searched as one dictionary. The category does not change the key.

Example:

```text
assetRef = cards/grove/dragon_slayer/art
sprite   = your sprite
```

If backend sends `cards/grove/dragon_slayer/art`, Unity uses that exact entry. If backend sends anything else, Unity shows the magenta missing-asset placeholder.

## No Client Naming Assumptions

These old behaviors were removed:

```text
Resources.Load fallback
icons/skills/{abilityId}
icons/statuses/{statusName}
automatic status names for icons
```

You can still choose names like `icons/skills/poison`, but only because the backend sends exactly that string.

## Recommended Backend Shape

```json
{
  "visualProfiles": [
    {
      "profileKey": "default",
      "isDefault": true,
      "layers": [
        {
          "surface": "hand",
          "layer": "frame",
          "assetRef": "frames/common-hand",
          "sortOrder": 0
        },
        {
          "surface": "hand",
          "layer": "art",
          "assetRef": "art/grove_0007",
          "sortOrder": 10
        }
      ]
    }
  ],
  "abilities": [
    {
      "abilityId": "poison",
      "displayName": "Poison",
      "iconAssetRef": "icons/skills/poison"
    }
  ],
  "statusEffects": [
    {
      "kind": 0,
      "abilityId": "poison",
      "iconAssetRef": "icons/statuses/poisoned"
    }
  ]
}
```
