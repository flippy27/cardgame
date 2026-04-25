using Flippy.CardDuelMobile.Core;
using System;

namespace Flippy.CardDuelMobile.Battle
{
    public static class SnapshotTurnAuthority
    {
        public static bool IsMatchPlayable(DuelSnapshotDto snapshot)
        {
            return snapshot != null &&
                   snapshot.matchPhase == MatchPhase.InProgress &&
                   !snapshot.duelEnded;
        }

        public static bool IsLocalTurn(DuelSnapshotDto snapshot)
        {
            if (!IsMatchPlayable(snapshot))
            {
                return false;
            }

            var gameState = GamePlayStateManager.Instance;
            var currentPlayerId = gameState != null
                ? gameState.GetMatchInfo().playerId
                : null;
            if (!string.IsNullOrWhiteSpace(snapshot.activePlayerId) &&
                !string.IsNullOrWhiteSpace(currentPlayerId))
            {
                return string.Equals(snapshot.activePlayerId, currentPlayerId, StringComparison.Ordinal);
            }

            return snapshot.isLocalPlayersTurn;
        }
    }
}
