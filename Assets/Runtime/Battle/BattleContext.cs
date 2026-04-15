using System.Collections.Generic;
using System.Linq;
using Flippy.CardDuelMobile.Core;

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
        /// Obtiene estado de jugador por índice.
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
        /// </summary>
        public void DealDamage(string sourceRuntimeId, string targetRuntimeId, int amount, bool ignoreArmor)
        {
            if (amount <= 0) return;

            var target = FindCard(targetRuntimeId);
            if (target == null) return; // Objetivo no existe, no hacer nada

            var pendingDamage = amount;
            if (!ignoreArmor && target.Armor > 0)
            {
                var absorbed = System.Math.Min(target.Armor, pendingDamage);
                target.Armor -= absorbed;
                pendingDamage -= absorbed;
            }

            if (pendingDamage > 0)
            {
                target.CurrentHealth -= pendingDamage;
            }

            _state.Logs.Add(new BattleLogEntry
            {
                type = BattleLogType.Attack,
                message = $"{sourceRuntimeId} hit {target.DisplayName} for {amount}."
            });

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

            player.HeroHealth -= amount;
            if (player.HeroHealth <= 0)
            {
                player.HeroHealth = 0;
                _state.DuelEnded = true;
                _state.EndReason = targetPlayerIndex == 0
                    ? DuelEndReason.LocalHeroDefeated
                    : DuelEndReason.EnemyHeroDefeated;
            }
        }

        /// <summary>
        /// Remueve cartas muertas del board.
        /// </summary>
        public void CleanupDeaths()
        {
            foreach (var player in _state.Players)
            {
                foreach (var slot in player.Board)
                {
                    if (slot.Occupant != null && slot.Occupant.IsDead)
                    {
                        _state.Logs.Add(new BattleLogEntry
                        {
                            type = BattleLogType.Death,
                            message = $"{slot.Occupant.DisplayName} died."
                        });

                        slot.Occupant = null;
                    }
                }
            }
        }
    }
}
