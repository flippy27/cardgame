using UnityEngine;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Reglas configurables del duelo.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Duel Rules Profile", fileName = "DuelRulesProfile")]
    public sealed class DuelRulesProfile : ScriptableObject
    {
        public int startingHeroHealth = 20;
        public int startingMana = 1;
        public int manaPerTurn = 1;
        public int maxMana = 10;
        public int startingHandSize = 4;
        public bool drawAtTurnStart = true;
    }
}
