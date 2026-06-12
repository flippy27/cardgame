# Card types — general rules

`[FACT]` `enum CardType { Unit = 0, Utility = 1, Equipment = 2, Spell = 3 }` (MatchEngine.cs).
All four types must exist and be playable. The SERVER (MatchEngine) is authoritative; the client
only renders snapshots and sends actions.

## Unit (0)
- Goes to a board slot, attacks/defends, takes damage, dies. Fully implemented today.
- Has `unitType` (Melee/Ranged/Magic), abilities, summoning sickness, etc.

## Equipment (2)
- Attaches to a **unit** (friendly OR enemy), acting like a persistent buff/debuff.
- Effect duration: **N turns** OR **permanent** (until the equipped unit dies).
- Examples: +X attack / +X armor / shield for N turns (friendly), or -X attack / poison for N turns (enemy).
- When the equipped unit dies, the equipment effect ends.
- Not a board occupant itself — it modifies a target unit's stats/abilities.

## Spell (3)
- **Instant** effect, then discarded (no board presence, no duration of its own).
- Targets a friendly or enemy **unit** (or the hero, where it makes sense).
- Examples: direct damage to a unit, heal a friendly unit, apply poison/stun, buff attack.

## Utility (1)
- Reusable / support effect (non-combat). Treated like a spell-ish support but may persist or repeat.
- Examples: draw, mana, a recurring board effect. (Exact scope TBD when implemented.)

## Targeting
- Equipment & Spell need a **target picker**: the player chooses a friendly or enemy unit (or hero)
  when playing the card. The server validates the target and applies the effect.
- Mana cost applies on play, same as units.

## Implementation status — PENDING (see task: Equipment/Spell/Utility playable mechanics)
- Today only Unit cards do anything in combat. Equipment/Spell/Utility are catalog-only (flavor).
- To make them playable the server engine needs: target selection on play (PlayCard with a target
  runtimeId), instant effect resolution for spells, equip/attach + timed buff/debuff for equipment
  (duration N turns or until-death), and utility effect handling — all emitting battleEvents the
  client can animate. Then author such cards into seed-data.sql (tools/generate_seed.py) so they
  are created and jugables.
