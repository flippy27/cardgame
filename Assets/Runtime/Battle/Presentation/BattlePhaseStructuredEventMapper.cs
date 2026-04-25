using System;
using System.Collections.Generic;
using System.Linq;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.UI
{
    internal static class BattlePhaseStructuredEventMapper
    {
        public static List<BattlePresentationEvent> Build(DuelSnapshotDto snapshot, IEnumerable<BattleEventDto> battleEvents)
        {
            var results = new List<BattlePresentationEvent>();
            if (battleEvents == null)
            {
                return results;
            }

            foreach (var battleEvent in battleEvents.OrderBy(item => item?.sequence ?? int.MaxValue))
            {
                if (battleEvent == null)
                {
                    continue;
                }

                results.Add(Map(snapshot, battleEvent));
            }

            return results;
        }

        private static BattlePresentationEvent Map(DuelSnapshotDto snapshot, BattleEventDto battleEvent)
        {
            var kind = NormalizeKind(battleEvent.kind);
            var presentationKind = kind switch
            {
                "skill_begin" => BattlePresentationEventKind.SkillBegin,
                "card_damage" => BattlePresentationEventKind.CardAttack,
                "hero_damage" => BattlePresentationEventKind.HeroAttack,
                "shield_block" => BattlePresentationEventKind.ShieldBlock,
                "status_applied" => BattlePresentationEventKind.StatusApplied,
                "status_expired" => BattlePresentationEventKind.StatusExpired,
                "stun_skip" => BattlePresentationEventKind.Skip,
                "enrage_cooldown_skip" => BattlePresentationEventKind.Skip,
                "attack_not_ready" => BattlePresentationEventKind.Skip,
                "heal" => BattlePresentationEventKind.Heal,
                "armor_gain" => BattlePresentationEventKind.ArmorGain,
                "attack_buff" => BattlePresentationEventKind.AttackBuff,
                "death" => BattlePresentationEventKind.Death,
                "card_attack" when battleEvent.amount > 0 => BattlePresentationEventKind.CardAttack,
                _ => BattlePresentationEventKind.Info
            };

            return new BattlePresentationEvent
            {
                kind = presentationKind,
                kindId = kind,
                eventId = battleEvent.eventId,
                sequence = battleEvent.sequence,
                sourcePlayerIndex = battleEvent.sourceSeatIndex,
                targetPlayerIndex = battleEvent.targetSeatIndex,
                sourceRuntimeId = battleEvent.sourceRuntimeId,
                targetRuntimeId = battleEvent.targetRuntimeId,
                sourceName = ResolveCardName(snapshot, battleEvent.sourceSeatIndex, battleEvent.sourceRuntimeId),
                targetName = ResolveCardName(snapshot, battleEvent.targetSeatIndex, battleEvent.targetRuntimeId),
                abilityId = battleEvent.abilityId,
                effectKind = battleEvent.effectKind,
                statusKind = battleEvent.statusKind,
                durationTurns = battleEvent.durationTurns,
                amount = battleEvent.amount,
                hpBefore = battleEvent.hpBefore,
                hpAfter = battleEvent.hpAfter,
                hasResolvedHealthAfter = HasResolvedHealth(kind, battleEvent),
                armorBefore = battleEvent.armorBefore,
                armorAfter = battleEvent.armorAfter,
                hasResolvedArmorAfter = HasResolvedArmor(kind, battleEvent),
                armorBlocked = Math.Max(0, battleEvent.armorBefore - battleEvent.armorAfter),
                rawMessage = BuildMessage(snapshot, battleEvent, kind),
                logType = MapLogType(presentationKind),
                fromStructuredEvent = true
            };
        }

        private static string BuildMessage(DuelSnapshotDto snapshot, BattleEventDto battleEvent, string kind)
        {
            if (!string.IsNullOrWhiteSpace(battleEvent.message))
            {
                return battleEvent.message.Trim();
            }

            var source = ResolveCardName(snapshot, battleEvent.sourceSeatIndex, battleEvent.sourceRuntimeId);
            var target = ResolveCardName(snapshot, battleEvent.targetSeatIndex, battleEvent.targetRuntimeId);
            source = string.IsNullOrWhiteSpace(source) ? ShortId(battleEvent.sourceRuntimeId) : source;
            target = string.IsNullOrWhiteSpace(target) ? ShortId(battleEvent.targetRuntimeId) : target;

            return kind switch
            {
                "card_damage" => $"{source} hit {target} for {battleEvent.amount}.",
                "hero_damage" => $"{source} hit hero P{battleEvent.targetSeatIndex} for {battleEvent.amount}.",
                "shield_block" => $"{target} blocked damage with shield.",
                "status_applied" => $"{StatusKindName(battleEvent.statusKind)} applied to {target}.",
                "status_expired" => $"{StatusKindName(battleEvent.statusKind)} expired on {target}.",
                "heal" => $"{target} healed {battleEvent.amount}.",
                "armor_gain" => $"{target} gained {battleEvent.amount} armor.",
                "attack_buff" => $"{target} gained {battleEvent.amount} attack.",
                "death" => $"{target} died.",
                "skill_begin" => $"{source} used {battleEvent.abilityId}.",
                _ => $"{kind} ({battleEvent.eventId})"
            };
        }

        private static string ResolveCardName(DuelSnapshotDto snapshot, int playerIndex, string runtimeId)
        {
            if (snapshot?.players == null || string.IsNullOrWhiteSpace(runtimeId))
            {
                return string.Empty;
            }

            if (playerIndex is 0 or 1 && playerIndex < snapshot.players.Length)
            {
                var exact = ResolveCardName(snapshot.players[playerIndex], runtimeId);
                if (!string.IsNullOrWhiteSpace(exact))
                {
                    return exact;
                }
            }

            foreach (var player in snapshot.players)
            {
                var found = ResolveCardName(player, runtimeId);
                if (!string.IsNullOrWhiteSpace(found))
                {
                    return found;
                }
            }

            return string.Empty;
        }

        private static string ResolveCardName(PlayerSnapshotDto player, string runtimeId)
        {
            if (player?.board == null)
            {
                return string.Empty;
            }

            foreach (var slot in player.board)
            {
                var occupant = slot?.occupant;
                if (occupant == null)
                {
                    continue;
                }

                if (string.Equals(occupant.runtimeId, runtimeId, StringComparison.OrdinalIgnoreCase))
                {
                    return occupant.displayName;
                }
            }

            return string.Empty;
        }

        private static bool HasResolvedHealth(string kind, BattleEventDto battleEvent)
        {
            return kind is "card_damage" or "hero_damage" or "heal" ||
                   battleEvent.hpBefore != 0 ||
                   battleEvent.hpAfter != 0;
        }

        private static bool HasResolvedArmor(string kind, BattleEventDto battleEvent)
        {
            return kind is "card_damage" or "shield_block" or "armor_gain" ||
                   battleEvent.armorBefore != 0 ||
                   battleEvent.armorAfter != 0;
        }

        private static BattleLogType MapLogType(BattlePresentationEventKind kind)
        {
            return kind switch
            {
                BattlePresentationEventKind.CardAttack => BattleLogType.Attack,
                BattlePresentationEventKind.HeroAttack => BattleLogType.Attack,
                BattlePresentationEventKind.StatusDamage => BattleLogType.Attack,
                BattlePresentationEventKind.Death => BattleLogType.Death,
                BattlePresentationEventKind.Heal => BattleLogType.Heal,
                _ => BattleLogType.Info
            };
        }

        public static string StatusKindName(int statusKind)
        {
            return statusKind switch
            {
                0 => "Poison",
                1 => "Stun",
                2 => "Shield",
                3 => "EnrageCooldown",
                _ => statusKind >= 0 ? $"Status{statusKind}" : "Status"
            };
        }

        private static string ShortId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            return value.Length <= 8 ? value : value.Substring(0, 8);
        }

        private static string NormalizeKind(string kind)
        {
            return string.IsNullOrWhiteSpace(kind) ? string.Empty : kind.Trim().ToLowerInvariant();
        }
    }
}
