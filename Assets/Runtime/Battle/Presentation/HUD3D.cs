using UnityEngine;
using TMPro;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// HUD 3D: muestra HP, mana, turn info en overlay canvas.
    /// </summary>
    public class HUD3D : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI localHeroInfoText;
        [SerializeField] private TextMeshProUGUI remoteHeroInfoText;
        [SerializeField] private TextMeshProUGUI turnInfoText;
        [SerializeField] private TextMeshProUGUI battleLogText;

        public TextMeshProUGUI LocalHeroInfoText
        {
            get => localHeroInfoText;
            set => localHeroInfoText = value;
        }

        public TextMeshProUGUI RemoteHeroInfoText
        {
            get => remoteHeroInfoText;
            set => remoteHeroInfoText = value;
        }

        public TextMeshProUGUI TurnInfoText
        {
            get => turnInfoText;
            set => turnInfoText = value;
        }

        public TextMeshProUGUI BattleLogText
        {
            get => battleLogText;
            set => battleLogText = value;
        }

        public void UpdateLocalHeroInfo(int health, int maxHealth, int mana, int maxMana)
        {
            if (localHeroInfoText != null)
            {
                localHeroInfoText.text = $"You\nHP: {health}/{maxHealth}\nMana: {mana}/{maxMana}";
            }
        }

        public void UpdateRemoteHeroInfo(int health, int maxHealth, int mana, int maxMana)
        {
            if (remoteHeroInfoText != null)
            {
                remoteHeroInfoText.text = $"Enemy\nHP: {health}/{maxHealth}\nMana: {mana}/{maxMana}";
            }
        }

        public void UpdateTurnInfo(int turnNumber, int activePlayerIndex, bool isLocalTurn)
        {
            if (turnInfoText != null)
            {
                string turnText = isLocalTurn ? "YOUR TURN" : "OPPONENT'S TURN";
                turnInfoText.text = $"Turn {turnNumber}\n{turnText}";
            }
        }

        public void Log(string message)
        {
            if (battleLogText != null)
            {
                battleLogText.text += "\n" + message;
            }
        }

        public void LogAttack(string attacker, int damage, string defender)
        {
            Log($"<color=red>{attacker}</color> attacked <color=blue>{defender}</color> for <b>{damage}</b> damage");
        }

        public void LogCardPlayed(string player, string cardName, string slot)
        {
            Log($"<color=cyan>{player}</color> played <b>{cardName}</b> to {slot}");
        }

        public void LogCardDied(string cardName)
        {
            Log($"<color=yellow>{cardName}</color> died");
        }

        public void LogTurnStart(string player)
        {
            Log($"<b>{player}'s Turn Started</b>");
        }

        public void ClearLog()
        {
            if (battleLogText != null)
            {
                battleLogText.text = "";
            }
        }
    }
}
