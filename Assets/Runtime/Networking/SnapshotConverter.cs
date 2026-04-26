using System;
using System.Collections.Generic;
using System.Linq;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Data;
using Flippy.CardDuelMobile.Networking.ApiClients;
using UnityEngine;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Converts MatchSnapshot (API format) to DuelSnapshotDto (Unity format).
    /// </summary>
    public static class SnapshotConverter
    {
        private const int ApiMatchPhaseInProgress = 2;
        private static readonly HashSet<string> MissingBackendFieldWarnings = new(StringComparer.Ordinal);

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
            var hasValidLocalSeat = resolvedLocalSeatIndex is 0 or 1;
            var winnerSeatIndex = apiSnapshot.winnerSeatIndex ?? -1;
            var winnerVisualIndex = ToVisualIndex(winnerSeatIndex, resolvedLocalSeatIndex);
            var activeVisualIndex = resolvedActiveSeatIndex >= 0
                ? ToVisualIndex(resolvedActiveSeatIndex, resolvedLocalSeatIndex)
                : ResolveActiveVisualIndexFromPlayerId(apiSnapshot.activePlayerId, currentPlayerId, isLocalPlayersTurn);
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
                logs = ConvertLogs(apiSnapshot.logs),
                battleEvents = ConvertBattleEvents(apiSnapshot.battleEvents, resolvedLocalSeatIndex)
            };
        }

        public static int ResolveLocalSeatIndex(MatchSnapshot apiSnapshot, int preferredSeatIndex)
        {
            if (apiSnapshot == null)
            {
                return NormalizeSeatIndex(preferredSeatIndex, -1);
            }

            if (apiSnapshot.localSeatIndex is 0 or 1)
            {
                var currentPlayerId = ResolveCurrentPlayerId();
                var snapshotPlayerId = ResolveSeatPlayerId(apiSnapshot, apiSnapshot.localSeatIndex);
                if (!string.IsNullOrWhiteSpace(currentPlayerId) &&
                    !string.IsNullOrWhiteSpace(snapshotPlayerId) &&
                    !string.Equals(currentPlayerId, snapshotPlayerId, StringComparison.Ordinal))
                {
                    Debug.LogWarning($"[SnapshotConverter] localSeatIndex={apiSnapshot.localSeatIndex} maps to player '{snapshotPlayerId}', expected '{currentPlayerId}'. Trusting snapshot seat.");
                }

                return apiSnapshot.localSeatIndex;
            }

            var currentMatchPlayerId = ResolveCurrentPlayerId();
            var seatFromPlayerId = FindSeatIndexByPlayerId(apiSnapshot.seats, currentMatchPlayerId);
            if (seatFromPlayerId is 0 or 1)
            {
                return seatFromPlayerId;
            }

            if (preferredSeatIndex is 0 or 1)
            {
                return preferredSeatIndex;
            }

            var inferredSeatIndex = InferLocalSeatIndexFromVisibleHand(apiSnapshot.seats);
            if (inferredSeatIndex is 0 or 1)
            {
                return inferredSeatIndex;
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
            var hasAuthoritativeTurnFields =
                apiSnapshot.localSeatIndex is 0 or 1 ||
                apiSnapshot.activeSeatIndex is 0 or 1 ||
                !string.IsNullOrWhiteSpace(apiSnapshot.activePlayerId);

            if (!string.IsNullOrWhiteSpace(currentPlayerId) &&
                !string.IsNullOrWhiteSpace(apiSnapshot.activePlayerId))
            {
                var resolvedFromPlayerId = string.Equals(apiSnapshot.activePlayerId, currentPlayerId, StringComparison.Ordinal);
                if (hasAuthoritativeTurnFields && apiSnapshot.isLocalPlayersTurn != resolvedFromPlayerId)
                {
                    Debug.LogWarning($"[SnapshotConverter] Turn payload mismatch: isLocalPlayersTurn={apiSnapshot.isLocalPlayersTurn}, activePlayerId={apiSnapshot.activePlayerId}, currentPlayerId={currentPlayerId}");
                }

                return resolvedFromPlayerId;
            }

            if (hasAuthoritativeTurnFields)
            {
                return apiSnapshot.isLocalPlayersTurn;
            }

            return localSeatIndex >= 0 && apiSnapshot.activeSeatIndex == localSeatIndex;
        }

        public static string ResolveSeatPlayerId(MatchSnapshot apiSnapshot, int seatIndex)
        {
            if (apiSnapshot?.seats == null || seatIndex < 0 || seatIndex >= apiSnapshot.seats.Length)
            {
                return null;
            }

            return apiSnapshot.seats[seatIndex]?.playerId;
        }

        public static string ResolveLocalPlayerId(MatchSnapshot apiSnapshot, int preferredSeatIndex)
        {
            var resolvedSeatIndex = ResolveLocalSeatIndex(apiSnapshot, preferredSeatIndex);
            return ResolveSeatPlayerId(apiSnapshot, resolvedSeatIndex);
        }

        public static string ResolveRemotePlayerId(MatchSnapshot apiSnapshot, int preferredSeatIndex)
        {
            var resolvedSeatIndex = ResolveLocalSeatIndex(apiSnapshot, preferredSeatIndex);
            if (resolvedSeatIndex is not (0 or 1))
            {
                return null;
            }

            return ResolveSeatPlayerId(apiSnapshot, 1 - resolvedSeatIndex);
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
                if (seat == null)
                {
                    continue;
                }

                var visualIndex = ToVisualIndex(seatIndex, localSeatIndex);
                if (visualIndex < 0 || visualIndex >= orderedPlayers.Length)
                {
                    continue;
                }

                orderedPlayers[visualIndex] = new PlayerSnapshotDto
                {
                    playerId = seat.playerId ?? string.Empty,
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

            return hand
                .Where(card => card != null)
                .Select(card => new CardInHandDto
            {
                runtimeCardKey = card.runtimeHandKey,
                cardId = card.cardId,
                displayName = card.displayName,
                manaCost = card.manaCost,
                attack = card.attack,
                health = card.health,
                armor = card.armor,
                isUnit = true, // Assume units for now
                unitType = ResolveCardUnitType(card.cardId, card.unitType),
                attackDeliveryType = ResolveCardDeliveryType(card.cardId, card.attackDeliveryType, card.unitType),
                abilities = ConvertAbilities(card.abilities, card.cardId)
            }).ToArray();
        }

        private static BoardSlotSnapshotDto[] ConvertBoard(BoardSlotSnapshot[] board, int localSeatIndex)
        {
            if (board == null)
            {
                return new BoardSlotSnapshotDto[0];
            }

            return board
                .Where(slot => slot != null)
                .Select(slot => new BoardSlotSnapshotDto
            {
                slot = (BoardSlot)slot.slot,
                occupied = slot.occupied,
                occupant = slot.occupant != null ? new BoardCardDto
                {
                    runtimeId = slot.occupant.runtimeId,
                    cardId = slot.occupant.cardId,
                    displayName = slot.occupant.displayName,
                    manaCost = ResolveBoardCardManaCost(slot.occupant.cardId),
                    attackMotionLevel = ResolveBoardCardMotionLevel(slot.occupant),
                    attackShakeLevel = ResolveBoardCardShakeLevel(slot.occupant),
                    attackDeliveryType = ResolveBoardCardDeliveryType(slot.occupant),
                    ownerIndex = ToVisualIndex(slot.occupant.ownerSeatIndex, localSeatIndex),
                    attack = slot.occupant.attack,
                    currentHealth = slot.occupant.currentHealth,
                    maxHealth = slot.occupant.maxHealth,
                    armor = slot.occupant.armor,
                    slot = (BoardSlot)slot.occupant.slot,
                    canAttack = false, // API doesn't track this
                    unitType = ResolveCardUnitType(slot.occupant.cardId, slot.occupant.unitType),
                    turnsUntilCanAttack = 0,
                    statusEffects = ConvertStatusEffects(slot.occupant.statusEffects),
                    abilities = ConvertAbilities(slot.occupant.abilities, slot.occupant.cardId)
                } : null
            }).ToArray();
        }

        private static BattleEventDto[] ConvertBattleEvents(BattleEventSnapshot[] events, int localSeatIndex)
        {
            if (events == null || events.Length == 0)
            {
                return new BattleEventDto[0];
            }

            return events
                .Where(battleEvent => battleEvent != null)
                .Select(battleEvent => new BattleEventDto
                {
                    eventId = battleEvent.eventId ?? string.Empty,
                    sequence = battleEvent.sequence,
                    kind = battleEvent.kind ?? string.Empty,
                    serverSourceSeatIndex = battleEvent.sourceSeatIndex,
                    serverTargetSeatIndex = battleEvent.targetSeatIndex,
                    sourceSeatIndex = HasMeaningfulSourceSeat(battleEvent) ? ToVisualIndex(battleEvent.sourceSeatIndex, localSeatIndex) : -1,
                    targetSeatIndex = HasMeaningfulTargetSeat(battleEvent) ? ToVisualIndex(battleEvent.targetSeatIndex, localSeatIndex) : -1,
                    sourceRuntimeId = battleEvent.sourceRuntimeId ?? string.Empty,
                    targetRuntimeId = battleEvent.targetRuntimeId ?? string.Empty,
                    abilityId = battleEvent.abilityId ?? string.Empty,
                    effectKind = battleEvent.effectKind,
                    amount = battleEvent.amount,
                    secondaryAmount = battleEvent.secondaryAmount,
                    hpBefore = battleEvent.hpBefore,
                    hpAfter = battleEvent.hpAfter,
                    armorBefore = battleEvent.armorBefore,
                    armorAfter = battleEvent.armorAfter,
                    statusKind = battleEvent.statusKind,
                    durationTurns = battleEvent.durationTurns,
                    message = battleEvent.message ?? string.Empty
                })
                .OrderBy(battleEvent => battleEvent.sequence)
                .ToArray();
        }

        private static bool HasMeaningfulSourceSeat(BattleEventSnapshot battleEvent)
        {
            if (battleEvent == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(battleEvent.sourceRuntimeId))
            {
                return true;
            }

            var kind = NormalizeEventKind(battleEvent.kind);
            return kind is "card_attack" or "card_damage" or "card_counterattack" or "card_destroyed" or "hero_damage" or "shield_block" or "skill_begin" or "heal" or "armor_gain" or "attack_buff" or "attack_position_blocked";
        }

        private static bool HasMeaningfulTargetSeat(BattleEventSnapshot battleEvent)
        {
            if (battleEvent == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(battleEvent.targetRuntimeId))
            {
                return true;
            }

            var kind = NormalizeEventKind(battleEvent.kind);
            return kind is "hero_damage";
        }

        private static string NormalizeEventKind(string kind)
        {
            return string.IsNullOrWhiteSpace(kind) ? string.Empty : kind.Trim().ToLowerInvariant();
        }

        private static StatusEffectDto[] ConvertStatusEffects(StatusEffectSnapshot[] effects)
        {
            if (effects == null || effects.Length == 0)
            {
                return new StatusEffectDto[0];
            }

            return effects
                .Where(effect => effect != null)
                .Select(effect => new StatusEffectDto
                {
                    kind = effect.kind,
                    amount = effect.amount,
                    remainingTurns = effect.remainingTurns,
                    sourceRuntimeId = effect.sourceRuntimeId ?? string.Empty,
                    abilityId = effect.abilityId ?? string.Empty,
                    iconAssetRef = effect.iconAssetRef ?? string.Empty
                })
                .ToArray();
        }

        private static CardAbilityDto[] ConvertAbilities(CardAbilitySnapshot[] abilities, string cardId)
        {
            if (abilities != null && abilities.Length > 0)
            {
                return abilities
                    .Where(ability => ability != null)
                    .Select(ConvertAbility)
                    .ToArray();
            }

            var catalogCard = ResolveCatalogCardDefinition(cardId);
            return catalogCard?.abilities ?? new CardAbilityDto[0];
        }

        private static CardAbilityDto ConvertAbility(CardAbilitySnapshot ability)
        {
            return new CardAbilityDto
            {
                abilityId = ability.abilityId ?? string.Empty,
                displayName = ability.displayName ?? string.Empty,
                iconAssetRef = ability.iconAssetRef ?? string.Empty,
                skillType = ability.skillType,
                triggerKind = ability.triggerKind,
                targetSelectorKind = ability.targetSelectorKind,
                animationCueId = ability.animationCueId ?? string.Empty,
                conditionsJson = ability.conditionsJson ?? string.Empty,
                metadataJson = ability.metadataJson ?? string.Empty,
                effects = ConvertEffects(ability.effects)
            };
        }

        private static CardEffectDto[] ConvertEffects(CardEffectSnapshot[] effects)
        {
            if (effects == null || effects.Length == 0)
            {
                return new CardEffectDto[0];
            }

            return effects
                .Where(effect => effect != null)
                .Select(effect => new CardEffectDto
                {
                    effectKind = effect.effectKind,
                    amount = effect.amount,
                    secondaryAmount = effect.secondaryAmount,
                    durationTurns = effect.durationTurns,
                    targetSelectorKindOverride = effect.targetSelectorKindOverride,
                    sequence = effect.sequence,
                    metadataJson = effect.metadataJson ?? string.Empty
                })
                .ToArray();
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

        private static int FindSeatIndexByPlayerId(SeatSnapshot[] seats, string playerId)
        {
            if (seats == null || string.IsNullOrWhiteSpace(playerId))
            {
                return -1;
            }

            for (var index = 0; index < seats.Length; index++)
            {
                var seatPlayerId = seats[index]?.playerId;
                if (!string.IsNullOrWhiteSpace(seatPlayerId) &&
                    string.Equals(seatPlayerId, playerId, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
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

        private static int ResolveBoardCardManaCost(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return 0;
            }

            var catalogCard = ResolveCatalogCardDefinition(cardId);
            if (catalogCard != null)
            {
                return catalogCard.manaCost;
            }

            return CardRegistry.GetCard(cardId)?.manaCost ?? 0;
        }

        private static int ResolveBoardCardMotionLevel(BoardCardSnapshot snapshotCard)
        {
            if (snapshotCard == null)
            {
                return 1;
            }

            if (snapshotCard.attackMotionLevel > 0)
            {
                return ClampPresentationLevel(snapshotCard.attackMotionLevel);
            }

            var catalogCard = ResolveCatalogCardDefinition(snapshotCard.cardId);
            if (catalogCard?.battlePresentation != null && catalogCard.battlePresentation.attackMotionLevel > 0)
            {
                return ClampPresentationLevel(catalogCard.battlePresentation.attackMotionLevel);
            }

            var definition = string.IsNullOrWhiteSpace(snapshotCard.cardId) ? null : CardRegistry.GetCard(snapshotCard.cardId);
            return AttackPresentationResolver.ResolveMotionLevel(definition, snapshotCard.attack);
        }

        private static int ResolveBoardCardShakeLevel(BoardCardSnapshot snapshotCard)
        {
            if (snapshotCard == null)
            {
                return 1;
            }

            if (snapshotCard.attackShakeLevel > 0)
            {
                return ClampPresentationLevel(snapshotCard.attackShakeLevel);
            }

            var catalogCard = ResolveCatalogCardDefinition(snapshotCard.cardId);
            if (catalogCard?.battlePresentation != null && catalogCard.battlePresentation.attackShakeLevel > 0)
            {
                return ClampPresentationLevel(catalogCard.battlePresentation.attackShakeLevel);
            }

            var definition = string.IsNullOrWhiteSpace(snapshotCard.cardId) ? null : CardRegistry.GetCard(snapshotCard.cardId);
            return AttackPresentationResolver.ResolveShakeLevel(definition, snapshotCard.attack);
        }

        private static string ResolveBoardCardDeliveryType(BoardCardSnapshot snapshotCard)
        {
            if (snapshotCard == null)
            {
                return AttackPresentationResolver.DeliveryTypeMelee;
            }

            if (!string.IsNullOrWhiteSpace(snapshotCard.attackDeliveryType))
            {
                return AttackPresentationResolver.NormalizeDeliveryType(snapshotCard.attackDeliveryType);
            }

            var catalogCard = ResolveCatalogCardDefinition(snapshotCard.cardId);
            if (!string.IsNullOrWhiteSpace(catalogCard?.battlePresentation?.attackDeliveryType))
            {
                return AttackPresentationResolver.NormalizeDeliveryType(catalogCard.battlePresentation.attackDeliveryType);
            }

            return ResolveCardDeliveryType(snapshotCard.cardId, snapshotCard.attackDeliveryType, snapshotCard.unitType);
        }

        private static int ResolveCardUnitType(string cardId, int snapshotUnitType)
        {
            if (snapshotUnitType >= 0)
            {
                return snapshotUnitType;
            }

            var catalogCard = ResolveCatalogCardDefinition(cardId);
            if (catalogCard != null && catalogCard.unitType >= 0)
            {
                return catalogCard.unitType;
            }

            WarnMissingBackendField(
                cardId,
                "unitType",
                "Unity will fall back to local CardRegistry/melee for presentation. Backend should send unitType in snapshots or card catalog.");

            var definition = string.IsNullOrWhiteSpace(cardId) ? null : CardRegistry.GetCard(cardId);
            return definition != null ? (int)definition.unitType : 0;
        }

        private static string ResolveCardDeliveryType(string cardId, string snapshotDeliveryType, int snapshotUnitType)
        {
            if (!string.IsNullOrWhiteSpace(snapshotDeliveryType))
            {
                return AttackPresentationResolver.NormalizeDeliveryType(snapshotDeliveryType);
            }

            var catalogCard = ResolveCatalogCardDefinition(cardId);
            if (!string.IsNullOrWhiteSpace(catalogCard?.attackDeliveryType))
            {
                return AttackPresentationResolver.NormalizeDeliveryType(catalogCard.attackDeliveryType);
            }

            if (!string.IsNullOrWhiteSpace(catalogCard?.battlePresentation?.attackDeliveryType))
            {
                return AttackPresentationResolver.NormalizeDeliveryType(catalogCard.battlePresentation.attackDeliveryType);
            }

            var definition = string.IsNullOrWhiteSpace(cardId) ? null : CardRegistry.GetCard(cardId);
            if (definition != null && !string.IsNullOrWhiteSpace(definition.attackDeliveryType))
            {
                return AttackPresentationResolver.NormalizeDeliveryType(definition.attackDeliveryType);
            }

            var unitType = ResolveCardUnitType(cardId, snapshotUnitType);
            return unitType switch
            {
                1 => AttackPresentationResolver.DeliveryTypeProjectile,
                2 => AttackPresentationResolver.DeliveryTypeBeam,
                _ => AttackPresentationResolver.DeliveryTypeMelee
            };
        }

        private static int ResolveActiveVisualIndexFromPlayerId(string activePlayerId, string currentPlayerId, bool isLocalPlayersTurn)
        {
            if (!string.IsNullOrWhiteSpace(activePlayerId) &&
                !string.IsNullOrWhiteSpace(currentPlayerId))
            {
                return string.Equals(activePlayerId, currentPlayerId, StringComparison.Ordinal) ? 0 : 1;
            }

            return isLocalPlayersTurn ? 0 : 1;
        }

        private static ServerCardDefinition ResolveCatalogCardDefinition(string cardId)
        {
            if (string.IsNullOrWhiteSpace(cardId) || GameService.Instance?.CardCatalog == null)
            {
                return null;
            }

            GameService.Instance.CardCatalog.TryGetCard(cardId, out var card);
            return card;
        }

        private static void WarnMissingBackendField(string cardId, string fieldName, string detail)
        {
            var key = $"{fieldName}:{cardId}";
            if (!MissingBackendFieldWarnings.Add(key))
            {
                return;
            }

            Debug.LogWarning($"[SnapshotConverter] Backend card data missing '{fieldName}' for card '{cardId}'. {detail}");
        }

        private static int ClampPresentationLevel(int level)
        {
            if (level < 1)
            {
                return 1;
            }

            if (level > 5)
            {
                return 5;
            }

            return level;
        }
    }
}
