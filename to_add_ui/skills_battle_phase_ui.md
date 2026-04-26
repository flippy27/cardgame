# Skills Battle Phase UI Setup

Esta guia explica como armar en Unity todo lo que usa el cliente para mostrar habilidades, buffs/debuffs, visuales de cartas y eventos de battle phase.

El cliente sigue siendo solo visual. Toda la logica real viene del backend. Unity no decide dano, targets, efectos, curaciones, muertes ni turnos. Unity recibe snapshots/eventos, los reproduce con UI/animaciones y despues reconcilia contra el snapshot final del servidor.

## Que Quedo Implementado

- `GameplayPresenter3D` consume `snapshot.battleEvents` como fuente principal para animaciones de battle phase.
- Si el backend todavia no envia `battleEvents`, el cliente puede usar logs como fallback visual, pero ese camino es menos preciso.
- Las cartas pueden mostrar iconos de habilidades, buffs y debuffs desde datos del backend.
- Los buffs/debuffs son puramente visuales en el cliente: se agregan, actualizan o quitan segun snapshot/eventos del servidor.
- Las cartas pueden renderizar frame, arte e iconos usando referencias que llegan en `visualProfiles`.
- Se generan archivos `.txt` bajo `battle_phases/` para comparar lo que Unity reprodujo contra lo que paso en backend.

## Scripts Y Clases

### `CardVisualAssetResolver`

`CardVisualAssetResolver` es un componente separado.

El archivo es:

`Assets/Runtime/Battle/Presentation/CardVisualAssetResolver.cs`

En Unity debes agregarlo con `Add Component` buscando:

`Card Visual Asset Resolver`

Si Unity no lo muestra inmediatamente, espera a que termine de recompilar scripts o revisa la consola por errores de C#. Antes estaba dentro de otro archivo, pero ahora esta separado para que Unity lo pueda listar como componente normal.

Este componente sirve como diccionario visual. Le das un `assetRef` string y devuelve el Sprite/Texture que corresponde.

Si estas viendo campos como `Default Surface`, `Requested Profile Key` y `Layer Bindings`, entonces estas mirando `Card Surface Visual Renderer`, no el resolver.

La diferencia es:

- `Card Visual Asset Resolver`: vive en un GameObject de escena, por ejemplo `CardVisualAssets`, y contiene `Asset Entries`.
- `Card Surface Visual Renderer`: vive en cada prefab de carta y contiene `Layer Bindings` como `art`, `frame`, `icon`, etc.

Ejemplos de `assetRef`:

- `frames/grove_common`
- `art/grove/grove_0059`
- `icons/skills/poison`
- `icons/statuses/poisoned`
- `icons/attack_types/melee`

### `CardSurfaceVisualRenderer`

Tambien esta en:

`Assets/Runtime/Battle/Presentation/CardSurfaceVisualRenderer.cs`

Va en cada prefab de carta que necesite renderizar frame/arte/capas visuales. Por ejemplo:

- `Card3DView` para cartas en mano.
- `Card3DPlayed` para cartas jugadas en mesa.
- El prefab o panel grande usado por `CardDetailOverlayUI`, si quieres que tambien muestre frame/arte grande.

Este script toma las capas que vienen del backend en `visualProfiles.layers` y las asigna a objetos visuales del prefab.

### `CardIconGroup`

`CardIconGroup` es el componente nuevo que debes usar para habilidades, buffs, debuffs y statuses.

El archivo es:

`Assets/Runtime/Battle/Presentation/CardIconGroup.cs`

En Unity debes agregarlo con `Add Component` buscando:

`Card Icon Group`

Este componente va en un parent UI, por ejemplo:

```text
AbilityIconsRoot
  Card Icon Group
  Grid Layout Group
```

o:

```text
StatusIconsRoot
  Card Icon Group
  Grid Layout Group
```

Tu no llenas iconos uno por uno. Tu solo creas el parent visual. En runtime, el codigo crea N iconos segun lo que venga del backend.

En la carta, arrastras ese `Card Icon Group` al campo correspondiente:

- `Ability Icon Group` en `Card3DView`, `Card3DPlayed` o `CardDetailOverlayUI`.
- `Status Icon Group` en `Card3DPlayed` o `CardDetailOverlayUI`.

### `CardStateIconSlot` Legacy

`CardStateIconSlot` queda solo como fallback legacy para no romper prefabs viejos.

Si ves `Legacy Ability Icon Slots`, `Legacy Buff/Debuff Icon Slots` o `Legacy State Icons`, puedes ignorarlos en prefabs nuevos. El flujo nuevo es `CardIconGroup`.

### `AttackEffectSystem`

Es un GameObject de escena, normalmente un empty, con el script `AttackEffectSystem`.

Controla proyectiles, pops de dano, recoil, shake, hit stop y otros efectos visuales de combate. Si no existe, algunos flujos intentan crear uno por fallback, pero lo recomendado es tenerlo explicitamente en la escena y asignar sus referencias.

## Setup De Escena

En `MainGame`, crea un GameObject vacio:

`CardVisualAssets`

Agregale:

`Card Visual Asset Resolver`

Despues de agregarlo, el inspector debe mostrar listas separadas por tipo.

Usa estas listas para mantener todo ordenado:

- `Frame Assets`: frames de carta.
- `Art Assets`: artes de carta.
- `Attack Icon Assets`: iconos melee/ranged/magic.
- `Skill Icon Assets`: iconos de habilidades.
- `Status Icon Assets`: iconos de buffs/debuffs/statuses.
- `Misc Assets`: assets especiales/futuros.
- `Legacy Asset Entries`: entradas migradas desde la lista antigua `assetEntries`.

Agrega una entrada por cada asset visual que quieras resolver en runtime.

Cada entrada debe tener:

- `Asset Ref`: el string exacto que viene del backend o que usa el cliente como fallback.
- `Sprite`: el Sprite correspondiente, si es una imagen UI.
- `Texture`: opcional, si algun renderer usa textura en vez de Sprite.

Ejemplo:

| Asset Ref | Sprite |
| --- | --- |
| `frames/grove_common` | sprite del frame Grove comun |
| `art/grove/grove_0059` | arte de Grove Storm Caller |
| `icons/skills/poison` | icono de habilidad Poison |
| `icons/statuses/poisoned` | icono del debuff Poisoned |
| `icons/attack_types/ranged` | icono de ataque ranged |

Tambien en `MainGame`, deja un GameObject:

`AttackEffectSystem`

Agregale:

`Attack Effect System`

Asigna ahi:

- `projectilePrefab`: prefab del proyectil/cubo que viaja entre atacante y target.
- `damagePopupPrefab`: prefab con `DamagePopup3D` para numeros flotantes.
- `cameraShake`: referencia al script de shake de la camara.
- Curvas, tiempos, intensidades y niveles de ataque segun quieras ajustar game feel.

En `GameplayPresenter3D`, asigna tambien los targets de heroes:

- `localHeroTarget`: Empty Transform cerca del HUD/posicion del jugador local.
- `remoteHeroTarget`: Empty Transform cerca del HUD/posicion del jugador remoto.

Estos empties son los puntos a los que viajan ataques cuando el target es el player y no una carta.

## Setup De Card3DView

`Card3DView` es la carta de mano y tambien puede ser base para el ghost/drag visual.

Debe tener referencias serializadas para UI manual, no UI creada por codigo.

Asignar:

- Texto de nombre.
- Texto de costo.
- Texto de ataque.
- Texto de HP.
- Texto de armor, si lo usas.
- Texto de descripcion/ability text, si aplica.
- Imagen de tipo de ataque.
- Sprites de tipo de ataque: melee, ranged, magic.
- `CardSurfaceVisualRenderer`, si quieres frame/arte dinamico.
- En el componente `Card3DView`, asignar `Ability Icon Group` arrastrando el parent UI que tiene `Card Icon Group`.

`Card3DView` no deberia tener `Status Icon Group`, porque los estados vivos pertenecen a cartas en juego.

Importante: el costo si va en `Card3DView`.

La carta que se arrastra usa el mismo tipo de carta que la mano. El tilt del ghost esta desactivado por defecto para que la UI se vea estable mientras arrastras.

## Setup De Card3DPlayed

`Card3DPlayed` es la carta en mesa.

Debe tener referencias serializadas para:

- Texto de nombre.
- Texto de ataque/dano.
- Texto de HP.
- Texto de armor, si lo usas.
- Imagen de tipo de ataque.
- Sprites de tipo de ataque: melee, ranged, magic.
- `CardSurfaceVisualRenderer`, si quieres frame/arte dinamico.
- En el componente `Card3DPlayed`, asignar `Ability Icon Group` arrastrando el parent UI de habilidades.
- En el componente `Card3DPlayed`, asignar `Status Icon Group` arrastrando el parent UI de buffs/debuffs/statuses.

Importante: el costo no deberia mostrarse en `Card3DPlayed`, porque la carta ya esta en juego.

Recomendacion visual:

- `Ability Icon Group`: ubicalo abajo o al centro de la carta, segun tu diseno.
- `Status Icon Group`: ubicalo arriba de la carta para buffs/debuffs/statuses activos.

## Setup De CardDetailOverlayUI

`CardDetailOverlayUI` es la vista grande de carta.

Debe mostrar la informacion completa para inspeccion client-side.

Asignar:

- Texto de nombre.
- Texto de costo.
- Texto de ataque.
- Texto de HP.
- Texto de armor.
- Texto de descripcion/abilities.
- Imagen de tipo de ataque.
- Sprites melee/ranged/magic.
- `CardSurfaceVisualRenderer` o referencias equivalentes para frame/arte.
- En el componente `CardDetailOverlayUI`, asignar `Ability Icon Group`.
- En el componente `CardDetailOverlayUI`, asignar `Status Icon Group`.

En cartas de mano normalmente los status estaran vacios. En cartas jugadas pueden mostrarse buffs/debuffs activos.

## Como Armar Card Icon Groups

Este es el flujo recomendado para cartas nuevas.

### Habilidades

En el prefab de carta, crea un parent UI:

```text
AbilityIconsRoot
```

Agregale:

- `Card Icon Group`
- `Grid Layout Group`, opcional porque el script puede crearlo automaticamente

Configura el `Card Icon Group` asi:

- `Layout`: `CenteredHorizontal`
- `Auto Configure Grid Layout`: activo si quieres que el script configure el grid.
- `Cell Size`: tamano de cada icono.
- `Spacing`: separacion entre iconos.
- `Icon Prefab`: opcional.

Luego arrastra el componente `Card Icon Group` al campo `Ability Icon Group` de la carta.

Con `CenteredHorizontal`, si llega 1 icono queda centrado. Si llegan 2 o mas, se expanden desde el centro hacia los lados porque el grid queda alineado al centro.

### Buffs, Debuffs Y Statuses

En el prefab de carta jugada o detail view, crea:

```text
StatusIconsRoot
```

Agregale:

- `Card Icon Group`
- `Grid Layout Group`, opcional porque el script puede crearlo automaticamente

Configura el `Card Icon Group` asi:

- `Layout`: `BottomLeftGrid`
- `Auto Configure Grid Layout`: activo si quieres que el script configure el grid.
- `Cell Size`: tamano de cada icono.
- `Spacing`: separacion entre iconos.
- `Icon Prefab`: opcional.

Luego arrastra el componente `Card Icon Group` al campo `Status Icon Group` de la carta.

Con `BottomLeftGrid`, los status se agregan de izquierda a derecha y de abajo hacia arriba.

### Icon Prefab Opcional

Puedes dejar `Icon Prefab` vacio. En ese caso el script crea un icono UI simple en runtime.

Si quieres controlar el look exacto, crea un prefab o template hijo:

```text
IconTemplate
  Image
  StackText
```

Asigna `IconTemplate` en `Icon Prefab`.

Si `IconTemplate` esta dentro del mismo root, puedes dejar `Hide Template On Awake` activo para que no se vea el template y solo se vean los clones runtime.

El sprite de cada icono no se pone manualmente en el prefab. Sale de `CardVisualAssetResolver` usando el id que viene del backend.

## Como Armar CardSurfaceVisualRenderer

Este es el componente que aparece en tu captura con:

- `Default Surface`
- `Requested Profile Key`
- `Fetch Detailed Card Data`
- `Clear Bindings When Missing`
- `Layer Bindings`

Ese componente no tiene `Asset Entries`. Solo dice donde se dibuja cada capa en esa carta.

En el prefab de carta, crea objetos UI para las capas visuales.

Ejemplo:

```text
CardRoot
  FrameImage
  ArtImage
  RarityGemImage
```

Agrega `CardSurfaceVisualRenderer` al root de la carta o a un hijo controlador.

Campos principales:

- `Default Surface`: normalmente `hand` para `Card3DView`, `played` para `Card3DPlayed`, y `detail` para una vista grande si el backend manda ese surface.
- `Requested Profile Key`: opcional. Dejalo vacio salvo que quieras forzar un profile especifico.
- `Fetch Detailed Card Data`: activado si quieres que busque detalles de carta por API cuando falten en catalogo/cache.
- `Clear Bindings When Missing`: si esta activo, limpia las imagenes cuando no encuentra visual profile.
- `Layer Bindings`: lista de capas visuales que este prefab sabe dibujar.

En `layerBindings`, crea una entrada por capa que quieres renderizar.

Ejemplo:

| Layer Name | Target |
| --- | --- |
| `frame` | `FrameImage` |
| `art` | `ArtImage` |
| `rarity_gem` | `RarityGemImage` |

El nombre de layer debe coincidir con lo que llega del backend en `visualProfiles.layers[].layer`.

Si el backend envia:

```json
{
  "layer": "frame",
  "assetRef": "frames/grove_common"
}
```

Entonces `CardSurfaceVisualRenderer` busca un binding llamado `frame`, resuelve `frames/grove_common` en `CardVisualAssetResolver` y pone el Sprite en la imagen asignada.

## Como Se Resuelven Los Iconos

El cliente ya no inventa rutas desde `abilityId`, `animationCueId` o `statusKind`.

Para habilidades, Unity usa exactamente:

- `ability.iconAssetRef`, si viene.
- `ability.metadataJson.iconAssetRef`, si viene.
- `ability.metadataJson.assetRef`, si viene.

Para buffs/debuffs/statuses, Unity usa exactamente:

- `status.iconAssetRef`, si viene.

Si no viene una key, o si la key no existe en `CardVisualAssetResolver`, se muestra el placeholder fucsia. Puedes usar nombres como `icons/skills/poison` o `icons/statuses/poisoned`, pero solo si el backend envia exactamente esas strings.

## Flujo Runtime

1. El backend envia snapshot con cartas, stats, status effects, visual profiles y battle events.
2. `GameplayPresenter3D` recibe el snapshot.
3. Si hay `battleEvents` nuevos, se transforman a eventos visuales.
4. `AttackEffectSystem` reproduce ataques, proyectiles, dano, shake, recoil, death, status applied/expired, etc.
5. La UI de cartas se actualiza durante la secuencia cuando el evento trae stats/status actualizados.
6. Al terminar la secuencia, el snapshot final reconcilia el estado visual.
7. Se escribe un log por battle phase en `battle_phases/`.

## Logs De Battle Phase

Los `.txt` bajo `battle_phases/` sirven para comparar frontend vs backend.

Contienen informacion como:

- Match id.
- Turno.
- Fase.
- Secuencia de eventos.
- Atacante.
- Target.
- Dano/curacion.
- Muertes.
- Status aplicados o expirados.
- Snapshot final visto por Unity.

Si algo se ve raro en multiplayer, este archivo es el primer lugar para comparar contra los logs del servidor.

## Checklist Rapido

- Crear `CardVisualAssets` en escena.
- Agregar `Card Visual Asset Resolver`.
- Llenar las listas del resolver por tipo (`Frame Assets`, `Art Assets`, `Skill Icon Assets`, `Status Icon Assets`, etc.) con keys exactas y sprites.
- En prefabs de carta, crear `AbilityIconsRoot` y/o `StatusIconsRoot`.
- Agregar `Card Icon Group` a cada root de iconos.
- En cada carta, arrastrar el componente `Card Icon Group` al campo `Ability Icon Group` o `Status Icon Group`.
- Agregar/asignar `CardSurfaceVisualRenderer` en prefabs que usan frame/arte.
- Crear bindings `frame`, `art`, etc segun lo que envia el backend.
- Asignar sprites de ataque melee/ranged/magic en cartas.
- Crear `AttackEffectSystem` en escena y asignar prefabs/curvas/camera shake.
- Crear `localHeroTarget` y `remoteHeroTarget` y asignarlos en `GameplayPresenter3D`.
- Probar una battle phase y revisar `battle_phases/`.

## Errores Comunes

- Buscar assets en `CardSurfaceVisualRenderer`: no van a aparecer ahi. Las listas de assets estan en `CardVisualAssetResolver`.
- Agregar `CardSurfaceVisualRenderer` cuando querias agregar `CardVisualAssetResolver`: el primero dibuja capas en una carta, el segundo resuelve sprites globalmente.
- Buscar `abilityIconSlots` como script: no existe. En prefabs nuevos usa `Card Icon Group` y arrastralo a `Ability Icon Group`.
- Buscar `statusIconSlots` como script: no existe. En prefabs nuevos usa `Card Icon Group` y arrastralo a `Status Icon Group`.
- Llenar iconos manualmente: no hace falta. Solo asignas el grupo/root; el runtime crea los iconos.
- Confundir habilidad con debuff: `poison` no es lo mismo que `poisoned`.
- Poner `Status Icon Group` en `Card3DView`: las cartas en mano normalmente no tienen buffs/debuffs activos.
- No agregar entries al resolver: la logica funciona, pero el icono/frame/arte no se vera.
- Usar nombres distintos a los del backend: las keys deben coincidir exactamente con el `assetRef` que envia el servidor.
