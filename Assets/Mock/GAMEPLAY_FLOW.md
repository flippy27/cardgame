# Complete Gameplay Flow

End-to-end explanation of how skills, abilities, cards, and decks interact during gameplay.

## Game Phases

### Pre-Battle Setup

```
1. Player selects Deck from DeckDefinition
2. Deck instantiated:
   - Get all CardDefinitions from deck.cards
   - Shuffle cards into player hand (respecting quantities)
3. Match starts with empty board
```

### Turn Structure

```
TURN START
├── OnTurnStart Abilities trigger
│   └── Example: Regenerate heals 1 HP
│
CARD PLAY PHASE
├── Player plays cards from hand to board
│   ├── Card enters board
│   ├── OnPlay Abilities trigger
│   │   └── Example: Charge/Haste attack immediately
│   └── Card added to board slot
│
ATTACK EXECUTION PHASE (ExecuteBattlePhase)
├── For each player's units (Front → BackLeft → BackRight)
│   ├── OnBattlePhase Abilities trigger
│   │   ├── Buff ATK (Enrage already triggered, apply cumulative)
│   │   ├── Set passive abilities (Fly blocks ranged, etc)
│   │   └── Special abilities ready (Cleave hits multiple, etc)
│   │
│   ├── Unit selects target using TargetSelector
│   │   ├── Default: StraightLineTargetSelector
│   │   │   └── Same slot as attacker (Front → Front, Left → Left, Right → Right)
│   │   ├── If target slot empty: try Front (tank)
│   │   ├── If Front also empty: damage enemy hero
│   │   └── Check Taunt: if enemy has Taunt, must target that card
│   │
│   ├── Damage calculation
│   │   ├── Base: attacker.attack - defender.armor
│   │   ├── Apply skill modifiers:
│   │   │   ├── Trample: ignore armor
│   │   │   ├── Execute: scale by missing health
│   │   │   ├── Leech: heal hero 50% of damage
│   │   │   └── Reflection: reflect damage back
│   │   ├── Apply defender skills:
│   │   │   ├── Shield: absorb attack (one-use)
│   │   │   ├── Dodge: ranged attacks miss
│   │   │   ├── Evasion: random dodge
│   │   │   └── Fly: only fliers can hit
│   │   └── Final damage → defender.health
│   │
│   ├── OnDamaged Abilities trigger
│   │   ├── Enrage: +1 ATK
│   │   └── Custom: any OnDamaged ability
│   │
│   ├── Check death
│   │   ├── If health ≤ 0:
│   │   │   ├── OnDeath Abilities trigger
│   │   │   │   └── Example: Revenge deals 2 damage to hero
│   │   │   ├── Card removed from board
│   │   │   ├── Reposition: back cards move forward
│   │   │   │   ├── Front dead → BackLeft moves to Front
│   │   │   │   └── Left dead → BackRight moves to Left
│   │   │   └── Board state updated
│   │
│   ├── Special attack mechanics
│   │   ├── Cleave: after calculating damage to target,
│   │   │   apply same damage to all other enemies in row
│   │   ├── Ricochet: if target dies,
│   │   │   excess damage bounces to adjacent card
│   │   └── Poison/DoT: handled in TurnEnd phase
│   │
│   └── Next unit in order
│
CLEANUP PHASE
├── Process status effects
│   ├── Poison: each poisoned card loses health
│   ├── Stun: remove stun for next turn
│   └── Other DoT effects
│
TURN END PHASE
├── OnTurnEnd Abilities trigger
│   └── Example: Regenerate heals 1 HP (second time in turn)
│
└── Next player's turn
```

## Example: Attack Sequence

### Setup

```
Player Board (Top):
├── Front: Cleaving Knight (3 ATK, Cleave ability)
├── BackLeft: Basic Soldier (1 ATK)
└── BackRight: Empty

Enemy Board (Bottom):
├── Front: Shielded Guardian (1 ATK, Shield ability)
├── BackLeft: Poisoner (1 ATK, Poison ability)
└── BackRight: Regenerating Warrior (2 ATK, Regenerate ability)
```

### Turn Execution

```
PLAYER TURN START
└── OnTurnStart
    └── No triggers

CARD PLAY
└── Player plays card (not shown in this example)

ATTACK EXECUTION
│
├─ Cleaving Knight attacks
│  ├─ OnBattlePhase
│  │  └── No buffs
│  ├─ Select target
│  │  └── StraightLineTargetSelector → Enemy Front
│  ├─ Damage calculation
│  │  ├─ Base: 3 - 2 (armor) = 1
│  │  ├─ Cleave active: hits all enemies
│  │  └─ Results: Front 1 dmg, Left 1 dmg, Right 1 dmg
│  ├─ OnDamaged triggers
│  │  ├─ Shielded Guardian: Shield absorbs 1, takes 0
│  │  ├─ Poisoner: takes 1, health 2→1, Enrage: ATK 1→2
│  │  └─ Regenerating Warrior: takes 1, health 2→1
│  └─ No deaths yet
│
├─ Basic Soldier attacks
│  ├─ OnBattlePhase
│  │  └── No triggers
│  ├─ Select target
│  │  └── StraightLineTargetSelector → Enemy Left
│  ├─ Damage calculation
│  │  └─ Base: 1 - 0 = 1
│  ├─ OnDamaged
│  │  └─ Poisoner: health 1→0 (DIES)
│  ├─ Death check
│  │  ├─ OnDeath
│  │  │  └── No triggers for Poisoner
│  │  ├─ Remove Poisoner
│  │  └─ Reposition: Regenerating Warrior moves Left
│  └─ Updated board:
│      ├─ Front: Shielded Guardian
│      ├─ BackLeft: Regenerating Warrior (was Right)
│      └─ BackRight: Empty
│
└─ No more units

CLEANUP PHASE
├── Poison processing
│   └── (No poisoned cards this turn)
└── Stun clearing
    └── (No stunned cards)

ENEMY TURN START
├── OnTurnStart
│   └── Regenerating Warrior: Heal 1 (health 1→2)
│
├── ATTACK EXECUTION
│   ├─ Shielded Guardian attacks
│   │  ├─ OnBattlePhase
│   │  │  └── Shield ready
│   │  ├─ Target: Player Front (empty) → no damage (to hero)
│   │  └── Damage to hero: 1
│   │
│   └─ Regenerating Warrior attacks
│      ├─ OnBattlePhase
│      │  └── Regenerate ready for turn end
│      ├─ Target: Player Left (empty) → damage to hero
│      └── Damage to hero: 2
│
├── CLEANUP
│   └── (No status effects)
│
└── TURN END
    └── OnTurnEnd
        └── Regenerating Warrior: Heal 1 (health 2→3)
```

## Skill Categories by Scope

### Self-Only Skills
Affect only the card that has them:
- **Regenerate** - Heals self
- **Enrage** - Buffs self ATK when damaged
- **Last Stand** - Damages more when alone
- **Charge/Haste** - Attacks immediately

### Enemy-Targeting Skills
Affect enemy cards during combat:
- **Stun** - Stops enemy attacks
- **Poison** - Applies damage over time
- **Mana Burn** - Costs enemy mana
- **Execute** - Scales damage by enemy health

### Board-Wide Skills
Affect multiple cards/areas:
- **Cleave** - Hits all in row
- **Ricochet** - Bounces damage
- **Taunt** - Forces targeting
- **Fly** - Blocks ranged attacks

### Passive Defense Skills
Provide constant protection:
- **Shield** - One-use absorption
- **Dodge** - Blocks ranged attacks
- **Evasion** - Chance to dodge
- **Reflection** - Bounces damage back

## Deck Building Strategy

### Defensive Fortress
```
Goal: Outlast and win through attrition
Cards:
- Shielded Guardian (3x) - Tank damage
- Regenerating Warrior (3x) - Sustain
- Taunt Bearer (2x) - Control aggression
- Basic Soldier (2x) - Early game

Play Style:
1. Establish front line with tanks
2. Absorb damage with shields
3. Heal each turn with Regenerate
4. Force attacks onto Taunt targets
```

### Aggressive Assault
```
Goal: End game quickly with high damage
Cards:
- Trampling Beast (3x) - Ignore defenses
- Cleaving Knight (2x) - AOE damage
- Charging Cavalry (3x) - Immediate threat
- Enraged Berserker (2x) - Growing damage

Play Style:
1. Play aggressive units early
2. Attack immediately with Charge
3. Use Cleave for board clears
4. End before opponent stabilizes
```

### Control Master
```
Goal: Disable enemy and win through card advantage
Cards:
- Poisoner (3x) - Sustained damage
- Stunning Mage (3x) - Stop threats
- Mana Drainer (2x) - Resource denial
- Executioner (2x) - Finish weakened

Play Style:
1. Poison threats early
2. Stun key targets
3. Drain resources
4. Execute damaged enemies
```

### Synergy Combo
```
Goal: Combine abilities for multiplied effects
Cards:
- Multi-Skill Champion (2x) - Trample+Enrage+Lifelink
- Last Stand Hero (2x) - Solo damage boost
- Leeching Vampire (2x) - Sustain
- Reflecting Mage (2x) - Defense

Play Style:
1. Develop multiple abilities on board
2. Champion provides 3 effects at once
3. Chain abilities for multiplicative damage
4. Sustain through Leech and Reflection
```

## Victory Conditions

### Immediate Win
- Reduce opponent hero health to 0

### Game End
- One player has no cards in deck and hand
- One player concedes
- Timer expires (if time limit exists)

## Rebalancing Through JSON

To adjust game balance without code changes:

1. **Tune card stats**: Modify `attack`, `health`, `manaCost` in cards.json
2. **Adjust ability effects**: Change `amount` in abilities.json
3. **Rebalance triggers**: Change `trigger` type for abilities
4. **Deck adjustments**: Modify card quantities in decks.json

Example: If Cleaving Knight is too strong:

```json
// Before
{
  "cardId": "cleaving_knight",
  "attack": 3,
  "health": 2,
  "manaCost": 4
}

// After
{
  "cardId": "cleaving_knight",
  "attack": 2,
  "health": 2,
  "manaCost": 4
}
```

Then run Tools → Data → Generate All in Unity editor.
