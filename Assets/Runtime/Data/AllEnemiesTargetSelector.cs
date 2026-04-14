using System.Collections.Generic;
using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.Data
{
    [CreateAssetMenu(menuName = "Cards/Target Selector/All Enemies", fileName = "TS_AllEnemies")]
    public sealed class AllEnemiesTargetSelector : TargetSelectorDefinition
    {
        public override void SelectTargets(BattleContext context, TargetSelectionRequest request, List<string> results)
        {
            results.Clear();
            var enemy = context.GetPlayerState(request.TargetPlayer);
            if (enemy == null)
            {
                return;
            }

            foreach (var slot in enemy.Board)
            {
                if (slot.Occupant != null)
                {
                    results.Add(slot.Occupant.RuntimeId);
                }
            }
        }
    }
}