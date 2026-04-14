using System.Collections.Generic;
using UnityEngine;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Data
{
    [CreateAssetMenu(menuName = "Cards/Target Selector/Frontline First", fileName = "TS_FrontlineFirst")]
    public sealed class FrontlineFirstTargetSelector : TargetSelectorDefinition
    {
        public override void SelectTargets(BattleContext context, TargetSelectionRequest request, List<string> results)
        {
            results.Clear();
            var enemy = context.GetPlayerState(request.TargetPlayer);
            if (enemy == null)
            {
                return;
            }

            var front = enemy.FindOccupant(BoardSlot.Front);
            if (front != null)
            {
                results.Add(front.RuntimeId);
                return;
            }

            foreach (var slot in enemy.Board)
            {
                if (slot.Occupant != null)
                {
                    results.Add(slot.Occupant.RuntimeId);
                    return;
                }
            }
        }
    }
}