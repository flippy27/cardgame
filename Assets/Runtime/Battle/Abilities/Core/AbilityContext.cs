using System.Collections.Generic;

namespace Flippy.CardDuelMobile.Battle.Abilities
{
    /// <summary>
    /// Context that flows through skill pipeline, allowing each phase to read/modify state.
    /// </summary>
    public class AbilityContext
    {
        public CardRuntime Attacker { get; set; }
        public CardRuntime Defender { get; set; }
        public BattleContext Battle { get; set; }

        // Attack state
        public int BaseDamage { get; set; }
        public int FinalDamage { get; set; }
        public List<CardRuntime> TargetList { get; set; } = new();

        // Flags that can be modified by skills
        public bool IsValidAttack { get; set; } = true;
        public bool IgnoreArmor { get; set; }
        public bool SkipAttack { get; set; }

        // Trigger for this context
        public AbilityTrigger Trigger { get; set; }

        public AbilityContext() { }

        public AbilityContext(CardRuntime attacker, CardRuntime defender, BattleContext battle, AbilityTrigger trigger)
        {
            Attacker = attacker;
            Defender = defender;
            Battle = battle;
            Trigger = trigger;
        }
    }
}
