using Flippy.CardDuelMobile.Battle.Skills.Effects;

namespace Flippy.CardDuelMobile.Battle.Skills
{
    /// <summary>
    /// Registers all skill effects in the SkillRegistry.
    /// Called once during game initialization.
    /// </summary>
    public static class SkillEffectRegistration
    {
        public static void RegisterAllEffects()
        {
            // Defensive effects
            SkillRegistry.AddEffect("armor", new ArmorEffect());
            SkillRegistry.AddEffect("shield", new ShieldEffect());
            SkillRegistry.AddEffect("reflection", new ReflectionEffect());
            SkillRegistry.AddEffect("evasion", new EvasionEffect());
            SkillRegistry.AddEffect("dodge", new DodgeEffect());

            // Offensive effects
            SkillRegistry.AddEffect("poison", new PoisonEffect());
            SkillRegistry.AddEffect("stun", new StunEffect());
            SkillRegistry.AddEffect("enrage", new EnrageEffect());
            SkillRegistry.AddEffect("leech", new LeechEffect());
            SkillRegistry.AddEffect("mana_burn", new ManaBurnEffect());

            // Utility effects
            SkillRegistry.AddEffect("trample", new TrampleEffect());
            SkillRegistry.AddEffect("execute", new ExecuteEffect());
            SkillRegistry.AddEffect("last_stand", new LastStandEffect());
            SkillRegistry.AddEffect("melee_range", new MeleeRangeEffect());
            SkillRegistry.AddEffect("cleave", new CleaveEffect());
            SkillRegistry.AddEffect("diagonal_attack", new DiagonalAttackEffect());
            SkillRegistry.AddEffect("ricochet", new RicochetEffect());
            SkillRegistry.AddEffect("chain", new ChainEffect());
            SkillRegistry.AddEffect("fly", new FlyEffect());
            SkillRegistry.AddEffect("taunt", new TauntEffect());
            SkillRegistry.AddEffect("regenerate", new RegenerateEffect());
            SkillRegistry.AddEffect("charge", new ChargeEffect());
        }
    }
}
