using System;
using System.Collections.Generic;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Data;
using Flippy.CardDuelMobile.Networking;

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
        /// Server-authoritative: decides a move purely from the server MatchSnapshot for the
        /// AI seat. Used when single-player runs as a real server match (no client DuelRuntime).
        /// </summary>
        public AiMove BuildMove(MatchSnapshot snapshot, int aiSeatIndex, AiDifficulty difficulty, HashSet<string> excludeKeys = null)
        {
            _moves.Clear();

            if (snapshot == null || snapshot.duelEnded || snapshot.activeSeatIndex != aiSeatIndex || snapshot.seats == null)
            {
                return new AiMove(true, string.Empty, BoardSlot.Front);
            }

            SeatSnapshot seat = null;
            foreach (var s in snapshot.seats)
            {
                if (s != null && s.seatIndex == aiSeatIndex)
                {
                    seat = s;
                    break;
                }
            }

            if (seat?.hand == null)
            {
                return new AiMove(true, string.Empty, BoardSlot.Front);
            }

            var frontOccupied = IsSlotOccupied(seat, BoardSlot.Front);
            var backLeftOccupied = IsSlotOccupied(seat, BoardSlot.BackLeft);
            var backRightOccupied = IsSlotOccupied(seat, BoardSlot.BackRight);
            var occupiedCount = (frontOccupied ? 1 : 0) + (backLeftOccupied ? 1 : 0) + (backRightOccupied ? 1 : 0);

            // A full board has no room to shift, so no unit can be placed this turn.
            if (occupiedCount >= 3)
            {
                return new AiMove(true, string.Empty, BoardSlot.Front);
            }

            foreach (var card in seat.hand)
            {
                if (card == null || card.manaCost > seat.mana)
                {
                    continue;
                }

                // Skip cards that already failed to play this turn (non-units needing a target,
                // illegal slot, etc.) so the AI moves on to a playable card instead of retrying.
                if (excludeKeys != null && !string.IsNullOrEmpty(card.runtimeHandKey) && excludeKeys.Contains(card.runtimeHandKey))
                {
                    continue;
                }

                // Legal target slots: the server's can-play flags combined with the board fill order
                // (back slots require the slot in front of them to be occupied). Server is the final judge.
                // unitType -1 = non-unit (spell/equipment/utility) which the simple AI can't target;
                // 0/1/2 = a real unit. The server's AllowedRow placement restriction is disabled, so
                // ANY unit can legally go into ANY open slot (subject only to the Front->Left->Right
                // fill order). Relax the can-be-played flags accordingly so a hand of e.g. only ranged
                // units still places a Front unit on an empty board instead of stalling all game.
                var isUnit = card.unitType >= 0;

                if (card.canBePlayedInFront || (isUnit && !frontOccupied))
                {
                    _moves.Add(new ScoredMove(card.runtimeHandKey, BoardSlot.Front, ScoreSnapshotCard(card, BoardSlot.Front, difficulty)));
                }
                if ((card.canBePlayedInBack || isUnit) && frontOccupied)
                {
                    _moves.Add(new ScoredMove(card.runtimeHandKey, BoardSlot.BackLeft, ScoreSnapshotCard(card, BoardSlot.BackLeft, difficulty)));
                    if (backLeftOccupied)
                    {
                        _moves.Add(new ScoredMove(card.runtimeHandKey, BoardSlot.BackRight, ScoreSnapshotCard(card, BoardSlot.BackRight, difficulty)));
                    }
                }
            }

            if (_moves.Count == 0)
            {
                return new AiMove(true, string.Empty, BoardSlot.Front);
            }

            return difficulty == AiDifficulty.Easy ? BuildEasyMove() : BuildMediumMove();
        }

        private static bool IsSlotOccupied(SeatSnapshot seat, BoardSlot slot)
        {
            if (seat?.board == null)
            {
                return false;
            }
            foreach (var b in seat.board)
            {
                if (b != null && b.slot == (int)slot)
                {
                    return b.occupied;
                }
            }
            return false;
        }

        private float ScoreSnapshotCard(HandCardSnapshot card, BoardSlot slot, AiDifficulty difficulty)
        {
            var score = 0f;
            score += card.attack * 2.2f;
            score += card.health * 1.3f;
            score += card.armor * 0.8f;
            score += card.manaCost * 0.45f;

            var isMelee = card.unitType == (int)UnitType.Melee;
            if (slot == BoardSlot.Front)
            {
                score += isMelee ? 2.5f : 0.4f;
                score += card.health * 0.6f;
            }
            else
            {
                score += !isMelee ? 2.5f : 0.4f;
                score += card.attack * 0.5f;
            }

            if (card.abilities != null)
            {
                score += card.abilities.Length * 0.7f;
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
    }
}
