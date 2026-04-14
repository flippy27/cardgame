using System;
using System.Collections.Generic;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.SinglePlayer
{
    /// <summary>
    /// IA base para jugar cartas y terminar turno en modo local.
    /// Está hecha para ser simple de entender y fácil de reemplazar por otra más avanzada.
    /// </summary>
    public sealed class SimpleCardAiAgent
    {
        public readonly struct AiMove
        {
            public readonly bool IsEndTurn;
            public readonly string RuntimeCardKey;
            public readonly BoardSlot Slot;

            public AiMove(bool isEndTurn, string runtimeCardKey, BoardSlot slot)
            {
                IsEndTurn = isEndTurn;
                RuntimeCardKey = runtimeCardKey;
                Slot = slot;
            }
        }

        private readonly System.Random _random = new();
        private readonly List<BoardSlot> _slotBuffer = new();
        private readonly List<ScoredMove> _moves = new();

        private readonly struct ScoredMove
        {
            public readonly string RuntimeCardKey;
            public readonly BoardSlot Slot;
            public readonly float Score;

            public ScoredMove(string runtimeCardKey, BoardSlot slot, float score)
            {
                RuntimeCardKey = runtimeCardKey;
                Slot = slot;
                Score = score;
            }
        }

        /// <summary>
        /// Calcula el siguiente movimiento. Si no hay jugadas razonables, termina el turno.
        /// </summary>
        public AiMove BuildMove(DuelRuntime runtime, int aiPlayerIndex, AiDifficulty difficulty)
        {
            _moves.Clear();

            if (runtime == null || runtime.State.DuelEnded || runtime.State.ActivePlayerIndex != aiPlayerIndex)
            {
                return new AiMove(true, string.Empty, BoardSlot.Front);
            }

            var player = runtime.State.GetPlayer(aiPlayerIndex);
            if (player == null)
            {
                return new AiMove(true, string.Empty, BoardSlot.Front);
            }

            foreach (var handCard in player.Hand)
            {
                if (handCard?.Definition == null)
                {
                    continue;
                }

                runtime.GetLegalPlaySlots(aiPlayerIndex, handCard.RuntimeHandKey, _slotBuffer);
                foreach (var slot in _slotBuffer)
                {
                    _moves.Add(new ScoredMove(
                        handCard.RuntimeHandKey,
                        slot,
                        EvaluateMove(runtime, aiPlayerIndex, handCard, slot, difficulty)));
                }
            }

            if (_moves.Count == 0)
            {
                return new AiMove(true, string.Empty, BoardSlot.Front);
            }

            return difficulty switch
            {
                AiDifficulty.Easy => BuildEasyMove(),
                AiDifficulty.Medium => BuildMediumMove(),
                _ => BuildHardMove(runtime, aiPlayerIndex)
            };
        }

        private AiMove BuildEasyMove()
        {
            var index = _random.Next(0, _moves.Count);
            var move = _moves[index];
            return new AiMove(false, move.RuntimeCardKey, move.Slot);
        }

        private AiMove BuildMediumMove()
        {
            var bestScore = float.MinValue;
            foreach (var move in _moves)
            {
                if (move.Score > bestScore)
                {
                    bestScore = move.Score;
                }
            }

            var candidates = new List<ScoredMove>();
            foreach (var move in _moves)
            {
                if (move.Score >= bestScore - 1.25f)
                {
                    candidates.Add(move);
                }
            }

            var selected = candidates[_random.Next(0, candidates.Count)];
            return new AiMove(false, selected.RuntimeCardKey, selected.Slot);
        }

        private AiMove BuildHardMove(DuelRuntime runtime, int aiPlayerIndex)
        {
            ScoredMove selected = default;
            var hasSelection = false;
            var enemy = runtime.State.GetPlayer(1 - aiPlayerIndex);

            foreach (var move in _moves)
            {
                var score = move.Score;

                if (enemy != null)
                {
                    var enemyFrontOccupied = !enemy.IsSlotEmpty(BoardSlot.Front);
                    var enemyBackOccupied = !enemy.IsSlotEmpty(BoardSlot.BackLeft) || !enemy.IsSlotEmpty(BoardSlot.BackRight);

                    if (move.Slot == BoardSlot.Front && enemyFrontOccupied)
                    {
                        score += 2.2f;
                    }

                    if ((move.Slot == BoardSlot.BackLeft || move.Slot == BoardSlot.BackRight) && enemyBackOccupied)
                    {
                        score += 1.8f;
                    }
                }

                if (!hasSelection || score > selected.Score)
                {
                    selected = new ScoredMove(move.RuntimeCardKey, move.Slot, score);
                    hasSelection = true;
                }
            }

            if (!hasSelection)
            {
                return new AiMove(true, string.Empty, BoardSlot.Front);
            }

            return new AiMove(false, selected.RuntimeCardKey, selected.Slot);
        }

        private float EvaluateMove(DuelRuntime runtime, int aiPlayerIndex, HandCardRuntime handCard, BoardSlot slot, AiDifficulty difficulty)
        {
            var definition = handCard.Definition;
            var player = runtime.State.GetPlayer(aiPlayerIndex);
            if (definition == null || player == null)
            {
                return float.MinValue;
            }

            var score = 0f;
            score += definition.attack * 2.2f;
            score += definition.health * 1.3f;
            score += definition.armor * 0.8f;
            score += definition.manaCost * 0.45f;

            if (slot == BoardSlot.Front)
            {
                score += definition.canBePlayedInFront && !definition.canBePlayedInBack ? 2.5f : 0.4f;
                score += definition.health * 0.6f;
            }
            else
            {
                score += definition.canBePlayedInBack && !definition.canBePlayedInFront ? 2.5f : 0.4f;
                score += definition.attack * 0.5f;
            }

            if (definition.abilities != null)
            {
                score += definition.abilities.Length * 0.7f;
            }

            var remainingMana = player.Mana - definition.manaCost;
            if (remainingMana == 0)
            {
                score += 1.2f;
            }
            else if (remainingMana < 0)
            {
                score -= 100f;
            }

            if (difficulty == AiDifficulty.Easy)
            {
                score += (float)_random.NextDouble() * 3f;
            }
            else if (difficulty == AiDifficulty.Medium)
            {
                score += (float)_random.NextDouble() * 1.4f;
            }

            return score;
        }
    }
}
