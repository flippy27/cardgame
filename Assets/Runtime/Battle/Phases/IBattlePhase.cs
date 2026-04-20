using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Battle;
namespace Flippy.CardDuelMobile.Battle.Phases
{
    /// <summary>
    /// Represents a phase in the battle turn.
    /// Each phase is independent and can be extended without modifying others.
    /// </summary>
    public interface IBattlePhase
    {
        /// <summary>Display name for debugging.</summary>
        string PhaseName { get; }

        /// <summary>Execute this phase.</summary>
        bool Execute(BattleContext context, int playerIndex);
    }
}
