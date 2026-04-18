using System.Collections.Generic;
using UnityEngine;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Melee Attack selector: Front attacks Front only.
    /// Standard melee pattern - attacks same position.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Target Selector/Melee Attack", fileName = "TS_MeleeAttack")]
    public sealed class MeleeAttackSelector : TargetSelectorDefinition
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

            // Melee attacks Front only
            var target = enemy.FindOccupant(BoardSlot.Front);
            if (target != null)
            {
                results.Add(target.RuntimeId);
                return;
            }

            // No target - hero takes damage
        }
    }
}
