using System;
using System.Collections.Generic;
using System.Linq;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Data;

namespace Flippy.CardDuelMobile.Battle
{
    [Serializable]
    public sealed class CardRuntime
    {
        public string RuntimeId;
        public string CardId;
        public string DisplayName;
        public int OwnerIndex;
        public int Attack;
        public int CurrentHealth;
        public int MaxHealth;
        public int Armor;
        public BoardSlot CurrentSlot;
        public CardDefinition Definition;
        public bool IsDead => CurrentHealth <= 0;

        // Status effects
        public int PoisonStacks;
        public bool Stunned;
        public bool HasShield;
        public int EnrageBonus;
    }

    [Serializable]
    public sealed class BoardSlotRuntime
    {
        public BoardSlot Slot;
        public CardRuntime Occupant;
    }

    [Serializable]
    public sealed class HandCardRuntime
    {
        public string RuntimeHandKey;
        public CardDefinition Definition;
    }

    [Serializable]
    public sealed class DuelPlayerState
    {
        public int PlayerIndex;
        public int HeroHealth;
        public int Mana;
        public int MaxMana;
        public readonly List<CardDefinition> Deck = new();
        public readonly List<HandCardRuntime> Hand = new();
        public readonly List<BoardSlotRuntime> Board = new()
        {
            new BoardSlotRuntime{ Slot = BoardSlot.Front },
            new BoardSlotRuntime{ Slot = BoardSlot.BackLeft },
            new BoardSlotRuntime{ Slot = BoardSlot.BackRight }
        };

        /// <summary>
        /// Busca slot por tipo.
        /// </summary>
        public BoardSlotRuntime FindSlot(BoardSlot slot)
        {
            return Board.FirstOrDefault(x => x.Slot == slot);
        }

        /// <summary>
        /// Busca ocupante por slot.
        /// </summary>
        public CardRuntime FindOccupant(BoardSlot slot)
        {
            return FindSlot(slot)?.Occupant;
        }

        /// <summary>
        /// Determina si existe espacio libre en ese slot.
        /// </summary>
        public bool IsSlotEmpty(BoardSlot slot)
        {
            return FindOccupant(slot) == null;
        }

        /// <summary>
        /// Repositions cards when Front dies.
        /// BackLeft → Front, BackRight → BackLeft
        /// </summary>
        public void Reposition()
        {
            var frontSlot = FindSlot(BoardSlot.Front);
            var backLeftSlot = FindSlot(BoardSlot.BackLeft);
            var backRightSlot = FindSlot(BoardSlot.BackRight);

            if (frontSlot == null || backLeftSlot == null || backRightSlot == null)
                return;

            // If Front is empty, shift back cards forward
            if (frontSlot.Occupant == null)
            {
                if (backLeftSlot.Occupant != null)
                {
                    frontSlot.Occupant = backLeftSlot.Occupant;
                    frontSlot.Occupant.CurrentSlot = BoardSlot.Front;

                    if (backRightSlot.Occupant != null)
                    {
                        backLeftSlot.Occupant = backRightSlot.Occupant;
                        backLeftSlot.Occupant.CurrentSlot = BoardSlot.BackLeft;
                        backRightSlot.Occupant = null;
                    }
                    else
                    {
                        backLeftSlot.Occupant = null;
                    }
                }
                else if (backRightSlot.Occupant != null)
                {
                    frontSlot.Occupant = backRightSlot.Occupant;
                    frontSlot.Occupant.CurrentSlot = BoardSlot.Front;
                    backRightSlot.Occupant = null;
                }
            }
        }
    }

    [Serializable]
    public sealed class BattleLogEntry
    {
        public BattleLogType type;
        public string message;
    }

    [Serializable]
    public sealed class DuelState
    {
        public DuelPlayerState[] Players = new DuelPlayerState[2];
        public int ActivePlayerIndex;
        public int TurnNumber;
        public bool DuelEnded;
        public DuelEndReason EndReason;
        public readonly List<BattleLogEntry> Logs = new();

        /// <summary>
        /// Devuelve estado de jugador por índice.
        /// </summary>
        public DuelPlayerState GetPlayer(int index)
        {
            if (Players == null || index < 0 || index >= Players.Length)
            {
                return null;
            }

            return Players[index];
        }
    }

    public readonly struct TargetSelectionRequest
    {
        public readonly int SourcePlayer;
        public readonly int TargetPlayer;
        public readonly string SourceRuntimeId;
        public readonly BoardSlot SourceSlot;

        public TargetSelectionRequest(int sourcePlayer, int targetPlayer, string sourceRuntimeId, BoardSlot sourceSlot)
        {
            SourcePlayer = sourcePlayer;
            TargetPlayer = targetPlayer;
            SourceRuntimeId = sourceRuntimeId;
            SourceSlot = sourceSlot;
        }
    }

    public readonly struct EffectExecution
    {
        public readonly int SourcePlayer;
        public readonly int TargetPlayer;
        public readonly string SourceRuntimeId;
        public readonly string TargetRuntimeId;

        public EffectExecution(int sourcePlayer, int targetPlayer, string sourceRuntimeId, string targetRuntimeId)
        {
            SourcePlayer = sourcePlayer;
            TargetPlayer = targetPlayer;
            SourceRuntimeId = sourceRuntimeId;
            TargetRuntimeId = targetRuntimeId;
        }
    }
}
