using System.Collections.Generic;
using UnityEngine;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Data
{
    [CreateAssetMenu(menuName = "Cards/Target Selector/Backline First", fileName = "TS_BacklineFirst")]
    public sealed class BacklineFirstTargetSelector : TargetSelectorDefinition
    {
        public override void SelectTargets(BattleContext context, TargetSelectionRequest request, List<string> results)
        {
            results.Clear();
            var enemy = context.GetPlayerState(request.TargetPlayer);
            if (enemy == null)
            {
                return;
            }

            var candidates = new[]
            {
                enemy.FindOccupant(BoardSlot.BackLeft),
                enemy.FindOccupant(BoardSlot.BackRight),
                enemy.FindOccupant(BoardSlot.Front)
            };

            foreach (var candidate in candidates)
            {
                if (candidate != null)
                {
                    results.Add(candidate.RuntimeId);
                    return;
                }
            }
        }
    }
}