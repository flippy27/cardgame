using System;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Data;

namespace Flippy.CardDuelMobile.Battle
{
    public static class AttackPresentationResolver
    {
        public const string DeliveryTypeMelee = "melee";
        public const string DeliveryTypeProjectile = "projectile";
        public const string DeliveryTypeBeam = "beam";
        public const string DeliveryTypeArc = "arc";

        public static int ResolveMotionLevel(BoardCardDto card)
        {
            if (card == null)
            {
                return 1;
            }

            if (card.attackMotionLevel > 0)
            {
                return ClampLevel(card.attackMotionLevel);
            }

            return AutoResolveLevel(card.attack);
        }

        public static int ResolveShakeLevel(BoardCardDto card)
        {
            if (card == null)
            {
                return 1;
            }

            if (card.attackShakeLevel > 0)
            {
                return ClampLevel(card.attackShakeLevel);
            }

            return AutoResolveLevel(card.attack);
        }

        public static int ResolveMotionLevel(CardDefinition definition, int fallbackAttack)
        {
            if (definition != null && definition.attackMotionLevel > 0)
            {
                return ClampLevel(definition.attackMotionLevel);
            }

            return AutoResolveLevel(definition != null ? definition.attack : fallbackAttack);
        }

        public static int ResolveShakeLevel(CardDefinition definition, int fallbackAttack)
        {
            if (definition != null && definition.attackShakeLevel > 0)
            {
                return ClampLevel(definition.attackShakeLevel);
            }

            return AutoResolveLevel(definition != null ? definition.attack : fallbackAttack);
        }

        public static string ResolveDeliveryType(BoardCardDto card)
        {
            if (card != null && !string.IsNullOrWhiteSpace(card.attackDeliveryType))
            {
                return NormalizeDeliveryType(card.attackDeliveryType);
            }

            if (card != null)
            {
                return AutoResolveDeliveryType(card.unitType);
            }

            return DeliveryTypeMelee;
        }

        public static string ResolveDeliveryType(CardDefinition definition, BoardSlot fallbackSlot, int fallbackUnitType = 0)
        {
            if (definition != null && !string.IsNullOrWhiteSpace(definition.attackDeliveryType))
            {
                return NormalizeDeliveryType(definition.attackDeliveryType);
            }

            var unitType = definition != null ? (int)definition.unitType : fallbackUnitType;
            return AutoResolveDeliveryType(unitType);
        }

        public static bool UsesProjectile(BoardCardDto card)
        {
            return UsesProjectile(ResolveDeliveryType(card));
        }

        public static bool UsesProjectile(CardDefinition definition, BoardSlot fallbackSlot, int fallbackUnitType = 0)
        {
            return UsesProjectile(ResolveDeliveryType(definition, fallbackSlot, fallbackUnitType));
        }

        public static bool UsesProjectile(string deliveryType)
        {
            var normalized = NormalizeDeliveryType(deliveryType);
            return !string.Equals(normalized, DeliveryTypeMelee, StringComparison.Ordinal);
        }

        public static string NormalizeDeliveryType(string deliveryType)
        {
            if (string.IsNullOrWhiteSpace(deliveryType))
            {
                return DeliveryTypeMelee;
            }

            return deliveryType.Trim().ToLowerInvariant() switch
            {
                "ranged" => DeliveryTypeProjectile,
                "arrow" => DeliveryTypeProjectile,
                "missile" => DeliveryTypeProjectile,
                "magic" => DeliveryTypeBeam,
                "spell" => DeliveryTypeBeam,
                _ => deliveryType.Trim().ToLowerInvariant()
            };
        }

        private static int AutoResolveLevel(int attack)
        {
            if (attack <= 1)
            {
                return 1;
            }

            if (attack == 2)
            {
                return 2;
            }

            if (attack == 3)
            {
                return 3;
            }

            if (attack == 4)
            {
                return 4;
            }

            return 5;
        }

        private static string AutoResolveDeliveryType(int unitType)
        {
            return unitType switch
            {
                1 => DeliveryTypeProjectile,
                2 => DeliveryTypeBeam,
                _ => DeliveryTypeMelee
            };
        }

        private static int ClampLevel(int level)
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
