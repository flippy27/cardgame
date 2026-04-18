using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Data;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Muestra iconos de estado de ataque en cartas en el board.
    /// - Cooldown: indica si aún falta turnos para poder atacar
    /// - CanAttack: indica si realmente puede atacar según posición/tipo
    /// </summary>
    public class CardAttackIndicator : MonoBehaviour
    {
        [SerializeField] private Image cooldownIcon;
        [SerializeField] private Image canAttackIcon;

        private CardRuntime _cardRuntime;

        /// <summary>Setea la carta en juego y actualiza indicadores.</summary>
        public void SetCard(CardRuntime cardRuntime)
        {
            _cardRuntime = cardRuntime;
            UpdateIndicators();
        }

        /// <summary>Actualiza visibilidad de iconos basado en estado actual.</summary>
        public void UpdateIndicators()
        {
            if (_cardRuntime == null || _cardRuntime.Definition == null)
            {
                HideAll();
                return;
            }

            // Cooldown icon: muestra si aún falta para poder atacar
            if (cooldownIcon != null)
            {
                bool hasAttackCooldown = _cardRuntime.TurnsUntilCanAttack > 0;
                cooldownIcon.gameObject.SetActive(hasAttackCooldown);
            }

            // CanAttack icon: muestra si puede atacar según posición y tipo
            if (canAttackIcon != null)
            {
                bool canAttack = CanAttackFromPosition(_cardRuntime);
                canAttackIcon.gameObject.SetActive(canAttack);
            }
        }

        /// <summary>Determina si la carta puede atacar desde su posición actual.</summary>
        private bool CanAttackFromPosition(CardRuntime card)
        {
            if (card.Definition.cardType != CardType.Unit)
                return false;

            // Debe haber esperado lo suficiente
            if (card.TurnsUntilCanAttack > 0)
                return false;

            var unitType = card.Definition.unitType;

            // Melee: solo ataca desde Front
            if (card.CurrentSlot == BoardSlot.Front)
                return unitType == UnitType.Melee;

            // Ranged/Magic: ataca desde BackLeft o BackRight
            if (card.CurrentSlot == BoardSlot.BackLeft || card.CurrentSlot == BoardSlot.BackRight)
                return unitType == UnitType.Ranged || unitType == UnitType.Magic;

            return false;
        }

        private void HideAll()
        {
            if (cooldownIcon != null)
                cooldownIcon.gameObject.SetActive(false);
            if (canAttackIcon != null)
                canAttackIcon.gameObject.SetActive(false);
        }

        public void Hide()
        {
            HideAll();
        }
    }
}
