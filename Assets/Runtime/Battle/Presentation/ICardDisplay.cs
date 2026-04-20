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
        void AnimateDrop(Vector3 targetPos, float duration = 0.3f);
        void AnimateAttack(Vector3 targetPos, float returnDuration = 0.4f);
        void AnimateDeath(float duration = 0.5f);
    }

    public static class CardDisplayExtensions
    {
        public static GameObject GetGameObject(this ICardDisplay card)
        {
            return (card as MonoBehaviour)?.gameObject;
        }

        public static Transform GetTransform(this ICardDisplay card)
        {
            return (card as MonoBehaviour)?.transform;
        }
    }
}
