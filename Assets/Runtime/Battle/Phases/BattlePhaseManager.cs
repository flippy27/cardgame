using System.Collections.Generic;

namespace Flippy.CardDuelMobile.Battle.Phases
{
    /// <summary>
    /// Orchestrates all battle phases for a complete turn.
    /// Extensible - new phases can be added without modifying this class.
    /// </summary>
    public class BattlePhaseManager
    {
        private List<IBattlePhase> _phases = new();

        public BattlePhaseManager()
        {
            RegisterPhases();
        }

        private void RegisterPhases()
        {
            // Register phases in order of execution
            _phases.Add(new TurnStartPhase());
            _phases.Add(new AttackExecutionPhase());
            _phases.Add(new TurnEndPhase());
        }

        /// <summary>Execute all phases for a complete turn.</summary>
        public bool ExecuteTurn(BattleContext context, int activePlayerIndex)
        {
            foreach (var phase in _phases)
            {
                if (!phase.Execute(context, activePlayerIndex))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Add a custom phase to the pipeline.</summary>
        public void RegisterPhase(IBattlePhase phase, int? insertAtIndex = null)
        {
            if (insertAtIndex.HasValue && insertAtIndex.Value >= 0 && insertAtIndex.Value <= _phases.Count)
            {
                _phases.Insert(insertAtIndex.Value, phase);
            }
            else
            {
                _phases.Add(phase);
            }
        }

        /// <summary>Remove a phase by type.</summary>
        public void UnregisterPhase<T>() where T : IBattlePhase
        {
            _phases.RemoveAll(p => p.GetType() == typeof(T));
        }
    }
}
