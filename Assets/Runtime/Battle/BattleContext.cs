using System.Collections.Generic;
using System.Linq;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Data;
using Flippy.CardDuelMobile.Battle.Abilities;
using AbilityTriggerEnum = Flippy.CardDuelMobile.Battle.Abilities.AbilityTrigger;
using CardAbilityDef = Flippy.CardDuelMobile.Data.AbilityDefinition;

namespace Flippy.CardDuelMobile.Battle
{
    /// <summary>
    /// Contexto mutador del duelo para efectos.
    /// Lanza excepciones en lugar de retornar null.
    /// </summary>
    public sealed class BattleContext
    {
        private readonly DuelState _state;

        public BattleContext(DuelState state)
        {
            _state = state ?? throw new ValidationException("DuelState cannot be null.");
        }

        /// <summary>
        /// Obtiene estado de jugador por índice. Lanza excepción si no existe.
        /// </summary>
        public DuelPlayerState GetPlayerState(int playerIndex)
        {
            var player = _state.GetPlayer(playerIndex);
            if (player == null)
            {
                throw new InvalidGameStateException($"Player at index {playerIndex} not found.");
            }
            return player;
        }

        /// <summary>
        /// Obtiene estado de jugador sin excepciones. Retorna null si no existe.
        /// </summary>
        public DuelPlayerState TryGetPlayerState(int playerIndex)
        {
            return _state.GetPlayer(playerIndex);
        }

        /// <summary>
        /// Busca taunt target en board enemigo. Retorna null si no hay taunt.
        /// TODO: Implement via TauntEffect in IAbilityEffect pipeline
        /// </summary>
        public CardRuntime FindTauntTarget(int playerIndex)
        {
            // Taunt is now handled via TauntEffect in IAbilityEffect pipeline
            return null;
        }

        /// <summary>
        /// Busca una carta por runtime id en ambos lados.
        /// Retorna null si no existe (para flujos que lo esperan).
        /// </summary>
        public CardRuntime FindCard(string runtimeId)
        {
            if (string.IsNullOrWhiteSpace(runtimeId))
            {
                return null;
            }

            foreach (var player in _state.Players.Where(p => p != null))
            {
                foreach (var slot in player.Board)
                {
                    if (slot.Occupant != null && slot.Occupant.RuntimeId == runtimeId)
                    {
                        return slot.Occupant;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Busca una carta por runtime id, lanzando excepción si no existe.
        /// </summary>
        public CardRuntime GetCard(string runtimeId)
        {
            var card = FindCard(runtimeId);
            if (card == null)
            {
                throw new InvalidGameStateException($"Card not found: {runtimeId}.");
            }
            return card;
        }

        /// <summary>
        /// Hace daño a una carta (sin excepciones, retorna silenciosamente si target no existe).
        /// Skills are processed in AbilityPipeline (AttackExecutionPhase) before DealDamage is called.
        /// </summary>
        public void DealDamage(string sourceRuntimeId, string targetRuntimeId, int amount, bool ignoreArmor)
        {
            if (amount <= 0) return;

            var source = FindCard(sourceRuntimeId);
            var target = FindCard(targetRuntimeId);
            if (target == null) return;

            var sourceName = source?.DisplayName ?? "Unknown";
            var sourcePlayer = source != null ? source.OwnerIndex : -1;
            var targetPlayer = target.OwnerIndex;
            var hpBefore = target.CurrentHealth;

            // Damage modification and blocking handled in AbilityPipeline (AttackExecutionPhase)
            var modifiedDamage = amount;
            var effectiveIgnoreArmor = ignoreArmor;

            // Calculate armor absorption
            var pendingDamage = modifiedDamage;
            var armorAbsorbed = 0;

            if (!effectiveIgnoreArmor && target.Armor > 0)
            {
                armorAbsorbed = System.Math.Min(target.Armor, pendingDamage);
                target.Armor -= armorAbsorbed;
                pendingDamage -= armorAbsorbed;
            }

            if (pendingDamage > 0)
            {
                target.CurrentHealth -= pendingDamage;
            }

            var hpAfter = target.CurrentHealth;
            var skillSuffix = effectiveIgnoreArmor && !ignoreArmor ? " (Trample!)" : "";
            _state.Logs.Add(new BattleLogEntry
            {
                type = BattleLogType.Attack,
                message = $"[P{sourcePlayer}] {sourceName} (ATK {amount}) → [P{targetPlayer}] {target.DisplayName}: {hpBefore}→{hpAfter}HP" + (armorAbsorbed > 0 ? $" (Armor blocked {armorAbsorbed})" : "") + skillSuffix
            });

            // All skill effects (poison, stun, mana_burn, leech, enrage, etc) are now handled
            // by IAbilityEffect implementations in AbilityPipeline (AttackExecutionPhase)

            // Execute OnDamaged abilities
            ExecuteDamagedAbilities(target.RuntimeId);

            CleanupDeaths();
        }

        /// <summary>
        /// Cura una carta.
        /// </summary>
        public void Heal(string targetRuntimeId, int amount)
        {
            var target = FindCard(targetRuntimeId);
            if (target == null || amount <= 0)
            {
                return;
            }

            target.CurrentHealth += amount;
            if (target.CurrentHealth > target.MaxHealth)
            {
                target.CurrentHealth = target.MaxHealth;
            }

            _state.Logs.Add(new BattleLogEntry
            {
                type = BattleLogType.Heal,
                message = $"{target.DisplayName} healed {amount}."
            });
        }

        /// <summary>
        /// Añade armadura.
        /// </summary>
        public void GainArmor(string targetRuntimeId, int amount)
        {
            var target = FindCard(targetRuntimeId);
            if (target == null || amount <= 0)
            {
                return;
            }

            target.Armor += amount;
        }

        /// <summary>
        /// Modifica el ataque actual.
        /// </summary>
        public void ModifyAttack(string targetRuntimeId, int amount)
        {
            var target = FindCard(targetRuntimeId);
            if (target == null)
            {
                return;
            }

            target.Attack += amount;
            if (target.Attack < 0)
            {
                target.Attack = 0;
            }
        }

        /// <summary>
        /// Daño directo al héroe.
        /// </summary>
        public void DamageHero(int targetPlayerIndex, int amount)
        {
            var player = GetPlayerState(targetPlayerIndex);
            if (player == null || amount <= 0)
            {
                return;
            }

            var hpBefore = player.HeroHealth;
            player.HeroHealth -= amount;
            if (player.HeroHealth <= 0)
            {
                player.HeroHealth = 0;
                _state.DuelEnded = true;
                _state.EndReason = targetPlayerIndex == 0
                    ? DuelEndReason.LocalHeroDefeated
                    : DuelEndReason.EnemyHeroDefeated;
            }

            var hpAfter = player.HeroHealth;
            _state.Logs.Add(new BattleLogEntry
            {
                type = BattleLogType.Attack,
                message = $"Direct attack to Player {targetPlayerIndex} Hero: {amount} damage dealt. {hpBefore}→{hpAfter}HP"
            });
        }

        /// <summary>
        /// Remueve cartas muertas del board y reposiciona cartas.
        /// </summary>
        public void CleanupDeaths()
        {
            foreach (var player in _state.Players)
            {
                foreach (var slot in player.Board)
                {
                    if (slot.Occupant != null && slot.Occupant.IsDead)
                    {
                        // Execute OnDeath abilities before removing the card
                        ExecuteDeathAbilities(slot.Occupant.RuntimeId);

                        _state.Logs.Add(new BattleLogEntry
                        {
                            type = BattleLogType.Death,
                            message = $"{slot.Occupant.DisplayName} died."
                        });

                        slot.Occupant = null;
                    }
                }

                player.Reposition();
            }
        }

        /// <summary>
        /// Procesa efectos de estado: veneno, aturdimiento, etc.
        /// Llamado al inicio de cada turno antes de ataques.
        /// </summary>
        public void ProcessStatusEffects(int playerIndex)
        {
            var player = GetPlayerState(playerIndex);
            foreach (var slot in player.Board)
            {
                if (slot.Occupant == null) continue;

                var card = slot.Occupant;

                // Apply poison damage
                if (card.PoisonStacks > 0)
                {
                    var poisonDamage = card.PoisonStacks;
                    card.CurrentHealth -= poisonDamage;
                    _state.Logs.Add(new BattleLogEntry
                    {
                        type = BattleLogType.Attack,
                        message = $"{card.DisplayName} took {poisonDamage} poison damage."
                    });
                }

                // Clear stun
                if (card.Stunned)
                {
                    card.Stunned = false;
                }
            }

            CleanupDeaths();
        }

        /// <summary>
        /// Ejecuta todas las abilities con trigger OnTurnStart para un jugador.
        /// </summary>
        public void ExecuteTurnStartAbilities(int playerIndex)
        {
            var player = GetPlayerState(playerIndex);
            foreach (var slot in player.Board)
            {
                if (slot.Occupant == null) continue;
                ExecuteAbilitiesForCard(slot.Occupant, AbilityTriggerEnum.OnTurnStart);
            }
        }

        /// <summary>
        /// Ejecuta todas las abilities con trigger OnTurnEnd para un jugador.
        /// </summary>
        public void ExecuteTurnEndAbilities(int playerIndex)
        {
            var player = GetPlayerState(playerIndex);
            foreach (var slot in player.Board)
            {
                if (slot.Occupant == null) continue;
                ExecuteAbilitiesForCard(slot.Occupant, AbilityTriggerEnum.OnTurnEnd);
            }
        }

        /// <summary>
        /// Ejecuta todas las abilities con trigger OnBattlePhase para un jugador.
        /// </summary>
        public void ExecuteBattlePhaseAbilities(int playerIndex)
        {
            var player = GetPlayerState(playerIndex);
            foreach (var slot in player.Board)
            {
                if (slot.Occupant == null) continue;
                ExecuteAbilitiesForCard(slot.Occupant, AbilityTriggerEnum.OnBattlePhaseStart);
            }
        }

        /// <summary>
        /// Ejecuta todas las abilities con trigger OnDamaged para una carta.
        /// </summary>
        public void ExecuteDamagedAbilities(string cardRuntimeId)
        {
            var card = FindCard(cardRuntimeId);
            if (card != null)
            {
                ExecuteAbilitiesForCard(card, AbilityTriggerEnum.OnDamageReceived);
            }
        }

        /// <summary>
        /// Ejecuta todas las abilities con trigger OnDeath para una carta.
        /// </summary>
        public void ExecuteDeathAbilities(string cardRuntimeId)
        {
            var card = FindCard(cardRuntimeId);
            if (card != null)
            {
                ExecuteAbilitiesForCard(card, AbilityTriggerEnum.OnCardKilled);
            }
        }

        /// <summary>
        /// Ejecuta todas las abilities de una carta que coincidan con el trigger.
        /// </summary>
        private void ExecuteAbilitiesForCard(CardRuntime card, AbilityTriggerEnum trigger)
        {
            if (card?.Definition?.abilities == null)
                return;

            foreach (var ability in card.Definition.abilities)
            {
                if (ability != null && ability.trigger == trigger)
                {
                    ExecuteAbility(card, ability);
                }
            }
        }

        /// <summary>
        /// Ejecuta una ability específica resolviendo targets y efectos.
        /// </summary>
        private void ExecuteAbility(CardRuntime source, CardAbilityDef ability)
        {
            if (ability.targetSelector == null || ability.effects == null)
                return;

            // Select targets
            var targets = new List<string>();
            var targetRequest = new TargetSelectionRequest(
                source.OwnerIndex,
                1 - source.OwnerIndex,
                source.RuntimeId,
                source.CurrentSlot
            );
            ability.targetSelector.SelectTargets(this, targetRequest, targets);

            // Resolve effects on each target
            foreach (var targetId in targets)
            {
                var target = FindCard(targetId);
                if (target == null) continue;

                var execution = new EffectExecution(
                    source.OwnerIndex,
                    target.OwnerIndex,
                    source.RuntimeId,
                    target.RuntimeId
                );

                ability.Resolve(this, execution);
            }
        }
    }
}
