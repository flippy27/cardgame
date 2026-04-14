using UnityEngine;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Habilidad compuesta por trigger, selector y efectos.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Ability Definition", fileName = "AbilityDefinition")]
    public sealed class AbilityDefinition : ScriptableObject
    {
        public string abilityId = "ability";
        public string displayName = "Ability";
        [TextArea] public string description;
        public AbilityTrigger trigger = AbilityTrigger.OnBattlePhase;
        public TargetSelectorDefinition targetSelector;
        public EffectDefinition[] effects;

        /// <summary>
        /// Ejecuta todos los efectos de la habilidad usando el execution ya resuelto.
        /// </summary>
        public void Resolve(BattleContext context, EffectExecution execution)
        {
            if (context == null || effects == null)
            {
                return;
            }

            for (int i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (effect == null)
                {
                    continue;
                }

                effect.Resolve(context, execution);
            }
        }
    }
}