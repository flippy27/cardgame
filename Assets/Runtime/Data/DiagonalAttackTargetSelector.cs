using System.Collections.Generic;
using UnityEngine;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Diagonal Attack selector: Back cards attack diagonal back cards.
    /// Front → BackLeft or BackRight (prefers BackLeft)
    /// BackLeft → BackRight
    /// BackRight → BackLeft
    /// If target doesn't exist, falls back to Front.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Target Selector/Diagonal Attack", fileName = "TS_DiagonalAttack")]
    public sealed class DiagonalAttackTargetSelector : TargetSelectorDefinition
    {
        public override void SelectTargets(BattleContext context, TargetSelectionRequest request, List<string> results)
        {
            results.Clear();
            var attacker = context.FindCard(request.SourceRuntimeId);
            var enemy = context.GetPlayerState(request.TargetPlayer);
            if (enemy == null || attacker == null)
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

            BoardSlot targetSlot = BoardSlot.Front;

            // Determine diagonal target based on attacker position
            if (attacker.CurrentSlot == BoardSlot.Front)
            {
                targetSlot = BoardSlot.BackLeft; // Front attacks BackLeft diagonally
            }
            else if (attacker.CurrentSlot == BoardSlot.BackLeft)
            {
                targetSlot = BoardSlot.BackRight; // BackLeft attacks BackRight diagonally
            }
            else if (attacker.CurrentSlot == BoardSlot.BackRight)
            {
                targetSlot = BoardSlot.BackLeft; // BackRight attacks BackLeft diagonally
            }

            // Try diagonal target first
            var target = enemy.FindOccupant(targetSlot);
            if (target != null)
            {
                results.Add(target.RuntimeId);
                return;
            }

            // Fallback to Front if diagonal doesn't exist
            target = enemy.FindOccupant(BoardSlot.Front);
            if (target != null)
            {
                results.Add(target.RuntimeId);
            }
        }
    }
}
