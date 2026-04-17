using System;

namespace Flippy.CardDuelMobile.Core
{
    public enum MatchState
    {
        Idle = 0,
        WaitingForPlayer2 = 1,
        BothPlayersReady = 2,
        InProgress = 3,
        PlayerDisconnected = 4,
        Completed = 5
    }

    [Serializable]
    public class MatchInfo
    {
        public string matchId;
        public string player1Id;
        public string player2Id;
        public MatchState state;
        public bool player1Ready;
        public bool player2Ready;
    }

    public static class MatchStateMachine
    {
        public static event Action<MatchState> OnStateChanged;
        public static event Action<string> OnPlayerReady;
        public static event Action<string> OnPlayerDisconnected;
        public static event Action<string> OnCanReconnect;

        private static MatchInfo _currentMatch;

        public static MatchInfo CurrentMatch => _currentMatch;
        public static MatchState CurrentState => _currentMatch?.state ?? MatchState.Idle;

        public static void InitializeMatch(string matchId, string player1Id, string player2Id)
        {
            _currentMatch = new MatchInfo
            {
                matchId = matchId,
                player1Id = player1Id,
                player2Id = player2Id,
                state = MatchState.WaitingForPlayer2,
                player1Ready = false,
                player2Ready = false
            };
            GameLogger.Info("Match", $"Init: {matchId}");
            ChangeState(MatchState.WaitingForPlayer2);
        }

        public static void SetPlayerReady(string playerId, bool ready)
        {
            if (_currentMatch == null) return;

            if (_currentMatch.player1Id == playerId)
                _currentMatch.player1Ready = ready;
            else if (_currentMatch.player2Id == playerId)
                _currentMatch.player2Ready = ready;

            GameLogger.Info("Match", $"{playerId} ready: {ready}");
            OnPlayerReady?.Invoke(playerId);

            if (_currentMatch.player1Ready && _currentMatch.player2Ready)
            {
                ChangeState(MatchState.BothPlayersReady);
            }
        }

        public static void StartMatch()
        {
            if (_currentMatch?.state != MatchState.BothPlayersReady) return;
            ChangeState(MatchState.InProgress);
            GameLogger.Info("Match", "Started");
        }

        public static void PlayerDisconnected(string playerId)
        {
            if (_currentMatch == null) return;
            ChangeState(MatchState.PlayerDisconnected);
            OnPlayerDisconnected?.Invoke(playerId);
            GameLogger.Warning("Match", $"{playerId} disconnected");
        }

        public static void AttemptReconnect(string playerId)
        {
            if (_currentMatch == null) return;
            if (_currentMatch.state != MatchState.PlayerDisconnected) return;

            ChangeState(MatchState.InProgress);
            OnCanReconnect?.Invoke(_currentMatch.matchId);
            GameLogger.Info("Match", $"{playerId} reconnecting");
        }

        public static void EndMatch()
        {
            if (_currentMatch == null) return;
            ChangeState(MatchState.Completed);
            GameLogger.Info("Match", "Ended");
            _currentMatch = null;
        }

        public static bool IsInMatch() => _currentMatch != null &&
            (_currentMatch.state == MatchState.InProgress ||
             _currentMatch.state == MatchState.BothPlayersReady ||
             _currentMatch.state == MatchState.PlayerDisconnected);

        public static bool CanInteract() => _currentMatch != null &&
            (_currentMatch.state == MatchState.InProgress ||
             _currentMatch.state == MatchState.BothPlayersReady);

        private static void ChangeState(MatchState newState)
        {
            if (_currentMatch != null)
                _currentMatch.state = newState;
            OnStateChanged?.Invoke(newState);
            GameLogger.Info("Match", $"State → {newState}");
        }
    }
}
