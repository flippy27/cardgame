# Cartoon FX Remaster → CardDuel battle VFX (mapping)

El sistema de VFX (`BattleVfxPlayer`) ahora **prefiere un prefab de partículas** sobre los sprites legacy.
Para activar un efecto con Cartoon FX: copiá el prefab CFX elegido a

    Assets/Resources/Art/vfx/cfx/{KEY}.prefab    (renombrado exactamente al KEY)

Cuando exista, ese efecto pasa a partículas automáticamente en todo el juego (un solo lugar = la carpeta `cfx/`).
Si no existe, cae al sprite-frame legacy. Escala global tuneable: `BattleVfxPlayer.ParticleWorldScale` (default 3.5).

## Keys que dispara la batalla hoy (renombrá el prefab a estos nombres)

| KEY | Cuándo se dispara | Sugerencia CFX |
|---|---|---|
| `card_damage` | impacto de ataque / daño a carta | CFX4 Hit / Slash / Sword Impact |
| `heal` | curación | CFX Heal / green sparkle up |
| `death` | carta muere | CFX Poof / small explosion / dust |
| `shield` `shield_block` `shielded` | escudo aplicado / bloqueo | CFX Shield bubble / spark |
| `poison` `poisoned` | veneno | CFX green poison cloud |
| `stun` `stunned` `stun_skip` | aturdido / pierde turno | CFX stars / dizzy |
| `magic` | ataque mágico (impacto) | CFX magic burst (genérico) |
| `frost_magic` | magia de hielo (Tidal) | CFX ice / frost burst |
| `status_applied` | buff aplicado | CFX buff aura up |
| `status_expired` | buff/debuff termina | CFX fizzle / puff |
| `skill_begin` | habilidad genérica | CFX cast flash |

> Habilidades específicas: `GameplayPresenter3D.AbilitySkillVfx(abilityId)` puede mapear ataque/skill por habilidad
> a su propio KEY (ej. `cleave`, `reflection`). Para cada uno, soltá un prefab `cfx/{abilityKey}.prefab`.

## Ataques (proyectiles)
El viaje del proyectil usa `ProjectileVfxLibrary` (sprites por facción). El **impacto** ya usa `card_damage`
(arriba). Si querés proyectiles de partículas (trail CFX), se puede hacer en una pasada aparte.

## Pasos
1. Importá "Cartoon FX Remaster (Free)" desde el Asset Store (Package Manager ▸ My Assets ▸ Import).
2. Por cada KEY, arrastrá el prefab CFX que quieras a `Assets/Resources/Art/vfx/cfx/` y renombralo al KEY.
3. Probá una partida; ajustá `BattleVfxPlayer.ParticleWorldScale` si salen muy chicas/grandes.
