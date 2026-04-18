using System.Collections.Generic;
using UnityEngine;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Ranged Attack selector: Same slot attacks (straight line).
    /// Left attacks Left, Right attacks Right.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Target Selector/Ranged Attack", fileName = "TS_RangedAttack")]
    public sealed class RangedAttackSelector : TargetSelectorDefinition
    {
        public override void SelectTargets(BattleContext context, TargetSelectionRequest request, List<string> results)
        {
            results.Clear();

            var attacker = context.FindCard(request.SourceRuntimeId);
            var enemy = context.GetPlayerState(request.TargetPlayer);

            if (enemy == null || attacker == null)
                return;

            // Check for taunt target first (overrides normal targeting)
            var tauntTarget = context.FindTauntTarget(request.TargetPlayer);
            if (tauntTarget != null)
            {
                results.Add(tauntTarget.RuntimeId);
                return;
            }

            // Ranged attacks same slot (straight line)
            var targetSlot = attacker.CurrentSlot;
            var target = enemy.FindOccupant(targetSlot);

            if (target != null)
            {
                results.Add(target.RuntimeId);
                return;
            }

            // No target - hero takes damage
        }
    }
}
