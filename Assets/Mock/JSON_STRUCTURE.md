# JSON Data Structure Reference

Complete documentation of all JSON files and their schemas.

## Directory Structure

```
Assets/
├── Mock/
│   ├── Skills/
│   │   └── skills.json (future expansion)
│   ├── Abilities/
│   │   └── abilities.json
│   ├── Cards/
│   │   └── cards.json
│   ├── Decks/
│   │   └── decks.json
│   ├── SKILL_REFERENCE.md
│   └── JSON_STRUCTURE.md
└── Runtime/
    └── Data/
        ├── Abilities/ (generated assets)
        ├── Cards/ (generated assets)
        └── Decks/ (generated assets)
```

## abilities.json Schema

Defines all ability effects triggered during gameplay.

```json
{
  "abilities": [
    {
      "abilityId": "string - unique identifier",
      "displayName": "string - player-facing name",
      "description": "string - tooltip text",
      "trigger": "OnPlay|OnTurnStart|OnTurnEnd|OnBattlePhase|OnDamaged|OnDeath",
      "skillType": "Defensive|Offensive|Synergy|Utility",
      "effects": [
        {
          "effectType": "BuffAttackEffectDefinition|DamageEffectDefinition|HealEffectDefinition|GainArmorEffectDefinition|HitHeroEffectDefinition",
          "amount": "int - primary parameter",
          "damage": "int - damage parameter (if applicable)",
          "ignoreArmor": "bool - if true, bypasses enemy armor"
        }
      ]
    }
  ]
}
```

### Available Effect Types

| Effect Type | Parameters | Purpose |
|------------|-----------|---------|
| `BuffAttackEffectDefinition` | amount | Increases ATK stat |
| `DamageEffectDefinition` | amount, ignoreArmor | Deals damage |
| `HealEffectDefinition` | amount | Heals target |
| `GainArmorEffectDefinition` | amount | Adds armor |
| `HitHeroEffectDefinition` | amount | Damages enemy hero directly |

### Available Triggers

| Trigger | When | Use Cases |
|---------|------|-----------|
| `OnPlay` | Card enters board | Haste, Charge effects |
| `OnTurnStart` | Turn begins | Draw/resource effects |
| `OnTurnEnd` | Turn ends | Regenerate, DoT processing |
| `OnBattlePhase` | During combat | Passive damage/defense abilities |
| `OnDamaged` | Card takes damage | Enrage, shield triggers |
| `OnDeath` | Card dies | Revenge/revenge damage |

## cards.json Schema

Defines all playable cards and their properties.

```json
{
  "cards": [
    {
      "cardId": "string - unique identifier",
      "displayName": "string - card name",
      "description": "string - flavor text",
      "faction": "Ember|Nature|Shadow|Celestial",
      "rarity": "Common|Uncommon|Rare|Epic|Legendary",
      "cardType": "Unit|Spell|Artifact",
      "manaCost": "int - mana required to play",
      "attack": "int - attack stat",
      "health": "int - health stat",
      "armor": "int - armor stat (damage reduction)",
      "unitType": "Melee|Ranged|Magic - only for Unit cardType",
      "abilities": ["string array - ability IDs to attach"]
    }
  ]
}
```

### Card Factions

| Faction | Theme | Color |
|---------|-------|-------|
| `Ember` | Fire, aggression, speed | Red |
| `Nature` | Growth, balance, healing | Green |
| `Shadow` | Control, debuffs, mana | Purple |
| `Celestial` | Protection, utility, synergy | Blue/White |

### Card Rarities

| Rarity | Strength | Appearance |
|--------|----------|-----------|
| `Common` | Basic | 1 copy minimum |
| `Uncommon` | Moderate | 2 copies |
| `Rare` | Strong | 2 copies, unique mechanics |
| `Epic` | Very Strong | 1 copy |
| `Legendary` | Powerful | 1 copy, special |

### Unit Types (for Units only)

| Type | Range | Attack Pattern |
|------|-------|-----------------|
| `Melee` | Adjacent | Straight-line (same row) |
| `Ranged` | Far | Straight-line (same slot) |
| `Magic` | Far | Diagonal (left-right diagonal) |

## decks.json Schema

Defines deck presets for quick deck building.

```json
{
  "decks": [
    {
      "deckId": "string - unique identifier",
      "displayName": "string - deck name",
      "description": "string - deck strategy",
      "deckType": "Defensive|Aggressive|Control|Balanced|Synergy",
      "faction": "Ember|Nature|Shadow|Celestial",
      "cards": [
        {
          "cardId": "string - card ID to include",
          "quantity": "int - how many copies (1-3)"
        }
      ]
    }
  ]
}
```

### Deck Types

| Type | Strategy | Goal |
|------|----------|------|
| `Defensive` | Shields, armor, healing | Survive and outlast |
| `Aggressive` | High ATK, rush | Fast wins |
| `Control` | Stuns, disables, debuffs | Counter opponent |
| `Balanced` | Mix of mechanics | Flexibility |
| `Synergy` | Multiple effects working together | Combo damage |

## Generation Flow

### Editor Tool Flow

```
Tools/Data/Generate All
├── Generate Abilities
│   └── abilities.json → AbilityDefinition assets
│       └── Each ability creates nested EffectDefinition assets
├── Generate Cards
│   └── cards.json → CardDefinition assets
│       └── Links to AbilityDefinition assets by ID
└── Generate Decks
    └── decks.json → DeckDefinition assets
        └── Links to CardDefinition assets by ID
```

### Asset Creation Order

1. **Abilities** - Created first, because cards reference them
   - Creates ability assets in `Assets/Runtime/Data/Abilities/`
   - Each ability includes its effect definitions as sub-assets
   - Naming: `Ability_{abilityId}.asset`

2. **Cards** - Created after abilities
   - Creates card assets in `Assets/Runtime/Data/Cards/`
   - References ability assets by loaded abilityId
   - Naming: `Card_{cardId}.asset`

3. **Decks** - Created after cards
   - Creates deck assets in `Assets/Runtime/Data/Decks/`
   - References card assets by loaded cardId
   - Naming: `Deck_{deckId}.asset`

## Usage Examples

### Add New Ability

1. Edit `Assets/Mock/Abilities/abilities.json`
2. Add new ability object with unique `abilityId`
3. In Unity Editor: Tools → Data → Generate Abilities
4. New ability asset appears in `Assets/Runtime/Data/Abilities/`

### Add New Card

1. Edit `Assets/Mock/Cards/cards.json`
2. Add new card object with unique `cardId`
3. Reference abilities by their `abilityId` in abilities array
4. In Unity Editor: Tools → Data → Generate Cards
5. New card asset appears in `Assets/Runtime/Data/Cards/`

### Create New Deck

1. Edit `Assets/Mock/Decks/decks.json`
2. Add new deck with unique `deckId`
3. Reference cards by their `cardId` and quantity
4. In Unity Editor: Tools → Data → Generate Decks
5. New deck asset appears in `Assets/Runtime/Data/Decks/`

## Future Extensions

### Skills.json (Planned)

For extensibility without code changes:
```json
{
  "skills": [
    {
      "skillId": "skill_id",
      "displayName": "Skill Name",
      "description": "Skill description",
      "skillType": "Defensive|Offensive|Utility",
      "metadata": {}
    }
  ]
}
```

### Game Balance

- Modify card stats directly in JSON
- Adjust ability effects (amounts, triggers)
- Rebalance with `Generate All` in editor
- No code recompilation needed

### Version Control

- All JSON files in `Assets/Mock/` are version controlled
- Generated assets auto-create `.meta` files
- Keep `.meta` files in version control
- Regenerate assets: `Tools → Data → Generate All`
