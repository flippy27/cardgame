using System.Collections.Generic;
using UnityEngine;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Cleave: hits all enemies in same row.
    /// Front → hits all 3 enemy positions
    /// BackLeft/BackRight → hits both back positions
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Target Selector/Cleave", fileName = "TS_Cleave")]
    public sealed class CleaveTargetSelector : TargetSelectorDefinition
    {
        public override void SelectTargets(BattleContext context, TargetSelectionRequest request, List<string> results)
        {
            results.Clear();
            var enemy = context.GetPlayerState(request.TargetPlayer);
            if (enemy == null)
            {
                return;
            }

            if (request.SourceSlot == BoardSlot.Front)
            {
                // Front hits all 3 positions
                var front = enemy.FindOccupant(BoardSlot.Front);
                var backLeft = enemy.FindOccupant(BoardSlot.BackLeft);
                var backRight = enemy.FindOccupant(BoardSlot.BackRight);

                if (front != null) results.Add(front.RuntimeId);
                if (backLeft != null) results.Add(backLeft.RuntimeId);
                if (backRight != null) results.Add(backRight.RuntimeId);
            }
            else
            {
                // Back positions hit both back slots
                var backLeft = enemy.FindOccupant(BoardSlot.BackLeft);
                var backRight = enemy.FindOccupant(BoardSlot.BackRight);

                if (backLeft != null) results.Add(backLeft.RuntimeId);
                if (backRight != null) results.Add(backRight.RuntimeId);

                // If no back targets, fallback to Front
                if (results.Count == 0)
                {
                    var front = enemy.FindOccupant(BoardSlot.Front);
                    if (front != null) results.Add(front.RuntimeId);
                }
            }
        }
    }
}
