using UnityEngine;
using UnityEngine.UI;

namespace Flippy.CardDuelMobile.UI
{
    public class HandArcLayout : LayoutGroup
    {
        public float arcRadius = 1500f;
        public float arcAngle = 15f;
        public float cardSpacing = 10f; // Gap between card edges (can be negative for overlap)
        public Vector2 cardSize = new Vector2(200, 280);
        [Range(0.1f, 1f)] public float flatness = 0.5f; // 0 = very curved, 1 = very flat
        [Range(0f, 1f)] public float overlapPercent = 0.3f; // How much cards overlap (0-1)

        public override void CalculateLayoutInputHorizontal()
        {
        }

        public override void CalculateLayoutInputVertical()
        {
        }

        public override void SetLayoutHorizontal()
        {
            LayoutCards();
        }

        public override void SetLayoutVertical()
        {
        }

        private void LayoutCards()
        {
            int childCount = rectTransform.childCount;
            if (childCount == 0) return;

            float overlapAmount = cardSize.x * overlapPercent;
            float cardStep = cardSize.x - overlapAmount + cardSpacing;
            float totalWidth = cardStep * childCount - (cardStep - cardSize.x);
            float startX = -totalWidth / 2f;

            float angleStep = childCount > 1 ? arcAngle / (childCount - 1) : 0;
            float startAngle = -arcAngle / 2f;

            for (int i = 0; i < childCount; i++)
            {
                var childRect = rectTransform.GetChild(i).GetComponent<RectTransform>();
                if (childRect == null) continue;

                float angle = startAngle + (angleStep * i);
                float x = startX + (i * cardStep) + cardSize.x / 2f;
                float y = -arcRadius * (1f - Mathf.Cos(angle * Mathf.Deg2Rad)) * flatness;

                childRect.sizeDelta = cardSize;
                childRect.anchorMin = new Vector2(0.5f, 0.5f);
                childRect.anchorMax = new Vector2(0.5f, 0.5f);
                childRect.pivot = new Vector2(0.5f, 0f);
                childRect.anchoredPosition = new Vector2(x, y);

                float rotation = -angle * flatness;
                childRect.eulerAngles = new Vector3(0, 0, rotation);
            }
        }
    }
}
