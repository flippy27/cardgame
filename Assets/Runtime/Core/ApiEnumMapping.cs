/*
 * ENUM MAPPING: Unity Client <-> CardGameAPI Server
 *
 * This file documents enum conversions between client and server.
 * Keep synchronized when API changes.
 *
 * SYNC STATUS (2026-04-14):
 * ✓ BoardSlot: identical (Front=0, BackLeft=1, BackRight=2)
 * ✓ CardRarity: identical (Common/Rare/Epic/Legendary)
 * ✓ CardFaction: identical (Ember/Tidal/Grove/Alloy/Void)
 * ⚠ AbilityTrigger vs TriggerKind: subset match
 * ⚠ TargetSide vs TargetSelectorKind: different design
 * ? EffectKind: not directly mapped in Unity
 *
 * DETAILS:
 *
 * === BoardSlot ===
 * Client AbilityTrigger          API TriggerKind
 * Front = 0                      Front = 0
 * BackLeft = 1                   BackLeft = 1
 * BackRight = 2                  BackRight = 2
 * ✓ NO CONVERSION NEEDED
 *
 * === AbilityTrigger vs TriggerKind ===
 * Client AbilityTrigger          API TriggerKind
 * OnPlay = 0                     OnPlay = 0
 * OnTurnStart = 1                OnTurnStart = 1
 * OnTurnEnd = 2                  OnTurnEnd = 2
 * OnBattlePhase = 3              OnBattlePhase = 3
 * OnDamaged = 4                  (not in API)
 * OnDeath = 5                    (not in API)
 *
 * ACTION: API needs OnDamaged/OnDeath support, OR client should drop them.
 * WORKAROUND: Client safely ignores unknown triggers when deserializing.
 *
 * === TargetSide vs TargetSelectorKind ===
 * Client TargetSide              API TargetSelectorKind
 * Self = 0                       Self = 0
 * Ally = 1                       LowestHealthAlly = 4 (partial)
 * Enemy = 2                      FrontlineFirst = 1, BacklineFirst = 2
 * Both = 3                       AllEnemies = 3 (partial)
 *
 * PROBLEM: Different models entirely.
 * Client: broad side categories (Ally/Enemy)
 * API: specific selectors (FrontlineFirst, etc)
 *
 * ACTION: Refactor client to match API selectors.
 * TRANSITION: Add conversion layer during migration.
 *
 * === EffectKind (API only) ===
 * API EffectKind
 * Damage = 0
 * Heal = 1
 * GainArmor = 2
 * BuffAttack = 3
 * HitHero = 4
 *
 * Client: Uses AbilityDefinition.Resolve() instead of enum.
 * ISSUE: Client effects are hardcoded in definition classes.
 *
 * ACTION: Extract effect enum into shared type, use in both systems.
 *
 * === Recommended Fixes (Priority Order) ===
 * 1. Unify EffectKind enum in both projects
 * 2. Extend API TriggerKind or drop client triggers (OnDamaged/OnDeath)
 * 3. Refactor client TargetSide to match API TargetSelectorKind
 * 4. Add test for enum value parity (values match for same semantics)
 */

using Flippy.CardDuelMobile.Battle.Abilities;
using AbilityTriggerEnum = Flippy.CardDuelMobile.Battle.Abilities.AbilityTrigger;

namespace Flippy.CardDuelMobile.Core
{
    /// <summary>
    /// Runtime helper for enum conversions to/from API.
    /// Maps Battle.Abilities.AbilityTrigger to server-compatible format.
    /// </summary>
    public static class ApiEnumMapping
    {
        /// <summary>
        /// Convert client AbilityTrigger to API-compatible format.
        /// Returns -1 if trigger is not supported by API.
        /// </summary>
        public static int TriggerToApi(AbilityTriggerEnum trigger)
        {
            return trigger switch
            {
                AbilityTriggerEnum.OnCardInitialize => 0,
                AbilityTriggerEnum.OnTurnStart => 1,
                AbilityTriggerEnum.OnTurnEnd => 2,
                AbilityTriggerEnum.OnBattlePhaseStart => 3,
                // Pipeline triggers (validate, select, damage calculation, etc.) not in API
                _ => -1
            };
        }

        /// <summary>
        /// Convert API TriggerKind to client AbilityTrigger.
        /// </summary>
        public static AbilityTriggerEnum TriggerFromApi(int apiTrigger)
        {
            return apiTrigger switch
            {
                0 => AbilityTriggerEnum.OnCardInitialize,
                1 => AbilityTriggerEnum.OnTurnStart,
                2 => AbilityTriggerEnum.OnTurnEnd,
                3 => AbilityTriggerEnum.OnBattlePhaseStart,
                _ => AbilityTriggerEnum.OnCardInitialize // default
            };
        }

        /// <summary>
        /// Check if trigger is supported by server API.
        /// </summary>
        public static bool IsTriggerApiSupported(AbilityTriggerEnum trigger)
        {
            return TriggerToApi(trigger) >= 0;
        }
    }
}
