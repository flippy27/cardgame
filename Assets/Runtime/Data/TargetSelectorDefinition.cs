using System.Collections.Generic;
using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Base para seleccionar targets desde datos.
    /// </summary>
    public abstract class TargetSelectorDefinition : ScriptableObject
    {
        /// <summary>
        /// Llena una lista de runtime ids targeteados.
        /// </summary>
        public abstract void SelectTargets(BattleContext context, TargetSelectionRequest request, List<string> results);
    }
}
