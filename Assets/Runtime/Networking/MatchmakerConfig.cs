using UnityEngine;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Configuración simple para quick match y queue avanzada.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Matchmaker Config", fileName = "MatchmakerConfig")]
    public sealed class MatchmakerConfig : ScriptableObject
    {
        public string sessionType = "CardDuel";
        public string advancedQueueName = "Friendly";
        public bool useAdvancedMatchmaker;
        public bool createSessionIfQuickJoinFails = true;
        public float quickJoinTimeoutSeconds = 5f;
    }
}
