using UnityEngine;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Manager simple para sonidos de batalla.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private AudioSource _audioSource;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        public void PlayCardPlay()
        {
            PlaySound("Card_Play");
        }

        public void PlayCardAttack()
        {
            PlaySound("Card_Attack");
        }

        public void PlayCardDie()
        {
            PlaySound("Card_Die");
        }

        public void PlayTurnEnd()
        {
            PlaySound("Turn_End");
        }

        public void PlayVictory()
        {
            PlaySound("Victory");
        }

        public void PlayDefeat()
        {
            PlaySound("Defeat");
        }

        private void PlaySound(string soundName)
        {
            // Por ahora solo log - los sonidos se agregarían como Resources/Sounds/
            Debug.Log($"[Audio] Playing: {soundName}");
        }
    }
}
