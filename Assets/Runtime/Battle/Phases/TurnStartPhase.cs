namespace Flippy.CardDuelMobile.Battle.Phases
{
    /// <summary>
    /// Start of turn phase: status effects, mana regen, abilities.
    /// </summary>
    public class TurnStartPhase : IBattlePhase
    {
        public string PhaseName => "Turn Start";

        public bool Execute(BattleContext context, int playerIndex)
        {
            var player = context.GetPlayerState(playerIndex);

            // Process status effects (poison damage, clear stun)
            context.ProcessStatusEffects(playerIndex);

            // Resolve OnTurnStart abilities
            context.ExecuteTurnStartAbilities(playerIndex);

            return true;
        }
    }
}
