using System.Collections.Generic;
using UnityEngine;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Straight-line attack selector: each position attacks its mirror position.
    /// Front → Front, BackLeft → BackLeft, BackRight → BackRight
    /// Falls back to Front if target slot empty.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Target Selector/Straight Line", fileName = "TS_StraightLine")]
    public sealed class StraightLineTargetSelector : TargetSelectorDefinition
    {
        public override void SelectTargets(BattleContext context, TargetSelectionRequest request, List<string> results)
        {
            results.Clear();
            var enemy = context.GetPlayerState(request.TargetPlayer);
            if (enemy == null)
            {
                return;
            }

            // Check for taunt target first
            var tauntTarget = context.FindTauntTarget(request.TargetPlayer);
            if (tauntTarget != null)
            {
                results.Add(tauntTarget.RuntimeId);
                return;
            }

            // Try to hit same slot
            var target = enemy.FindOccupant(request.SourceSlot);
            if (target != null)
            {
                results.Add(target.RuntimeId);
                return;
            }

            // Fallback to Front if source slot empty
            target = enemy.FindOccupant(BoardSlot.Front);
            if (target != null)
            {
                results.Add(target.RuntimeId);
            }
        }
    }
}
