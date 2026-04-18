using System.Collections.Generic;
using UnityEngine;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Straight-line target selector: attacks same slot as attacker.
    /// If target slot empty, targets Front slot.
    /// If both empty, returns no target (hero takes damage).
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Target Selector/Straight Line", fileName = "TS_StraightLine")]
    public sealed class StraightLineTargetSelector : TargetSelectorDefinition
    {
        public override void SelectTargets(BattleContext context, TargetSelectionRequest request, List<string> results)
        {
            results.Clear();
            
            if (string.IsNullOrEmpty(request.SourceRuntimeId))
                return;

            var source = context.FindCard(request.SourceRuntimeId);
            if (source == null)
                return;

            var targetPlayer = context.GetPlayerState(request.TargetPlayer);
            if (targetPlayer == null)
                return;

            // Try same slot as attacker
            var slotRuntime = targetPlayer.FindSlot(source.CurrentSlot);
            if (slotRuntime?.Occupant != null)
            {
                results.Add(slotRuntime.Occupant.RuntimeId);
                return;
            }

            // Fall back to Front
            var frontSlot = targetPlayer.FindSlot(BoardSlot.Front);
            if (frontSlot?.Occupant != null)
            {
                results.Add(frontSlot.Occupant.RuntimeId);
                return;
            }

            // No target - hero takes damage (results stays empty)
        }
    }
}
