using System.Collections.Generic;
using UnityEngine;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Melee Range selector: Allows Front card to attack Back cards.
    /// Tries to attack any Back card first (BackLeft preferred).
    /// Falls back to Front if no Back cards exist.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Target Selector/Melee Range", fileName = "TS_MeleeRange")]
    public sealed class MeleeRangeTargetSelector : TargetSelectorDefinition
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

            // Try BackLeft first
            var target = enemy.FindOccupant(BoardSlot.BackLeft);
            if (target != null)
            {
                results.Add(target.RuntimeId);
                return;
            }

            // Try BackRight
            target = enemy.FindOccupant(BoardSlot.BackRight);
            if (target != null)
            {
                results.Add(target.RuntimeId);
                return;
            }

            // Fallback to Front
            target = enemy.FindOccupant(BoardSlot.Front);
            if (target != null)
            {
                results.Add(target.RuntimeId);
            }
        }
    }
}
