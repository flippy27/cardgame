using System.Collections.Generic;
using System.Linq;

namespace Flippy.CardDuelMobile.Battle.Skills
{
    /// <summary>
    /// Central registry for all skills and their effects.
    /// Allows easy querying and extension of skill behavior.
    /// </summary>
    public static class SkillRegistry
    {
        private static Dictionary<string, SkillDefinition> _skills = new();
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;

            RegisterAllSkills();
            SkillEffectRegistration.RegisterAllEffects();
            _initialized = true;
        }

        private static void RegisterAllSkills()
        {
            // Damage reduction skills
            RegisterSkill("armor");
            RegisterSkill("shield");
            RegisterSkill("reflection");
            RegisterSkill("evasion");
            RegisterSkill("dodge");

            // Damage application skills
            RegisterSkill("poison");
            RegisterSkill("stun");
            RegisterSkill("enrage");
            RegisterSkill("leech");
            RegisterSkill("mana_burn");

            // Attack modifiers
            RegisterSkill("trample");
            RegisterSkill("execute");
            RegisterSkill("last_stand");
            RegisterSkill("melee_range");

            // Area/targeting skills
            RegisterSkill("cleave");
            RegisterSkill("diagonal_attack");
            RegisterSkill("ricochet");
            RegisterSkill("chain");

            // Blocking/defensive
            RegisterSkill("fly");
            RegisterSkill("taunt");

            // Passive/turn-based
            RegisterSkill("regenerate");
            RegisterSkill("charge");
        }

        public static void RegisterSkill(string skillId)
        {
            if (!_skills.ContainsKey(skillId))
            {
                _skills[skillId] = new SkillDefinition(skillId);
            }
        }

        public static SkillDefinition GetSkill(string skillId)
        {
            _skills.TryGetValue(skillId, out var skill);
            return skill;
        }

        public static void AddEffect(string skillId, ISkillEffect effect)
        {
            var skill = GetSkill(skillId);
            if (skill != null)
            {
                skill.AddEffect(effect);
            }
        }

        public static List<ISkillEffect> GetEffects(string skillId, SkillTrigger trigger)
        {
            var skill = GetSkill(skillId);
            if (skill == null) return new();

            return skill.Effects
                .Where(e => e.Trigger == trigger)
                .OrderByDescending(e => e.Priority)
                .ToList();
        }

        public static bool HasSkill(string skillId)
        {
            return _skills.ContainsKey(skillId);
        }
    }
}
