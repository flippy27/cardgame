# 3D Battle System Setup Tool

## ¿Qué hace?

El **BattleSceneSetupTool** es un editor tool que genera automáticamente:

✅ Jerarquía completa de MainGame.unity
✅ Carpeta Assets/Generated/3DBattle con organización
✅ Materiales prototipo (slot, highlight, card)
✅ Prefabs para Board3DSlot y Card3DView
✅ Canvas con HUD completo (HP, mana, turn info, battle log)
✅ Camera con controllers (orbit, drag, audio)
✅ Button End Turn funcional
✅ GameplayPresenter3D configurado
✅ AudioManager y GameModeManager listos
✅ MatchCompletionScreen creado

## Cómo usar

### 1. Abre el Tool
```
Menu: Tools → Battle System → Setup 3D Battle Scene
```

### 2. Configura opcionales (si necesario)
- **Generated Folder Path**: Dónde guardar assets (default: Assets/Generated/3DBattle)
- **Scene Path**: Dónde crear MainGame.unity (default: Assets/Scenes/MainGame.unity)

### 3. Click en "Generate Complete Battle Scene"

Eso es todo. El tool:
- Crea todas las carpetas
- Genera materiales
- Crea prefabs
- Genera la escena con jerarquía completa
- Asigna todos los scripts
- Configura referencias
- Abre la escena

## Estructura generada

```
Assets/Generated/3DBattle/
├── Materials/
│   ├── SlotMaterial.mat
│   ├── SlotHighlightMaterial.mat
│   └── CardMaterial.mat
├── Prefabs/
│   ├── Board/
│   │   └── Board3DSlot.prefab
│   └── Cards/
│       └── Card3DView.prefab
└── Scenes/
    └── MainGame.unity

MainGame Hierarchy:
├── Main Camera
│   ├── Camera3DController
│   ├── DragHandler3D
│   └── AudioListener
├── Board3DContainer
│   ├── Board3DManager (configured)
│   └── [6 slots creados automáticamente]
├── Hand3DContainer
│   └── Hand3DManager (configured)
├── Canvas (World Space)
│   ├── HUD3D
│   │   ├── LocalHeroInfo
│   │   ├── RemoteHeroInfo
│   │   ├── TurnInfo
│   │   └── BattleLog
│   ├── EndTurnButton3D
│   └── MatchCompletionScreen
├── GameplayPresenter3D
├── AudioManager
└── GameModeManager
```

## Personalización después

### Ajustar tamaño de cartas
`Board3DManager.slotSize = 0.8f` en la escena

### Cambiar colores de materiales
Edita los assets en `Assets/Generated/3DBattle/Materials/`

### Modificar layout de HUD
Edita RectTransform de cada elemento en el Canvas

### Agregar audio
Arrastra clips a AudioManager desde `Assets/Generated/3DBattle/`

## Qué hace cada componente

| Componente | Función |
|-----------|---------|
| **Camera3DController** | Cámara libre con orbit (mouse derecho + scroll) |
| **DragHandler3D** | Raycast drag & drop de cartas |
| **Board3DManager** | Crea y gestiona 6 slots del board |
| **Hand3DManager** | Crea y gestiona cartas en mano (arc formation) |
| **GameplayPresenter3D** | Orquesta todo (suscribe a snapshots, actualiza UI) |
| **HUD3D** | Muestra HP, mana, turn info, battle log |
| **EndTurnButton3D** | Botón para terminar turno |
| **CardTooltip** | Tooltip al hover sobre cartas |
| **AudioManager** | Reproduce sonidos de eventos |
| **GameModeManager** | Gestiona modo de juego (local/online) |

## Scripts necesarios

Todos estos scripts deben existir en el proyecto:
- ✅ GameplayPresenter3D.cs
- ✅ Board3DManager.cs
- ✅ Board3DSlot.cs
- ✅ Card3DView.cs
- ✅ Hand3DManager.cs
- ✅ Camera3DController.cs
- ✅ DragHandler3D.cs
- ✅ HUD3D.cs
- ✅ EndTurnButton3D.cs
- ✅ CardTooltip.cs
- ✅ AttackEffectSystem.cs
- ✅ AudioManager.cs
- ✅ MatchCompletionScreen.cs
- ✅ GameModeManager.cs

## Troubleshooting

**Error: "Scene not saved"**
→ El tool intenta guardar en la ruta especificada. Verifica que exista `Assets/Scenes/`

**Prefabs vacíos**
→ El tool debería crear GameObjects con componentes. Si no ves quad/canvas, revisa Console.

**Componentes no asignados**
→ Los scripts deben estar en la carpeta Assets/Runtime/. Verifica los namespaces.

**Canvas no visible**
→ El canvas está a Z=5, alejado de la cámara. La cámara debería estar ajustada.

## Próximos pasos

Después de generar:
1. Abre Assets/Scenes/MainGame.unity
2. Prueba en Play mode
3. Arrastra cartas desde la mano a los slots
4. Verifica animaciones:
   - Hand cards slide-up
   - Attack effects (line + flash)
   - Repositioning (dead cards)
   - Tooltips (hover)

## Editar después de generar

Puedes:
- Modificar materiales directamente
- Ajustar parámetros en Board3DManager, Hand3DManager, etc.
- Agregar más prefabs basados en los generados
- Customizar Canvas layout
- Agregar audio clips a AudioManager

**No necesitas volver a ejecutar el tool a menos que hayas eliminado la escena.**
