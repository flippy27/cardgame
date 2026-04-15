using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Pantalla de leaderboards globales.
    /// Muestra top 100 jugadores por rating.
    /// </summary>
    public sealed class LeaderboardScreen : MonoBehaviour
    {
        [Header("UI")]
        public Text titleText;
        public Button backButton;
        public Button refreshButton;
        public CanvasGroup screenGroup;

        [Header("Leaderboard")]
        public Transform entryContainer;
        public GameObject entryPrefab;
        public ScrollRect scrollRect;

        [Header("Filters")]
        public Dropdown seasonDropdown;
        public Dropdown timeframeDropdown;

        [Header("Your Position")]
        public Text yourPositionText;
        public Text yourRatingText;

        private List<GameObject> _spawnedEntries = new();
        private bool _isLoading;

        private void Start()
        {
            if (backButton != null) backButton.onClick.AddListener(HandleBack);
            if (refreshButton != null) refreshButton.onClick.AddListener(HandleRefresh);

            // TODO: Fetch leaderboards
            SetupMockData();
        }

        private void SetupMockData()
        {
            // Mock data para testing
            var mockEntries = new List<(int rank, string name, int rating, int wins, int losses)>
            {
                (1, "ProPlayer123", 2850, 145, 32),
                (2, "CardMaster", 2720, 132, 41),
                (3, "RankGrinder", 2610, 128, 45),
                (4, "StrategyKing", 2540, 121, 48),
                (5, "LuckyDraws", 2450, 115, 52),
            };

            foreach (var (rank, name, rating, wins, losses) in mockEntries)
            {
                var entry = Instantiate(entryPrefab, entryContainer);
                var texts = entry.GetComponentsInChildren<Text>();

                if (texts.Length >= 4)
                {
                    texts[0].text = rank.ToString();
                    texts[1].text = name;
                    texts[2].text = rating.ToString();
                    texts[3].text = $"{wins}W-{losses}L";
                }

                _spawnedEntries.Add(entry);
            }

            yourPositionText.text = "#247";
            yourRatingText.text = "1842";
        }

        private async void HandleRefresh()
        {
            if (_isLoading) return;

            _isLoading = true;
            refreshButton.interactable = false;

            Debug.Log("[Leaderboard] Refreshing...");
            await Task.Delay(1000); // Mock delay

            _isLoading = false;
            refreshButton.interactable = true;

            Debug.Log("[Leaderboard] Refreshed");
        }

        private void HandleBack()
        {
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            foreach (var entry in _spawnedEntries)
            {
                if (entry != null) Destroy(entry);
            }
            _spawnedEntries.Clear();

            if (backButton != null) backButton.onClick.RemoveListener(HandleBack);
            if (refreshButton != null) refreshButton.onClick.RemoveListener(HandleRefresh);
        }
    }
}
