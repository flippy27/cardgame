using System.Collections.Generic;
using System.Linq;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Converts MatchSnapshot (API format) to DuelSnapshotDto (Unity format).
    /// </summary>
    public static class SnapshotConverter
    {
        public static DuelSnapshotDto Convert(MatchSnapshot apiSnapshot, int localSeatIndex)
        {
            if (apiSnapshot == null)
            {
                return null;
            }

            var winnerSeatIndex = apiSnapshot.winnerSeatIndex ?? -1;
            DuelEndReason endReason = DuelEndReason.None;
            if (apiSnapshot.duelEnded && winnerSeatIndex >= 0)
            {
                endReason = winnerSeatIndex == localSeatIndex ? DuelEndReason.EnemyHeroDefeated : DuelEndReason.LocalHeroDefeated;
            }

            return new DuelSnapshotDto
            {
                snapshotVersion = 1,
                matchId = apiSnapshot.matchId,
                localPlayerIndex = localSeatIndex,
                activePlayerIndex = apiSnapshot.activeSeatIndex,
                turnNumber = apiSnapshot.turnNumber,
                duelEnded = apiSnapshot.duelEnded,
                endReason = endReason,
                matchPhase = (MatchPhase)apiSnapshot.phase,
                localPlayerReady = apiSnapshot.seats?[localSeatIndex]?.ready ?? false,
                remotePlayerReady = apiSnapshot.seats?[1 - localSeatIndex]?.ready ?? false,
                localPlayerConnected = apiSnapshot.seats?[localSeatIndex]?.connected ?? false,
                remotePlayerConnected = apiSnapshot.seats?[1 - localSeatIndex]?.connected ?? false,
                connectedPlayers = apiSnapshot.connectedPlayers,
                winnerPlayerIndex = winnerSeatIndex,
                matchSeed = apiSnapshot.matchSeed,
                reconnectGraceRemainingSeconds = (float)apiSnapshot.reconnectGraceRemainingSeconds,
                statusMessage = apiSnapshot.statusMessage,
                players = ConvertPlayers(apiSnapshot.seats, localSeatIndex),
                logs = ConvertLogs(apiSnapshot.logs)
            };
        }

        private static PlayerSnapshotDto[] ConvertPlayers(SeatSnapshot[] seats, int localSeatIndex)
        {
            if (seats == null || seats.Length == 0)
            {
                return new PlayerSnapshotDto[0];
            }

            return seats.Select((seat, index) => new PlayerSnapshotDto
            {
                playerId = "", // API doesn't expose player ID in snapshot (privacy)
                playerIndex = index,
                heroHealth = seat.heroHealth,
                mana = seat.mana,
                maxMana = seat.maxMana,
                hand = ConvertHand(seat.hand, index == localSeatIndex),
                board = ConvertBoard(seat.board),
                remainingDeckCount = seat.remainingDeckCount,
                deadCardPileCount = 0
            }).ToArray();
        }

        private static CardInHandDto[] ConvertHand(HandCardSnapshot[] hand, bool isLocalPlayer)
        {
            if (!isLocalPlayer)
            {
                // Hide opponent's hand
                return new CardInHandDto[0];
            }

            if (hand == null)
            {
                return new CardInHandDto[0];
            }

            return hand.Select(card => new CardInHandDto
            {
                runtimeCardKey = card.runtimeHandKey,
                cardId = card.cardId,
                displayName = card.displayName,
                manaCost = card.manaCost,
                attack = card.attack,
                health = card.health,
                isUnit = true, // Assume units for now
                unitType = 0 // Default to Melee
            }).ToArray();
        }

        private static BoardSlotSnapshotDto[] ConvertBoard(BoardSlotSnapshot[] board)
        {
            if (board == null)
            {
                return new BoardSlotSnapshotDto[0];
            }

            return board.Select(slot => new BoardSlotSnapshotDto
            {
                slot = (BoardSlot)slot.slot,
                occupied = slot.occupied,
                occupant = slot.occupant != null ? new BoardCardDto
                {
                    runtimeId = slot.occupant.runtimeId,
                    cardId = slot.occupant.cardId,
                    displayName = slot.occupant.displayName,
                    ownerIndex = slot.occupant.ownerSeatIndex,
                    attack = slot.occupant.attack,
                    currentHealth = slot.occupant.currentHealth,
                    maxHealth = slot.occupant.maxHealth,
                    armor = slot.occupant.armor,
                    slot = (BoardSlot)slot.occupant.slot,
                    canAttack = false, // API doesn't track this
                    unitType = 0, // API doesn't expose unit type
                    turnsUntilCanAttack = 0
                } : null
            }).ToArray();
        }

        private static List<BattleLogEntry> ConvertLogs(string[] logs)
        {
            if (logs == null)
            {
                return new List<BattleLogEntry>();
            }

            return logs.Select(log => new BattleLogEntry
            {
                type = BattleLogType.Info,
                message = log,
                timestamp = System.DateTime.UtcNow
            }).ToList();
        }
    }
}
