using System.Collections.Generic;
using UnityEngine;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Data;

namespace Flippy.CardDuelMobile.Data
{
    [CreateAssetMenu(menuName = "Selectors/Diagonal Target Selector", fileName = "DiagonalTargetSelector")]
    public sealed class DiagonalTargetSelector : TargetSelectorDefinition
    {
        public override void SelectTargets(BattleContext context, TargetSelectionRequest request, List<string> results)
        {
            results.Clear();

            var targetPlayer = context.GetPlayerState(request.TargetPlayer);
            if (targetPlayer == null)
                return;

            BoardSlot diagonalSlot;
            if (request.SourceSlot == BoardSlot.BackLeft)
                diagonalSlot = BoardSlot.BackRight;
            else if (request.SourceSlot == BoardSlot.BackRight)
                diagonalSlot = BoardSlot.BackLeft;
            else
                return;

            var slot = targetPlayer.FindSlot(diagonalSlot);
            if (slot?.Occupant != null)
            {
                results.Add(slot.Occupant.RuntimeId);
            }
        }
    }
}
