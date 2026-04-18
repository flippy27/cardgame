using System.Collections.Generic;
using System.Linq;

namespace Flippy.CardDuelMobile.Battle.Skills
{
    /// <summary>
    /// Orchestrates skill execution through multiple phases.
    /// Each phase represents a point in the attack/damage flow where skills can intervene.
    /// </summary>
    public class SkillPipeline
    {
        private List<ISkillPhase> _phases = new();

        public SkillPipeline()
        {
            RegisterPhases();
        }

        private void RegisterPhases()
        {
            _phases.Add(new AttackValidationPhase());
            _phases.Add(new TargetSelectionPhase());
            _phases.Add(new DamageCalculationPhase());
            _phases.Add(new EffectApplicationPhase());
        }

        /// <summary>Execute skill pipeline on the given context.</summary>
        public SkillContext Execute(SkillContext context)
        {
            if (context == null) return context;

            foreach (var phase in _phases)
            {
                if (!phase.CanExecute(context))
                    continue;

                context = phase.Execute(context);

                if (context.SkipAttack)
                    break;
            }

            return context;
        }

    }

    /// <summary>Represents a phase in the skill pipeline.</summary>
    public interface ISkillPhase
    {
        bool CanExecute(SkillContext context);
        SkillContext Execute(SkillContext context);
    }
}
