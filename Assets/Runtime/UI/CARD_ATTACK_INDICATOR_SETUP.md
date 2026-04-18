# Card Attack Indicator Setup

Sistema de iconos para mostrar estado de ataque de cartas en el board.

## Componente

**CardAttackIndicator.cs** - Muestra 2 iconos:
1. **Cooldown Icon** - Visible mientras `TurnsUntilCanAttack > 0`
2. **Can Attack Icon** - Visible si realmente puede atacar (posición + tipo válidos)

## Reglas de Ataque

```
Melee:  Ataca solo desde Front (Top)
Ranged: Ataca solo desde BackLeft/BackRight (recto)
Magic:  Ataca solo desde BackLeft/BackRight (diagonal)
```

## Setup en Prefab

### 1. Agregar Images para Iconos

En tu prefab de card en el board (ej: BoardCardPrefab):

```
BoardCardUI (Image)
├─ CardArtImage
├─ CardFrame
├─ CardStats (texts)
└─ AttackIndicators (new Panel/Container)
   ├─ CooldownIcon (Image) ← "reloj" o "hourglass"
   └─ CanAttackIcon (Image)  ← "espada" o "checkmark"
```

### 2. Agregar Componente

En la raíz del prefab:
- **Add Component** → **CardAttackIndicator**
- Drag "CooldownIcon" Image → **Cooldown Icon**
- Drag "CanAttackIcon" Image → **Can Attack Icon**

### 3. Desde Script que Renderiza Card en Board

```csharp
// En el script que renderiza cartas en el board (ej: BattleScreenPresenter)
var indicator = card.GetComponent<CardAttackIndicator>();
if (indicator != null && cardRuntime != null)
{
    indicator.SetCard(cardRuntime);
}
```

O durante updates:
```csharp
// Cada frame o cuando el estado cambia
indicator?.UpdateIndicators();
```

## Estados Visuales

| Estado | Cooldown Icon | Can Attack Icon |
|--------|---------------|-----------------|
| Recién jugada, sin poder atacar | ✅ Visible | ❌ Hidden |
| Esperando turno para atacar | ✅ Visible | ❌ Hidden |
| Puede atacar pero en slot incorrecto | ❌ Hidden | ❌ Hidden |
| Puede atacar, en slot correcto | ❌ Hidden | ✅ Visible |

## Ejemplo Completo

```csharp
public class BoardCardUI : MonoBehaviour
{
    private CardAttackIndicator _attackIndicator;
    private CardRuntime _cardRuntime;

    public void Initialize(CardRuntime cardRuntime)
    {
        _cardRuntime = cardRuntime;
        _attackIndicator = GetComponent<CardAttackIndicator>();
        
        if (_attackIndicator != null)
        {
            _attackIndicator.SetCard(_cardRuntime);
        }
    }

    public void RefreshAttackState()
    {
        if (_attackIndicator != null)
        {
            _attackIndicator.UpdateIndicators();
        }
    }
}
```

## Iconos Sugeridos

**Cooldown**: Reloj, hourglass, o temporizador (muestra espera)
**Can Attack**: Espada, checkmark, o rayo (muestra listo)

Puedes usar los mismos assets de SkillIconGenerator o crear custom.
