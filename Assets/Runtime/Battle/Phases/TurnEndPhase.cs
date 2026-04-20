using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Battle;
namespace Flippy.CardDuelMobile.Battle.Phases
{
    /// <summary>
    /// End of turn phase: cleanup, switching active player.
    /// </summary>
    public class TurnEndPhase : IBattlePhase
    {
        public string PhaseName => "Turn End";

        public bool Execute(BattleContext context, int playerIndex)
        {
            // Resolve OnTurnEnd abilities
            context.ExecuteTurnEndAbilities(playerIndex);

            return true;
        }
    }
}
