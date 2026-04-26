# Battle Phase Contract Findings From Unity

Unity now treats the server as authoritative and only animates events that carry a real visual state change. While testing the provided battle debug log, these backend-side inconsistencies showed up:

## 1. `attack_position_blocked` for melee Front

Observed:

```text
Grove Dragon Slayer #7 entered Front.
attack_position_blocked: Grove Dragon Slayer #7 cannot attack from Front.
Card3D runtime name includes unittype:0.
```

If `unitType = 0` means melee and Front is the melee slot, this event is inconsistent with the rules. Backend should either:

```text
allow unitType 0 to attack from Front
```

or include enough event diagnostics to prove why the card is blocked:

```json
{
  "kind": "attack_position_blocked",
  "sourceRuntimeId": "...",
  "sourceSeatIndex": 1,
  "slot": "Front",
  "unitType": 0,
  "reason": "..."
}
```

## 2. Trigger timing for skill events

Observed before an end-turn battle phase:

```text
skill_begin: Grove Storm Caller used Armor
skill_begin: Grove Storm Caller used Stun
skill_begin: Alloy Undead Knight used Stun
skill_begin: Alloy Undead Knight used Taunt
```

Armor can make sense as an on-play effect if the card's trigger says so. Stun and Taunt are suspicious if they are on-hit or passive effects. Backend should only emit `skill_begin` when that ability actually triggers, and ideally include:

```json
{
  "kind": "skill_begin",
  "abilityId": "stun",
  "triggerKind": "OnHit",
  "sourceRuntimeId": "...",
  "targetRuntimeId": "..."
}
```

For passive effects such as Taunt, prefer an event like `passive_enabled` or only expose it as card state/status unless it actively changes target selection during battle.

## 3. Damage animation duplication risk

Unity now prefers to animate damage from:

```text
card_damage
card_counterattack
hero_damage
death
```

Unity treats `card_attack` as an intent/windup event when a matching `card_damage` or `card_counterattack` exists for the same source/target. If the backend sends only `card_attack`, Unity can animate it as a fallback. Please keep:

```text
card_attack        intent/windup/no health mutation
card_damage        actual health/armor mutation
card_counterattack actual counterattack mutation
death              removal/death event
```

## 4. Snapshot/event window

The client writes one battle debug file per match under `/battle_phases`. If the backend sends old events in every snapshot, Unity dedupes by sequence. The safest contract is:

```text
Every battle event has stable eventId + monotonically increasing sequence per match.
Snapshots can include cumulative events, but sequences must never be reused.
```

If the backend can include `actionId` or `phaseId`, debugging and exact battle comparison becomes much easier.
