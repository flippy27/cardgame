using UnityEngine;
using UnityEngine.UI;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Drop target for server-authoritative played-card deletion.
    /// Can live on a UI RectTransform or on a world object with a Collider.
    /// </summary>
    public sealed class BoardCardDestroyDropZone : MonoBehaviour
    {
        [SerializeField] private int acceptedPlayerIndex = 0;
        [SerializeField] private GameObject highlightRoot;
        [SerializeField] private Graphic[] highlightGraphics;
        [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color highlightedColor = new Color(1f, 0.15f, 0.75f, 0.8f);

        private RectTransform _rectTransform;
        private Collider _collider;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _collider = GetComponent<Collider>();
            SetHighlighted(false);
        }

        private void Reset()
        {
            _rectTransform = GetComponent<RectTransform>();
            _collider = GetComponent<Collider>();
            highlightGraphics = GetComponentsInChildren<Graphic>(true);
        }

        public bool CanAccept(Card3DPlayed card)
        {
            return card != null &&
                   card.CardData != null &&
                   card.PlayerIndex == acceptedPlayerIndex;
        }

        public bool ContainsScreenPosition(Vector2 screenPosition, Camera worldCamera)
        {
            if (_rectTransform != null)
            {
                return RectTransformUtility.RectangleContainsScreenPoint(_rectTransform, screenPosition, ResolveUiCamera(worldCamera));
            }

            if (_collider == null || worldCamera == null)
            {
                return false;
            }

            var ray = worldCamera.ScreenPointToRay(screenPosition);
            return _collider.Raycast(ray, out _, 100f);
        }

        public void SetHighlighted(bool highlighted)
        {
            if (highlightRoot != null)
            {
                highlightRoot.SetActive(highlighted);
            }

            if (highlightGraphics == null)
            {
                return;
            }

            var color = highlighted ? highlightedColor : normalColor;
            foreach (var graphic in highlightGraphics)
            {
                if (graphic != null)
                {
                    graphic.color = color;
                }
            }
        }

        private Camera ResolveUiCamera(Camera worldCamera)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return canvas != null && canvas.worldCamera != null ? canvas.worldCamera : worldCamera;
        }
    }
}
