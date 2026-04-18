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
        public TMPro.TextMeshProUGUI unitTypeText;

        // Attack status indicators
        public Image cooldownIcon;
        public Image canAttackIcon;

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

            if (unitTypeText != null && dto.isUnit)
            {
                var type = (Data.UnitType)dto.unitType;
                unitTypeText.text = type == Data.UnitType.Melee ? "MELEE" : "RANGED";
            }
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

            // Show unit type
            if (unitTypeText != null)
            {
                var type = (Data.UnitType)dto.unitType;
                unitTypeText.text = type == Data.UnitType.Melee ? "MELEE" : "RANGED";
            }

            // Visual feedback for cards that cannot attack
            if (frameImage != null)
            {
                frameImage.color = dto.canAttack
                    ? Color.white
                    : new Color(0.6f, 0.6f, 0.6f, 1f);
            }

            if (artImage != null)
            {
                artImage.color = dto.canAttack
                    ? Color.white
                    : new Color(0.7f, 0.7f, 0.7f, 1f);
            }
        }

        /// <summary>
        /// Actualiza iconos de estado de ataque.
        /// </summary>
        public void UpdateAttackIndicators(bool hasCooldown, bool canAttack)
        {
            if (cooldownIcon != null)
                cooldownIcon.gameObject.SetActive(hasCooldown);

            if (canAttackIcon != null)
                canAttackIcon.gameObject.SetActive(canAttack);
        }

        /// <summary>
        /// Oculta todos los iconos de ataque.
        /// </summary>
        public void HideAttackIndicators()
        {
            UpdateAttackIndicators(false, false);
        }
    }
}
