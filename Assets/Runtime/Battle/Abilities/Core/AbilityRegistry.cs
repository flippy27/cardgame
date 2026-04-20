using System.Collections.Generic;
using System.Linq;

namespace Flippy.CardDuelMobile.Battle.Abilities
{
    /// <summary>
    /// Central registry for all skills and their effects.
    /// Allows easy querying and extension of skill behavior.
    /// </summary>
    public static class AbilityRegistry
    {
        private static Dictionary<string, AbilityDefinition> _abilities = new();
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;

            RegisterAllAbilities();
            AbilityEffectRegistration.RegisterAllEffects();
            _initialized = true;
        }

        private static void RegisterAllAbilities()
        {
            // Damage reduction abilities
            RegisterAbility("armor");
            RegisterAbility("shield");
            RegisterAbility("reflection");
            RegisterAbility("evasion");
            RegisterAbility("dodge");

            // Damage application abilities
            RegisterAbility("poison");
            RegisterAbility("stun");
            RegisterAbility("enrage");
            RegisterAbility("leech");
            RegisterAbility("mana_burn");

            // Attack modifiers
            RegisterAbility("trample");
            RegisterAbility("execute");
            RegisterAbility("last_stand");
            RegisterAbility("melee_range");

            // Area/targeting abilities
            RegisterAbility("cleave");
            RegisterAbility("diagonal_attack");
            RegisterAbility("ricochet");
            RegisterAbility("chain");

            // Blocking/defensive
            RegisterAbility("fly");
            RegisterAbility("taunt");

            // Passive/turn-based
            RegisterAbility("regenerate");
            RegisterAbility("charge");
        }

        public static void RegisterAbility(string abilityId)
        {
            if (!_abilities.ContainsKey(abilityId))
            {
                _abilities[abilityId] = new AbilityDefinition(abilityId);
            }
        }

        public static AbilityDefinition GetAbility(string abilityId)
        {
            _abilities.TryGetValue(abilityId, out var ability);
            return ability;
        }

        public static void AddEffect(string abilityId, IAbilityEffect effect)
        {
            var ability = GetAbility(abilityId);
            if (ability != null)
            {
                ability.AddEffect(effect);
            }
        }

        public static List<IAbilityEffect> GetEffects(string abilityId, AbilityTrigger trigger)
        {
            var ability = GetAbility(abilityId);
            if (ability == null) return new();

            return ability.Effects
                .Where(e => e.Trigger == trigger)
                .OrderByDescending(e => e.Priority)
                .ToList();
        }

        public static List<IAbilityEffect> GetAllEffects(AbilityTrigger trigger)
        {
            var effects = new List<IAbilityEffect>();
            foreach (var ability in _abilities.Values)
            {
                effects.AddRange(ability.Effects.Where(e => e.Trigger == trigger));
            }
            return effects.OrderByDescending(e => e.Priority).ToList();
        }

        public static bool HasAbility(string abilityId)
        {
            return _abilities.ContainsKey(abilityId);
        }
    }
}
