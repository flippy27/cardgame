using Flippy.CardDuelMobile.Battle.Abilities.Effects;

namespace Flippy.CardDuelMobile.Battle.Abilities
{
    /// <summary>
    /// Registers all skill effects in the AbilityRegistry.
    /// Called once during game initialization.
    /// </summary>
    public static class AbilityEffectRegistration
    {
        public static void RegisterAllEffects()
        {
            // Defensive effects
            AbilityRegistry.AddEffect("armor", new ArmorEffect());
            AbilityRegistry.AddEffect("shield", new ShieldEffect());
            AbilityRegistry.AddEffect("reflection", new ReflectionEffect());
            AbilityRegistry.AddEffect("evasion", new EvasionEffect());
            AbilityRegistry.AddEffect("dodge", new DodgeEffect());

            // Offensive effects
            AbilityRegistry.AddEffect("poison", new PoisonEffect());
            AbilityRegistry.AddEffect("stun", new StunEffect());
            AbilityRegistry.AddEffect("enrage", new EnrageEffect());
            AbilityRegistry.AddEffect("leech", new LeechEffect());
            AbilityRegistry.AddEffect("mana_burn", new ManaBurnEffect());

            // Utility effects
            AbilityRegistry.AddEffect("trample", new TrampleEffect());
            AbilityRegistry.AddEffect("execute", new ExecuteEffect());
            AbilityRegistry.AddEffect("last_stand", new LastStandEffect());
            AbilityRegistry.AddEffect("melee_range", new MeleeRangeEffect());
            AbilityRegistry.AddEffect("cleave", new CleaveEffect());
            AbilityRegistry.AddEffect("diagonal_attack", new DiagonalAttackEffect());
            AbilityRegistry.AddEffect("ricochet", new RicochetEffect());
            AbilityRegistry.AddEffect("chain", new ChainEffect());
            AbilityRegistry.AddEffect("fly", new FlyEffect());
            AbilityRegistry.AddEffect("taunt", new TauntEffect());
            AbilityRegistry.AddEffect("regenerate", new RegenerateEffect());
            AbilityRegistry.AddEffect("charge", new ChargeEffect());
        }
    }
}
