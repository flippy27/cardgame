using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Widget liviano para render de carta.
    /// </summary>
    public sealed class CardViewWidget : MonoBehaviour
    {
        public Image artImage;
        public Image frameImage;
        // public Text titleText;
        // public Text costText;
        // public Text attackText;
        // public Text healthText;
        // public Text armorText;

        public TMPro.TextMeshProUGUI titleText;
        public TMPro.TextMeshProUGUI costText;
        public TMPro.TextMeshProUGUI attackText;
        public TMPro.TextMeshProUGUI healthText;
        public TMPro.TextMeshProUGUI armorText;

        /// <summary>
        /// Pinta una carta de mano.
        /// </summary>
        public void Bind(CardInHandDto dto)
        {
            titleText.text = dto.displayName;
            costText.text = dto.manaCost.ToString();
            attackText.text = dto.attack.ToString();
            healthText.text = dto.health.ToString();
            armorText.text = string.Empty;
        }

        /// <summary>
        /// Pinta una carta en board.
        /// </summary>
        public void Bind(BoardCardDto dto)
        {
            titleText.text = dto.displayName.ToString();
            costText.text = string.Empty;
            attackText.text = dto.attack.ToString();
            healthText.text = dto.currentHealth.ToString();
            armorText.text = dto.armor > 0 ? dto.armor.ToString() : string.Empty;
        }
    }
}
