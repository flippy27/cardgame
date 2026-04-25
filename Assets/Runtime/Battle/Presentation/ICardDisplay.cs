using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.UI
{
    public interface ICardDisplay
    {
        BoardCardDto CardData { get; }
        int PlayerIndex { get; }
        void UpdateStatsDisplay();
        void SetColor(Color color);
        void ResetColor();
        void AnimateDrop(Vector3 targetPos, float duration = 0.3f);
        void AnimateAttack(Vector3 targetPos, float returnDuration = 0.4f);
        void AnimateDeath(float duration = 0.5f);
    }

    public static class CardDisplayExtensions
    {
        public static GameObject GetGameObject(this ICardDisplay card)
        {
            if (card is not MonoBehaviour behaviour || behaviour == null)
            {
                return null;
            }

            return behaviour.gameObject;
        }

        public static Transform GetTransform(this ICardDisplay card)
        {
            if (card is not MonoBehaviour behaviour || behaviour == null)
            {
                return null;
            }

            return behaviour.transform;
        }

        public static bool TryGetTransform(this ICardDisplay card, out Transform transform)
        {
            transform = card.GetTransform();
            return transform != null;
        }
    }
}
