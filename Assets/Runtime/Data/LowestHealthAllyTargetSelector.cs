using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    [CreateAssetMenu(menuName = "Cards/Target Selector/Lowest Health Ally", fileName = "TS_LowestHealthAlly")]
    public sealed class LowestHealthAllyTargetSelector : TargetSelectorDefinition
    {
        public override void SelectTargets(BattleContext context, TargetSelectionRequest request, List<string> results)
        {
            results.Clear();
            var ally = context.GetPlayerState(request.SourcePlayer);
            if (ally == null)
            {
                return;
            }

            var target = ally.Board
                .Where(slot => slot.Occupant != null)
                .OrderBy(slot => slot.Occupant.CurrentHealth)
                .Select(slot => slot.Occupant)
                .FirstOrDefault();

            if (target != null)
            {
                results.Add(target.RuntimeId);
            }
        }
    }
}