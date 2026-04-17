# Armor & Weapon Ideas

Equipment and augmentation system concepts for future expansion.

## Armor Concepts

### Basic Armor
- **Leather Armor**: +1 Armor (light, cheap)
- **Chainmail**: +2 Armor (medium weight)
- **Plate Armor**: +3 Armor (heavy, reduces move speed?)
- **Mithril Plate**: +4 Armor, +1 Max HP

### Special Armor
- **Damage Reflection Plate**: Reflects 25% of blocked damage back to attacker
- **Phase Armor**: Reduces incoming damage by 1 (minimum 1 always reaches)
- **Thorns Armor**: Deals 1 damage to any attacker
- **Scales of Protection**: Gain +1 Armor for each card on board

## Weapon Concepts

### Basic Weapons
- **Sword**: +1 Attack
- **Great Sword**: +2 Attack
- **Spear**: +2 Attack, gains +1 vs Ranged units
- **Dagger**: +1 Attack, Ranged units can hold it

### Special Weapons
- **Executioner's Axe**: Bonus damage to low-health enemies (+25% per 25% missing HP)
- **Plague Blade**: Poison effect (2 stacks per hit)
- **Stunning Mace**: Stun effect (25% chance)
- **Life Steal Blade**: Heals 50% of damage dealt
- **Holy Sword**: Gains +1/+1 for each poison/stun on enemy board
- **Cursed Blade**: Deals +2 damage but heals opponent's hero 1 HP

## Potion Concepts

### Combat Potions
- **Health Potion**: Heal 3 HP (single use, consumable)
- **Strength Potion**: +2 Attack this turn
- **Armor Potion**: +3 Armor this turn
- **Shield Potion**: Grant Shield status (block next attack)
- **Evasion Potion**: +50% evasion chance this turn
- **Haste Potion**: Draw 1 extra card this turn

## Buff / Debuff System

### Self-Buffs (Applied to friendly units)
- **Berserk**: +3 Attack, but takes +2 damage (high risk/reward)
- **Fortify**: +2 Armor, cannot attack (defensive stance)
- **Embolden**: +1 Attack and +1 Armor
- **Regeneration**: Heal 1 HP per turn (needs status tracking)
- **Invulnerability**: Cannot take damage for 1 turn

### Enemy Debuffs (Applied to opposing units)
- **Weaken**: -2 Attack for target
- **Fragile**: Target takes +1 extra damage
- **Curse**: Target's armor reduced by 50% (rounded down)
- **Silence**: Target cannot use skills next turn
- **Vulnerability**: Target takes double damage from next attack

## Implementation Notes

- Equipment would require new CardDefinition fields (equippedArmor, equippedWeapon)
- Potions could be single-use abilities or hero-targeted effects
- Buffs/Debuffs need status effect infrastructure similar to Poison/Stun
- Could introduce "Enchantment" cards that grant buffs to board
- Equipment could be cards played on top of existing cards (equipment slot concept)

## Balance Considerations

- Armor values should scale with game progression
- Weapons should provide meaningful tradeoffs (high attack vs utility)
- Potions should be limited/scarce to prevent overuse
- Debuffs should have counterplay (remove debuff effects)
- Equipment value caps should prevent infinite scaling

## Future Mechanics

- Equipment evolution/upgrade system
- Weapon enchantment combining
- Armor set bonuses (wearing full set grants bonus)
- Legendary equipment with unique effects
- Equipment breaking/durability system
