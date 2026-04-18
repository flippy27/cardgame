using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Permite a la carta atacar el mismo turno que fue jugada.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Effect/Haste", fileName = "FX_Haste")]
    public sealed class HasteEffectDefinition : EffectDefinition
    {
        public override void Resolve(BattleContext context, EffectExecution execution)
        {
            // Set source card to able to attack immediately
            var sourceCard = context.FindCard(execution.SourceRuntimeId);
            if (sourceCard != null)
            {
                sourceCard.TurnsUntilCanAttack = 0;
                Debug.Log($"[Haste] {sourceCard.DisplayName} can now attack immediately");
            }
        }
    }
}
