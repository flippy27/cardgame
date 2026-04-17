using System.Collections.Generic;
using UnityEngine;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Chain: hits all enemy cards on board.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Target Selector/Chain", fileName = "TS_Chain")]
    public sealed class ChainTargetSelector : TargetSelectorDefinition
    {
        public override void SelectTargets(BattleContext context, TargetSelectionRequest request, List<string> results)
        {
            results.Clear();
            var enemy = context.GetPlayerState(request.TargetPlayer);
            if (enemy == null)
            {
                return;
            }

            // Hit all occupied positions
            foreach (var slot in enemy.Board)
            {
                if (slot.Occupant != null)
                {
                    results.Add(slot.Occupant.RuntimeId);
                }
            }
        }
    }
}
