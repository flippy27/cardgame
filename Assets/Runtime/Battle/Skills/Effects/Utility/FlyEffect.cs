namespace Flippy.CardDuelMobile.Battle.Skills.Effects
{
    /// <summary>Fly: Only flying units can attack this card.</summary>
    public class FlyEffect : ISkillEffect
    {
        public SkillTrigger Trigger => SkillTrigger.OnValidateAttack;
        public int Priority => 100;

        public void Apply(SkillContext context)
        {
            // Fly: Only flying units can attack this card.
            // TODO: Implement attackerHasFly check via context or alternate mechanism
            // For now, flying blocks all non-flying attacks by triggering FlyEffect for non-flyers
            if (context.Attacker == null)
                return;

            // Placeholder: will be enhanced when card skill association is available
            // context.IsValidAttack = false;  // Block unless attacker also has fly
        }
    }
}
