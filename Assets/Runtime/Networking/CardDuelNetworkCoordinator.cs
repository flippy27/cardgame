using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Data;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Coordinador autoritativo de la partida.
    /// En prototipo usa host authority con NGO, pero con flujo real de seats / ready / abandono / rejoin.
    /// </summary>
    public sealed class CardDuelNetworkCoordinator : NetworkBehaviour
    {
        [Header("Gameplay")]
        public DuelRulesProfile rulesProfile;
        public DeckDefinition deckPlayerA;
        public DeckDefinition deckPlayerB;

        [Header("Match Flow")]
        public float disconnectGraceSeconds = 20f;
        public bool requireBothPlayersReady = true;

        private DuelRuntime _runtime;
        private MatchPhase _matchPhase = MatchPhase.WaitingForPlayers;
        private int _authoritativeSeed;
        private int _winnerPlayerIndex = -1;
        private readonly SeatState[] _seats = { new SeatState(0), new SeatState(1) };
        private bool _callbacksRegistered;

        public static CardDuelNetworkCoordinator Instance { get; private set; }

        private sealed class SeatState
        {
            public SeatState(int seatIndex)
            {
                SeatIndex = seatIndex;
            }

            public int SeatIndex { get; }
            public ulong ClientId;
            public bool HasClient;
            public bool IsConnected;
            public bool IsReady;
            public bool ExplicitLeave;
            public float DisconnectAt;
            public string AuthPlayerId = string.Empty;
            public string SubmittedDeckHash = string.Empty;
            public string ExpectedDeckHash = string.Empty;
            public bool DeckValidated;
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            if (!IsServer)
            {
                return;
            }

            TickDisconnectGrace();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this)
            {
                Instance = null;
            }

            UnregisterCallbacks();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                RegisterCallbacks();
                BootstrapConnectedClients();
                RefreshPhaseWithoutStarting();
                BroadcastSnapshot();
            }

            if (IsClient)
            {
                TryRegisterLocalClient();
            }
        }

        public void SubmitReady(bool isReady)
        {
            if (!IsClient)
            {
                return;
            }

            var authId = ResolveLocalAuthId();
            RequestSetReadyServerRpc(isReady, authId, ResolveLocalDeckHash());
        }

        public void SubmitLeaveIntent()
        {
            if (!IsClient)
            {
                return;
            }

            NotifyLeaveIntentServerRpc(ResolveLocalAuthId());
        }

        public string GetLocalDeckHash()
        {
            return ResolveLocalDeckHash();
        }

        [ServerRpc(RequireOwnership = false)]
        public void RegisterPlayerServerRpc(string authPlayerId, string submittedDeckHash, ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            GameLogger.Info("Net", $"RegisterPlayerServerRpc: clientId={clientId}, authPlayerId={authPlayerId}, deckHash={submittedDeckHash}");

            if (string.IsNullOrWhiteSpace(authPlayerId))
            {
                GameLogger.Warning("Net", "RegisterPlayer rejected: empty authPlayerId");
                return;
            }

            var seat = BindSeat(clientId, authPlayerId, submittedDeckHash);
            if (seat == null)
            {
                GameLogger.Warning("Net", "RegisterPlayer rejected: no free seat");
                return;
            }

            seat.IsConnected = true;
            seat.ExplicitLeave = false;
            seat.DisconnectAt = -1f;
            GameLogger.Info("Net", $"RegisterPlayer success: seat={seat.SeatIndex}, connected={_seats.Count(s => s.HasClient && s.IsConnected)}/2");
            RefreshPhaseWithoutStarting();
            BroadcastSnapshot();
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestSetReadyServerRpc(bool isReady, string authPlayerId, string submittedDeckHash, ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            GameLogger.Info("Net", $"RequestSetReadyServerRpc: clientId={clientId}, authPlayerId={authPlayerId}, isReady={isReady}");

            var seat = BindSeat(clientId, authPlayerId, submittedDeckHash);
            if (seat == null)
            {
                GameLogger.Warning("Net", "RequestSetReady rejected: no seat");
                return;
            }

            seat.IsReady = isReady && seat.DeckValidated;
            seat.SubmittedDeckHash = submittedDeckHash ?? string.Empty;
            GameLogger.Info("Net", $"Player {seat.SeatIndex}: ready={seat.IsReady}, validated={seat.DeckValidated}");

            if (!seat.DeckValidated)
            {
                AppendLog($"Seat {seat.SeatIndex + 1} failed deck validation.");
            }

            GameLogger.Info("Net", $"CanStartMatch: {CanStartMatch()}, bothReady={_seats[0].IsReady && _seats[1].IsReady}");
            if (CanStartMatch())
            {
                GameLogger.Info("Net", "Starting match");
                StartMatch();
            }
            else
            {
                RefreshPhaseWithoutStarting();
                BroadcastSnapshot();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void NotifyLeaveIntentServerRpc(string authPlayerId, ServerRpcParams rpcParams = default)
        {
            var seat = FindSeatByClientId(rpcParams.Receive.SenderClientId) ?? FindSeatByAuthId(authPlayerId);
            if (seat == null)
            {
                return;
            }

            seat.ExplicitLeave = true;
            seat.IsConnected = false;
            seat.IsReady = false;
            seat.DisconnectAt = Time.unscaledTime;

            if (_matchPhase == MatchPhase.InProgress)
            {
                FinalizeByAbandon(seat.SeatIndex, immediate: true);
                return;
            }

            ClearSeatIfSafe(seat);
            RefreshPhaseWithoutStarting();
            BroadcastSnapshot();
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestPlayCardServerRpc(string runtimeCardKey, int slotIndex, ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            var playerIndex = ResolvePlayerIndex(clientId);
            GameLogger.Info("Net", $"RequestPlayCardServerRpc: clientId={clientId}, playerIndex={playerIndex}, card={runtimeCardKey}, slot={slotIndex}");

            if (_runtime == null || _matchPhase != MatchPhase.InProgress)
            {
                GameLogger.Warning("Net", $"PlayCard rejected: runtime={_runtime != null}, phase={_matchPhase}");
                return;
            }

            if (playerIndex < 0)
            {
                GameLogger.Warning("Net", $"PlayCard rejected: invalid playerIndex");
                return;
            }

            if (_runtime.TryPlayCard(playerIndex, runtimeCardKey, (BoardSlot)slotIndex))
            {
                GameLogger.Info("Net", $"PlayCard succeeded");

                // Log action to API (non-blocking)
                if (MatchActionService.Instance != null)
                {
                    var card = _runtime.State.GetPlayer(playerIndex).Hand.Find(c => c.RuntimeHandKey == runtimeCardKey);
                    var manaCost = card?.Definition?.manaCost ?? 0;
                    MatchActionService.Instance.LogCardPlay(card?.Definition?.cardId ?? "unknown", (int)slotIndex, manaCost);
                }

                BroadcastSnapshot();
            }
            else
            {
                GameLogger.Warning("Net", $"PlayCard failed in TryPlayCard");
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestEndTurnServerRpc(ServerRpcParams rpcParams = default)
        {
            var clientId = rpcParams.Receive.SenderClientId;
            var playerIndex = ResolvePlayerIndex(clientId);
            GameLogger.Info("Net", $"RequestEndTurnServerRpc: clientId={clientId}, playerIndex={playerIndex}, phase={_matchPhase}");

            if (_runtime == null)
            {
                GameLogger.Warning("Net", $"EndTurn rejected: runtime is null");
                return;
            }

            if (_matchPhase != MatchPhase.InProgress)
            {
                GameLogger.Warning("Net", $"EndTurn rejected: not InProgress, phase={_matchPhase}");
                return;
            }

            if (playerIndex < 0)
            {
                GameLogger.Warning("Net", $"EndTurn rejected: invalid playerIndex");
                return;
            }

            GameLogger.Info("Net", $"EndTurn attempting for player {playerIndex}");
            if (_runtime.TryEndTurn(playerIndex))
            {
                GameLogger.Info("Net", $"EndTurn succeeded, duelEnded={_runtime.State.DuelEnded}");

                // Log action to API (non-blocking)
                if (MatchActionService.Instance != null)
                {
                    MatchActionService.Instance.LogEndTurn(_runtime.State.TurnNumber);
                }

                if (_runtime.State.DuelEnded)
                {
                    _matchPhase = MatchPhase.Completed;
                    _winnerPlayerIndex = ResolveWinnerFromRuntime();
                    var p1HP = _runtime.State.Players[0].HeroHealth;
                    var p2HP = _runtime.State.Players[1].HeroHealth;
                    GameLogger.Info("Net", $"🏁 DUEL ENDED: winner={_winnerPlayerIndex}, P1={p1HP}HP, P2={p2HP}HP, reason={_runtime.State.EndReason}");
                }

                BroadcastSnapshot();
            }
            else
            {
                GameLogger.Warning("Net", $"EndTurn failed in TryEndTurn");
            }
        }

        private void RegisterCallbacks()
        {
            if (_callbacksRegistered || NetworkManager == null)
            {
                return;
            }

            NetworkManager.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            _callbacksRegistered = true;
        }

        private void UnregisterCallbacks()
        {
            if (!_callbacksRegistered || NetworkManager == null)
            {
                return;
            }

            NetworkManager.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            _callbacksRegistered = false;
        }

        private void BootstrapConnectedClients()
        {
            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                var seat = FindSeatByClientId(clientId);
                if (seat != null)
                {
                    seat.IsConnected = true;
                    continue;
                }

                var firstEmpty = FindFirstFreeSeat();
                if (firstEmpty == null)
                {
                    continue;
                }

                firstEmpty.ClientId = clientId;
                firstEmpty.HasClient = true;
                firstEmpty.IsConnected = true;
                firstEmpty.ExpectedDeckHash = ComputeExpectedDeckHash(firstEmpty.SeatIndex);
            }
        }

        private void HandleClientConnected(ulong clientId)
        {
            var seat = FindSeatByClientId(clientId);
            if (seat == null)
            {
                seat = FindFirstFreeSeat();
                if (seat == null)
                {
                    return;
                }

                seat.ClientId = clientId;
                seat.HasClient = true;
                seat.ExpectedDeckHash = ComputeExpectedDeckHash(seat.SeatIndex);
            }

            seat.IsConnected = true;
            seat.ExplicitLeave = false;
            seat.DisconnectAt = -1f;

            RefreshPhaseWithoutStarting();
            BroadcastSnapshot();
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            var seat = FindSeatByClientId(clientId);
            if (seat == null)
            {
                return;
            }

            seat.IsConnected = false;
            seat.IsReady = false;
            seat.DisconnectAt = Time.unscaledTime;

            if (_matchPhase == MatchPhase.InProgress)
            {
                AppendLog($"Seat {seat.SeatIndex + 1} disconnected. Waiting {disconnectGraceSeconds:0}s for reconnect.");
            }
            else
            {
                ClearSeatIfSafe(seat);
                RefreshPhaseWithoutStarting();
            }

            BroadcastSnapshot();
        }

        private void TickDisconnectGrace()
        {
            if (_matchPhase != MatchPhase.InProgress)
            {
                return;
            }

            foreach (var seat in _seats)
            {
                if (!seat.HasClient || seat.IsConnected || seat.DisconnectAt < 0f)
                {
                    continue;
                }

                if (Time.unscaledTime - seat.DisconnectAt >= disconnectGraceSeconds)
                {
                    FinalizeByAbandon(seat.SeatIndex, immediate: false);
                    return;
                }
            }
        }

        private SeatState BindSeat(ulong clientId, string authPlayerId, string submittedDeckHash)
        {
            var seat = FindSeatByAuthId(authPlayerId);
            if (seat == null)
            {
                seat = FindSeatByClientId(clientId);
            }

            if (seat == null)
            {
                seat = FindFirstFreeSeat();
            }

            if (seat == null)
            {
                return null;
            }

            seat.ClientId = clientId;
            seat.HasClient = true;
            seat.IsConnected = true;
            seat.AuthPlayerId = authPlayerId ?? string.Empty;
            seat.SubmittedDeckHash = submittedDeckHash ?? string.Empty;
            seat.DeckValidated = true;

            return seat;
        }

        private bool CanStartMatch()
        {
            if (!requireBothPlayersReady)
            {
                return _seats.All(seat => seat.HasClient && seat.IsConnected);
            }

            return _matchPhase != MatchPhase.InProgress
                && _seats.All(seat => seat.HasClient && seat.IsConnected && seat.IsReady && seat.DeckValidated);
        }

        private void StartMatch()
        {
            GameLogger.Info("Match", "StartMatch called");
            _matchPhase = MatchPhase.Starting;
            _winnerPlayerIndex = -1;
            _authoritativeSeed = Mathf.Abs(Guid.NewGuid().GetHashCode());
            _runtime = new DuelRuntime(rulesProfile);
            _runtime.StartGame(deckPlayerA, deckPlayerB, _authoritativeSeed);

            foreach (var seat in _seats)
            {
                seat.IsReady = false;
            }

            _matchPhase = MatchPhase.InProgress;
            GameLogger.Info("Match", "Match started, broadcasting snapshot");
            AppendLog("Both players ready. Match started.");

            // Initialize action logging and checkpointing (non-blocking)
            var matchId = $"match-{Guid.NewGuid()}"; // TODO: use actual match ID from server
            if (MatchActionService.Instance != null)
            {
                var playerId = _seats[0].AuthPlayerId; // Local player
                MatchActionService.Instance.InitializeMatch(matchId, playerId);
            }
            if (MatchCheckpointService.Instance != null)
            {
                var playerId = _seats[0].AuthPlayerId;
                MatchCheckpointService.Instance.InitializeMatch(matchId, playerId, _runtime, localPlayerIndex: 0);
            }

            BroadcastSnapshot();
        }

        private void FinalizeByAbandon(int disconnectedSeatIndex, bool immediate)
        {
            _matchPhase = MatchPhase.Completed;
            _winnerPlayerIndex = 1 - disconnectedSeatIndex;

            if (_runtime == null)
            {
                _runtime = new DuelRuntime(rulesProfile);
                _runtime.StartGame(deckPlayerA, deckPlayerB, _authoritativeSeed == 0 ? 1 : _authoritativeSeed);
            }

            _runtime.State.DuelEnded = true;
            _runtime.State.EndReason = DuelEndReason.OpponentDisconnected;
            AppendLog(immediate
                ? $"Seat {disconnectedSeatIndex + 1} left the match. Victory by abandonment."
                : $"Seat {disconnectedSeatIndex + 1} did not reconnect in time. Victory by abandonment.");

            BroadcastSnapshot();
        }

        private void RefreshPhaseWithoutStarting()
        {
            if (_matchPhase == MatchPhase.InProgress || _matchPhase == MatchPhase.Completed)
            {
                return;
            }

            var connectedCount = _seats.Count(seat => seat.HasClient && seat.IsConnected);
            _matchPhase = connectedCount < 2 ? MatchPhase.WaitingForPlayers : MatchPhase.WaitingForReady;
        }

        private void BroadcastSnapshot()
        {
            if (NetworkManager == null)
            {
                return;
            }

            foreach (var seat in _seats.Where(s => s.HasClient))
            {
                var snapshot = BuildSnapshotForSeat(seat.SeatIndex);
                var payload = JsonUtility.ToJson(snapshot);

                PushSnapshotClientRpc(payload, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new[] { seat.ClientId }
                    }
                });
            }
        }

        private DuelSnapshotDto BuildSnapshotForSeat(int localSeatIndex)
        {
            var snapshot = _runtime != null
                ? _runtime.CreateSnapshot(localSeatIndex)
                : CreateEmptySnapshot(localSeatIndex);

            var localSeat = _seats[localSeatIndex];
            var remoteSeat = _seats[1 - localSeatIndex];

            snapshot.matchPhase = _matchPhase;
            snapshot.localPlayerReady = localSeat.IsReady;
            snapshot.remotePlayerReady = remoteSeat.IsReady;
            snapshot.localPlayerConnected = localSeat.HasClient && localSeat.IsConnected;
            snapshot.remotePlayerConnected = remoteSeat.HasClient && remoteSeat.IsConnected;
            snapshot.connectedPlayers = _seats.Count(seat => seat.HasClient && seat.IsConnected);
            snapshot.winnerPlayerIndex = _winnerPlayerIndex;
            snapshot.matchSeed = _authoritativeSeed;
            snapshot.reconnectGraceRemainingSeconds = CalculateReconnectGraceRemaining(remoteSeat);
            snapshot.statusMessage = BuildStatusMessage(localSeatIndex);

            if (_runtime != null && _runtime.State.DuelEnded && _winnerPlayerIndex < 0)
            {
                snapshot.winnerPlayerIndex = ResolveWinnerFromRuntime();
            }

            return snapshot;
        }

        private DuelSnapshotDto CreateEmptySnapshot(int localSeatIndex)
        {
            return new DuelSnapshotDto
            {
                localPlayerIndex = localSeatIndex,
                activePlayerIndex = 0,
                turnNumber = 0,
                duelEnded = false,
                endReason = DuelEndReason.None,
                logs = new List<BattleLogEntry>(),
                players = new[]
                {
                    BuildEmptyPlayer(0),
                    BuildEmptyPlayer(1)
                }
            };
        }

        private static PlayerSnapshotDto BuildEmptyPlayer(int index)
        {
            return new PlayerSnapshotDto
            {
                playerIndex = index,
                heroHealth = 20,
                mana = 0,
                maxMana = 0,
                remainingDeckCount = 0,
                hand = Array.Empty<CardInHandDto>(),
                board = new[]
                {
                    new BoardSlotSnapshotDto{ slot = BoardSlot.Front, occupied = false },
                    new BoardSlotSnapshotDto{ slot = BoardSlot.BackLeft, occupied = false },
                    new BoardSlotSnapshotDto{ slot = BoardSlot.BackRight, occupied = false }
                }
            };
        }

        private int ResolvePlayerIndex(ulong clientId)
        {
            return FindSeatByClientId(clientId)?.SeatIndex ?? -1;
        }

        private SeatState FindSeatByClientId(ulong clientId)
        {
            return _seats.FirstOrDefault(seat => seat.HasClient && seat.ClientId == clientId);
        }

        private SeatState FindSeatByAuthId(string authPlayerId)
        {
            if (string.IsNullOrWhiteSpace(authPlayerId))
            {
                return null;
            }

            return _seats.FirstOrDefault(seat => !string.IsNullOrWhiteSpace(seat.AuthPlayerId)
                && string.Equals(seat.AuthPlayerId, authPlayerId, StringComparison.Ordinal));
        }

        private SeatState FindFirstFreeSeat()
        {
            return _seats.FirstOrDefault(seat => !seat.HasClient || (!seat.IsConnected && seat.ExplicitLeave));
        }

        private void ClearSeatIfSafe(SeatState seat)
        {
            if (seat == null)
            {
                return;
            }

            seat.HasClient = false;
            seat.IsConnected = false;
            seat.IsReady = false;
            seat.ExplicitLeave = false;
            seat.AuthPlayerId = string.Empty;
            seat.SubmittedDeckHash = string.Empty;
            seat.ExpectedDeckHash = string.Empty;
            seat.DeckValidated = false;
            seat.DisconnectAt = -1f;
        }

        private string ComputeExpectedDeckHash(int seatIndex)
        {
            return DeckHashUtility.ComputeHash(seatIndex == 0 ? deckPlayerA : deckPlayerB);
        }

        private string ResolveLocalAuthId()
        {
            var service = FindFirstObjectByType<MpsGameSessionService>();
            return service != null ? service.LocalPlayerId : SystemInfo.deviceUniqueIdentifier;
        }

        private string ResolveLocalDeckHash()
        {
            if (NetworkManager == null)
            {
                return DeckHashUtility.ComputeHash(deckPlayerA);
            }

            return NetworkManager.IsHost
                ? DeckHashUtility.ComputeHash(deckPlayerA)
                : DeckHashUtility.ComputeHash(deckPlayerB);
        }

        private void TryRegisterLocalClient()
        {
            var authId = ResolveLocalAuthId();
            if (string.IsNullOrWhiteSpace(authId))
            {
                return;
            }

            RegisterPlayerServerRpc(authId, ResolveLocalDeckHash());
        }

        private int ResolveWinnerFromRuntime()
        {
            if (_runtime == null)
            {
                return -1;
            }

            var playerA = _runtime.State.GetPlayer(0);
            var playerB = _runtime.State.GetPlayer(1);
            if (playerA == null || playerB == null)
            {
                return -1;
            }

            if (playerA.HeroHealth <= 0 && playerB.HeroHealth > 0)
            {
                return 1;
            }

            if (playerB.HeroHealth <= 0 && playerA.HeroHealth > 0)
            {
                return 0;
            }

            return -1;
        }

        private float CalculateReconnectGraceRemaining(SeatState seat)
        {
            if (seat == null || seat.IsConnected || seat.DisconnectAt < 0f || _matchPhase != MatchPhase.InProgress)
            {
                return 0f;
            }

            return Mathf.Max(0f, disconnectGraceSeconds - (Time.unscaledTime - seat.DisconnectAt));
        }

        private string BuildStatusMessage(int localSeatIndex)
        {
            var remoteSeat = _seats[1 - localSeatIndex];
            return _matchPhase switch
            {
                MatchPhase.WaitingForPlayers => "Waiting for the second player...",
                MatchPhase.WaitingForReady => $"Ready up to start. Opponent ready: {(remoteSeat.IsReady ? "yes" : "no")}",
                MatchPhase.Starting => "Starting match...",
                MatchPhase.InProgress when !remoteSeat.IsConnected => $"Opponent disconnected. Hold for {CalculateReconnectGraceRemaining(remoteSeat):0}s.",
                MatchPhase.InProgress => "Match in progress.",
                MatchPhase.Completed when _winnerPlayerIndex == localSeatIndex => "Victory.",
                MatchPhase.Completed => "Defeat.",
                MatchPhase.Abandoned => "Match abandoned.",
                _ => string.Empty
            };
        }

        private void AppendLog(string message)
        {
            if (_runtime == null)
            {
                return;
            }

            _runtime.State.Logs.Add(new BattleLogEntry
            {
                type = BattleLogType.Info,
                message = message
            });
        }

        [ClientRpc]
        private void PushSnapshotClientRpc(string json, ClientRpcParams clientRpcParams = default)
        {
            Debug.Log($"[Net] PushSnapshotClientRpc called on client, publishing to BattleSnapshotBus");
            BattleSnapshotBus.Publish(json);
        }
    }
}
