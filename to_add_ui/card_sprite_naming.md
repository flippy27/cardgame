# Card Sprite Naming And Asset Refs

Los sprites no se arrastran directo a cada carta para abilities/statuses. Se registran una vez en `CardVisualAssetResolver`, y los `Card Icon Group` de las cartas los piden en runtime por nombre.

El resolver ahora esta separado por listas para que el inspector no quede como una lista eterna:

- `Frame Assets`: frames de carta, por ejemplo `frames/common-hand`.
- `Art Assets`: artes de cartas, por ejemplo `art/ember_0032`.
- `Attack Icon Assets`: iconos de tipo de ataque, por ejemplo `icons/attack/melee`.
- `Skill Icon Assets`: iconos de habilidades impresas/visibles, por ejemplo `icons/skills/poison`.
- `Status Icon Assets`: iconos de buffs/debuffs/status activos, por ejemplo `icons/statuses/poisoned`.
- `Misc Assets`: assets futuros o especiales que no calzan aun en una categoria.
- `Legacy Asset Entries`: lista vieja migrada desde `assetEntries`; mantenla por compatibilidad y mueve entradas a las listas nuevas cuando te convenga.

## Visual profile layers

`visualProfiles.layers[].assetRef` is resolved exactly first through all `CardVisualAssetResolver` lists, then through `Resources.Load`.

Recommended names:

- `frames/common-hand`
- `frames/common-played`
- `frames/rare-hand`
- `frames/rare-played`
- `art/ember_0032`
- `art/tidal_0024`
- `icons/attack/melee`
- `icons/attack/ranged`
- `icons/attack/magic`

## Skill icons

Skill icons are resolved from `abilityId` or `animationCueId`.

These icons are rendered by `Card Icon Group` assigned to `Ability Icon Group` in `Card3DView`, `Card3DPlayed`, or `CardDetailOverlayUI`.

Recommended names:

- `icons/skills/poison`
- `icons/skills/stun`
- `icons/skills/shield`
- `icons/skills/fly`
- `icons/skills/trample`
- `icons/skills/leech`
- `icons/skills/enrage`
- `icons/skills/regenerate_left`
- `icons/skills/haste`

## Buff/debuff status icons

Statuses are not the same as the skill that created them. For example, `poison` is the ability, `poisoned` is the active debuff icon.

These icons are rendered by `Card Icon Group` assigned to `Status Icon Group` in `Card3DPlayed` or `CardDetailOverlayUI`.

Recommended names:

- `icons/statuses/poisoned`
- `icons/statuses/stunned`
- `icons/statuses/shielded`
- `icons/statuses/enrage_cooldown`

## Resolver fallback order

For an ability id like `poison`, Unity checks:

- `icons/skills/poison`
- `skills/poison`
- `icons/poison`
- `poison`

For a status like `poisoned`, Unity checks:

- `icons/statuses/poisoned`
- `statuses/poisoned`
- `icons/poisoned`
- `poisoned`
