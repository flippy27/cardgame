using System;
using System.Collections.Generic;
using System.Linq;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Data;

namespace Flippy.CardDuelMobile.Battle
{
    /// <summary>
    /// Runtime puro del duelo. No depende de UI ni de NGO.
    /// </summary>
    public sealed class DuelRuntime
    {
        private readonly DuelRulesProfile _rules;
        private readonly DuelState _state = new();
        private readonly BattleContext _context;
        private readonly List<string> _targetBuffer = new();

        public DuelRuntime(DuelRulesProfile rules)
        {
            _rules = rules;
            _context = new BattleContext(_state);
        }

        /// <summary>
        /// Estado actual.
        /// </summary>
        public DuelState State => _state;

        /// <summary>
        /// Inicializa la partida con dos mazos y un seed determinista.
        /// </summary>
        public void StartGame(DeckDefinition deckA, DeckDefinition deckB, int seed = 0)
        {
            _state.Players[0] = BuildPlayerState(0, deckA, seed ^ 0x51A7);
            _state.Players[1] = BuildPlayerState(1, deckB, seed ^ 0x8CC1);
            _state.ActivePlayerIndex = 0;
            _state.TurnNumber = 1;
            _state.DuelEnded = false;
            _state.EndReason = DuelEndReason.None;
            _state.Logs.Clear();

            for (var i = 0; i < _rules.startingHandSize; i++)
            {
                DrawCard(0);
                DrawCard(1);
            }

            _state.Logs.Add(new BattleLogEntry
            {
                type = BattleLogType.Info,
                message = $"Duel started with seed {seed}."
            });
        }

        public bool CanPlayCard(int playerIndex, string runtimeCardKey, BoardSlot slot, out string reason)
        {
            reason = string.Empty;

            if (_state.DuelEnded)
            {
                reason = "Duel has ended.";
                return false;
            }

            if (_state.ActivePlayerIndex != playerIndex)
            {
                reason = "Not this player's turn.";
                return false;
            }

            var player = _state.GetPlayer(playerIndex);
            if (player == null)
            {
                reason = "Player not found.";
                return false;
            }

            if (!player.IsSlotEmpty(slot))
            {
                reason = "Slot is occupied.";
                return false;
            }

            var handEntry = player.Hand.FirstOrDefault(x => x.RuntimeHandKey == runtimeCardKey);
            if (handEntry == null)
            {
                reason = "Card not found in hand.";
                return false;
            }

            if (handEntry.Definition == null)
            {
                reason = "Card definition is missing (corrupted state).";
                return false;
            }

            if (handEntry.Definition.manaCost > player.Mana)
            {
                reason = $"Not enough mana (need {handEntry.Definition.manaCost}, have {player.Mana}).";
                return false;
            }

            if (slot == BoardSlot.Front && !handEntry.Definition.canBePlayedInFront)
            {
                reason = "This card cannot be played in the front row.";
                return false;
            }

            if ((slot == BoardSlot.BackLeft || slot == BoardSlot.BackRight) && !handEntry.Definition.canBePlayedInBack)
            {
                reason = "This card cannot be played in the back row.";
                return false;
            }

            return true;
        }

        public void GetLegalPlaySlots(int playerIndex, string runtimeCardKey, List<BoardSlot> results)
        {
            results.Clear();

            foreach (BoardSlot slot in Enum.GetValues(typeof(BoardSlot)))
            {
                if (CanPlayCard(playerIndex, runtimeCardKey, slot, out _))
                {
                    results.Add(slot);
                }
            }
        }

        public bool TryPlayCard(int playerIndex, string runtimeCardKey, BoardSlot slot)
        {
            if (!CanPlayCard(playerIndex, runtimeCardKey, slot, out _))
            {
                return false;
            }

            var player = _state.GetPlayer(playerIndex);
            var handEntry = player.Hand.First(x => x.RuntimeHandKey == runtimeCardKey);

            player.Mana -= handEntry.Definition.manaCost;
            player.Hand.Remove(handEntry);

            var cardRuntime = new CardRuntime
            {
                RuntimeId = Guid.NewGuid().ToString("N"),
                CardId = handEntry.Definition.cardId,
                DisplayName = handEntry.Definition.displayName,
                OwnerIndex = playerIndex,
                Attack = handEntry.Definition.attack,
                CurrentHealth = handEntry.Definition.health,
                MaxHealth = handEntry.Definition.health,
                Armor = handEntry.Definition.armor,
                CurrentSlot = slot,
                Definition = handEntry.Definition
            };

            var boardSlot = player.Board.First(x => x.Slot == slot);
            boardSlot.Occupant = cardRuntime;

            _state.Logs.Add(new BattleLogEntry
            {
                type = BattleLogType.Summon,
                message = $"{cardRuntime.DisplayName} entered {slot}."
            });

            ResolveAbilities(cardRuntime, AbilityTrigger.OnPlay);
            return true;
        }

        public bool TryEndTurn(int playerIndex)
        {
            if (_state.DuelEnded || _state.ActivePlayerIndex != playerIndex)
            {
                return false;
            }

            var active = _state.GetPlayer(playerIndex);
            if (active == null)
            {
                return false;
            }

            _state.Logs.Add(new BattleLogEntry
            {
                type = BattleLogType.Turn,
                message = $"Player {playerIndex} ended turn."
            });

            ResolveTurnAbilities(playerIndex, AbilityTrigger.OnTurnEnd);
            ExecuteBattlePhase(playerIndex);
            _context.CleanupDeaths();

            if (!_state.DuelEnded)
            {
                _state.ActivePlayerIndex = 1 - playerIndex;
                _state.TurnNumber++;

                var nextPlayer = _state.GetPlayer(_state.ActivePlayerIndex);
                nextPlayer.MaxMana = Math.Min(_rules.maxMana, nextPlayer.MaxMana + _rules.manaPerTurn);
                nextPlayer.Mana = nextPlayer.MaxMana;

                if (_rules.drawAtTurnStart)
                {
                    DrawCard(_state.ActivePlayerIndex);
                }

                _context.ProcessStatusEffects(_state.ActivePlayerIndex);
                ResolveTurnAbilities(_state.ActivePlayerIndex, AbilityTrigger.OnTurnStart);
            }

            return true;
        }

        public DuelSnapshotDto CreateSnapshot(int localPlayerIndex)
        {
            return new DuelSnapshotDto
            {
                localPlayerIndex = localPlayerIndex,
                activePlayerIndex = _state.ActivePlayerIndex,
                turnNumber = _state.TurnNumber,
                duelEnded = _state.DuelEnded,
                endReason = _state.EndReason,
                logs = BuildRecentLogs(12),
                players = _state.Players.Select(BuildPlayerSnapshot).ToArray()
            };
        }

        private DuelPlayerState BuildPlayerState(int playerIndex, DeckDefinition deck, int seed)
        {
            var player = new DuelPlayerState
            {
                PlayerIndex = playerIndex,
                HeroHealth = _rules.startingHeroHealth,
                MaxMana = _rules.startingMana,
                Mana = _rules.startingMana
            };

            if (deck != null && deck.cards != null)
            {
                var source = deck.cards.Where(c => c != null).ToList();
                var random = new Random(seed == 0 ? 17 + playerIndex : seed);
                while (source.Count > 0)
                {
                    var pick = random.Next(0, source.Count);
                    player.Deck.Add(source[pick]);
                    source.RemoveAt(pick);
                }
            }

            return player;
        }

        private void DrawCard(int playerIndex)
        {
            var player = _state.GetPlayer(playerIndex);
            if (player == null || player.Deck.Count == 0)
            {
                return;
            }

            var nextCard = player.Deck[0];
            player.Deck.RemoveAt(0);
            player.Hand.Add(new HandCardRuntime
            {
                RuntimeHandKey = Guid.NewGuid().ToString("N"),
                Definition = nextCard
            });
        }

        private PlayerSnapshotDto BuildPlayerSnapshot(DuelPlayerState player)
        {
            return new PlayerSnapshotDto
            {
                playerIndex = player.PlayerIndex,
                heroHealth = player.HeroHealth,
                mana = player.Mana,
                maxMana = player.MaxMana,
                remainingDeckCount = player.Deck.Count,
                hand = player.Hand.Select(card => new CardInHandDto
                {
                    runtimeCardKey = card.RuntimeHandKey,
                    cardId = card.Definition.cardId,
                    displayName = card.Definition.displayName,
                    manaCost = card.Definition.manaCost,
                    attack = card.Definition.attack,
                    health = card.Definition.health,
                    canBePlayedInFront = card.Definition.canBePlayedInFront,
                    canBePlayedInBack = card.Definition.canBePlayedInBack
                }).ToArray(),
                board = player.Board.Select(slot => new BoardSlotSnapshotDto
                {
                    slot = slot.Slot,
                    occupied = slot.Occupant != null,
                    occupant = slot.Occupant == null ? null : new BoardCardDto
                    {
                        runtimeId = slot.Occupant.RuntimeId,
                        cardId = slot.Occupant.CardId,
                        displayName = slot.Occupant.DisplayName,
                        ownerIndex = slot.Occupant.OwnerIndex,
                        attack = slot.Occupant.Attack,
                        currentHealth = slot.Occupant.CurrentHealth,
                        maxHealth = slot.Occupant.MaxHealth,
                        armor = slot.Occupant.Armor,
                        slot = slot.Occupant.CurrentSlot
                    }
                }).ToArray()
            };
        }

        private List<BattleLogEntry> BuildRecentLogs(int count)
        {
            var results = new List<BattleLogEntry>();
            var start = _state.Logs.Count - count;
            if (start < 0)
            {
                start = 0;
            }

            for (var i = start; i < _state.Logs.Count; i++)
            {
                results.Add(_state.Logs[i]);
            }

            return results;
        }

        private void ExecuteBattlePhase(int sourcePlayerIndex)
        {
            var attackerState = _state.GetPlayer(sourcePlayerIndex);
            var defenderIndex = 1 - sourcePlayerIndex;

            // Attack order: Front → BackLeft → BackRight
            var attackOrder = new BoardSlot[] { BoardSlot.Front, BoardSlot.BackLeft, BoardSlot.BackRight };

            foreach (var slotType in attackOrder)
            {
                var slotData = attackerState.FindSlot(slotType);
                if (slotData == null)
                {
                    continue;
                }

                ExecuteSlotAttack(sourcePlayerIndex, defenderIndex, slotData);
            }
        }

        private void ExecuteSlotAttack(int sourcePlayerIndex, int defenderIndex, BoardSlotRuntime slot)
        {
            var attacker = slot.Occupant;
            if (attacker == null || attacker.IsDead)
            {
                return;
            }

            if (attacker.Stunned)
            {
                _state.Logs.Add(new BattleLogEntry
                {
                    type = BattleLogType.Attack,
                    message = $"{attacker.DisplayName} is stunned and cannot attack!"
                });
                return;
            }

            ResolveAbilities(attacker, AbilityTrigger.OnBattlePhase);

            var targetSelector = attacker.Definition.defaultAttackTargetSelector;
            var attackPower = attacker.Attack + attacker.EnrageBonus;

            // LastStand bonus: double damage if alone
            var enemyBoard = _state.GetPlayer(defenderIndex);
            var allyBoard = _state.GetPlayer(sourcePlayerIndex);
            var alliesOnBoard = 0;
            foreach (var s in allyBoard.Board)
            {
                if (s.Occupant != null && !s.Occupant.IsDead)
                {
                    alliesOnBoard++;
                }
            }
            if (alliesOnBoard == 1 && attacker.Definition?.skills != null)
            {
                foreach (var skill in attacker.Definition.skills)
                {
                    if (skill != null && skill.skillId == "last_stand")
                    {
                        attackPower *= 2;
                        break;
                    }
                }
            }

            if (targetSelector == null)
            {
                _context.DamageHero(defenderIndex, attackPower);
                return;
            }

            _targetBuffer.Clear();
            targetSelector.SelectTargets(
                _context,
                new TargetSelectionRequest(sourcePlayerIndex, defenderIndex, attacker.RuntimeId, attacker.CurrentSlot),
                _targetBuffer);

            if (_targetBuffer.Count == 0)
            {
                _context.DamageHero(defenderIndex, attackPower);
                return;
            }

            foreach (var targetId in _targetBuffer)
            {
                _context.DealDamage(attacker.RuntimeId, targetId, attackPower, ignoreArmor: false);
            }
        }

        private void ResolveTurnAbilities(int playerIndex, AbilityTrigger trigger)
        {
            var player = _state.GetPlayer(playerIndex);
            foreach (var slot in player.Board)
            {
                if (slot.Occupant != null)
                {
                    ResolveAbilities(slot.Occupant, trigger);
                }
            }
        }

        private void ResolveAbilities(CardRuntime source, AbilityTrigger trigger)
        {
            if (source == null || source.Definition == null || source.Definition.abilities == null)
            {
                return;
            }

            foreach (var ability in source.Definition.abilities)
            {
                if (ability == null || ability.trigger != trigger)
                {
                    continue;
                }

                _targetBuffer.Clear();
                if (ability.targetSelector != null)
                {
                    ability.targetSelector.SelectTargets(
                        _context,
                        new TargetSelectionRequest(source.OwnerIndex, source.OwnerIndex == 0 ? 1 : 0, source.RuntimeId, source.CurrentSlot),
                        _targetBuffer);
                }

                if (_targetBuffer.Count == 0)
                {
                    ability.Resolve(_context, new EffectExecution(source.OwnerIndex, source.OwnerIndex, source.RuntimeId, source.RuntimeId));
                    continue;
                }

                foreach (var targetId in _targetBuffer)
                {
                    var target = _context.FindCard(targetId);
                    var targetPlayer = target == null ? (source.OwnerIndex == 0 ? 1 : 0) : target.OwnerIndex;
                    ability.Resolve(_context, new EffectExecution(source.OwnerIndex, targetPlayer, source.RuntimeId, targetId));
                }
            }
        }
    }
}
