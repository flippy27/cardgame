using System;
using System.Collections.Generic;
using System.Linq;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Converts MatchSnapshot (API format) to DuelSnapshotDto (Unity format).
    /// </summary>
    public static class SnapshotConverter
    {
        private const int ApiMatchPhaseInProgress = 2;

        public static DuelSnapshotDto Convert(MatchSnapshot apiSnapshot, int localSeatIndex)
        {
            if (apiSnapshot == null)
            {
                return null;
            }

            var resolvedLocalSeatIndex = ResolveLocalSeatIndex(apiSnapshot, localSeatIndex);
            var resolvedActiveSeatIndex = apiSnapshot.activeSeatIndex is 0 or 1
                ? apiSnapshot.activeSeatIndex
                : -1;
            var isLocalPlayersTurn = ResolveLocalPlayersTurn(apiSnapshot, resolvedLocalSeatIndex);
            var currentPlayerId = ResolveCurrentPlayerId();
            var canResolveTurnFromPlayerId = !string.IsNullOrWhiteSpace(currentPlayerId) &&
                                             !string.IsNullOrWhiteSpace(apiSnapshot.activePlayerId);
            var hasValidLocalSeat = resolvedLocalSeatIndex is 0 or 1;
            var winnerSeatIndex = apiSnapshot.winnerSeatIndex ?? -1;
            var winnerVisualIndex = ToVisualIndex(winnerSeatIndex, resolvedLocalSeatIndex);
            var activeVisualIndex = canResolveTurnFromPlayerId
                ? isLocalPlayersTurn ? 0 : 1
                : resolvedActiveSeatIndex >= 0
                ? ToVisualIndex(resolvedActiveSeatIndex, resolvedLocalSeatIndex)
                : isLocalPlayersTurn ? 0 : 1;
            var localSeatForRules = resolvedLocalSeatIndex is 0 or 1 ? resolvedLocalSeatIndex : 0;
            var remoteSeatForRules = localSeatForRules == 0 ? 1 : 0;
            var localHeroMaxHealth = ResolveSeatMaxHeroHealth(apiSnapshot.rules, localSeatForRules);
            var remoteHeroMaxHealth = ResolveSeatMaxHeroHealth(apiSnapshot.rules, remoteSeatForRules);
            DuelEndReason endReason = DuelEndReason.None;
            if (apiSnapshot.duelEnded && winnerVisualIndex >= 0)
            {
                endReason = winnerVisualIndex == 0
                    ? DuelEndReason.EnemyHeroDefeated
                    : DuelEndReason.LocalHeroDefeated;
            }

            return new DuelSnapshotDto
            {
                snapshotVersion = 1,
                matchId = apiSnapshot.matchId,
                rulesetId = !string.IsNullOrWhiteSpace(apiSnapshot.rulesetId)
                    ? apiSnapshot.rulesetId
                    : apiSnapshot.rules?.rulesetId,
                rulesetName = apiSnapshot.rules?.displayName ?? string.Empty,
                rules = apiSnapshot.rules,
                localPlayerIndex = 0,
                activePlayerIndex = activeVisualIndex,
                activePlayerId = apiSnapshot.activePlayerId,
                isLocalPlayersTurn = isLocalPlayersTurn,
                turnNumber = apiSnapshot.turnNumber,
                duelEnded = apiSnapshot.duelEnded,
                endReason = endReason,
                matchPhase = MapApiMatchPhase(apiSnapshot.phase),
                localPlayerReady = hasValidLocalSeat ? apiSnapshot.seats?[resolvedLocalSeatIndex]?.ready ?? false : false,
                remotePlayerReady = hasValidLocalSeat ? apiSnapshot.seats?[1 - resolvedLocalSeatIndex]?.ready ?? false : false,
                localPlayerConnected = hasValidLocalSeat ? apiSnapshot.seats?[resolvedLocalSeatIndex]?.connected ?? false : false,
                remotePlayerConnected = hasValidLocalSeat ? apiSnapshot.seats?[1 - resolvedLocalSeatIndex]?.connected ?? false : false,
                connectedPlayers = apiSnapshot.connectedPlayers,
                winnerPlayerIndex = winnerVisualIndex,
                matchSeed = apiSnapshot.matchSeed,
                localHeroMaxHealth = localHeroMaxHealth,
                remoteHeroMaxHealth = remoteHeroMaxHealth,
                reconnectGraceRemainingSeconds = (float)apiSnapshot.reconnectGraceRemainingSeconds,
                statusMessage = apiSnapshot.statusMessage,
                players = ConvertPlayers(apiSnapshot.seats, resolvedLocalSeatIndex),
                logs = ConvertLogs(apiSnapshot.logs)
            };
        }

        public static int ResolveLocalSeatIndex(MatchSnapshot apiSnapshot, int preferredSeatIndex)
        {
            if (apiSnapshot == null)
            {
                return NormalizeSeatIndex(preferredSeatIndex, -1);
            }

            var inferredSeatIndex = InferLocalSeatIndexFromVisibleHand(apiSnapshot.seats);
            if (inferredSeatIndex is 0 or 1)
            {
                return inferredSeatIndex;
            }

            if (apiSnapshot.localSeatIndex is 0 or 1)
            {
                return apiSnapshot.localSeatIndex;
            }

            return NormalizeSeatIndex(preferredSeatIndex, -1);
        }

        public static bool ResolveLocalPlayersTurn(MatchSnapshot apiSnapshot, int localSeatIndex)
        {
            if (apiSnapshot == null || apiSnapshot.phase != ApiMatchPhaseInProgress || apiSnapshot.duelEnded)
            {
                return false;
            }

            var currentPlayerId = ResolveCurrentPlayerId();
            if (!string.IsNullOrWhiteSpace(currentPlayerId) &&
                !string.IsNullOrWhiteSpace(apiSnapshot.activePlayerId))
            {
                return string.Equals(apiSnapshot.activePlayerId, currentPlayerId, StringComparison.Ordinal);
            }

            if (apiSnapshot.isLocalPlayersTurn)
            {
                return true;
            }

            return localSeatIndex >= 0 && apiSnapshot.activeSeatIndex == localSeatIndex;
        }

        private static PlayerSnapshotDto[] ConvertPlayers(SeatSnapshot[] seats, int localSeatIndex)
        {
            var orderedPlayers = new PlayerSnapshotDto[2]
            {
                CreateEmptyPlayerSnapshot(0),
                CreateEmptyPlayerSnapshot(1)
            };

            if (seats == null || seats.Length == 0)
            {
                return orderedPlayers;
            }

            for (var seatIndex = 0; seatIndex < seats.Length; seatIndex++)
            {
                var seat = seats[seatIndex];
                var visualIndex = ToVisualIndex(seatIndex, localSeatIndex);
                if (visualIndex < 0 || visualIndex >= orderedPlayers.Length)
                {
                    continue;
                }

                orderedPlayers[visualIndex] = new PlayerSnapshotDto
                {
                    playerId = "",
                    playerIndex = visualIndex,
                    heroHealth = seat.heroHealth,
                    mana = seat.mana,
                    maxMana = seat.maxMana,
                    hand = ConvertHand(seat.hand, seatIndex == localSeatIndex),
                    board = ConvertBoard(seat.board, localSeatIndex),
                    remainingDeckCount = seat.remainingDeckCount,
                    deadCardPileCount = 0
                };
            }

            return orderedPlayers;
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

        private static BoardSlotSnapshotDto[] ConvertBoard(BoardSlotSnapshot[] board, int localSeatIndex)
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
                    ownerIndex = ToVisualIndex(slot.occupant.ownerSeatIndex, localSeatIndex),
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

        private static int ToVisualIndex(int seatIndex, int localSeatIndex)
        {
            if (seatIndex < 0)
            {
                return -1;
            }

            if (localSeatIndex is not (0 or 1))
            {
                return seatIndex is 0 or 1 ? seatIndex : -1;
            }

            return seatIndex == localSeatIndex ? 0 : 1;
        }

        private static int NormalizeSeatIndex(int primarySeatIndex, int fallbackSeatIndex)
        {
            if (primarySeatIndex is 0 or 1)
            {
                return primarySeatIndex;
            }

            if (fallbackSeatIndex is 0 or 1)
            {
                return fallbackSeatIndex;
            }

            return -1;
        }

        private static int InferLocalSeatIndexFromVisibleHand(SeatSnapshot[] seats)
        {
            if (seats == null || seats.Length < 2)
            {
                return -1;
            }

            var inferredSeatIndex = -1;
            for (var index = 0; index < seats.Length; index++)
            {
                var handCount = seats[index]?.hand?.Length ?? 0;
                if (handCount <= 0)
                {
                    continue;
                }

                if (inferredSeatIndex >= 0)
                {
                    return -1;
                }

                inferredSeatIndex = index;
            }

            return inferredSeatIndex;
        }

        private static string ResolveCurrentPlayerId()
        {
            if (GamePlayStateManager.Instance == null)
            {
                return null;
            }

            var (_, playerId, _) = GamePlayStateManager.Instance.GetMatchInfo();
            return playerId;
        }

        private static PlayerSnapshotDto CreateEmptyPlayerSnapshot(int visualIndex)
        {
            return new PlayerSnapshotDto
            {
                playerId = string.Empty,
                playerIndex = visualIndex,
                hand = new CardInHandDto[0],
                board = new BoardSlotSnapshotDto[0]
            };
        }

        private static int ResolveSeatMaxHeroHealth(GameRulesDto rules, int seatIndex)
        {
            if (rules == null)
            {
                return 20;
            }

            try
            {
                var resolvedSeatRules = rules.ResolveSeatRules(seatIndex);
                if (resolvedSeatRules == null)
                {
                    return Math.Max(1, rules.maxHeroHealth);
                }

                return Math.Max(1, resolvedSeatRules.maxHeroHealth);
            }
            catch
            {
                return Math.Max(1, rules.maxHeroHealth);
            }
        }

        private static MatchPhase MapApiMatchPhase(int apiPhase)
        {
            return apiPhase switch
            {
                0 => MatchPhase.WaitingForPlayers,
                1 => MatchPhase.WaitingForReady,
                2 => MatchPhase.InProgress,
                3 => MatchPhase.Completed,
                4 => MatchPhase.Abandoned,
                _ => MatchPhase.WaitingForPlayers
            };
        }
    }
}
