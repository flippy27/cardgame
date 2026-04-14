using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Base de efectos reutilizables.
    /// </summary>
    public abstract class EffectDefinition : ScriptableObject
    {
        [TextArea]
        public string designerNotes;

        /// <summary>
        /// Resuelve el efecto usando contexto de batalla.
        /// </summary>
        public abstract void Resolve(BattleContext context, EffectExecution execution);
    }
}
