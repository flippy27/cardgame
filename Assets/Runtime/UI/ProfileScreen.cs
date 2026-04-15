using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Pantalla de perfil de jugador.
    /// Muestra estadísticas, logros, y últimos matches.
    /// </summary>
    public sealed class ProfileScreen : MonoBehaviour
    {
        [Header("Profile Info")]
        public Text playerNameText;
        public Text playerIdText;
        public Image avatarImage;

        [Header("Stats")]
        public Text ratingText;
        public Text winRateText;
        public Text totalMatchesText;
        public Text highestRatingText;

        [Header("Matches")]
        public Transform matchesContainer;
        public GameObject matchEntryPrefab;
        public ScrollRect matchesScroll;

        [Header("Achievements")]
        public Transform achievementsContainer;
        public GameObject achievementPrefab;

        [Header("Buttons")]
        public Button editProfileButton;
        public Button addFriendButton;
        public Button blockButton;
        public Button backButton;

        private List<GameObject> _spawnedMatches = new();
        private List<GameObject> _spawnedAchievements = new();

        private void Start()
        {
            if (backButton != null) backButton.onClick.AddListener(HandleBack);
            if (editProfileButton != null) editProfileButton.onClick.AddListener(HandleEditProfile);
            if (addFriendButton != null) addFriendButton.onClick.AddListener(HandleAddFriend);
            if (blockButton != null) blockButton.onClick.AddListener(HandleBlock);

            SetupMockProfile();
        }

        private void SetupMockProfile()
        {
            // Mock data
            playerNameText.text = "CardMaster";
            playerIdText.text = "player_12345";
            ratingText.text = "2120";
            winRateText.text = "58.5%";
            totalMatchesText.text = "89";
            highestRatingText.text = "2410";

            // Mock matches
            var mockMatches = new List<(string opponent, string result, string rating, string date)>
            {
                ("ProPlayer", "WIN", "+25", "Hace 2h"),
                ("CardGuy", "LOSS", "-18", "Hace 4h"),
                ("RankGrinder", "WIN", "+32", "Ayer"),
            };

            foreach (var (opponent, result, rating, date) in mockMatches)
            {
                var entry = Instantiate(matchEntryPrefab, matchesContainer);
                var texts = entry.GetComponentsInChildren<Text>();

                if (texts.Length >= 4)
                {
                    texts[0].text = opponent;
                    texts[1].text = result;
                    texts[2].text = rating;
                    texts[3].text = date;
                }

                _spawnedMatches.Add(entry);
            }

            // Mock achievements
            var mockAchievements = new List<string>
            {
                "First Win",
                "10 Victories",
                "Ranked Master",
                "Speedrun",
            };

            foreach (var achievement in mockAchievements)
            {
                var badge = Instantiate(achievementPrefab, achievementsContainer);
                var text = badge.GetComponentInChildren<Text>();
                if (text != null) text.text = achievement;
                _spawnedAchievements.Add(badge);
            }
        }

        private void HandleEditProfile()
        {
            Debug.Log("[Profile] Edit profile (TODO)");
            // TODO: Abrir editor de perfil
        }

        private void HandleAddFriend()
        {
            Debug.Log("[Profile] Add friend (TODO)");
            // TODO: Enviar friend request
        }

        private void HandleBlock()
        {
            Debug.Log("[Profile] Block player (TODO)");
            // TODO: Bloquear jugador
        }

        private void HandleBack()
        {
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            foreach (var match in _spawnedMatches)
            {
                if (match != null) Destroy(match);
            }
            _spawnedMatches.Clear();

            foreach (var achievement in _spawnedAchievements)
            {
                if (achievement != null) Destroy(achievement);
            }
            _spawnedAchievements.Clear();

            if (backButton != null) backButton.onClick.RemoveListener(HandleBack);
            if (editProfileButton != null) editProfileButton.onClick.RemoveListener(HandleEditProfile);
            if (addFriendButton != null) addFriendButton.onClick.RemoveListener(HandleAddFriend);
            if (blockButton != null) blockButton.onClick.RemoveListener(HandleBlock);
        }
    }
}
