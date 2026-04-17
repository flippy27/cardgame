using System.Collections;
using UnityEngine;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Data;
using Flippy.CardDuelMobile.Networking;

namespace Flippy.CardDuelMobile.SinglePlayer
{
    /// <summary>
    /// Controlador local para jugar contra IA sin depender de NGO.
    /// Esta versión adapta el snapshot al flujo nuevo del presenter
    /// (matchPhase / ready / connected / winner / status).
    /// </summary>
    public sealed class LocalSinglePlayerCoordinator : MonoBehaviour
    {
        [Header("Setup")]
        public bool autoStartOnStart = false;
        public int localPlayerIndex = 0;
        public DuelRulesProfile rulesProfile;
        public DeckDefinition localPlayerDeck;
        public DeckDefinition enemyDeck;

        [Header("AI")]
        public AiDifficulty aiDifficulty = AiDifficulty.Medium;
        public float aiFirstActionDelay = 0.45f;
        public float aiActionDelay = 0.35f;
        public int maxAiActionsPerTurn = 8;

        private readonly SimpleCardAiAgent _aiAgent = new();
        private DuelRuntime _runtime;
        private Coroutine _aiRoutine;

        public static LocalSinglePlayerCoordinator Instance { get; private set; }

        public bool IsActive => _runtime != null;
        public DuelSnapshotDto LatestSnapshot { get; private set; }
        public DuelRuntime DuelRuntime => _runtime;
        public DuelState DuelState => _runtime?.State;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (autoStartOnStart)
            {
                StartMatch();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Reinicia la partida local completa.
        /// </summary>
        public void StartMatch()
        {
            if (rulesProfile == null || localPlayerDeck == null || enemyDeck == null)
            {
                Debug.LogWarning("LocalSinglePlayerCoordinator requires rules and both decks.");
                return;
            }

            if (_aiRoutine != null)
            {
                StopCoroutine(_aiRoutine);
                _aiRoutine = null;
            }

            _runtime = new DuelRuntime(rulesProfile);
            _runtime.StartGame(localPlayerDeck, enemyDeck);

            PublishSnapshot();
            TryStartAiTurnIfNeeded();
        }

        /// <summary>
        /// Juega una carta del humano local.
        /// </summary>
        public bool RequestPlayCard(string runtimeCardKey, BoardSlot slot)
        {
            if (_runtime == null)
            {
                return false;
            }

            if (_runtime.TryPlayCard(localPlayerIndex, runtimeCardKey, slot))
            {
                PublishSnapshot();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Termina el turno del humano local.
        /// </summary>
        public bool RequestEndTurn()
        {
            Debug.Log("[LocalAI] RequestEndTurn called");
            if (_runtime == null)
            {
                Debug.LogWarning("[LocalAI] RequestEndTurn rejected: runtime is null");
                return false;
            }

            if (_runtime.TryEndTurn(localPlayerIndex))
            {
                Debug.Log("[LocalAI] RequestEndTurn succeeded, publishing snapshot");
                PublishSnapshot();
                TryStartAiTurnIfNeeded();
                return true;
            }

            Debug.LogWarning("[LocalAI] RequestEndTurn failed: TryEndTurn returned false");
            return false;
        }

        private void TryStartAiTurnIfNeeded()
        {
            if (_runtime == null || _runtime.State.DuelEnded)
            {
                return;
            }

            if (_runtime.State.ActivePlayerIndex == localPlayerIndex)
            {
                return;
            }

            if (_aiRoutine != null)
            {
                StopCoroutine(_aiRoutine);
            }

            _aiRoutine = StartCoroutine(RunAiTurn());
        }

        private IEnumerator RunAiTurn()
        {
            yield return new WaitForSeconds(aiFirstActionDelay);

            var aiPlayerIndex = 1 - localPlayerIndex;
            var safety = Mathf.Max(1, maxAiActionsPerTurn);

            while (safety-- > 0 &&
                   _runtime != null &&
                   !_runtime.State.DuelEnded &&
                   _runtime.State.ActivePlayerIndex == aiPlayerIndex)
            {
                var move = _aiAgent.BuildMove(_runtime, aiPlayerIndex, aiDifficulty);

                if (move.IsEndTurn)
                {
                    _runtime.TryEndTurn(aiPlayerIndex);
                    PublishSnapshot();
                    _aiRoutine = null;
                    yield break;
                }

                if (_runtime.TryPlayCard(aiPlayerIndex, move.RuntimeCardKey, move.Slot))
                {
                    PublishSnapshot();
                }
                else
                {
                    _runtime.TryEndTurn(aiPlayerIndex);
                    PublishSnapshot();
                    _aiRoutine = null;
                    yield break;
                }

                yield return new WaitForSeconds(aiActionDelay);
            }

            if (_runtime != null &&
                !_runtime.State.DuelEnded &&
                _runtime.State.ActivePlayerIndex == aiPlayerIndex)
            {
                _runtime.TryEndTurn(aiPlayerIndex);
                PublishSnapshot();
            }

            _aiRoutine = null;
        }

        private void PublishSnapshot()
        {
            if (_runtime == null)
            {
                return;
            }

            var snapshot = _runtime.CreateSnapshot(localPlayerIndex);

            // Adaptación al presenter nuevo.
            snapshot.matchPhase = _runtime.State.DuelEnded
                ? MatchPhase.Completed
                : MatchPhase.InProgress;

            snapshot.localPlayerReady = true;
            snapshot.remotePlayerReady = true;
            snapshot.localPlayerConnected = true;
            snapshot.remotePlayerConnected = true;
            snapshot.connectedPlayers = 2;
            snapshot.reconnectGraceRemainingSeconds = 0f;
            snapshot.matchSeed = 0;

            if (_runtime.State.DuelEnded)
            {
                snapshot.winnerPlayerIndex = ResolveWinnerIndex(snapshot);
                snapshot.statusMessage = snapshot.winnerPlayerIndex == localPlayerIndex
                    ? "Victory"
                    : "Defeat";
            }
            else
            {
                snapshot.winnerPlayerIndex = -1;
                snapshot.statusMessage = snapshot.activePlayerIndex == localPlayerIndex
                    ? "Your turn"
                    : "Enemy turn";
            }

            LatestSnapshot = snapshot;
            BattleSnapshotBus.Publish(JsonUtility.ToJson(snapshot));
        }

        private int ResolveWinnerIndex(DuelSnapshotDto snapshot)
        {
            if (snapshot == null)
            {
                return -1;
            }

            return snapshot.endReason switch
            {
                DuelEndReason.EnemyHeroDefeated => localPlayerIndex,
                DuelEndReason.LocalHeroDefeated => 1 - localPlayerIndex,
                DuelEndReason.OpponentDisconnected => localPlayerIndex,
                _ => ResolveByHeroHealth(snapshot)
            };
        }

        private int ResolveByHeroHealth(DuelSnapshotDto snapshot)
        {
            if (snapshot.players == null || snapshot.players.Length < 2)
            {
                return -1;
            }

            var a = snapshot.players[0];
            var b = snapshot.players[1];

            if (a.heroHealth > b.heroHealth) return 0;
            if (b.heroHealth > a.heroHealth) return 1;
            return -1;
        }
    }
}