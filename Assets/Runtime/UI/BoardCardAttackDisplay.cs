using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Componente para mostrar estado de ataque en cards del board.
    /// Wrapper simple para CardAttackIndicator.
    /// </summary>
    public class BoardCardAttackDisplay : MonoBehaviour
    {
        [SerializeField] private CardAttackIndicator attackIndicator;

        private CardRuntime _cardRuntime;

        public void SetCard(CardRuntime cardRuntime)
        {
            _cardRuntime = cardRuntime;

            if (attackIndicator == null)
                attackIndicator = GetComponent<CardAttackIndicator>();

            if (attackIndicator != null)
            {
                attackIndicator.SetCard(_cardRuntime);
            }
        }

        /// <summary>Actualiza indicadores cuando estado cambia (ej: fin de turno).</summary>
        public void RefreshState()
        {
            if (attackIndicator != null)
            {
                attackIndicator.UpdateIndicators();
            }
        }

        public void Hide()
        {
            if (attackIndicator != null)
            {
                attackIndicator.Hide();
            }
        }
    }
}
