using System.Collections.Generic;
using UnityEngine;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Magic Attack selector: Diagonal opposite attacks only.
    /// BackLeft attacks BackRight, BackRight attacks BackLeft.
    /// No fallback to Front - if opposite empty, hero takes damage.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Target Selector/Magic Attack", fileName = "TS_MagicAttack")]
    public sealed class MagicAttackSelector : TargetSelectorDefinition
    {
        public override void SelectTargets(BattleContext context, TargetSelectionRequest request, List<string> results)
        {
            results.Clear();

            var attacker = context.FindCard(request.SourceRuntimeId);
            var enemy = context.GetPlayerState(request.TargetPlayer);

            if (enemy == null || attacker == null)
                return;

            // Check for taunt target first (overrides diagonal targeting)
            var tauntTarget = context.FindTauntTarget(request.TargetPlayer);
            if (tauntTarget != null)
            {
                results.Add(tauntTarget.RuntimeId);
                return;
            }

            // Magic attacks diagonal opposite only
            BoardSlot targetSlot = BoardSlot.Front; // default, but won't be used

            if (attacker.CurrentSlot == BoardSlot.BackLeft)
            {
                targetSlot = BoardSlot.BackRight; // Left attacks Right diagonally
            }
            else if (attacker.CurrentSlot == BoardSlot.BackRight)
            {
                targetSlot = BoardSlot.BackLeft; // Right attacks Left diagonally
            }
            else
            {
                // Magic can't attack from Front position
                return;
            }

            var target = enemy.FindOccupant(targetSlot);
            if (target != null)
            {
                results.Add(target.RuntimeId);
            }

            // No fallback to Front - if opposite empty, hero takes damage
        }
    }
}
