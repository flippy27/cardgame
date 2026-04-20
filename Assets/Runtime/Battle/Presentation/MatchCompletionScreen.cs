using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Pantalla de victoria/derrota al terminar partida.
    /// </summary>
    public class MatchCompletionScreen : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private TextMeshProUGUI statsText;
        [SerializeField] private Button menuButton;
        [SerializeField] private CanvasGroup canvasGroup;

        private void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0;
                canvasGroup.blocksRaycasts = false;
            }

            if (menuButton != null)
            {
                menuButton.onClick.AddListener(OnMenuClicked);
            }
        }

        public void ShowVictory(string opponentName, int turnsPlayed)
        {
            Show(true, opponentName, turnsPlayed);
        }

        public void ShowDefeat(string opponentName, int turnsPlayed)
        {
            Show(false, opponentName, turnsPlayed);
        }

        private void Show(bool isVictory, string opponentName, int turnsPlayed)
        {
            if (resultText != null)
            {
                resultText.text = isVictory ? "VICTORY!" : "DEFEAT!";
                resultText.color = isVictory ? Color.green : Color.red;
            }

            if (statsText != null)
            {
                statsText.text = $"vs {opponentName}\nTurns: {turnsPlayed}";
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1;
                canvasGroup.blocksRaycasts = true;
            }

            GameLogger.Info("UI", isVictory ? "Match won!" : "Match lost!");
        }

        private void OnMenuClicked()
        {
            SceneBootstrap.LoadMenu();
        }

        private void OnDestroy()
        {
            if (menuButton != null)
            {
                menuButton.onClick.RemoveListener(OnMenuClicked);
            }
        }
    }
}
