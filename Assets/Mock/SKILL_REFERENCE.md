# Skill System Reference

Complete list of all available skills and their properties.

## Defensive Skills (Protection & Survival)

| Skill | Type | Trigger | Effect |
|-------|------|---------|--------|
| **Regenerate** | Healing | OnTurnEnd | Heals 1 HP per turn |
| **Shield** | Damage Block | OnBattlePhase | Absorbs one full attack |
| **Taunt** | Forced Target | OnBattlePhase | All enemy attacks must target this card |
| **Dodge** | Evasion | OnBattlePhase | Ranged attacks cannot target this card |
| **Evasion** | Chance Block | OnBattlePhase | X% chance to dodge any attack |
| **Reflection** | Damage Return | OnBattlePhase | Reflects X% damage back to attacker |
| **Fly** | Position Lock | OnBattlePhase | Only flying units can attack this card |

## Offensive Skills (Damage & Control)

| Skill | Type | Trigger | Effect |
|-------|------|---------|--------|
| **Trample** | Armor Ignore | OnBattlePhase | Ignores armor when attacking |
| **Cleave** | AoE | OnBattlePhase | Hits all enemies in same row |
| **Poison** | DoT | OnBattlePhase | Applies 1 poison stack (damage per turn) |
| **Stun** | Disable | OnBattlePhase | Target skips next attack |
| **Execute** | Scaling | OnBattlePhase | Bonus damage proportional to missing health |
| **Ricochet** | Bounce | OnBattlePhase | After kill, excess damage hits adjacent card |
| **Leech** | Sustain | OnBattlePhase | Heals hero equal to 50% damage dealt |
| **Enrage** | Growth | OnDamaged | Gains +1 ATK when damaged |
| **Mana Burn** | Resource | OnBattlePhase | Costs enemy 1 mana when attacking |

## Utility Skills (Synergy & Special)

| Skill | Type | Trigger | Effect |
|-------|------|---------|--------|
| **Last Stand** | Synergy | OnBattlePhase | If alone on board, deal double damage |
| **Charge** | Initiative | OnPlay | Can attack the turn it's played |
| **Haste** | Speed | OnPlay | Can attack immediately when played |
| **Lifelink** | Sustain | OnBattlePhase | Heals hero equal to damage dealt |

## Skill Triggers

### Available Triggers
- **OnPlay** - When card is played from hand
- **OnTurnStart** - At start of turn
- **OnTurnEnd** - At end of turn
- **OnBattlePhase** - During attack execution
- **OnDamaged** - When card takes damage
- **OnDeath** - When card dies

### Trigger Flow (Per Turn)

```
1. OnPlay (if new card played)
2. OnTurnStart (all cards)
3. OnBattlePhase (attacking cards)
4. OnDamaged (damaged cards)
5. OnDeath (dying cards)
6. OnTurnEnd (all cards)
```

## Skill Categories

### By Impact Type
- **Protective** - Shield, Dodge, Evasion, Reflection, Taunt, Fly
- **Scaling** - Enrage, Last Stand, Execute
- **Utility** - Regenerate, Charge, Haste, Lifelink, Leech
- **Control** - Stun, Poison, Mana Burn
- **Crowd Control** - Cleave, Trample, Ricochet

### By Target
- **Self-only** - Regenerate, Enrage, Last Stand, Charge, Haste
- **Enemy-targeting** - Poison, Stun, Execute, Ricochet, Taunt, Mana Burn
- **Both** - Trample, Cleave, Shield, Dodge, Evasion, Reflection, Fly, Leech, Lifelink

## Deck Archetypes

### Defensive Fortress
Focus: Shields, armor, tanking
Core Cards: Shielded Guardian, Regenerating Warrior, Taunt Bearer

### Aggressive Assault  
Focus: Damage, fast finish
Core Cards: Trampling Beast, Cleaving Knight, Charging Cavalry, Enraged Berserker

### Control Master
Focus: Stuns, disables, mana denial
Core Cards: Poisoner, Stunning Mage, Mana Drainer, Executioner

### Balanced Builder
Focus: Mix of mechanics, flexibility
Core Cards: Ricochet Archer, Evasive Scout, Regenerating Warrior

### Synergy Combo
Focus: Multiple abilities working together
Core Cards: Multi-Skill Champion, Last Stand Hero, Leeching Vampire, Reflecting Mage
