using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// HUD 3D: muestra HP, mana, turn info en overlay canvas.
    /// Mantiene fallback al layout viejo de texto combinado mientras se conectan
    /// los textos separados desde el inspector.
    /// </summary>
    public class HUD3D : MonoBehaviour
    {
        [Header("Legacy Combined Texts")]
        [SerializeField] private TextMeshProUGUI localHeroInfoText;
        [SerializeField] private TextMeshProUGUI remoteHeroInfoText;

        [Header("Separated Hero Texts")]
        [SerializeField] private TextMeshProUGUI localHeroHpText;
        [SerializeField] private TextMeshProUGUI localHeroManaText;
        [SerializeField] private TextMeshProUGUI remoteHeroHpText;
        [SerializeField] private TextMeshProUGUI remoteHeroManaText;

        [Header("General HUD")]
        [SerializeField] private TextMeshProUGUI turnInfoText;
        [SerializeField] private TextMeshProUGUI battleLogText;

        private int _localHealth;
        private int _localMaxHealth = 20;
        private int _localMana;
        private int _localMaxMana;
        private int _remoteHealth;
        private int _remoteMaxHealth = 20;
        private int _remoteMana;
        private int _remoteMaxMana;

        private void Awake()
        {
            DisableRaycast(localHeroInfoText);
            DisableRaycast(remoteHeroInfoText);
            DisableRaycast(localHeroHpText);
            DisableRaycast(localHeroManaText);
            DisableRaycast(remoteHeroHpText);
            DisableRaycast(remoteHeroManaText);
            DisableRaycast(turnInfoText);
            DisableRaycast(battleLogText);
        }

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

        public TextMeshProUGUI LocalHeroHpText
        {
            get => localHeroHpText;
            set => localHeroHpText = value;
        }

        public TextMeshProUGUI LocalHeroManaText
        {
            get => localHeroManaText;
            set => localHeroManaText = value;
        }

        public TextMeshProUGUI RemoteHeroHpText
        {
            get => remoteHeroHpText;
            set => remoteHeroHpText = value;
        }

        public TextMeshProUGUI RemoteHeroManaText
        {
            get => remoteHeroManaText;
            set => remoteHeroManaText = value;
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

        public int LocalHeroHealth => _localHealth;
        public int RemoteHeroHealth => _remoteHealth;

        public void UpdateLocalHeroInfo(int health, int maxHealth, int mana, int maxMana)
        {
            _localHealth = health;
            _localMaxHealth = maxHealth;
            _localMana = mana;
            _localMaxMana = maxMana;
            RefreshLocalHeroTexts();
        }

        public void UpdateRemoteHeroInfo(int health, int maxHealth, int mana, int maxMana)
        {
            _remoteHealth = health;
            _remoteMaxHealth = maxHealth;
            _remoteMana = mana;
            _remoteMaxMana = maxMana;
            RefreshRemoteHeroTexts();
        }

        public void PreviewLocalHeroHealth(int health)
        {
            _localHealth = health;
            RefreshLocalHeroTexts();
        }

        public void PreviewRemoteHeroHealth(int health)
        {
            _remoteHealth = health;
            RefreshRemoteHeroTexts();
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

        private void RefreshLocalHeroTexts()
        {
            if (localHeroHpText != null)
            {
                localHeroHpText.text = $"You\nHP: {_localHealth}/{_localMaxHealth}";
            }

            if (localHeroManaText != null)
            {
                localHeroManaText.text = $"Mana: {_localMana}/{_localMaxMana}";
            }

            if (localHeroInfoText != null)
            {
                localHeroInfoText.text = $"You\nHP: {_localHealth}/{_localMaxHealth}\nMana: {_localMana}/{_localMaxMana}";
            }
        }

        private void RefreshRemoteHeroTexts()
        {
            if (remoteHeroHpText != null)
            {
                remoteHeroHpText.text = $"Enemy\nHP: {_remoteHealth}/{_remoteMaxHealth}";
            }

            if (remoteHeroManaText != null)
            {
                remoteHeroManaText.text = $"Mana: {_remoteMana}/{_remoteMaxMana}";
            }

            if (remoteHeroInfoText != null)
            {
                remoteHeroInfoText.text = $"Enemy\nHP: {_remoteHealth}/{_remoteMaxHealth}\nMana: {_remoteMana}/{_remoteMaxMana}";
            }
        }

        private static void DisableRaycast(Graphic graphic)
        {
            if (graphic != null)
            {
                graphic.raycastTarget = false;
            }
        }
    }
}
