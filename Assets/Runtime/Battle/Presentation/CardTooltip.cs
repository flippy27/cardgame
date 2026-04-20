using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Tooltip que muestra info detallada de carta al hover.
    /// </summary>
    public class CardTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Card3DView _cardView;
        private GameObject _tooltipGo;
        private TextMeshProUGUI _tooltipText;

        private void Start()
        {
            _cardView = GetComponent<Card3DView>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_cardView == null || _cardView.CardData == null)
                return;

            ShowTooltip();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HideTooltip();
        }

        private void ShowTooltip()
        {
            if (_tooltipGo != null)
                return;

            var card = _cardView.CardData;

            // Crear tooltip
            _tooltipGo = new GameObject("Tooltip");
            _tooltipGo.transform.SetParent(transform);
            _tooltipGo.transform.localPosition = Vector3.forward * -0.5f;

            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(_tooltipGo.transform);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(600, 400);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(canvasGo.transform);

            _tooltipText = textGo.AddComponent<TextMeshProUGUI>();
            _tooltipText.text = FormatTooltip(card);
            _tooltipText.fontSize = 40;
            _tooltipText.color = Color.white;
            _tooltipText.alignment = TextAlignmentOptions.TopLeft;

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(600, 400);
            textRect.localPosition = Vector3.zero;
        }

        private void HideTooltip()
        {
            if (_tooltipGo != null)
            {
                Destroy(_tooltipGo);
                _tooltipGo = null;
            }
        }

        private string FormatTooltip(BoardCardDto card)
        {
            return $"<b>{card.displayName}</b>\n\n" +
                   $"ATK: {card.attack}\n" +
                   $"HP: {card.currentHealth}/{card.maxHealth}\n" +
                   $"Armor: {card.armor}\n\n" +
                   $"Type: {GetCardType(card)}\n";
        }

        private string GetCardType(BoardCardDto card)
        {
            // Simplificado - en realidad vendría del CardInHandDto
            return "Unit";
        }
    }
}
