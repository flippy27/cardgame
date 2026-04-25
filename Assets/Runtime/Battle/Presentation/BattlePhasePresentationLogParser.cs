using System.Text.RegularExpressions;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.UI
{
    internal enum BattlePresentationEventKind
    {
        SkillBegin,
        CardAttack,
        HeroAttack,
        StatusDamage,
        ShieldBlock,
        StatusApplied,
        StatusExpired,
        Heal,
        ArmorGain,
        AttackBuff,
        Skip,
        Death,
        Info
    }

    internal sealed class BattlePresentationEvent
    {
        public BattlePresentationEventKind kind;
        public int sourcePlayerIndex = -1;
        public int targetPlayerIndex = -1;
        public int sequence = -1;
        public string eventId;
        public string kindId;
        public string sourceRuntimeId;
        public string targetRuntimeId;
        public string sourceName;
        public string targetName;
        public string abilityId;
        public int effectKind = -1;
        public int statusKind = -1;
        public int durationTurns;
        public int amount;
        public int hpBefore;
        public int hpAfter;
        public bool hasResolvedHealthAfter;
        public int armorBefore;
        public int armorAfter;
        public bool hasResolvedArmorAfter;
        public int armorBlocked;
        public string rawMessage;
        public BattleLogType logType;
        public bool fromStructuredEvent;
    }

    internal static class BattlePhasePresentationLogParser
    {
        private const string ArrowPattern = @"(?:→|â†’|Ã¢â€ â€™|->)";

        private static readonly Regex CardAttackRegex = new(
            @"^\[P(?<source>\d+)\]\s+(?<attacker>.+?)\s+\(ATK\s+(?<amount>\d+)\)\s+" + ArrowPattern +
            @"\s+\[P(?<target>\d+)\]\s+(?<defender>.+?):\s+(?<hpBefore>-?\d+)" + ArrowPattern +
            @"(?<hpAfter>-?\d+)HP(?:\s+\(Armor blocked (?<armor>\d+)\))?.*$",
            RegexOptions.Compiled);

        private static readonly Regex HeroAttackRegex = new(
            @"^Direct attack to Player (?<target>\d+) Hero:\s+(?<amount>\d+) damage dealt\.\s+(?<hpBefore>-?\d+)" +
            ArrowPattern + @"(?<hpAfter>-?\d+)HP$",
            RegexOptions.Compiled);

        private static readonly Regex PoisonRegex = new(
            @"^(?<target>.+?) took (?<amount>\d+) poison damage\.$",
            RegexOptions.Compiled);

        private static readonly Regex DeathRegex = new(
            @"^(?<target>.+?) died\.$",
            RegexOptions.Compiled);

        private static readonly Regex GenericHitRegex = new(
            @"^(?<attacker>.+?)\s+hit\s+(?<target>.+?)\s+for\s+(?<amount>\d+)(?:\s+damage)?\.$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static BattlePresentationEvent Parse(BattleLogEntry log, int fallbackSourcePlayerIndex = -1)
        {
            if (log == null || string.IsNullOrWhiteSpace(log.message))
            {
                return null;
            }

            var normalizedMessage = NormalizeMessage(log.message);

            var cardAttack = CardAttackRegex.Match(normalizedMessage);
            if (cardAttack.Success)
            {
                return new BattlePresentationEvent
                {
                    kind = BattlePresentationEventKind.CardAttack,
                    sourcePlayerIndex = ParseInt(cardAttack, "source"),
                    targetPlayerIndex = ParseInt(cardAttack, "target"),
                    sourceName = cardAttack.Groups["attacker"].Value.Trim(),
                    targetName = cardAttack.Groups["defender"].Value.Trim(),
                    amount = ParseInt(cardAttack, "amount"),
                    hpBefore = ParseInt(cardAttack, "hpBefore"),
                    hpAfter = ParseInt(cardAttack, "hpAfter"),
                    hasResolvedHealthAfter = true,
                    armorBlocked = ParseInt(cardAttack, "armor"),
                    rawMessage = normalizedMessage,
                    logType = log.type
                };
            }

            var heroAttack = HeroAttackRegex.Match(normalizedMessage);
            if (heroAttack.Success)
            {
                var targetPlayerIndex = ParseInt(heroAttack, "target");
                return new BattlePresentationEvent
                {
                    kind = BattlePresentationEventKind.HeroAttack,
                    sourcePlayerIndex = 1 - targetPlayerIndex,
                    targetPlayerIndex = targetPlayerIndex,
                    amount = ParseInt(heroAttack, "amount"),
                    hpBefore = ParseInt(heroAttack, "hpBefore"),
                    hpAfter = ParseInt(heroAttack, "hpAfter"),
                    hasResolvedHealthAfter = true,
                    rawMessage = normalizedMessage,
                    logType = log.type
                };
            }

            var genericHit = GenericHitRegex.Match(normalizedMessage);
            if (genericHit.Success)
            {
                var attacker = genericHit.Groups["attacker"].Value.Trim();
                var target = genericHit.Groups["target"].Value.Trim();
                var amount = ParseInt(genericHit, "amount");

                if (TryResolveHeroTargetPlayerIndex(target, fallbackSourcePlayerIndex, out var heroTargetPlayerIndex))
                {
                    return new BattlePresentationEvent
                    {
                        kind = BattlePresentationEventKind.HeroAttack,
                        sourcePlayerIndex = fallbackSourcePlayerIndex,
                        targetPlayerIndex = heroTargetPlayerIndex,
                        sourceName = attacker,
                        targetName = target,
                        amount = amount,
                        rawMessage = normalizedMessage,
                        logType = log.type
                    };
                }

                return new BattlePresentationEvent
                {
                    kind = BattlePresentationEventKind.CardAttack,
                    sourcePlayerIndex = fallbackSourcePlayerIndex,
                    targetPlayerIndex = fallbackSourcePlayerIndex is 0 or 1 ? 1 - fallbackSourcePlayerIndex : -1,
                    sourceName = attacker,
                    targetName = target,
                    amount = amount,
                    rawMessage = normalizedMessage,
                    logType = log.type
                };
            }

            var poison = PoisonRegex.Match(normalizedMessage);
            if (poison.Success)
            {
                return new BattlePresentationEvent
                {
                    kind = BattlePresentationEventKind.StatusDamage,
                    targetName = poison.Groups["target"].Value.Trim(),
                    amount = ParseInt(poison, "amount"),
                    rawMessage = normalizedMessage,
                    logType = log.type
                };
            }

            var death = DeathRegex.Match(normalizedMessage);
            if (death.Success)
            {
                return new BattlePresentationEvent
                {
                    kind = BattlePresentationEventKind.Death,
                    targetName = death.Groups["target"].Value.Trim(),
                    rawMessage = normalizedMessage,
                    logType = log.type
                };
            }

            return new BattlePresentationEvent
            {
                kind = BattlePresentationEventKind.Info,
                rawMessage = normalizedMessage,
                logType = log.type
            };
        }

        private static int ParseInt(Match match, string groupName)
        {
            if (match == null || !match.Groups[groupName].Success)
            {
                return 0;
            }

            return int.TryParse(match.Groups[groupName].Value, out var value) ? value : 0;
        }

        private static bool TryResolveHeroTargetPlayerIndex(string target, int fallbackSourcePlayerIndex, out int targetPlayerIndex)
        {
            targetPlayerIndex = -1;
            if (string.IsNullOrWhiteSpace(target))
            {
                return false;
            }

            var normalized = target.Trim().ToLowerInvariant();
            var compact = normalized.Replace(" ", string.Empty);
            if (normalized.Contains("player 0") || compact.Contains("player0") || normalized.Contains("hero p0") || compact.Contains("herop0"))
            {
                targetPlayerIndex = 0;
                return true;
            }

            if (normalized.Contains("player 1") || compact.Contains("player1") || normalized.Contains("hero p1") || compact.Contains("herop1"))
            {
                targetPlayerIndex = 1;
                return true;
            }

            if ((normalized.Contains("enemy hero") || normalized.Contains("opponent hero")) && fallbackSourcePlayerIndex is 0 or 1)
            {
                targetPlayerIndex = 1 - fallbackSourcePlayerIndex;
                return true;
            }

            if ((normalized.Contains("your hero") || normalized.Contains("local hero")) && fallbackSourcePlayerIndex is 0 or 1)
            {
                targetPlayerIndex = fallbackSourcePlayerIndex;
                return true;
            }

            if ((normalized == "hero" || normalized == "player" || compact == "playerhero") && fallbackSourcePlayerIndex is 0 or 1)
            {
                targetPlayerIndex = 1 - fallbackSourcePlayerIndex;
                return true;
            }

            return false;
        }

        private static string NormalizeMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return string.Empty;
            }

            var normalized = message.Trim();
            if (normalized.StartsWith("[BattlePhase]", System.StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring("[BattlePhase]".Length).Trim();
            }

            return normalized;
        }
    }
}
