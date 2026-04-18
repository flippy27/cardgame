namespace Flippy.CardDuelMobile.Battle.Skills.Effects
{
    /// <summary>LastStand: Double damage if alone on board.</summary>
    public class LastStandEffect : ISkillEffect
    {
        public SkillTrigger Trigger => SkillTrigger.OnDamageCalculation;
        public int Priority => 50;

        public void Apply(SkillContext context)
        {
            if (context.Attacker == null || context.Battle == null)
                return;

            // Count allies
            var allyBoard = context.Battle.GetPlayerState(context.Attacker.OwnerIndex);
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
