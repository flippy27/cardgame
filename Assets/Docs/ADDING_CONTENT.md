# Cómo agregar contenido nuevo

## Crear una carta nueva

1. Duplica una carta existente o crea una nueva con:
   - `Create > Cards > Card Definition`
2. Completa:
   - nombre
   - costo
   - ataque
   - vida
   - rareza
   - facción
   - selector de objetivo
   - habilidades
3. Asigna un `CardVisualProfile`.

## Crear una habilidad nueva

1. Crea un selector si el target cambia.
2. Crea uno o varios efectos.
3. Crea `AbilityDefinition`.
4. Asigna trigger + selector + efectos.
5. Conecta la habilidad a la carta.

## Crear un efecto nuevo por código

Ejemplo conceptual:

```csharp
public sealed class StealArmorEffect : EffectDefinition
{
    public override void Resolve(BattleContext context, EffectExecution execution)
    {
        // lógica
    }
}
```

## Crear un selector nuevo

```csharp
public sealed class LowestAttackEnemySelector : TargetSelectorDefinition
{
    public override void SelectTargets(BattleContext context, TargetSelectionRequest request, List<CardRuntimeId> results)
    {
        // lógica
    }
}
```

## Balance
La gracia de esta estructura es que el balance está mayormente en assets:
- stats
- costo
- triggers
- número de targets
- modificadores

## Recomendación
No metas lógica especial directamente en la UI o en `NetworkBehaviour`.
La lógica del juego debería quedar en `Runtime/Battle`.
