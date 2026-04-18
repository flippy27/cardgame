using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Data;
using Flippy.CardDuelMobile.Battle.Skills;

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

            // Initialize registries
            CardRegistry.Initialize();
            SkillRegistry.Initialize();
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

            // For Units, allow replacement (slot can be occupied)
            // For non-units, slot must be empty
            if (handEntry.Definition.cardType != CardType.Unit && !player.IsSlotEmpty(slot))
            {
                reason = "Slot is occupied.";
                return false;
            }

            // If slot is occupied (replacement), validate repositioning space
            if (!player.IsSlotEmpty(slot) && handEntry.Definition.cardType == CardType.Unit)
            {
                var isMelee = handEntry.Definition.unitType == UnitType.Melee;
                if (isMelee && slot == BoardSlot.Front)
                {
                    // Front → BackLeft, BackLeft → BackRight
                    // Need space in BackLeft for the current Front occupant
                    var backLeft = player.FindSlot(BoardSlot.BackLeft);
                    if (backLeft != null && backLeft.Occupant != null)
                    {
                        // BackLeft is occupied, need space in BackRight
                        var backRight = player.FindSlot(BoardSlot.BackRight);
                        if (backRight != null && backRight.Occupant != null)
                        {
                            // All slots are full, can't do replacement
                            reason = "Board is full. No space for replacement.";
                            return false;
                        }
                    }
                }
            }

            if (handEntry.Definition.manaCost > player.Mana)
            {
                reason = $"Not enough mana (need {handEntry.Definition.manaCost}, have {player.Mana}).";
                return false;
            }

            // Units can play in any slot, but only attack from designated slots
            if (handEntry.Definition.cardType == CardType.Unit)
            {
                // Check board space for replacement
                var occupiedCount = player.Board.Count(x => x.Occupant != null);
                if (occupiedCount >= 3)
                {
                    reason = "Board is full. No space for more units.";
                    return false;
                }

                return true;
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
                Definition = handEntry.Definition,
                TurnsUntilCanAttack = handEntry.Definition.turnsUntilCanAttack
            };

            // Initialize skills
            InitializeCardSkills(cardRuntime);

            var boardSlot = player.Board.First(x => x.Slot == slot);

            // Handle replacement: if slot occupied, shift existing card back
            if (boardSlot.Occupant != null)
            {
                var displacedCard = boardSlot.Occupant;

                if (slot == BoardSlot.Front)
                {
                    var backLeftSlot = player.Board.First(x => x.Slot == BoardSlot.BackLeft);
                    if (backLeftSlot.Occupant == null)
                    {
                        backLeftSlot.Occupant = displacedCard;
                        displacedCard.CurrentSlot = BoardSlot.BackLeft;
                        _state.Logs.Add(new BattleLogEntry
                        {
                            type = BattleLogType.Info,
                            message = $"{displacedCard.DisplayName} moved to BackLeft (replacement)."
                        });
                    }
                    else
                    {
                        var backRightSlot = player.Board.First(x => x.Slot == BoardSlot.BackRight);
                        if (backRightSlot.Occupant == null)
                        {
                            var oldBackLeft = backLeftSlot.Occupant;
                            backRightSlot.Occupant = oldBackLeft;
                            oldBackLeft.CurrentSlot = BoardSlot.BackRight;
                            backLeftSlot.Occupant = displacedCard;
                            displacedCard.CurrentSlot = BoardSlot.BackLeft;
                            _state.Logs.Add(new BattleLogEntry
                            {
                                type = BattleLogType.Info,
                                message = $"{displacedCard.DisplayName} moved to BackLeft, {oldBackLeft.DisplayName} moved to BackRight (replacement chain)."
                            });
                        }
                    }
                }
                else if (slot == BoardSlot.BackLeft)
                {
                    var backRightSlot = player.Board.First(x => x.Slot == BoardSlot.BackRight);
                    if (backRightSlot.Occupant == null)
                    {
                        backRightSlot.Occupant = displacedCard;
                        displacedCard.CurrentSlot = BoardSlot.BackRight;
                        _state.Logs.Add(new BattleLogEntry
                        {
                            type = BattleLogType.Info,
                            message = $"{displacedCard.DisplayName} moved to BackRight (replacement)."
                        });
                    }
                }
            }

            boardSlot.Occupant = cardRuntime;

            _state.Logs.Add(new BattleLogEntry
            {
                type = BattleLogType.Summon,
                message = $"{cardRuntime.DisplayName} entered {slot}."
            });

            ResolveAbilities(cardRuntime, AbilityTrigger.OnPlay);
            return true;
        }

        private bool CanAttackFromPosition(CardRuntime attacker, BoardSlot slot)
        {
            if (attacker?.Definition == null)
                return true; // Default allow if not a unit

            if (attacker.Definition.cardType != CardType.Unit)
                return true; // Non-units can always attack

            var unitType = attacker.Definition.unitType;

            return unitType switch
            {
                UnitType.Melee => slot == BoardSlot.Front,
                UnitType.Ranged => slot == BoardSlot.BackLeft || slot == BoardSlot.BackRight,
                UnitType.Magic => slot == BoardSlot.BackLeft || slot == BoardSlot.BackRight,
                _ => true
            };
        }

        private void ApplyStraightLineTargetSelection(BattleContext context, int sourcePlayer, int targetPlayer, CardRuntime attacker, List<string> outTargets)
        {
            outTargets.Clear();

            var enemy = context.GetPlayerState(targetPlayer);
            if (enemy == null || attacker == null)
                return;

            // Try same slot as attacker
            var slotRuntime = enemy.FindSlot(attacker.CurrentSlot);
            if (slotRuntime?.Occupant != null)
            {
                outTargets.Add(slotRuntime.Occupant.RuntimeId);
                return;
            }

            // Fall back to Front
            var frontSlot = enemy.FindSlot(BoardSlot.Front);
            if (frontSlot?.Occupant != null)
            {
                outTargets.Add(frontSlot.Occupant.RuntimeId);
                return;
            }

            // No target - hero takes damage (outTargets stays empty)
        }

        private void InitializeCardSkills(CardRuntime card)
        {
            if (card?.Definition == null) return;

            // Assign effective attack selector based on unitType
            AssignEffectiveSelector(card);

            // Skill initialization moved to ISkillEffect + SkillRegistry system
            // No per-card initialization needed for new pipeline
        }

        private void AssignEffectiveSelector(CardRuntime card)
        {
            // Use custom selector if defined
            if (card.Definition?.defaultAttackTargetSelector != null)
            {
                card.EffectiveAttackSelector = card.Definition.defaultAttackTargetSelector;
                return;
            }

            // Otherwise, assign based on unitType
            var unitType = card.Definition?.unitType ?? UnitType.Melee;

            card.EffectiveAttackSelector = unitType switch
            {
                UnitType.Melee => ScriptableObject.CreateInstance<MeleeAttackSelector>(),
                UnitType.Ranged => ScriptableObject.CreateInstance<RangedAttackSelector>(),
                UnitType.Magic => ScriptableObject.CreateInstance<MagicAttackSelector>(),
                _ => ScriptableObject.CreateInstance<MeleeAttackSelector>()
            };
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

            if (active.Hand.Count > CardConstants.MaxHandSize)
            {
                return false;
            }

            _state.Logs.Add(new BattleLogEntry
            {
                type = BattleLogType.Turn,
                message = $"Player {playerIndex} ended turn."
            });

            ResolveTurnAbilities(playerIndex, AbilityTrigger.OnTurnEnd);

            // Use new BattlePhaseManager for clean attack execution
            var phaseManager = new Flippy.CardDuelMobile.Battle.Phases.BattlePhaseManager();
            phaseManager.ExecuteTurn(_context, playerIndex);

            if (!_state.DuelEnded)
            {
                _state.ActivePlayerIndex = 1 - playerIndex;
                _state.TurnNumber++;

                var nextPlayer = _state.GetPlayer(_state.ActivePlayerIndex);
                nextPlayer.MaxMana = Math.Min(_rules.maxMana, nextPlayer.MaxMana + _rules.manaPerTurn);
                nextPlayer.Mana = nextPlayer.MaxMana;

                if (_rules.drawAtTurnStart)
                {
                    if (!DrawCard(_state.ActivePlayerIndex))
                    {
                        _context.DamageHero(_state.ActivePlayerIndex, 1);
                        _state.Logs.Add(new BattleLogEntry
                        {
                            type = BattleLogType.Info,
                            message = $"Player {_state.ActivePlayerIndex} deck is empty! Takes 1 damage."
                        });
                    }
                }

                _context.ProcessStatusEffects(_state.ActivePlayerIndex);
                ResolveTurnAbilities(_state.ActivePlayerIndex, AbilityTrigger.OnTurnStart);
            }

            return true;
        }

        public bool DiscardCard(int playerIndex, string runtimeHandKey)
        {
            var player = _state.GetPlayer(playerIndex);
            if (player == null || player.Hand.Count <= CardConstants.MaxHandSize)
            {
                return false;
            }

            var handCard = player.Hand.FirstOrDefault(x => x.RuntimeHandKey == runtimeHandKey);
            if (handCard == null)
            {
                return false;
            }

            var cardRuntime = new CardRuntime
            {
                RuntimeId = Guid.NewGuid().ToString("N"),
                CardId = handCard.Definition.cardId,
                DisplayName = handCard.Definition.displayName,
                Definition = handCard.Definition
            };

            player.Hand.Remove(handCard);
            player.DeadCardPile.Add(cardRuntime);

            _state.Logs.Add(new BattleLogEntry
            {
                type = BattleLogType.Info,
                message = $"{cardRuntime.DisplayName} discarded to dead pile."
            });

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
                var random = new System.Random(seed == 0 ? 17 + playerIndex : seed);
                while (source.Count > 0)
                {
                    var pick = random.Next(source.Count);
                    var deckCard = source[pick];
                    if (deckCard?.card != null)
                    {
                        for (int i = 0; i < deckCard.quantity; i++)
                        {
                            player.Deck.Add(deckCard.card);
                        }
                    }
                    source.RemoveAt(pick);
                }
            }

            return player;
        }

        private bool DrawCard(int playerIndex)
        {
            var player = _state.GetPlayer(playerIndex);
            if (player == null || player.Deck.Count == 0)
            {
                return false;
            }

            var nextCard = player.Deck[0];
            player.Deck.RemoveAt(0);
            player.Hand.Add(new HandCardRuntime
            {
                RuntimeHandKey = Guid.NewGuid().ToString("N"),
                Definition = nextCard
            });

            _state.Logs.Add(new BattleLogEntry
            {
                type = BattleLogType.Info,
                message = $"Player {playerIndex} drew a card. Deck: {player.Deck.Count} remaining."
            });

            return true;
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
                deadCardPileCount = player.DeadCardPile.Count,
                hand = player.Hand.Select(card => new CardInHandDto
                {
                    runtimeCardKey = card.RuntimeHandKey,
                    cardId = card.Definition.cardId,
                    displayName = card.Definition.displayName,
                    manaCost = card.Definition.manaCost,
                    attack = card.Definition.attack,
                    health = card.Definition.health,
                    isUnit = card.Definition.cardType == CardType.Unit,
                    unitType = (int)card.Definition.unitType
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
                        slot = slot.Occupant.CurrentSlot,
                        canAttack = DetermineCanAttack(slot.Occupant),
                        unitType = slot.Occupant.Definition != null ? (int)slot.Occupant.Definition.unitType : 0,
                        turnsUntilCanAttack = slot.Occupant.TurnsUntilCanAttack
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

            // Validate card can attack from current position based on unitType
            if (!CanAttackFromPosition(attacker, slot.Slot))
            {
                _state.Logs.Add(new BattleLogEntry
                {
                    type = BattleLogType.Attack,
                    message = $"{attacker.DisplayName} cannot attack from {slot.Slot}."
                });
                return;
            }

            ResolveAbilities(attacker, AbilityTrigger.OnBattlePhase);

            // Use effective attack selector (based on unitType or custom)
            var targetSelector = attacker.EffectiveAttackSelector;
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
            // LastStand bonus damage handled via ISkillEffect in SkillPipeline

            _targetBuffer.Clear();

            if (targetSelector == null)
            {
                // Use StraightLineTargetSelector as default
                ApplyStraightLineTargetSelection(_context, sourcePlayerIndex, defenderIndex, attacker, _targetBuffer);
            }
            else
            {
                targetSelector.SelectTargets(
                    _context,
                    new TargetSelectionRequest(sourcePlayerIndex, defenderIndex, attacker.RuntimeId, attacker.CurrentSlot),
                    _targetBuffer);
            }

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

        private bool DetermineCanAttack(CardRuntime card)
        {
            if (card == null || card.Definition == null)
                return false;

            if (card.Definition.cardType != CardType.Unit)
                return false;

            // Check if card has waited long enough to attack
            if (card.TurnsUntilCanAttack > 0)
                return false;

            var unitType = card.Definition.unitType;

            if (card.CurrentSlot == BoardSlot.Front)
                return unitType == UnitType.Melee;

            if (card.CurrentSlot == BoardSlot.BackLeft || card.CurrentSlot == BoardSlot.BackRight)
                return unitType == UnitType.Ranged || unitType == UnitType.Magic;

            return false;
        }
    }
}
