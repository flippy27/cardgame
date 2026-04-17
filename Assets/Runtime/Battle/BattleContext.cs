using System.Collections.Generic;
using System.Linq;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Data;

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
        /// Busca taunt target en board enemigo. Retorna null si no hay taunt.
        /// </summary>
        public CardRuntime FindTauntTarget(int playerIndex)
        {
            var player = GetPlayerState(playerIndex);
            foreach (var slot in player.Board)
            {
                if (slot.Occupant == null) continue;

                var card = slot.Occupant;
                if (card.Definition?.skills != null)
                {
                    foreach (var skill in card.Definition.skills)
                    {
                        if (skill != null && skill.skillId == "taunt")
                        {
                            return card;
                        }
                    }
                }
            }
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
        /// Applies attacker skills (Trample, Fly check, etc).
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

            // Check defender skills (Fly blocks if attacker doesn't have Fly)
            if (target.Definition?.skills != null)
            {
                foreach (var skill in target.Definition.skills)
                {
                    if (skill != null && skill.BlocksDamage(source, target))
                    {
                        // Damage blocked - redirect to hero
                        var logMsg = skill.GetLogMessage(source, target, amount);
                        if (!string.IsNullOrEmpty(logMsg))
                        {
                            _state.Logs.Add(new BattleLogEntry
                            {
                                type = BattleLogType.Attack,
                                message = logMsg
                            });
                        }
                        DamageHero(targetPlayer, amount);
                        return;
                    }
                }
            }

            // Check attacker skills (Trample ignores armor)
            var effectiveIgnoreArmor = ignoreArmor;
            if (source?.Definition?.skills != null)
            {
                foreach (var skill in source.Definition.skills)
                {
                    if (skill != null && skill.skillId == "trample")
                    {
                        effectiveIgnoreArmor = true;
                    }
                }
            }

            var pendingDamage = amount;
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

            // Apply poison from attacker
            if (source?.Definition?.skills != null)
            {
                foreach (var skill in source.Definition.skills)
                {
                    if (skill != null && skill.skillId == "poison")
                    {
                        var poisonSkill = skill as Data.PoisonSkill;
                        if (poisonSkill != null && target != null)
                        {
                            target.PoisonStacks += poisonSkill.poisonStacks;
                            _state.Logs.Add(new BattleLogEntry
                            {
                                type = BattleLogType.Attack,
                                message = $"{target.DisplayName} was poisoned! ({target.PoisonStacks} stacks)"
                            });
                        }
                    }
                    else if (skill != null && skill.skillId == "stun")
                    {
                        if (target != null)
                        {
                            target.Stunned = true;
                            _state.Logs.Add(new BattleLogEntry
                            {
                                type = BattleLogType.Attack,
                                message = $"{target.DisplayName} was stunned!"
                            });
                        }
                    }
                    else if (skill != null && skill.skillId == "mana_burn")
                    {
                        var manaBurnSkill = skill as Data.ManaBurnSkill;
                        if (manaBurnSkill != null && target != null)
                        {
                            var defender = GetPlayerState(targetPlayer);
                            defender.Mana -= manaBurnSkill.manaCost;
                            if (defender.Mana < 0) defender.Mana = 0;
                            _state.Logs.Add(new BattleLogEntry
                            {
                                type = BattleLogType.Attack,
                                message = $"Player {targetPlayer} lost {manaBurnSkill.manaCost} mana!"
                            });
                        }
                    }
                }
            }

            // Apply leech from attacker
            if (source?.Definition?.skills != null && source != null)
            {
                foreach (var skill in source.Definition.skills)
                {
                    if (skill != null && skill.skillId == "leech")
                    {
                        var leechSkill = skill as Data.LeechSkill;
                        if (leechSkill != null)
                        {
                            var healAmount = (pendingDamage * leechSkill.leechPercent) / 100;
                            if (healAmount > 0)
                            {
                                var sourcePlayerState = GetPlayerState(sourcePlayer);
                                sourcePlayerState.HeroHealth += healAmount;
                                _state.Logs.Add(new BattleLogEntry
                                {
                                    type = BattleLogType.Heal,
                                    message = $"[P{sourcePlayer}] {sourceName} leeched {healAmount} HP!"
                                });
                            }
                        }
                    }
                }
            }

            // Apply enrage from defender being hit
            if (target?.Definition?.skills != null && target != null)
            {
                foreach (var skill in target.Definition.skills)
                {
                    if (skill != null && skill.skillId == "enrage")
                    {
                        var enrageSkill = skill as Data.EnrageSkill;
                        if (enrageSkill != null)
                        {
                            target.EnrageBonus += enrageSkill.bonusPerHit;
                            _state.Logs.Add(new BattleLogEntry
                            {
                                type = BattleLogType.Attack,
                                message = $"{target.DisplayName} enraged! (+{enrageSkill.bonusPerHit} ATK)"
                            });
                        }
                    }
                }
            }

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
    }
}
