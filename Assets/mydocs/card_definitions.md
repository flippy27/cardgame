# Card Definitions System

Documento de referencia para la estructura actual de ScriptableObjects y definiciones de cartas.

## Enums Principales

### Core Enums (CardEnums.cs)

```
BoardSlot:
  - Front (0)
  - BackLeft (1)
  - BackRight (2)

CardRarity:
  - Common
  - Rare
  - Epic
  - Legendary

CardFaction:
  - Ember
  - Tidal
  - Grove
  - Alloy
  - Void

AbilityTrigger:
  - OnPlay (0)
  - OnTurnStart (1)
  - OnTurnEnd (2)
  - OnBattlePhase (3)
  - OnDamaged (4)
  - OnDeath (5)

TargetSide:
  - Self
  - Ally
  - Enemy
  - Both

DuelEndReason:
  - None
  - EnemyHeroDefeated
  - LocalHeroDefeated
  - OpponentDisconnected

BattleLogType:
  - Info
  - Attack
  - Heal
  - Summon
  - Death
  - Turn

MatchPhase:
  - WaitingForPlayers
  - WaitingForReady
  - Starting
  - InProgress
  - Completed
  - Abandoned
```

### Card Type Enums (Data folder)

**CardType.cs**
```
- Unit (cartas de unidad)
- Utility (efectos reutilizables)
- Equipment (armas/armadura que equipar en unidades)
- Spell (hechizos de efecto único)
```

**UnitType.cs** (solo para cartas Unit)
```
- Melee (ataca Front → Front)
- Ranged (ataca Left/Right → Left/Right)
- Magic (ataca diagonal: Left↔Right)
```

**SkillType.cs** (categorización de skills)
```
- Defensive (armor, shield, evasion, reflection, dodge, etc)
- Offensive (poison, stun, leech, mana_burn, enrage, etc)
- Equipable (weapon/armor cards, etc)
- Utility (regenerate, charge, taunt, etc)
- Modifier (cambio de patrón ataque: cleave, diagonal_attack, etc)
```

---

## CardDefinition (Asset principal)

**File**: `Assets/Runtime/Data/CardDefinition.cs`

Estructura ScriptableObject base para todas las cartas.

### Identity Fields
```csharp
string cardId = "card"
string displayName = "New Card"
string description
CardFaction faction = CardFaction.Ember
CardRarity rarity = CardRarity.Common
CardType cardType = CardType.Unit
```

### Stats Fields
```csharp
int manaCost = 1
int attack = 1
int health = 1
int armor = 0
```

### Unit Fields (solo si cardType == Unit)
```csharp
UnitType unitType = UnitType.Melee
```

### Gameplay Fields
```csharp
TargetSelectorDefinition defaultAttackTargetSelector
CardSkillDefinition[] skills
AbilityDefinition[] abilities
```

### Visuals Fields
```csharp
CardVisualProfile visualProfile
```

---

## Sistema Antiguo: CardSkillDefinition

**File**: `Assets/Runtime/Data/CardSkillDefinition.cs` (base abstract)

Skills que afectan comportamiento de ataque. Métodos virtuales:

```csharp
bool CanAttack(CardRuntime attacker, CardRuntime defender, int damage, out int modifiedDamage)
int ModifyDamage(CardRuntime attacker, CardRuntime defender, int baseDamage, bool ignoreArmor)
int GetArmorAbsorption(CardRuntime attacker, CardRuntime defender, int damage)
bool BlocksDamage(CardRuntime attacker, CardRuntime defender)
string GetLogMessage(CardRuntime attacker, CardRuntime defender, int damage)
```

### Campos
```csharp
string skillId = "skill"
string displayName = "Skill"
string description
SkillType skillType = SkillType.Utility
```

### Skills Existentes (Asset files)

**Defensive:**
- ArmorSkill.cs - absorbe X daño antes de health

**Modifier:**
- TrampleSkill.cs - ignora armor del defensor
- FlySkill.cs
- DiagonalAttackSkill.cs

**Status Effects:**
- PoisonSkill.cs - daño por turno

Estado: **PARCIAL** - Solo ~4-5 skills implementadas. Falta completar ~17-18 skills.

---

## Sistema Nuevo: ISkillEffect Pipeline

**Location**: `Assets/Runtime/Battle/Skills/`

Nuevo sistema modular y escalable basado en:
- **SkillTrigger** - cuándo se ejecuta (OnValidateAttack, OnSelectTarget, OnDamageCalculation, etc)
- **ISkillEffect** - interfaz para efectos modulares
- **SkillRegistry** - registro central
- **SkillPipeline** - orquestador de ejecución

### 22 Skills Implementados

**Defensive (5)**
- ArmorEffect.cs
- ShieldEffect.cs
- ReflectionEffect.cs
- EvasionEffect.cs
- DodgeEffect.cs

**Offensive (5)**
- PoisonEffect.cs
- StunEffect.cs
- EnrageEffect.cs
- LeechEffect.cs
- ManaBurnEffect.cs

**Utility/Modifier (12)**
- CleaveEffect.cs
- RegenerateEffect.cs
- ExecuteEffect.cs
- DiagonalAttackEffect.cs
- FlyEffect.cs
- LastStandEffect.cs
- TauntEffect.cs
- ChainEffect.cs
- RicochetEffect.cs
- TrampleEffect.cs
- MeleeRangeEffect.cs
- ChargeEffect.cs

Estado: **COMPLETO** - 22 skills con pipeline completo (4 fases: validación, selección, cálculo, aplicación).

---

## Target Selectors

**Base**: `Assets/Runtime/Data/TargetSelectorDefinition.cs` (abstract)

```csharp
abstract void SelectTargets(BattleContext context, TargetSelectionRequest request, List<string> results)
```

### Selectors Implementados

**Attack Pattern Selectors** (asignados automáticamente por UnitType)
- MeleeAttackSelector.cs - Front → Front
- RangedAttackSelector.cs - Left/Right → Left/Right
- MagicAttackSelector.cs - Left↔Right (diagonal only)
- StraightLineTargetSelector.cs - same slot as attacker

**Special Selectors**
- FrontlineFirstTargetSelector.cs - prioritiza Front
- BacklineFirstTargetSelector.cs - prioriza BackLeft/BackRight
- AllEnemiesTargetSelector.cs - todos los enemigos
- DiagonalTargetSelector.cs - diagonal opuesta
- DiagonalAttackTargetSelector.cs - ataque diagonal
- CleaveTargetSelector.cs - fila completa
- ChainTargetSelector.cs - tipo/facción matching
- MeleeRangeTargetSelector.cs - expande rango melee
- LowestHealthAllyTargetSelector.cs - aliado con menos HP

Estado: **COMPLETO** - 13+ variantes cubriendo patrones y casos especiales.

---

## AbilityDefinition

**File**: `Assets/Runtime/Data/AbilityDefinition.cs`

Habilidades compuestas por trigger + selector + efectos.

```csharp
string abilityId = "ability"
string displayName = "Ability"
string description
AbilityTrigger trigger = AbilityTrigger.OnBattlePhase
TargetSelectorDefinition targetSelector
EffectDefinition[] effects

void Resolve(BattleContext context, EffectExecution execution)
```

Estado: **FUNCIONAL PERO INACTIVO** - Estructura lista, triggers no se ejecutan en BattleContext.

---

## EffectDefinition

**File**: `Assets/Runtime/Data/EffectDefinition.cs` (base abstract)

Efectos reutilizables para abilities.

```csharp
string designerNotes
abstract void Resolve(BattleContext context, EffectExecution execution)
```

### Effects Implementados

**Damage/Healing**
- DamageEffect.cs - `int damageAmount`, `bool ignoreArmor`
- HealEffect.cs - heals target
- SplashDamageEffect.cs - área
- HeroHealEffect.cs - heals hero
- HeroPingEffect.cs - damage to hero

**Buffs**
- BuffAttackEffect.cs - aumenta ataque
- ArmorEffect.cs - aumenta armor
- ShieldEffect.cs - escudo

Estado: **PARCIAL** - Efectos básicos. Falta: buffs temporales, debuffs, status effects avanzados.

---

## Clases de Soporte

### BattleModels.cs
```csharp
CardRuntime
  - attack, health, armor, manaCount, etc.
  - EffectiveAttackSelector (para override dinámico)
  - PoisonStacks, Stunned, HasShield, EnrageBonus (status)
  - Definition (referencia a CardDefinition)

DuelPlayerState
  - hand, deck, board (3 BoardSlot)
  - hero health/armor
```

### SkillContext.cs
```csharp
CardRuntime Attacker
CardRuntime Defender
BattleContext Battle
int BaseDamage
int FinalDamage
List<CardRuntime> TargetList
bool IsValidAttack
bool IgnoreArmor
bool SkipAttack
SkillTrigger Trigger
```

### EffectExecution.cs
```csharp
string SourceRuntimeId
string TargetRuntimeId
```

---

## Estructura de Carpetas Actual

```
Assets/Runtime/Data/
├── CardDefinition.cs                    [✓ Principal]
├── CardSkillDefinition.cs               [△ Antigua, hybrid]
├── AbilityDefinition.cs                 [✓]
├── EffectDefinition.cs                  [✓]
├── TargetSelectorDefinition.cs          [✓]
├── CardType.cs                          [✓]
├── UnitType.cs                          [✓]
├── SkillType.cs                         [✓]
│
├── *AttackSelector.cs                   [✓ 4 patrones]
├── *TargetSelector.cs                   [✓ 9+ variantes]
│
├── Skills/                              [△ Viejos ~4-5]
│   ├── ArmorSkill.cs
│   ├── TrampleSkill.cs
│   ├── PoisonSkill.cs
│   ├── FlySkill.cs
│   └── ...
│
├── Effects/                             [△ Efectos viejos ~8]
│   ├── DamageEffect.cs
│   ├── HealEffect.cs
│   ├── ShieldEffect.cs
│   ├── BuffAttackEffect.cs
│   └── ...
│
└── (Core/)
    └── CardEnums.cs                     [✓]

Assets/Runtime/Battle/
├── DuelRuntime.cs                       [✓]
├── BattleContext.cs                     [✓]
├── BattleModels.cs                      [✓]
│
├── Skills/                              [✓ NUEVO SISTEMA - 22]
│   ├── Core/
│   │   ├── SkillTrigger.cs
│   │   ├── SkillContext.cs
│   │   ├── ISkillEffect.cs
│   │   ├── SkillRegistry.cs
│   │   ├── SkillDefinition.cs
│   │   └── SkillEffectRegistration.cs
│   ├── Pipeline/
│   │   ├── SkillPipeline.cs
│   │   ├── AttackValidationPhase.cs
│   │   ├── TargetSelectionPhase.cs
│   │   ├── DamageCalculationPhase.cs
│   │   └── EffectApplicationPhase.cs
│   ├── Effects/
│   │   ├── Defensive/ (5)
│   │   ├── Offensive/ (5)
│   │   └── Utility/ (12)
│   └── Managers/
│       └── CardSkillManager.cs
│
└── Phases/
    ├── IBattlePhase.cs
    ├── BattlePhaseManager.cs
    ├── AttackExecutionPhase.cs
    ├── TurnStartPhase.cs
    └── TurnEndPhase.cs
```

---

## Status: Qué Existe, Falta y Sobra

### ✓ EXISTE Y FUNCIONA

1. **Estructura base** - CardDefinition, AbilityDefinition, EffectDefinition
2. **Tipos de carta** - CardType (Unit/Utility/Equipment/Spell), UnitType (Melee/Ranged/Magic)
3. **Nuevo sistema skills** - 22 effects completos con 4-phase pipeline
4. **Target selectors** - 13+ variantes para patrones y casos especiales
5. **Battle phases** - TurnStart, AttackExecution, TurnEnd
6. **Repositioning** - Shift automático cuando muere Front
7. **Status effects básicos** - Poison, Stun, Enrage en CardRuntime

### △ PARCIALMENTE IMPLEMENTADO

1. **CardSkillDefinition** (antigua) - 4-5 skills viejos
   - Métodos virtuales: CanAttack, ModifyDamage, GetArmorAbsorption, BlocksDamage, GetLogMessage
   - DECISIÓN PENDIENTE: Deprecar o mantener como legacy?

2. **EffectDefinition** - 8 effects en carpeta Effects/
   - Usados por AbilityDefinition
   - Separado del pipeline ISkillEffect
   - DECISIÓN PENDIENTE: Mantener dual-system o unificar?

3. **AbilityDefinition** - Estructura lista, triggers NO ejecutados
   - Soporte para 6 triggers definidos
   - Falta wiring en BattleContext

### ✗ FALTA IMPLEMENTAR

1. **Abilities system - CRÍTICO**
   - [ ] OnPlay → en TryPlayCard
   - [ ] OnTurnStart → en TurnStartPhase
   - [ ] OnTurnEnd → en TurnEndPhase
   - [ ] OnBattlePhase → en AttackExecutionPhase
   - [ ] OnDamaged → en DealDamage
   - [ ] OnDeath → en CleanupDeaths

2. **Equipment card system**
   - CardType.Equipment cartas
   - Equipar en unidad (TryPlayCard lógica)
   - Skills/abilities desde equipo

3. **Status effects avanzados**
   - Poison: daño por turno (básico existe)
   - Stun: skip ataque (básico existe)
   - Buffs temporales: +X ATK por N turnos
   - Debuffs: -X DEF por N turnos

4. **Advanced mechanics**
   - Reflect damage back
   - Taunt completo (force target)
   - Chain damages
   - Ricochet

5. **Mana system**
   - Validación de costo en TryPlayCard
   - ManaBurn skill

6. **Card types: Spell y Utility**
   - No hay ejecución para estos tipos

---

## Decisiones Pendientes

### 1. Deprecate Old Skill System?

**Option A: Deprecate (RECOMENDADO)**
- Mover CardSkillDefinition → Legacy/
- Migrar 4-5 skills viejos a ISkillEffect (ya parcialmente hecho)
- Resultado: 1 skill system limpio
- Ventaja: Menos code clutter, 22 effects > 5 skills
- Desventaja: Refactor

**Option B: Maintain Dual**
- CardSkillDefinition = simple, legacy
- ISkillEffect = nuevo, modular
- Resultado: 2 sistemas claros pero paralelos
- Ventaja: No refactor inmediato
- Desventaja: Confusión en futuro

### 2. Unify Effect Systems?

**Option A: Keep Separate (RECOMENDADO)**
- EffectDefinition = para abilities
- ISkillEffect = para ataque/defensa
- Resultado: Separación clara de concerns
- Ventaja: Cada uno optimizado para su caso

**Option B: Super-interfaz**
- Crear IGameplayEffect que une ambos
- Un solo registry
- Resultado: Unificado pero complejo

---

## Próximos Pasos (Prioridad)

1. **CRÍTICO: Activar AbilityDefinition**
   - Wire 6 triggers en BattleContext
   - Crear assets de abilities de prueba
   - Verificar ejecución en batalla

2. **IMPORTANTE: Limpiar Skills viejos**
   - Revisar qué 4-5 skills viejos existen
   - Deprecar o migrar todos a ISkillEffect
   - Remover archivos legacy

3. **IMPORTANTE: Implementar Equipment**
   - CardType.Equipment can spawn
   - TryPlayCard logic para equipar
   - Skills from equipment en CardRuntime

4. **MENOR: Expand Effects**
   - Temporal buffs (duration)
   - Debuffs
   - Advanced status effects

5. **TESTING: Full battle flow**
   - Crear test deck con abilities
   - Verificar todos triggers se ejecutan
   - Verificar effects aplican correctamente

