using System.Collections.Generic;
using System.Linq;

namespace Flippy.CardDuelMobile.Battle.Abilities
{
    /// <summary>
    /// Orchestrates skill execution through multiple phases.
    /// Each phase represents a point in the attack/damage flow where skills can intervene.
    /// </summary>
    public class AbilityPipeline
    {
        private List<IAbilityPhase> _phases = new();

        public AbilityPipeline()
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
        public AbilityContext Execute(AbilityContext context)
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
    public interface IAbilityPhase
    {
        bool CanExecute(AbilityContext context);
        AbilityContext Execute(AbilityContext context);
    }
}
