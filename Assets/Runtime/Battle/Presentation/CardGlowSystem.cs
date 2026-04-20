using UnityEngine;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Sistema de glow para cartas que pueden atacar.
    /// Glow dorado = carta activa en turno local.
    /// </summary>
    public static class CardGlowSystem
    {
        public static void UpdateGlow(Card3DView cardView, DuelSnapshotDto snapshot, Board3DManager boardManager)
        {
            if (cardView == null || snapshot == null)
                return;

            bool isLocalTurn = snapshot.activePlayerIndex == snapshot.localPlayerIndex;
            bool isLocalCard = cardView.PlayerIndex == snapshot.localPlayerIndex;

            if (isLocalTurn && isLocalCard)
            {
                // Card es del jugador local en su turno - glow dorado
                var color = new Color(1f, 0.84f, 0f, 1f); // Gold
                cardView.SetColor(color);
            }
            else
            {
                // Color normal
                cardView.SetColor(new Color(0.1f, 0.1f, 0.15f, 0.9f));
            }
        }

        public static void ClearAllGlow(Board3DManager boardManager)
        {
            // Buscar todas las cartas en el board y resetearlas
            // (implementado en UpdateBoard al recibir snapshot)
        }
    }
}
