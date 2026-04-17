using UnityEngine;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;

namespace Flippy.CardDuelMobile.UI
{
    public class UIManager : MonoBehaviour
    {
        public GameObject menuPanel;
        public GameObject gameplayPanel;

        private void Awake()
        {
            BattleSnapshotBus.SnapshotReceived += HandleSnapshot;
        }

        private void OnDestroy()
        {
            BattleSnapshotBus.SnapshotReceived -= HandleSnapshot;
        }

        private void HandleSnapshot(string json)
        {
            var snapshot = JsonUtility.FromJson<DuelSnapshotDto>(json);
            if (snapshot == null) return;

            bool isInProgress = snapshot.matchPhase == MatchPhase.InProgress;

            if (menuPanel != null)
                menuPanel.SetActive(!isInProgress);

            if (gameplayPanel != null)
                gameplayPanel.SetActive(isInProgress);
        }
    }
}
