using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Armor Effect: grants armor to target.
    /// </summary>
    [CreateAssetMenu(menuName = "Effects/Armor", fileName = "FX_Armor")]
    public sealed class ArmorEffect : EffectDefinition
    {
        [Min(1)] public int armorAmount = 1;

        public override void Resolve(BattleContext context, EffectExecution execution)
        {
            var target = context.FindCard(execution.TargetRuntimeId);
            if (target != null)
            {
                target.Armor += armorAmount;
                Debug.Log($"[Effect] {target.DisplayName} gained +{armorAmount} Armor (total: {target.Armor})");
            }
        }
    }
}
