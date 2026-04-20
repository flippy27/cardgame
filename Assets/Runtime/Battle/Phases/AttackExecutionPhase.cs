using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Battle.Abilities;
using AbilityTriggerEnum = Flippy.CardDuelMobile.Battle.Abilities.AbilityTrigger;

namespace Flippy.CardDuelMobile.Battle.Phases
{
    /// <summary>
    /// Attack execution phase: all cards attack in priority order.
    /// Uses skill pipeline for flexible attack flow.
    /// </summary>
    public class AttackExecutionPhase : IBattlePhase
    {
        public string PhaseName => "Attack Execution";

        public bool Execute(BattleContext context, int playerIndex)
        {
            var player = context.GetPlayerState(playerIndex);
            var defenderIndex = 1 - playerIndex;

            // Resolve OnBattlePhase abilities before attacks
            context.ExecuteBattlePhaseAbilities(playerIndex);

            // Attack order: Front → BackLeft → BackRight
            var attackOrder = new BoardSlot[] { BoardSlot.Front, BoardSlot.BackLeft, BoardSlot.BackRight };

            foreach (var slotType in attackOrder)
            {
                var slotData = player.FindSlot(slotType);
                if (slotData?.Occupant == null || slotData.Occupant.IsDead)
                    continue;

                ExecuteCardAttack(context, slotData.Occupant, playerIndex, defenderIndex);
            }

            return true;
        }

        private void ExecuteCardAttack(BattleContext context, CardRuntime attacker, int sourcePlayerIndex, int defenderIndex)
        {
            var pipeline = new AbilityPipeline();

            // 1. Validate attack
            var validationContext = new AbilityContext(attacker, null, context, AbilityTrigger.OnValidateAttack);
            validationContext = pipeline.Execute(validationContext);

            if (!validationContext.IsValidAttack)
                return;

            // 2. Select targets
            var selectionContext = new AbilityContext(attacker, null, context, AbilityTrigger.OnSelectTarget);
            var targets = new System.Collections.Generic.List<string>();

            // Use effective selector to find targets
            if (attacker.EffectiveAttackSelector != null)
            {
                attacker.EffectiveAttackSelector.SelectTargets(
                    context,
                    new TargetSelectionRequest(sourcePlayerIndex, defenderIndex, attacker.RuntimeId, attacker.CurrentSlot),
                    targets
                );
            }
            else
            {
                // Fallback: StraightLineTargetSelector logic (same slot or Front)
                var enemyPlayer = context.GetPlayerState(defenderIndex);
                var slotRuntime = enemyPlayer?.FindSlot(attacker.CurrentSlot);
                if (slotRuntime?.Occupant != null)
                {
                    targets.Add(slotRuntime.Occupant.RuntimeId);
                }
                else
                {
                    var frontSlot = enemyPlayer?.FindSlot(BoardSlot.Front);
                    if (frontSlot?.Occupant != null)
                    {
                        targets.Add(frontSlot.Occupant.RuntimeId);
                    }
                }
            }

            foreach (var targetId in targets)
            {
                selectionContext.TargetList.Add(context.FindCard(targetId));
            }

            if (selectionContext.TargetList.Count == 0)
            {
                // No targets - damage goes to hero
                context.DamageHero(defenderIndex, attacker.Attack + attacker.EnrageBonus);
                return;
            }

            // 3. Apply damage to each target
            foreach (var target in selectionContext.TargetList)
            {
                var damageContext = new AbilityContext(attacker, target, context, AbilityTrigger.OnDamageCalculation);
                damageContext.BaseDamage = attacker.Attack + attacker.EnrageBonus;
                damageContext.TargetList.Add(target);
                damageContext = pipeline.Execute(damageContext);

                // If pipeline didn't set FinalDamage, use BaseDamage
                var finalDamage = damageContext.FinalDamage > 0 ? damageContext.FinalDamage : damageContext.BaseDamage;
                context.DealDamage(attacker.RuntimeId, target.RuntimeId, finalDamage, false);

                // 4. Apply post-damage effects (poison, stun, leech, reflection, etc)
                var postDamageContext = new AbilityContext(attacker, target, context, AbilityTrigger.OnDamageDealt);
                postDamageContext.BaseDamage = attacker.Attack + attacker.EnrageBonus;
                postDamageContext.FinalDamage = finalDamage;
                postDamageContext.TargetList.Add(target);
                pipeline.Execute(postDamageContext);
            }
        }
    }
}
