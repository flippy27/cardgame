# Attack Intensity Contract

## Goal

Let the server stay authoritative for battle resolution while also sending enough presentation metadata for the client to play attacks with good game feel.

The client already supports:

- sequential battle-phase playback
- melee / projectile / beam / arc delivery hints from the server
- projectile motion presets
- camera shake presets
- short impact hit stop
- defender recoil + squash
- impacted slot emissive pulse
- floating damage numbers
- death animation + local visual reposition before final snapshot sync

For now the client auto-resolves intensity from the card attack stat if these values are missing.

## Recommended fields

These fields should become part of the card data that reaches the battle snapshot.

### On board cards

```json
{
  "runtimeId": "string",
  "cardId": "string",
  "displayName": "string",
  "attack": 0,
  "currentHealth": 0,
  "maxHealth": 0,
  "armor": 0,
  "attackMotionLevel": 0,
  "attackShakeLevel": 0,
  "attackDeliveryType": "melee"
}
```

## Field meaning

- `attackMotionLevel`
  - `0` = client auto
  - `1..5` = selects attack feel preset
- `attackShakeLevel`
  - `0` = client auto
  - `1..5` = selects camera shake preset
- `attackDeliveryType`
  - explicit delivery hint the client should trust when present
  - supported values today: `melee`, `projectile`, `beam`, `arc`

These two values are intentionally independent.

Example:

- a precise ranged attack can use `attackMotionLevel = 2` and `attackShakeLevel = 1`
- a heavy hammer hit can use `attackMotionLevel = 4` and `attackShakeLevel = 5`
- a magical fast beam can use `attackMotionLevel = 5` and `attackShakeLevel = 2`

## Current client fallback

If the server does not send the values yet, the client uses:

- ATK `<= 1` => level `1`
- ATK `2` => level `2`
- ATK `3` => level `3`
- ATK `4` => level `4`
- ATK `>= 5` => level `5`

## Why send both

One number is not enough if we want good feel.

Projectile motion and camera shake answer different questions:

- motion = how the attack travels and impacts
- shake = how strong the impact feels

They should not be forced to match.

## Current client behavior

Current fallback only when `attackDeliveryType` is missing:

- `Front` attacks = melee hit
- `BackLeft` / `BackRight` attacks = projectile hit
- unit-type fallback still exists locally for single-player/local data

Each `attackMotionLevel` currently drives:

- attacker windup / lunge
- melee reach or projectile arc/speed
- impact hit stop duration
- defender recoil distance
- defender squash amount
- impacted slot pulse intensity/duration

`attackShakeLevel` still only controls camera shake.

## Future-safe optional fields

These are still optional, but the client is already prepared to consume them later:

```json
{
  "attackDeliveryType": "melee",
  "attackProjectileColor": "#RRGGBBAA",
  "attackProjectileScale": 0.25,
  "attackProjectileCurveId": "default",
  "attackHitStopSeconds": 0.04,
  "attackRecoilDistance": 0.18,
  "attackSquashAmount": 0.12,
  "attackSlotPulseIntensity": 1.1,
  "attackSlotPulseColor": "#FF6A33FF",
  "damagePopupStyleId": "default",
  "impactFxId": "default",
  "attackAudioCueId": "default"
}
```

## Important note for server-authoritative flow

The server should keep deciding:

- attack order
- targets
- damage dealt
- armor blocked
- deaths
- repositioning

The client should only receive:

- battle logs in exact order
- final snapshot after resolution
- presentation hints like the intensity fields above

That keeps gameplay authoritative while still allowing slow readable battle playback on the client.
