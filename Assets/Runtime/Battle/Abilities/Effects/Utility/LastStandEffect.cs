using Flippy.CardDuelMobile.Battle.Abilities;
namespace Flippy.CardDuelMobile.Battle.Abilities.Effects
{
    /// <summary>LastStand: Double damage if alone on board.</summary>
    public class LastStandEffect : IAbilityEffect
    {
        public AbilityTrigger Trigger => AbilityTrigger.OnDamageCalculation;
        public int Priority => 50;

        public void Apply(AbilityContext context)
        {
            if (context.Attacker == null || context.Battle == null)
                return;

            // Count allies
            var allyBoard = context.Battle.TryGetPlayerState(context.Attacker.OwnerIndex);
            if (allyBoard != null)
            {
                int alliesOnBoard = 0;
                foreach (var slot in allyBoard.Board)
                {
                    if (slot.Occupant != null && !slot.Occupant.IsDead)
                        alliesOnBoard++;
                }

                if (alliesOnBoard == 1)
                {
                    context.FinalDamage *= 2;
                }
            }
        }
    }
}
