using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.SinglePlayer;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.UI;
using UnityEngine.Serialization;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Orquestador principal del gameplay 3D.
    /// Se suscribe a BattleSnapshotBus y actualiza la presentación 3D.
    /// </summary>
    public class GameplayPresenter3D : MonoBehaviour
    {
        [SerializeField] private Board3DManager board3DManager;
        [SerializeField] private Hand3DManager hand3DManager;
        [SerializeField] private HUD3D hud3D;
        [SerializeField] private DragHandler3D dragHandler;
        [SerializeField] private EndTurnButton3D endTurnButton;
        [SerializeField] public GameObject boardCardPrefab;
        [SerializeField] public GameObject boardCardPlayedPrefab;
        //[SerializeField] private DebugPanel debugPanel;
        [SerializeField] private AttackEffectSystem attackEffectSystem;
        [SerializeField] private Transform localHeroAttackTarget;
        [SerializeField] private Transform remoteHeroAttackTarget;

        public Board3DManager Board3DManager
        {
            get => board3DManager;
            set => board3DManager = value;
        }

        public Hand3DManager Hand3DManager
        {
            get => hand3DManager;
            set => hand3DManager = value;
        }

        public HUD3D Hud3D
        {
            get => hud3D;
            set => hud3D = value;
        }

        public DragHandler3D DragHandler
        {
            get => dragHandler;
            set => dragHandler = value;
        }

        public EndTurnButton3D EndTurnButton
        {
            get => endTurnButton;
            set => endTurnButton = value;
        }

        private DuelSnapshotDto _latestSnapshot;
        private DuelSnapshotDto _previousSnapshot;
        private bool _subscribed;
        private int _lastLogCount;
        private readonly List<string> _lastSnapshotLogWindow = new();
        private bool _processingSnapshotQueue;
        private readonly Queue<DuelSnapshotDto> _snapshotQueue = new();
        private string _lastBattleEventMatchId;
        private int _lastBattleEventSequence = -1;
        private string _battleDebugMatchId;
        private string _battleDebugFilePath;
        private DateTime _battleDebugStartedUtc;
        private readonly List<BattleDebugStep> _battleDebugSteps = new();
        private readonly HashSet<string> _battleDebugEventKeys = new(StringComparer.Ordinal);
        private readonly HashSet<string> _battleDebugLogKeys = new(StringComparer.Ordinal);

        // Preview displacement
        private Dictionary<ICardDisplay, Vector3> _originalCardPositions = new();
        private System.Collections.IEnumerator _previewAnimCoroutine;

        public static GameplayPresenter3D Instance { get; private set; }

        public static DuelSnapshotDto GetLatestSnapshot() => Instance?._latestSnapshot;

        private void Awake()
        {
            //Debug.Log("[GameplayPresenter3D] Awake");
            Instance = this;
            EnsureEventSystem();

            // Board manager
            bool boardManagerCreated = false;
            if (board3DManager == null)
            {
                var boardGo = new GameObject("Board3D");
                boardGo.transform.SetParent(transform);
                board3DManager = boardGo.AddComponent<Board3DManager>();
                boardManagerCreated = true;
            }

            // Hand manager
            bool handManagerCreated = false;
            if (hand3DManager == null)
            {
                var handGo = new GameObject("Hand3D");
                handGo.transform.SetParent(transform);
                hand3DManager = handGo.AddComponent<Hand3DManager>();
                handManagerCreated = true;
            }

            // HUD
            if (hud3D == null)
            {
                hud3D = FindFirstObjectByType<HUD3D>();
            }

            // Drag handler
            if (dragHandler == null)
            {
                dragHandler = FindFirstObjectByType<DragHandler3D>();
                if (dragHandler == null && Camera.main != null)
                {
                    dragHandler = Camera.main.gameObject.AddComponent<DragHandler3D>();
                    dragHandler.enabled = true;
                }
            }

            // End turn button
            if (endTurnButton == null)
            {
                endTurnButton = FindFirstObjectByType<EndTurnButton3D>();
                if (endTurnButton == null)
                {
                    var canvas = FindFirstObjectByType<Canvas>();
                    if (canvas != null)
                    {
                        var btnGo = new GameObject("EndTurnButton");
                        btnGo.transform.SetParent(canvas.transform);
                        btnGo.transform.localPosition = Vector3.zero;

                        var btnRect = btnGo.AddComponent<RectTransform>();
                        btnRect.anchoredPosition = new Vector2(0, -50);
                        btnRect.sizeDelta = new Vector2(200, 60);

                        var btn = btnGo.AddComponent<UnityEngine.UI.Button>();
                        btn.targetGraphic = btnGo.AddComponent<Image>();

                        var txt = btnGo.AddComponent<TextMeshProUGUI>();
                        txt.text = "END TURN";
                        txt.alignment = TextAlignmentOptions.Center;

                        endTurnButton = btnGo.AddComponent<EndTurnButton3D>();
                        endTurnButton.enabled = true;

                        //Debug.Log("[GameplayPresenter3D] Created EndTurnButton");
                    }
                }
            }

            if (endTurnButton != null)
            {
                endTurnButton.Presenter = this;
            }

            if (attackEffectSystem == null)
            {
                attackEffectSystem = FindFirstObjectByType<AttackEffectSystem>();
                if (attackEffectSystem == null)
                {
                    attackEffectSystem = gameObject.AddComponent<AttackEffectSystem>();
                }
            }

            // Always initialize managers (idempotent)
            if (board3DManager != null)
                board3DManager.Initialize();
            if (hand3DManager != null)
                hand3DManager.Initialize();

            // Debug panel
            // if (debugPanel == null)
            // {
            //     debugPanel = FindFirstObjectByType<DebugPanel>();
            // }
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
            eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
            //Debug.Log("[GameplayPresenter3D] Created EventSystem for MainGame UI.");
        }

        private void Start()
        {
            bool isLocal = GameModeManager.Instance != null && GameModeManager.Instance.IsLocalMode;

            if (isLocal)
            {
                // Local mode: start match via LocalSinglePlayerCoordinator
                var coordinator = LocalSinglePlayerCoordinator.Instance;
                if (coordinator != null && !coordinator.IsActive)
                {
                    Debug.Log("[GameplayPresenter3D] Starting local match from MainGame");
                    coordinator.StartMatch();
                }
            }
            else
            {
                // Multiplayer mode: initialize debug panel with network coordinator
                var netCoordinator = CardDuelNetworkCoordinator.Instance;
                // if (debugPanel != null && netCoordinator != null)
                // {
                //     debugPanel.Initialize(netCoordinator.DuelRuntime, netCoordinator.DuelState, hand3DManager, board3DManager);
                //     //Debug.Log("[GameplayPresenter3D] Initialized debug panel for multiplayer");
                // }
            }
        }

        private void OnEnable()
        {
            if (!_subscribed)
            {
                BattleSnapshotBus.SubscribeAndGetLast(HandleSnapshot);
                _subscribed = true;
                Debug.Log("[GameplayPresenter3D] Subscribed to BattleSnapshotBus");
            }
        }

        private void OnDisable()
        {
            BattleSnapshotBus.SnapshotReceived -= HandleSnapshot;
            _subscribed = false;
        }

        private void HandleSnapshot(string snapshotJson)
        {
            if (string.IsNullOrEmpty(snapshotJson))
                return;

            var snapshot = JsonUtility.FromJson<DuelSnapshotDto>(snapshotJson);
            if (snapshot == null)
                return;

            _latestSnapshot = snapshot;
            _snapshotQueue.Enqueue(snapshot);

            if (!_processingSnapshotQueue)
            {
                _processingSnapshotQueue = true;
                StartCoroutine(ProcessSnapshotQueue());
            }
        }

        private System.Collections.IEnumerator ProcessSnapshotQueue()
        {
            while (_snapshotQueue.Count > 0)
            {
                var snapshot = _snapshotQueue.Dequeue();
                InitializeDebugPanelIfNeeded(snapshot);

                var newLogs = CollectNewLogs(snapshot);
                var presentationEvents = BuildPresentationEvents(snapshot, newLogs);
                var hasVisualBattleEvents = presentationEvents.Any(HasVisualPresentation);
                WriteBattlePhaseDebugFile(snapshot, presentationEvents, newLogs);
                Debug.Log($"[BattlePhase] Snapshot queue step: newLogs={newLogs.Count}, battleEvents={snapshot.battleEvents?.Length ?? 0}, parsedEvents={presentationEvents.Count}, visualEvents={presentationEvents.Count(HasVisualPresentation)}");

                if (_previousSnapshot != null && hasVisualBattleEvents)
                {
                    yield return PlayBattlePresentationSequence(presentationEvents);
                    ApplySnapshotState(snapshot, animateReposition: false);
                }
                else
                {
                    LogBattlePhaseEvents(presentationEvents);
                    ApplySnapshotState(snapshot, animateReposition: true);
                }

            }

            _processingSnapshotQueue = false;
        }

        private void InitializeDebugPanelIfNeeded(DuelSnapshotDto snapshot)
        {
            // if (debugPanel == null || snapshot?.players == null || snapshot.players.Length < 2)
            // {
            //     return;
            // }

            // if (GameModeManager.Instance.IsLocalMode)
            // {
            //     var coordinator = LocalSinglePlayerCoordinator.Instance;
            //     if (coordinator != null && coordinator.DuelRuntime != null)
            //     {
            //         debugPanel.Initialize(coordinator.DuelRuntime, coordinator.DuelState, hand3DManager, board3DManager);
            //     }
            // }
            // else
            // {
            //     var netCoordinator = CardDuelNetworkCoordinator.Instance;
            //     if (netCoordinator != null && netCoordinator.DuelRuntime != null)
            //     {
            //         debugPanel.Initialize(netCoordinator.DuelRuntime, netCoordinator.DuelState, hand3DManager, board3DManager);
            //     }
            // }
        }

        private List<BattleLogEntry> CollectNewLogs(DuelSnapshotDto snapshot)
        {
            var results = new List<BattleLogEntry>();
            if (snapshot?.logs == null || snapshot.logs.Count == 0)
            {
                _lastSnapshotLogWindow.Clear();
                return results;
            }

            var currentWindow = new List<string>(snapshot.logs.Count);
            foreach (var log in snapshot.logs)
            {
                currentWindow.Add(BuildLogSignature(log));
            }

            if (_lastSnapshotLogWindow.Count == 0)
            {
                _lastSnapshotLogWindow.Clear();
                _lastSnapshotLogWindow.AddRange(currentWindow);
                return results;
            }

            var overlap = CountLogWindowOverlap(_lastSnapshotLogWindow, currentWindow);
            for (var index = overlap; index < snapshot.logs.Count; index++)
            {
                results.Add(snapshot.logs[index]);
            }

            _lastSnapshotLogWindow.Clear();
            _lastSnapshotLogWindow.AddRange(currentWindow);
            return results;
        }

        private static string BuildLogSignature(BattleLogEntry log)
        {
            if (log == null)
            {
                return string.Empty;
            }

            return $"{(int)log.type}|{log.message}";
        }

        private static int CountLogWindowOverlap(List<string> previousWindow, List<string> currentWindow)
        {
            if (previousWindow == null || currentWindow == null || previousWindow.Count == 0 || currentWindow.Count == 0)
            {
                return 0;
            }

            var maxOverlap = Mathf.Min(previousWindow.Count, currentWindow.Count);
            for (var overlap = maxOverlap; overlap > 0; overlap--)
            {
                var matches = true;
                for (var index = 0; index < overlap; index++)
                {
                    var previousIndex = previousWindow.Count - overlap + index;
                    if (!string.Equals(previousWindow[previousIndex], currentWindow[index], System.StringComparison.Ordinal))
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    return overlap;
                }
            }

            return 0;
        }

        private List<BattleEventDto> CollectNewStructuredBattleEvents(DuelSnapshotDto snapshot)
        {
            var results = new List<BattleEventDto>();
            if (snapshot == null)
            {
                return results;
            }

            if (!string.Equals(_lastBattleEventMatchId, snapshot.matchId, StringComparison.Ordinal))
            {
                _lastBattleEventMatchId = snapshot.matchId;
                _lastBattleEventSequence = -1;
            }

            if (snapshot.battleEvents == null || snapshot.battleEvents.Length == 0)
            {
                return results;
            }

            var orderedEvents = snapshot.battleEvents
                .Where(battleEvent => battleEvent != null)
                .OrderBy(battleEvent => battleEvent.sequence)
                .ToList();

            if (orderedEvents.Count == 0)
            {
                return results;
            }

            if (_lastBattleEventSequence < 0 && _previousSnapshot == null)
            {
                _lastBattleEventSequence = orderedEvents.Max(battleEvent => battleEvent.sequence);
                Debug.Log($"[BattlePhase] Initial structured event window skipped through sequence {_lastBattleEventSequence}.");
                return results;
            }

            foreach (var battleEvent in orderedEvents)
            {
                if (battleEvent.sequence <= _lastBattleEventSequence)
                {
                    continue;
                }

                results.Add(battleEvent);
            }

            if (orderedEvents.Count > 0)
            {
                _lastBattleEventSequence = Mathf.Max(_lastBattleEventSequence, orderedEvents.Max(battleEvent => battleEvent.sequence));
            }

            return results;
        }

        private List<BattlePresentationEvent> BuildPresentationEvents(DuelSnapshotDto snapshot, List<BattleLogEntry> logs)
        {
            var results = new List<BattlePresentationEvent>();

            var structuredEvents = CollectNewStructuredBattleEvents(snapshot);
            if (structuredEvents.Count > 0)
            {
                results = BattlePhaseStructuredEventMapper.Build(snapshot, structuredEvents);
                foreach (var result in results)
                {
                    ResolvePresentationEventParticipants(result, snapshot);
                }

                return results;
            }

            if (logs == null)
            {
                return results;
            }

            var fallbackSourcePlayerIndex = ResolveBattleLogSourcePlayerIndex(snapshot);

            foreach (var log in logs)
            {
                var parsed = BattlePhasePresentationLogParser.Parse(log, fallbackSourcePlayerIndex);
                if (parsed != null)
                {
                    ResolvePresentationEventParticipants(parsed, snapshot);
                    results.Add(parsed);
                }
            }

            return results;
        }

        private void ResolvePresentationEventParticipants(BattlePresentationEvent presentationEvent, DuelSnapshotDto snapshot)
        {
            if (presentationEvent == null)
            {
                return;
            }

            if (presentationEvent.sourcePlayerIndex is not (0 or 1) && !string.IsNullOrWhiteSpace(presentationEvent.sourceRuntimeId))
            {
                presentationEvent.sourcePlayerIndex = FindPlayerIndexByRuntimeId(_previousSnapshot, presentationEvent.sourceRuntimeId);
                if (presentationEvent.sourcePlayerIndex is not (0 or 1))
                {
                    presentationEvent.sourcePlayerIndex = FindPlayerIndexByRuntimeId(snapshot, presentationEvent.sourceRuntimeId);
                }
            }

            if (presentationEvent.targetPlayerIndex is not (0 or 1) && !string.IsNullOrWhiteSpace(presentationEvent.targetRuntimeId))
            {
                presentationEvent.targetPlayerIndex = FindPlayerIndexByRuntimeId(_previousSnapshot, presentationEvent.targetRuntimeId);
                if (presentationEvent.targetPlayerIndex is not (0 or 1))
                {
                    presentationEvent.targetPlayerIndex = FindPlayerIndexByRuntimeId(snapshot, presentationEvent.targetRuntimeId);
                }
            }

            if (presentationEvent.sourcePlayerIndex is not (0 or 1) && !string.IsNullOrWhiteSpace(presentationEvent.sourceName))
            {
                presentationEvent.sourcePlayerIndex = FindPlayerIndexByIdentifier(_previousSnapshot, presentationEvent.sourceName);
                if (presentationEvent.sourcePlayerIndex is not (0 or 1))
                {
                    presentationEvent.sourcePlayerIndex = FindPlayerIndexByIdentifier(snapshot, presentationEvent.sourceName);
                }
            }

            if (presentationEvent.kind == BattlePresentationEventKind.CardAttack)
            {
                if (presentationEvent.targetPlayerIndex is not (0 or 1))
                {
                    if (presentationEvent.sourcePlayerIndex is 0 or 1)
                    {
                        presentationEvent.targetPlayerIndex = 1 - presentationEvent.sourcePlayerIndex;
                    }
                    else if (!string.IsNullOrWhiteSpace(presentationEvent.targetName))
                    {
                        presentationEvent.targetPlayerIndex = FindPlayerIndexByIdentifier(_previousSnapshot, presentationEvent.targetName);
                        if (presentationEvent.targetPlayerIndex is not (0 or 1))
                        {
                            presentationEvent.targetPlayerIndex = FindPlayerIndexByIdentifier(snapshot, presentationEvent.targetName);
                        }
                    }
                }

                if (presentationEvent.sourcePlayerIndex is not (0 or 1) && presentationEvent.targetPlayerIndex is 0 or 1)
                {
                    presentationEvent.sourcePlayerIndex = 1 - presentationEvent.targetPlayerIndex;
                }
            }
            else if (presentationEvent.kind == BattlePresentationEventKind.HeroAttack)
            {
                if (presentationEvent.sourcePlayerIndex is not (0 or 1) && presentationEvent.targetPlayerIndex is 0 or 1)
                {
                    presentationEvent.sourcePlayerIndex = 1 - presentationEvent.targetPlayerIndex;
                }

                if (presentationEvent.targetPlayerIndex is not (0 or 1) && presentationEvent.sourcePlayerIndex is 0 or 1)
                {
                    presentationEvent.targetPlayerIndex = 1 - presentationEvent.sourcePlayerIndex;
                }
            }
            else if (presentationEvent.kind is BattlePresentationEventKind.StatusDamage or
                     BattlePresentationEventKind.StatusApplied or
                     BattlePresentationEventKind.StatusExpired or
                     BattlePresentationEventKind.Heal or
                     BattlePresentationEventKind.ArmorGain or
                     BattlePresentationEventKind.AttackBuff or
                     BattlePresentationEventKind.Death)
            {
                if (presentationEvent.targetPlayerIndex is not (0 or 1) && !string.IsNullOrWhiteSpace(presentationEvent.targetName))
                {
                    presentationEvent.targetPlayerIndex = FindPlayerIndexByIdentifier(_previousSnapshot, presentationEvent.targetName);
                    if (presentationEvent.targetPlayerIndex is not (0 or 1))
                    {
                        presentationEvent.targetPlayerIndex = FindPlayerIndexByIdentifier(snapshot, presentationEvent.targetName);
                    }
                }
            }
        }

        private int FindPlayerIndexByRuntimeId(DuelSnapshotDto snapshot, string runtimeId)
        {
            if (snapshot?.players == null || string.IsNullOrWhiteSpace(runtimeId))
            {
                return -1;
            }

            for (var playerIndex = 0; playerIndex < snapshot.players.Length; playerIndex++)
            {
                var player = snapshot.players[playerIndex];
                if (player?.board == null)
                {
                    continue;
                }

                foreach (var slotSnapshot in player.board)
                {
                    if (slotSnapshot?.occupant != null &&
                        string.Equals(slotSnapshot.occupant.runtimeId, runtimeId, StringComparison.OrdinalIgnoreCase))
                    {
                        return playerIndex;
                    }
                }
            }

            return -1;
        }

        private int FindPlayerIndexByIdentifier(DuelSnapshotDto snapshot, string identifier)
        {
            if (snapshot?.players == null || string.IsNullOrWhiteSpace(identifier))
            {
                return -1;
            }

            for (var playerIndex = 0; playerIndex < snapshot.players.Length; playerIndex++)
            {
                var player = snapshot.players[playerIndex];
                if (player?.board == null)
                {
                    continue;
                }

                foreach (var slotSnapshot in player.board)
                {
                    if (slotSnapshot?.occupant != null && CardDataMatchesIdentifier(slotSnapshot.occupant, identifier))
                    {
                        return playerIndex;
                    }
                }
            }

            return -1;
        }

        private int ResolveBattleLogSourcePlayerIndex(DuelSnapshotDto snapshot)
        {
            if (_previousSnapshot != null &&
                _previousSnapshot.activePlayerIndex is 0 or 1 &&
                snapshot != null &&
                snapshot.activePlayerIndex is 0 or 1 &&
                _previousSnapshot.activePlayerIndex != snapshot.activePlayerIndex)
            {
                return _previousSnapshot.activePlayerIndex;
            }

            if (snapshot?.activePlayerIndex is 0 or 1)
            {
                return 1 - snapshot.activePlayerIndex;
            }

            return -1;
        }

        private static bool HasVisualPresentation(BattlePresentationEvent presentationEvent)
        {
            return presentationEvent != null &&
                   (presentationEvent.kind == BattlePresentationEventKind.CardAttack ||
                    presentationEvent.kind == BattlePresentationEventKind.HeroAttack ||
                    presentationEvent.kind == BattlePresentationEventKind.StatusDamage ||
                    presentationEvent.kind == BattlePresentationEventKind.ShieldBlock ||
                    presentationEvent.kind == BattlePresentationEventKind.StatusApplied ||
                    presentationEvent.kind == BattlePresentationEventKind.StatusExpired ||
                    presentationEvent.kind == BattlePresentationEventKind.Heal ||
                    presentationEvent.kind == BattlePresentationEventKind.ArmorGain ||
                    presentationEvent.kind == BattlePresentationEventKind.AttackBuff ||
                    presentationEvent.kind == BattlePresentationEventKind.Death);
        }

        private void ApplySnapshotState(DuelSnapshotDto snapshot, bool animateReposition)
        {
            _latestSnapshot = snapshot;
            UpdateBoard(snapshot);
            ClearPreviewPositions();

            if (animateReposition)
            {
                DetectAndAnimateRepositioning(snapshot);
            }
            else
            {
                _previousSnapshot = snapshot;
            }

            UpdateLocalHand(snapshot);
            UpdateHUD(snapshot);

            if (snapshot.duelEnded && snapshot.winnerPlayerIndex >= 0)
            {
                HandleMatchCompletion(snapshot);
            }
        }

        private void ProcessAttackLogs(DuelSnapshotDto snapshot)
        {
            if (snapshot?.logs == null || snapshot.logs.Count == 0)
                return;

            // Procesar logs nuevos
            for (int i = _lastLogCount; i < snapshot.logs.Count; i++)
            {
                var log = snapshot.logs[i];
                if (log.type == BattleLogType.Attack && log.message != null)
                {
                    // Buscar "→" para separar attacker y defender
                    int arrowIdx = log.message.IndexOf("→");
                    if (arrowIdx > 0)
                    {
                        // Extraer nombre del atacante y el defensor
                        string beforeArrow = log.message.Substring(0, arrowIdx).Trim();
                        string afterArrow = log.message.Substring(arrowIdx + 1).Trim();

                        // Extraer el nombre después de "] "
                        int nameStart = beforeArrow.LastIndexOf("] ") + 2;
                        if (nameStart > 1)
                        {
                            string attackerName = beforeArrow.Substring(nameStart).Split('(')[0].Trim();
                            string defenderName = afterArrow.Split(':')[0].Split('→')[0].Trim();

                            // Buscar las cartas en el board
                            ICardDisplay attacker = FindCardByName(attackerName);
                            ICardDisplay defender = FindCardByName(defenderName);

                            if (attacker != null && defender != null)
                            {
                                // Legacy path disabled in favor of the queued battle presentation sequence.
                            }
                        }
                    }
                }
            }

            _lastLogCount = snapshot.logs.Count;
        }

        private ICardDisplay FindCardByName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return null;
            }

            for (var playerIndex = 0; playerIndex < 2; playerIndex++)
            {
                foreach (BoardSlot slot in System.Enum.GetValues(typeof(BoardSlot)))
                {
                    var card = board3DManager.GetCardInSlot(playerIndex, slot);
                    if (CardMatchesIdentifier(card, displayName))
                    {
                        return card;
                    }
                }
            }

            if (_latestSnapshot?.players == null)
                return null;

            for (int playerIndex = 0; playerIndex < _latestSnapshot.players.Length; playerIndex++)
            {
                var playerSnapshot = _latestSnapshot.players[playerIndex];
                if (playerSnapshot?.board == null)
                    continue;

                foreach (var slotSnapshot in playerSnapshot.board)
                {
                    if (slotSnapshot.occupied &&
                        slotSnapshot.occupant != null &&
                        CardDataMatchesIdentifier(slotSnapshot.occupant, displayName))
                    {
                        // Buscar la ICardDisplay correspondiente en el board manager
                        var cardDisplay = board3DManager.GetCardInSlot(playerIndex, slotSnapshot.slot);
                        if (CardMatchesIdentifier(cardDisplay, displayName))
                        {
                            return cardDisplay;
                        }
                    }
                }
            }

            return null;
        }

        private ICardDisplay FindCardByName(string displayName, int playerIndex)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return null;
            }

            if (playerIndex is not (0 or 1))
            {
                return FindCardByName(displayName);
            }

            foreach (BoardSlot slot in System.Enum.GetValues(typeof(BoardSlot)))
            {
                var card = board3DManager.GetCardInSlot(playerIndex, slot);
                if (CardMatchesIdentifier(card, displayName))
                {
                    return card;
                }
            }

            return FindCardByName(displayName);
        }

        private ICardDisplay FindCardByRuntimeId(string runtimeId, int playerIndex = -1)
        {
            if (string.IsNullOrWhiteSpace(runtimeId))
            {
                return null;
            }

            if (playerIndex is 0 or 1)
            {
                foreach (BoardSlot slot in System.Enum.GetValues(typeof(BoardSlot)))
                {
                    var card = board3DManager.GetCardInSlot(playerIndex, slot);
                    if (card?.CardData != null &&
                        string.Equals(card.CardData.runtimeId, runtimeId, StringComparison.OrdinalIgnoreCase))
                    {
                        return card;
                    }
                }
            }

            for (var searchPlayerIndex = 0; searchPlayerIndex < 2; searchPlayerIndex++)
            {
                if (searchPlayerIndex == playerIndex)
                {
                    continue;
                }

                foreach (BoardSlot slot in System.Enum.GetValues(typeof(BoardSlot)))
                {
                    var card = board3DManager.GetCardInSlot(searchPlayerIndex, slot);
                    if (card?.CardData != null &&
                        string.Equals(card.CardData.runtimeId, runtimeId, StringComparison.OrdinalIgnoreCase))
                    {
                        return card;
                    }
                }
            }

            return null;
        }

        private ICardDisplay FindCardOnBoardByName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return null;
            }

            for (var playerIndex = 0; playerIndex < 2; playerIndex++)
            {
                foreach (BoardSlot slot in System.Enum.GetValues(typeof(BoardSlot)))
                {
                    var card = board3DManager.GetCardInSlot(playerIndex, slot);
                    if (CardMatchesIdentifier(card, displayName))
                    {
                        return card;
                    }
                }
            }

            return null;
        }

        private static bool CardMatchesIdentifier(ICardDisplay card, string identifier)
        {
            if (card?.CardData == null || string.IsNullOrWhiteSpace(identifier))
            {
                return false;
            }

            return CardDataMatchesIdentifier(card.CardData, identifier);
        }

        private static bool CardDataMatchesIdentifier(BoardCardDto cardData, string identifier)
        {
            if (cardData == null || string.IsNullOrWhiteSpace(identifier))
            {
                return false;
            }

            var normalizedIdentifier = NormalizeCardIdentifier(identifier);
            if (string.IsNullOrWhiteSpace(normalizedIdentifier))
            {
                return false;
            }

            var runtimeId = NormalizeCardIdentifier(cardData.runtimeId);
            if (!string.IsNullOrWhiteSpace(runtimeId) &&
                (string.Equals(runtimeId, normalizedIdentifier, System.StringComparison.OrdinalIgnoreCase) ||
                 runtimeId.StartsWith(normalizedIdentifier, System.StringComparison.OrdinalIgnoreCase) ||
                 normalizedIdentifier.StartsWith(runtimeId, System.StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var displayName = NormalizeCardIdentifier(cardData.displayName);
            if (!string.IsNullOrWhiteSpace(displayName) &&
                (string.Equals(displayName, normalizedIdentifier, System.StringComparison.OrdinalIgnoreCase) ||
                 displayName.IndexOf(normalizedIdentifier, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                 normalizedIdentifier.IndexOf(displayName, System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }

            return false;
        }

        private static string NormalizeCardIdentifier(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }

        private System.Collections.IEnumerator PlayBattlePresentationSequence(List<BattlePresentationEvent> presentationEvents)
        {
            LogBattlePhaseEvents(presentationEvents);

            if (attackEffectSystem == null)
            {
                yield break;
            }

            var consumedAttackers = new HashSet<string>();
            foreach (var presentationEvent in presentationEvents)
            {
                if (presentationEvent == null)
                {
                    continue;
                }

                switch (presentationEvent.kind)
                {
                    case BattlePresentationEventKind.CardAttack:
                        yield return PlayCardAttackEvent(presentationEvent, consumedAttackers);
                        break;

                    case BattlePresentationEventKind.HeroAttack:
                        yield return PlayHeroAttackEvent(presentationEvent, consumedAttackers);
                        break;

                    case BattlePresentationEventKind.StatusDamage:
                        yield return PlayStatusDamageEvent(presentationEvent);
                        break;

                    case BattlePresentationEventKind.ShieldBlock:
                        yield return PlayShieldBlockEvent(presentationEvent, consumedAttackers);
                        break;

                    case BattlePresentationEventKind.StatusApplied:
                    case BattlePresentationEventKind.StatusExpired:
                        yield return PlayStatusIconEvent(presentationEvent);
                        break;

                    case BattlePresentationEventKind.Heal:
                    case BattlePresentationEventKind.ArmorGain:
                    case BattlePresentationEventKind.AttackBuff:
                        yield return PlayStatChangeEvent(presentationEvent);
                        break;

                    case BattlePresentationEventKind.Death:
                        yield return PlayDeathEvent(presentationEvent);
                        break;
                }

                if (attackEffectSystem.BetweenEventsDelay > 0f)
                {
                    yield return new WaitForSeconds(attackEffectSystem.BetweenEventsDelay);
                }
            }
        }

        private System.Collections.IEnumerator PlayCardAttackEvent(BattlePresentationEvent presentationEvent, HashSet<string> consumedAttackers)
        {
            var attacker = ResolveAttackerForPresentation(presentationEvent, consumedAttackers);
            if (attacker != null)
            {
                presentationEvent.sourcePlayerIndex = attacker.PlayerIndex;
                if (presentationEvent.targetPlayerIndex is not (0 or 1))
                {
                    presentationEvent.targetPlayerIndex = 1 - attacker.PlayerIndex;
                }
            }

            var defender = FindCardByRuntimeId(presentationEvent.targetRuntimeId, presentationEvent.targetPlayerIndex) ??
                           FindCardByName(presentationEvent.targetName, presentationEvent.targetPlayerIndex);
            if (defender != null && attacker != null && defender.PlayerIndex == attacker.PlayerIndex)
            {
                var oppositePlayerIndex = 1 - attacker.PlayerIndex;
                var oppositeDefender = FindCardByName(presentationEvent.targetName, oppositePlayerIndex);
                if (oppositeDefender != null)
                {
                    presentationEvent.targetPlayerIndex = oppositePlayerIndex;
                    defender = oppositeDefender;
                }
            }

            if (attacker == null || defender == null)
            {
                Debug.LogWarning($"[BattlePhase] Unable to resolve card attack presentation. attacker='{presentationEvent.sourceName}' resolved={attacker != null}, defender='{presentationEvent.targetName}' resolved={defender != null}");
                yield break;
            }

            var defenderSlot = board3DManager.GetSlot(presentationEvent.targetPlayerIndex, defender.CardData.slot);
            var deliveryType = AttackPresentationResolver.ResolveDeliveryType(attacker.CardData);
            var attackKind = string.IsNullOrWhiteSpace(deliveryType)
                ? "Melee"
                : char.ToUpperInvariant(deliveryType[0]) + deliveryType.Substring(1);
            Debug.Log($"[BattlePhase P{presentationEvent.sourcePlayerIndex}] {attackKind} attack start: {presentationEvent.sourceName} -> {presentationEvent.targetName} for {presentationEvent.amount}");

            yield return attackEffectSystem.PlayCardAttack(
                attacker,
                defender,
                defenderSlot,
                presentationEvent.amount,
                AttackPresentationResolver.ResolveMotionLevel(attacker.CardData),
                AttackPresentationResolver.ResolveShakeLevel(attacker.CardData));

            StartCoroutine(attackEffectSystem.FlashCard(defender));
            if (defender.TryGetTransform(out var defenderTransform))
            {
                var isStatusDamage = string.Equals(presentationEvent.abilityId, "poison", StringComparison.OrdinalIgnoreCase) ||
                                     presentationEvent.statusKind == 0;
                yield return attackEffectSystem.PlayDamagePopup(defenderTransform.position, presentationEvent.amount, isStatusDamage);
            }

            var hpBefore = defender.CardData.currentHealth;
            var previousArmor = defender.CardData.armor;
            defender.CardData.armor = presentationEvent.hasResolvedArmorAfter
                ? Mathf.Max(0, presentationEvent.armorAfter)
                : Mathf.Max(0, previousArmor - presentationEvent.armorBlocked);
            defender.CardData.currentHealth = presentationEvent.hasResolvedHealthAfter
                ? presentationEvent.hpAfter
                : Mathf.Max(0, hpBefore - presentationEvent.amount);
            defender.UpdateStatsDisplay();

            Debug.Log($"[BattlePhase P{presentationEvent.sourcePlayerIndex}] Impact resolved: {presentationEvent.targetName} {(presentationEvent.hasResolvedHealthAfter ? presentationEvent.hpBefore : hpBefore)}->{defender.CardData.currentHealth} HP, armor blocked {presentationEvent.armorBlocked}");
        }

        private System.Collections.IEnumerator PlayHeroAttackEvent(BattlePresentationEvent presentationEvent, HashSet<string> consumedAttackers)
        {
            var attacker = ResolveAttackerForPresentation(presentationEvent, consumedAttackers);
            if (attacker == null)
            {
                Debug.LogWarning($"[BattlePhase] Unable to resolve hero attack attacker '{presentationEvent.sourceName}'.");
                yield break;
            }

            presentationEvent.sourcePlayerIndex = attacker.PlayerIndex;
            if (presentationEvent.targetPlayerIndex is not (0 or 1))
            {
                presentationEvent.targetPlayerIndex = 1 - attacker.PlayerIndex;
            }

            var heroImpactPosition = GetHeroImpactPosition(presentationEvent.targetPlayerIndex);
            var heroImpactSlot = board3DManager.GetSlot(presentationEvent.targetPlayerIndex, BoardSlot.Front);
            Debug.Log($"[BattlePhase P{presentationEvent.sourcePlayerIndex}] Direct hero attack: {presentationEvent.sourceName ?? attacker.CardData?.displayName} -> Hero P{presentationEvent.targetPlayerIndex} for {presentationEvent.amount}");
            yield return attackEffectSystem.PlayHeroAttack(
                attacker,
                heroImpactSlot,
                heroImpactPosition,
                presentationEvent.amount,
                AttackPresentationResolver.ResolveMotionLevel(attacker.CardData),
                AttackPresentationResolver.ResolveShakeLevel(attacker.CardData));

            yield return attackEffectSystem.PlayDamagePopup(heroImpactPosition, presentationEvent.amount);
            PreviewHeroHealthAfterImpact(presentationEvent.targetPlayerIndex, presentationEvent.amount, presentationEvent.hasResolvedHealthAfter ? presentationEvent.hpAfter : (int?)null);
        }

        private ICardDisplay ResolveAttackerForPresentation(BattlePresentationEvent presentationEvent, HashSet<string> consumedAttackers)
        {
            var attacker = FindCardByRuntimeId(presentationEvent.sourceRuntimeId, presentationEvent.sourcePlayerIndex) ??
                           FindCardByName(presentationEvent.sourceName, presentationEvent.sourcePlayerIndex);
            if (attacker == null)
            {
                attacker = FindNextAvailableAttacker(presentationEvent.sourcePlayerIndex, consumedAttackers);
            }

            if (attacker != null && attacker.CardData != null)
            {
                consumedAttackers?.Add(attacker.CardData.runtimeId);
            }

            return attacker;
        }

        private ICardDisplay FindNextAvailableAttacker(int playerIndex, HashSet<string> consumedAttackers)
        {
            var attackOrder = new[] { BoardSlot.Front, BoardSlot.BackLeft, BoardSlot.BackRight };
            foreach (var slot in attackOrder)
            {
                var card = board3DManager.GetCardInSlot(playerIndex, slot);
                if (card == null || card.CardData == null)
                {
                    continue;
                }

                if (consumedAttackers != null && consumedAttackers.Contains(card.CardData.runtimeId))
                {
                    continue;
                }

                return card;
            }

            return null;
        }

        private System.Collections.IEnumerator PlayStatusDamageEvent(BattlePresentationEvent presentationEvent)
        {
            var target = ResolveTargetCardForPresentation(presentationEvent);
            if (target == null)
            {
                Debug.LogWarning($"[BattlePhase] Unable to resolve status damage target '{presentationEvent.targetName}'.");
                yield break;
            }

            Debug.Log($"[BattlePhase] Status damage: {presentationEvent.targetName} took {presentationEvent.amount}");
            StartCoroutine(attackEffectSystem.FlashCard(target));
            if (target.TryGetTransform(out var targetTransform))
            {
                yield return attackEffectSystem.PlayDamagePopup(targetTransform.position, presentationEvent.amount, isPoison: true);
            }
        }

        private System.Collections.IEnumerator PlayShieldBlockEvent(BattlePresentationEvent presentationEvent, HashSet<string> consumedAttackers)
        {
            var attacker = ResolveAttackerForPresentation(presentationEvent, consumedAttackers);
            var defender = ResolveTargetCardForPresentation(presentationEvent);
            if (attacker == null || defender == null)
            {
                Debug.LogWarning($"[BattlePhase] Unable to resolve shield block. attacker='{presentationEvent.sourceRuntimeId}:{presentationEvent.sourceName}', defender='{presentationEvent.targetRuntimeId}:{presentationEvent.targetName}'");
                yield break;
            }

            var defenderSlot = board3DManager.GetSlot(defender.PlayerIndex, defender.CardData.slot);
            Debug.Log($"[BattlePhase P{attacker.PlayerIndex}] Shield block: {attacker.CardData.displayName} -> {defender.CardData.displayName}");
            yield return attackEffectSystem.PlayCardAttack(
                attacker,
                defender,
                defenderSlot,
                0,
                AttackPresentationResolver.ResolveMotionLevel(attacker.CardData),
                AttackPresentationResolver.ResolveShakeLevel(attacker.CardData));

            if (presentationEvent.hasResolvedArmorAfter)
            {
                defender.CardData.armor = Mathf.Max(0, presentationEvent.armorAfter);
            }

            defender.UpdateStatsDisplay();
        }

        private System.Collections.IEnumerator PlayStatusIconEvent(BattlePresentationEvent presentationEvent)
        {
            var target = ResolveTargetCardForPresentation(presentationEvent);
            if (target == null)
            {
                Debug.LogWarning($"[BattlePhase] Unable to resolve status event target '{presentationEvent.targetRuntimeId}:{presentationEvent.targetName}'.");
                yield break;
            }

            if (presentationEvent.kind == BattlePresentationEventKind.StatusApplied)
            {
                ApplyStatusEffectToCard(target.CardData, presentationEvent);
            }
            else
            {
                RemoveStatusEffectFromCard(target.CardData, presentationEvent);
            }

            target.UpdateStatsDisplay();
            if (target.TryGetTransform(out var targetTransform))
            {
                yield return attackEffectSystem.PlayDamagePopup(targetTransform.position, Mathf.Max(0, presentationEvent.amount), presentationEvent.statusKind == 0);
            }
        }

        private System.Collections.IEnumerator PlayStatChangeEvent(BattlePresentationEvent presentationEvent)
        {
            var target = ResolveTargetCardForPresentation(presentationEvent);
            if (target == null)
            {
                Debug.Log($"[BattlePhase] Stat event without board target: {presentationEvent.rawMessage}");
                yield break;
            }

            switch (presentationEvent.kind)
            {
                case BattlePresentationEventKind.Heal:
                    if (presentationEvent.hasResolvedHealthAfter)
                    {
                        target.CardData.currentHealth = presentationEvent.hpAfter;
                    }
                    else
                    {
                        target.CardData.currentHealth += Mathf.Max(0, presentationEvent.amount);
                    }
                    break;

                case BattlePresentationEventKind.ArmorGain:
                    target.CardData.armor = presentationEvent.hasResolvedArmorAfter
                        ? Mathf.Max(0, presentationEvent.armorAfter)
                        : target.CardData.armor + Mathf.Max(0, presentationEvent.amount);
                    break;

                case BattlePresentationEventKind.AttackBuff:
                    target.CardData.attack += presentationEvent.amount;
                    break;
            }

            target.UpdateStatsDisplay();
            if (target.TryGetTransform(out var targetTransform))
            {
                yield return attackEffectSystem.PlayDamagePopup(targetTransform.position, Mathf.Max(0, presentationEvent.amount));
            }
        }

        private System.Collections.IEnumerator PlayDeathEvent(BattlePresentationEvent presentationEvent)
        {
            var target = ResolveTargetCardForPresentation(presentationEvent);
            if (target == null)
            {
                Debug.LogWarning($"[BattlePhase] Unable to resolve death target '{presentationEvent.targetName}'.");
                yield break;
            }

            var playerIndex = target.PlayerIndex;
            Debug.Log($"[BattlePhase P{playerIndex}] Death: {presentationEvent.targetName}");
            yield return PlayCardDeath(target);
            yield return AnimateVisualRepositionForPlayer(playerIndex, 0.28f);
        }

        private ICardDisplay ResolveTargetCardForPresentation(BattlePresentationEvent presentationEvent)
        {
            if (presentationEvent == null)
            {
                return null;
            }

            return FindCardByRuntimeId(presentationEvent.targetRuntimeId, presentationEvent.targetPlayerIndex) ??
                   (presentationEvent.targetPlayerIndex is 0 or 1
                       ? FindCardByName(presentationEvent.targetName, presentationEvent.targetPlayerIndex)
                       : FindCardOnBoardByName(presentationEvent.targetName));
        }

        private static void ApplyStatusEffectToCard(BoardCardDto card, BattlePresentationEvent presentationEvent)
        {
            if (card == null)
            {
                return;
            }

            var effects = card.statusEffects != null
                ? card.statusEffects.ToList()
                : new List<StatusEffectDto>();
            var existing = effects.FirstOrDefault(effect =>
                effect != null &&
                effect.kind == presentationEvent.statusKind &&
                string.Equals(effect.abilityId, presentationEvent.abilityId, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                existing = new StatusEffectDto();
                effects.Add(existing);
            }

            existing.kind = presentationEvent.statusKind;
            existing.amount = presentationEvent.amount;
            existing.remainingTurns = presentationEvent.durationTurns;
            existing.sourceRuntimeId = presentationEvent.sourceRuntimeId ?? string.Empty;
            existing.abilityId = presentationEvent.abilityId ?? string.Empty;
            card.statusEffects = effects.ToArray();
        }

        private static void RemoveStatusEffectFromCard(BoardCardDto card, BattlePresentationEvent presentationEvent)
        {
            if (card?.statusEffects == null || card.statusEffects.Length == 0)
            {
                return;
            }

            card.statusEffects = card.statusEffects
                .Where(effect => effect != null && !StatusEffectMatches(effect, presentationEvent))
                .ToArray();
        }

        private static bool StatusEffectMatches(StatusEffectDto effect, BattlePresentationEvent presentationEvent)
        {
            if (effect == null || presentationEvent == null)
            {
                return false;
            }

            if (presentationEvent.statusKind >= 0 && effect.kind != presentationEvent.statusKind)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(presentationEvent.abilityId) &&
                !string.Equals(effect.abilityId, presentationEvent.abilityId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private System.Collections.IEnumerator PlayCardDeath(ICardDisplay target)
        {
            if (target == null)
            {
                yield break;
            }

            var playerIndex = target.PlayerIndex;
            var slot = target.CardData.slot;
            target.AnimateDeath();

            yield return new WaitForSeconds(Mathf.Max(0.5f, attackEffectSystem.DeathPause));
            board3DManager.RemoveCardReference(playerIndex, slot);
        }

        private System.Collections.IEnumerator AnimateVisualRepositionForPlayer(int playerIndex, float duration)
        {
            var moves = BuildVisualRepositionMoves(playerIndex);
            if (moves.Count == 0)
            {
                yield break;
            }

            var moveCoroutines = new List<System.Collections.IEnumerator>();
            foreach (var move in moves)
            {
                if (!move.card.TryGetTransform(out var cardTransform))
                {
                    continue;
                }

                var startPosition = cardTransform.position;
                board3DManager.MoveCardBetweenSlots(playerIndex, move.from, move.to, move.card);

                var targetSlot = board3DManager.GetSlot(playerIndex, move.to);
                if (targetSlot == null)
                {
                    continue;
                }

                move.card.CardData.slot = move.to;
                cardTransform.position = startPosition;
                moveCoroutines.Add(AnimateCardMovement(move.card, startPosition, targetSlot.transform.position, duration));
            }

            foreach (var coroutine in moveCoroutines)
            {
                StartCoroutine(coroutine);
            }

            yield return new WaitForSeconds(duration);
        }

        private List<(ICardDisplay card, BoardSlot from, BoardSlot to)> BuildVisualRepositionMoves(int playerIndex)
        {
            var results = new List<(ICardDisplay card, BoardSlot from, BoardSlot to)>();
            var front = board3DManager.GetCardInSlot(playerIndex, BoardSlot.Front);
            var backLeft = board3DManager.GetCardInSlot(playerIndex, BoardSlot.BackLeft);
            var backRight = board3DManager.GetCardInSlot(playerIndex, BoardSlot.BackRight);

            if (front == null)
            {
                if (backLeft != null)
                {
                    results.Add((backLeft, BoardSlot.BackLeft, BoardSlot.Front));
                    if (backRight != null)
                    {
                        results.Add((backRight, BoardSlot.BackRight, BoardSlot.BackLeft));
                    }
                }
                else if (backRight != null)
                {
                    results.Add((backRight, BoardSlot.BackRight, BoardSlot.Front));
                }
            }
            else if (backLeft == null && backRight != null)
            {
                results.Add((backRight, BoardSlot.BackRight, BoardSlot.BackLeft));
            }

            return results;
        }

        private Vector3 GetHeroImpactPosition(int playerIndex)
        {
            var heroTarget = GetHeroAttackTarget(playerIndex);
            if (heroTarget != null)
            {
                return heroTarget.position;
            }

            var frontSlot = board3DManager.GetSlot(playerIndex, BoardSlot.Front);
            if (frontSlot != null)
            {
                return frontSlot.transform.position + (playerIndex == 0 ? Vector3.down : Vector3.up) * 1.2f;
            }

            return Vector3.zero;
        }

        private Transform GetHeroAttackTarget(int playerIndex)
        {
            return playerIndex == 0 ? localHeroAttackTarget : remoteHeroAttackTarget;
        }

        private void PreviewHeroHealthAfterImpact(int targetPlayerIndex, int damageAmount, int? resolvedHpAfter)
        {
            if (hud3D == null)
            {
                return;
            }

            var hpAfter = resolvedHpAfter ?? ResolveHeroHealthAfterDamage(targetPlayerIndex, damageAmount);
            if (targetPlayerIndex == 0)
            {
                hud3D.PreviewLocalHeroHealth(hpAfter);
            }
            else
            {
                hud3D.PreviewRemoteHeroHealth(hpAfter);
            }
        }

        private int ResolveHeroHealthAfterDamage(int targetPlayerIndex, int damageAmount)
        {
            if (hud3D == null)
            {
                return Mathf.Max(0, damageAmount);
            }

            var currentHealth = targetPlayerIndex == 0
                ? hud3D.LocalHeroHealth
                : hud3D.RemoteHeroHealth;

            return Mathf.Max(0, currentHealth - damageAmount);
        }

        private void LogBattlePhaseEvents(List<BattlePresentationEvent> presentationEvents)
        {
            foreach (var presentationEvent in presentationEvents)
            {
                if (presentationEvent == null || string.IsNullOrWhiteSpace(presentationEvent.rawMessage))
                {
                    continue;
                }

                var prefix = presentationEvent.sourcePlayerIndex >= 0
                    ? $"[BattlePhase P{presentationEvent.sourcePlayerIndex}]"
                    : "[BattlePhase]";

                Debug.Log($"{prefix} {presentationEvent.rawMessage}");
                hud3D?.Log($"{prefix} {presentationEvent.rawMessage}");
            }
        }

        private void WriteBattlePhaseDebugFile(DuelSnapshotDto snapshot, List<BattlePresentationEvent> presentationEvents, List<BattleLogEntry> newLogs)
        {
            if (snapshot == null)
            {
                return;
            }

            try
            {
                EnsureBattleDebugFile(snapshot);
                RecordBattleDebugStep(snapshot, presentationEvents, newLogs);

                var builder = new StringBuilder();
                builder.AppendLine("Card Duel Unity Battle Debug Log");
                builder.AppendLine($"FileStartedUtc: {_battleDebugStartedUtc:O}");
                builder.AppendLine($"LastUpdatedUtc: {DateTime.UtcNow:O}");
                builder.AppendLine($"MatchId: {snapshot.matchId}");
                builder.AppendLine($"CurrentTurn: {snapshot.turnNumber}");
                builder.AppendLine($"ActivePlayerIndex: {snapshot.activePlayerIndex}");
                builder.AppendLine($"ActivePlayerId: {snapshot.activePlayerId}");
                builder.AppendLine($"IsLocalPlayersTurn: {snapshot.isLocalPlayersTurn}");
                builder.AppendLine($"DuelEnded: {snapshot.duelEnded}");
                builder.AppendLine($"WinnerPlayerIndex: {snapshot.winnerPlayerIndex}");
                builder.AppendLine($"Ruleset: {snapshot.rulesetName} ({snapshot.rulesetId})");
                builder.AppendLine();

                builder.AppendLine("Human Timeline");
                var humanIndex = 1;
                foreach (var step in _battleDebugSteps)
                {
                    foreach (var item in step.presentationEvents)
                    {
                        builder.AppendLine($"{humanIndex++:000}. {DescribePresentationEvent(item)}");
                    }
                }

                if (humanIndex == 1)
                {
                    builder.AppendLine("No battle presentation events recorded yet.");
                }

                builder.AppendLine();
                builder.AppendLine("Step By Step Snapshots");
                foreach (var step in _battleDebugSteps)
                {
                    builder.AppendLine($"-- Step {step.index} | UTC {step.utc:O} | Turn {step.turnNumber} | Active P{step.activePlayerIndex} | LocalTurn={step.isLocalPlayersTurn} | Phase={step.matchPhase}");
                    builder.AppendLine($"   Players: {step.playerSummary}");
                    if (step.presentationEvents.Count == 0)
                    {
                        builder.AppendLine("   Events: none");
                    }

                    foreach (var item in step.presentationEvents)
                    {
                        builder.AppendLine($"   - {DescribePresentationEvent(item)}");
                    }
                }

                builder.AppendLine();
                builder.AppendLine("Raw Structured Battle Events Seen By Unity");
                foreach (var step in _battleDebugSteps)
                {
                    foreach (var battleEvent in step.rawStructuredEvents)
                    {
                        builder.AppendLine(FormatRawBattleEvent(battleEvent));
                    }
                }

                builder.AppendLine();
                builder.AppendLine("Raw Logs Seen By Unity");
                foreach (var step in _battleDebugSteps)
                {
                    foreach (var log in step.rawLogs)
                    {
                        builder.AppendLine($"turn={step.turnNumber} {log?.type}: {log?.message}");
                    }
                }

                builder.AppendLine();
                builder.AppendLine("Latest Unity Snapshot JSON");
                builder.AppendLine(JsonUtility.ToJson(snapshot, true));

                File.WriteAllText(_battleDebugFilePath, builder.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BattlePhase] Failed to write debug battle phase file: {ex.Message}");
            }
        }

        private void EnsureBattleDebugFile(DuelSnapshotDto snapshot)
        {
            var matchId = string.IsNullOrWhiteSpace(snapshot.matchId) ? "match" : snapshot.matchId;
            if (string.Equals(_battleDebugMatchId, matchId, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(_battleDebugFilePath))
            {
                return;
            }

            _battleDebugMatchId = matchId;
            _battleDebugStartedUtc = DateTime.UtcNow;
            _battleDebugSteps.Clear();
            _battleDebugEventKeys.Clear();
            _battleDebugLogKeys.Clear();

            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var directory = Path.Combine(root, "battle_phases");
            Directory.CreateDirectory(directory);

            var fileName = $"battle_{SanitizeFilePart(matchId)}_{_battleDebugStartedUtc:yyyyMMdd_HHmmss}.txt";
            _battleDebugFilePath = Path.Combine(directory, fileName);
        }

        private void RecordBattleDebugStep(DuelSnapshotDto snapshot, List<BattlePresentationEvent> presentationEvents, List<BattleLogEntry> newLogs)
        {
            var step = new BattleDebugStep
            {
                index = _battleDebugSteps.Count + 1,
                utc = DateTime.UtcNow,
                turnNumber = snapshot.turnNumber,
                activePlayerIndex = snapshot.activePlayerIndex,
                isLocalPlayersTurn = snapshot.isLocalPlayersTurn,
                matchPhase = snapshot.matchPhase,
                playerSummary = BuildPlayerSummary(snapshot),
                presentationEvents = DeduplicatePresentationEvents(presentationEvents),
                rawStructuredEvents = DeduplicateRawStructuredEvents(snapshot.battleEvents),
                rawLogs = DeduplicateRawLogs(newLogs)
            };

            if (step.presentationEvents.Count == 0 &&
                step.rawStructuredEvents.Count == 0 &&
                step.rawLogs.Count == 0 &&
                _battleDebugSteps.Count > 0)
            {
                return;
            }

            _battleDebugSteps.Add(step);
        }

        private List<BattlePresentationEvent> DeduplicatePresentationEvents(List<BattlePresentationEvent> events)
        {
            var results = new List<BattlePresentationEvent>();
            if (events == null)
            {
                return results;
            }

            foreach (var item in events)
            {
                if (item == null)
                {
                    continue;
                }

                var key = !string.IsNullOrWhiteSpace(item.eventId)
                    ? $"event:{item.eventId}"
                    : $"presentation:{item.sequence}:{item.kind}:{item.sourceRuntimeId}:{item.targetRuntimeId}:{item.rawMessage}";
                if (_battleDebugEventKeys.Add(key))
                {
                    results.Add(item);
                }
            }

            return results;
        }

        private List<BattleEventDto> DeduplicateRawStructuredEvents(BattleEventDto[] events)
        {
            var results = new List<BattleEventDto>();
            if (events == null)
            {
                return results;
            }

            foreach (var item in events.OrderBy(item => item?.sequence ?? int.MaxValue))
            {
                if (item == null)
                {
                    continue;
                }

                var key = !string.IsNullOrWhiteSpace(item.eventId)
                    ? $"raw:{item.eventId}"
                    : $"raw:{item.sequence}:{item.kind}:{item.sourceRuntimeId}:{item.targetRuntimeId}:{item.message}";
                if (_battleDebugEventKeys.Add(key))
                {
                    results.Add(item);
                }
            }

            return results;
        }

        private List<BattleLogEntry> DeduplicateRawLogs(List<BattleLogEntry> logs)
        {
            var results = new List<BattleLogEntry>();
            if (logs == null)
            {
                return results;
            }

            foreach (var log in logs)
            {
                if (log == null)
                {
                    continue;
                }

                var key = $"{(int)log.type}:{log.message}";
                if (_battleDebugLogKeys.Add(key))
                {
                    results.Add(log);
                }
            }

            return results;
        }

        private static string BuildPlayerSummary(DuelSnapshotDto snapshot)
        {
            if (snapshot?.players == null)
            {
                return "no players";
            }

            return string.Join(" | ", snapshot.players.Select(player =>
                player == null
                    ? "null"
                    : $"P{player.playerIndex} id={ShortRuntimeId(player.playerId)} hp={player.heroHealth} mana={player.mana}/{player.maxMana} board=[{DescribeBoard(player)}] hand={player.hand?.Length ?? 0} deck={player.remainingDeckCount}"));
        }

        private static string DescribeBoard(PlayerSnapshotDto player)
        {
            if (player?.board == null)
            {
                return string.Empty;
            }

            return string.Join(", ", player.board.Select(slot =>
            {
                if (slot == null || !slot.occupied || slot.occupant == null)
                {
                    return $"{slot?.slot.ToString() ?? "?"}:empty";
                }

                var card = slot.occupant;
                return $"{slot.slot}:{card.displayName}#{ShortRuntimeId(card.runtimeId)} hp={card.currentHealth}/{card.maxHealth} armor={card.armor} unitType={card.unitType} delivery={card.attackDeliveryType}";
            }));
        }

        private static string DescribePresentationEvent(BattlePresentationEvent item)
        {
            if (item == null)
            {
                return "null event";
            }

            var source = !string.IsNullOrWhiteSpace(item.sourceName)
                ? $"{item.sourceName}#{ShortRuntimeId(item.sourceRuntimeId)}"
                : $"P{item.sourcePlayerIndex}";
            var target = !string.IsNullOrWhiteSpace(item.targetName)
                ? $"{item.targetName}#{ShortRuntimeId(item.targetRuntimeId)}"
                : $"P{item.targetPlayerIndex}";

            return item.kind switch
            {
                BattlePresentationEventKind.CardAttack =>
                    $"seq={item.sequence} P{item.sourcePlayerIndex} {source} attacks {target} for {item.amount}. HP {item.hpBefore}->{item.hpAfter}, Armor {item.armorBefore}->{item.armorAfter}. {item.rawMessage}",
                BattlePresentationEventKind.HeroAttack =>
                    $"seq={item.sequence} P{item.sourcePlayerIndex} {source} hits hero P{item.targetPlayerIndex} for {item.amount}. HP {item.hpBefore}->{item.hpAfter}. {item.rawMessage}",
                BattlePresentationEventKind.Death =>
                    $"seq={item.sequence} {target} dies. {item.rawMessage}",
                BattlePresentationEventKind.Heal =>
                    $"seq={item.sequence} {target} heals {item.amount}. HP {item.hpBefore}->{item.hpAfter}. {item.rawMessage}",
                BattlePresentationEventKind.ArmorGain =>
                    $"seq={item.sequence} {target} gains armor {item.amount}. Armor {item.armorBefore}->{item.armorAfter}. {item.rawMessage}",
                BattlePresentationEventKind.StatusApplied =>
                    $"seq={item.sequence} status {item.statusKind} applied to {target} for {item.durationTurns} turns by {source}. {item.rawMessage}",
                BattlePresentationEventKind.StatusExpired =>
                    $"seq={item.sequence} status {item.statusKind} expired on {target}. {item.rawMessage}",
                BattlePresentationEventKind.ShieldBlock =>
                    $"seq={item.sequence} {target} blocks {source}. {item.rawMessage}",
                BattlePresentationEventKind.Skip =>
                    $"seq={item.sequence} {source} skipped. {item.rawMessage}",
                _ =>
                    $"seq={item.sequence} {item.kindId}/{item.kind}: {item.rawMessage}"
            };
        }

        private static string FormatRawBattleEvent(BattleEventDto battleEvent)
        {
            if (battleEvent == null)
            {
                return "null";
            }

            return $"seq={battleEvent.sequence} id={battleEvent.eventId} kind={battleEvent.kind} serverSrcSeat={battleEvent.serverSourceSeatIndex} srcVisual={battleEvent.sourceSeatIndex} src={battleEvent.sourceRuntimeId} serverTargetSeat={battleEvent.serverTargetSeatIndex} targetVisual={battleEvent.targetSeatIndex} target={battleEvent.targetRuntimeId} ability={battleEvent.abilityId} effect={battleEvent.effectKind} status={battleEvent.statusKind} amount={battleEvent.amount} hp={battleEvent.hpBefore}->{battleEvent.hpAfter} armor={battleEvent.armorBefore}->{battleEvent.armorAfter} message={battleEvent.message}";
        }

        private sealed class BattleDebugStep
        {
            public int index;
            public DateTime utc;
            public int turnNumber;
            public int activePlayerIndex;
            public bool isLocalPlayersTurn;
            public MatchPhase matchPhase;
            public string playerSummary;
            public List<BattlePresentationEvent> presentationEvents = new();
            public List<BattleEventDto> rawStructuredEvents = new();
            public List<BattleLogEntry> rawLogs = new();
        }

        private static string SanitizeFilePart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "match";
            }

            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (var character in value)
            {
                builder.Append(invalid.Contains(character) ? '_' : character);
            }

            return builder.ToString();
        }

        private static string ShortRuntimeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "none";
            }

            return value.Length <= 8 ? value : value.Substring(0, 8);
        }

        private void HandleMatchCompletion(DuelSnapshotDto snapshot)
        {
            var isLocalWin = snapshot.winnerPlayerIndex == snapshot.localPlayerIndex;
            var opponentIndex = 1 - snapshot.localPlayerIndex;
            var opponentName = snapshot.players?[opponentIndex]?.playerId ?? "Opponent";

            var completionScreen = FindFirstObjectByType<MatchCompletionScreen>();
            if (completionScreen != null)
            {
                if (isLocalWin)
                {
                    completionScreen.ShowVictory(opponentName, snapshot.turnNumber);
                    AudioManager.Instance?.PlayVictory();
                }
                else
                {
                    completionScreen.ShowDefeat(opponentName, snapshot.turnNumber);
                    AudioManager.Instance?.PlayDefeat();
                }
            }
        }

        private void UpdateBoard(DuelSnapshotDto snapshot)
        {
            if (UpdateBoardAuthoritative(snapshot))
            {
                return;
            }

            if (snapshot?.players == null || snapshot.players.Length < 2)
                return;

            for (int playerIndex = 0; playerIndex < 2; playerIndex++)
            {
                var playerSnapshot = snapshot.players[playerIndex];
                if (playerSnapshot?.board == null)
                    continue;

                // Build maps to track card movement by runtimeId
                var currentCards = new Dictionary<string, (BoardSlot slot, ICardDisplay view)>();
                var snapshotCards = new Dictionary<string, (BoardSlot slot, BoardCardDto data)>();

                // Map current board state
                foreach (var slot in System.Enum.GetValues(typeof(BoardSlot)) as BoardSlot[])
                {
                    var card = board3DManager.GetCardInSlot(playerIndex, slot);
                    if (card != null && card.CardData != null)
                    {
                        currentCards[card.CardData.runtimeId] = (slot, card);
                    }
                }

                // Map snapshot state
                foreach (var boardCard in playerSnapshot.board)
                {
                    if (boardCard.occupied && boardCard.occupant != null)
                    {
                        snapshotCards[boardCard.occupant.runtimeId] = (boardCard.slot, boardCard.occupant);
                    }
                }

                // Debug: log snapshot vs current state
                Debug.Log($"[UpdateBoard] P{playerIndex} Snapshot cards: {string.Join(", ", snapshotCards.Select(x => $"{x.Value.slot}={x.Value.data.displayName}(ID:{ShortRuntimeId(x.Key)})").ToList())}");
                Debug.Log($"[UpdateBoard] P{playerIndex} Current cards:  {string.Join(", ", currentCards.Select(x => $"{x.Value.slot}={x.Value.view.CardData.displayName}(ID:{ShortRuntimeId(x.Key)})").ToList())}");

                // Process snapshot cards
                foreach (var entry in snapshotCards)
                {
                    var runtimeId = entry.Key;
                    var snapshotSlot = entry.Value.slot;
                    var snapshotData = entry.Value.data;

                    if (currentCards.TryGetValue(runtimeId, out var current))
                    {
                        var currentSlot = current.slot;
                        var currentView = current.view;

                        // Card exists, check if moved
                        if (currentSlot != snapshotSlot)
                        {
                            // Reparent to new slot (keep world position)
                            Debug.Log($"[UpdateBoard] Moving {snapshotData.displayName} {currentSlot}→{snapshotSlot}");
                            ReparentCardToSlot(currentView, playerIndex, currentSlot, snapshotSlot);
                            CopyBoardCardData(currentView.CardData, snapshotData);
                            currentView.UpdateStatsDisplay();
                            Debug.Log($"[GameplayPresenter3D] Moved card {snapshotData.displayName} P{playerIndex} {currentSlot}→{snapshotSlot}");
                        }
                        else
                        {
                            // Update stats
                            var oldHP = currentView.CardData.currentHealth;
                            var newHP = snapshotData.currentHealth;

                            CopyBoardCardData(currentView.CardData, snapshotData);
                            currentView.UpdateStatsDisplay();

                            if (newHP < oldHP && newHP > 0)
                            {
                                currentView.SetColor(Color.red);
                                StartCoroutine(ResetCardColor(currentView, 0.2f));
                            }
                            else if (newHP <= 0)
                            {
                                currentView.AnimateDeath();
                                StartCoroutine(ClearBoardSlotAfterDelay(playerIndex, snapshotSlot, currentView.CardData.runtimeId, 0.5f));
                            }
                        }
                    }
                    else
                    {
                        // New card
                        Debug.Log($"[UpdateBoard] Card NOT in currentCards: {snapshotData.displayName} (ID:{ShortRuntimeId(runtimeId)}) → creating at {snapshotSlot}");
                        CreateBoardCard(snapshotData, playerIndex, snapshotSlot);
                        Debug.Log($"[GameplayPresenter3D] Created card {snapshotData.displayName} at P{playerIndex} {snapshotSlot}");
                    }
                }

                // Detect deaths (cards no longer in snapshot)
                foreach (var entry in currentCards)
                {
                    var runtimeId = entry.Key;
                    var currentSlot = entry.Value.slot;
                    var currentView = entry.Value.view;

                    if (!snapshotCards.ContainsKey(runtimeId))
                    {
                        if (currentView.CardData.currentHealth > 0)
                        {
                            currentView.AnimateDeath();
                        }
                        StartCoroutine(ClearBoardSlotAfterDelay(playerIndex, currentSlot, currentView.CardData.runtimeId, 0.5f));
                    }
                }
            }
        }

        private bool UpdateBoardAuthoritative(DuelSnapshotDto snapshot)
        {
            if (snapshot?.players == null || snapshot.players.Length < 2)
            {
                return true;
            }

            for (var playerIndex = 0; playerIndex < 2; playerIndex++)
            {
                var playerSnapshot = snapshot.players[playerIndex];
                if (playerSnapshot?.board == null)
                {
                    continue;
                }

                var currentCards = new Dictionary<string, (BoardSlot slot, ICardDisplay view)>();
                var snapshotCards = new Dictionary<string, (BoardSlot slot, BoardCardDto data)>();

                foreach (var slot in System.Enum.GetValues(typeof(BoardSlot)) as BoardSlot[])
                {
                    var card = board3DManager.GetCardInSlot(playerIndex, slot);
                    if (card?.CardData != null && !string.IsNullOrWhiteSpace(card.CardData.runtimeId))
                    {
                        currentCards[card.CardData.runtimeId] = (slot, card);
                    }
                }

                foreach (var boardCard in playerSnapshot.board)
                {
                    if (boardCard.occupied &&
                        boardCard.occupant != null &&
                        !string.IsNullOrWhiteSpace(boardCard.occupant.runtimeId))
                    {
                        snapshotCards[boardCard.occupant.runtimeId] = (boardCard.slot, boardCard.occupant);
                    }
                }

                Debug.Log($"[UpdateBoard] P{playerIndex} Snapshot cards: {string.Join(", ", snapshotCards.Select(x => $"{x.Value.slot}={x.Value.data.displayName}(ID:{ShortRuntimeId(x.Key)})").ToList())}");
                Debug.Log($"[UpdateBoard] P{playerIndex} Current cards:  {string.Join(", ", currentCards.Select(x => $"{x.Value.slot}={x.Value.view.CardData.displayName}(ID:{ShortRuntimeId(x.Key)})").ToList())}");

                foreach (var entry in snapshotCards)
                {
                    var runtimeId = entry.Key;
                    if (!currentCards.TryGetValue(runtimeId, out var current) || current.view?.CardData == null)
                    {
                        continue;
                    }

                    var snapshotSlot = entry.Value.slot;
                    var snapshotData = entry.Value.data;
                    var oldHP = current.view.CardData.currentHealth;

                    if (current.slot != snapshotSlot)
                    {
                        Debug.Log($"[UpdateBoard] Moving {snapshotData.displayName} {current.slot}->{snapshotSlot}");
                        ReparentCardToSlot(current.view, playerIndex, current.slot, snapshotSlot);
                        Debug.Log($"[GameplayPresenter3D] Moved card {snapshotData.displayName} P{playerIndex} {current.slot}->{snapshotSlot}");
                    }

                    CopyBoardCardData(current.view.CardData, snapshotData);
                    current.view.UpdateStatsDisplay();

                    if (snapshotData.currentHealth < oldHP && snapshotData.currentHealth > 0)
                    {
                        current.view.SetColor(Color.red);
                        StartCoroutine(ResetCardColor(current.view, 0.2f));
                    }
                    else if (snapshotData.currentHealth <= 0)
                    {
                        current.view.AnimateDeath();
                        StartCoroutine(ClearBoardSlotAfterDelay(playerIndex, snapshotSlot, current.view.CardData.runtimeId, 0.5f));
                    }
                }

                foreach (var entry in currentCards)
                {
                    var runtimeId = entry.Key;
                    var currentSlot = entry.Value.slot;
                    var currentView = entry.Value.view;

                    if (snapshotCards.ContainsKey(runtimeId))
                    {
                        continue;
                    }

                    if (currentView?.CardData != null && currentView.CardData.currentHealth > 0)
                    {
                        currentView.AnimateDeath();
                    }

                    if (currentView?.CardData != null)
                    {
                        StartCoroutine(RemoveBoardCardViewAfterDelay(currentView, playerIndex, currentSlot, currentView.CardData.runtimeId, 0.5f));
                    }
                }

                foreach (var entry in snapshotCards)
                {
                    if (currentCards.ContainsKey(entry.Key))
                    {
                        continue;
                    }

                    var snapshotSlot = entry.Value.slot;
                    var snapshotData = entry.Value.data;
                    Debug.Log($"[UpdateBoard] Card NOT in currentCards: {snapshotData.displayName} (ID:{ShortRuntimeId(entry.Key)}) -> creating at {snapshotSlot}");
                    CreateBoardCard(snapshotData, playerIndex, snapshotSlot);
                    Debug.Log($"[GameplayPresenter3D] Created card {snapshotData.displayName} at P{playerIndex} {snapshotSlot}");
                }
            }

            return true;
        }

        private static void CopyBoardCardData(BoardCardDto destination, BoardCardDto source)
        {
            if (destination == null || source == null)
            {
                return;
            }

            destination.runtimeId = source.runtimeId;
            destination.cardId = source.cardId;
            destination.displayName = source.displayName;
            destination.manaCost = source.manaCost;
            destination.attackMotionLevel = source.attackMotionLevel;
            destination.attackShakeLevel = source.attackShakeLevel;
            destination.attackDeliveryType = source.attackDeliveryType;
            destination.ownerIndex = source.ownerIndex;
            destination.attack = source.attack;
            destination.currentHealth = source.currentHealth;
            destination.maxHealth = source.maxHealth;
            destination.armor = source.armor;
            destination.slot = source.slot;
            destination.canAttack = source.canAttack;
            destination.unitType = source.unitType;
            destination.turnsUntilCanAttack = source.turnsUntilCanAttack;
            destination.statusEffects = source.statusEffects;
            destination.abilities = source.abilities;
        }

        private void UpdateLocalHand(DuelSnapshotDto snapshot)
        {
            if (snapshot?.players == null || snapshot.players.Length == 0)
                return;

            var localPlayer = snapshot.players[snapshot.localPlayerIndex];
            if (localPlayer?.hand != null)
            {
                hand3DManager.RefreshHand(localPlayer.hand);
            }
        }

        private void UpdateHUD(DuelSnapshotDto snapshot)
        {
            if (hud3D == null)
                return;

            var local = snapshot.players[snapshot.localPlayerIndex];
            var remote = snapshot.players[1 - snapshot.localPlayerIndex];

            if (local != null)
            {
                var localHeroMaxHealth = Mathf.Max(1, snapshot.localHeroMaxHealth);
                hud3D.UpdateLocalHeroInfo(local.heroHealth, localHeroMaxHealth, local.mana, local.maxMana);
            }

            if (remote != null)
            {
                var remoteHeroMaxHealth = Mathf.Max(1, snapshot.remoteHeroMaxHealth);
                hud3D.UpdateRemoteHeroInfo(remote.heroHealth, remoteHeroMaxHealth, remote.mana, remote.maxMana);
            }

            bool isLocalTurn = ResolveDisplayedLocalTurn(snapshot);
            hud3D.UpdateTurnInfo(snapshot.turnNumber, snapshot.activePlayerIndex, isLocalTurn);

            // Actualizar botón End Turn
            if (endTurnButton != null)
            {
                endTurnButton.SetEnabled(isLocalTurn);
            }

            // Actualizar glow en slots jugables
            HighlightPlayableSlots(snapshot, isLocalTurn);
        }

        private bool ResolveDisplayedLocalTurn(DuelSnapshotDto snapshot)
        {
            if (snapshot == null)
            {
                return false;
            }

            if (!SnapshotTurnAuthority.IsMatchPlayable(snapshot))
            {
                return false;
            }

            string currentPlayerId = null;
            if (GamePlayStateManager.Instance != null)
            {
                (_, currentPlayerId, _) = GamePlayStateManager.Instance.GetMatchInfo();
            }

            if (!string.IsNullOrWhiteSpace(snapshot.activePlayerId) &&
                !string.IsNullOrWhiteSpace(currentPlayerId))
            {
                var resolvedFromPlayerId = string.Equals(snapshot.activePlayerId, currentPlayerId, System.StringComparison.Ordinal);
                if (snapshot.isLocalPlayersTurn != resolvedFromPlayerId)
                {
                    Debug.LogWarning($"[GameplayPresenter3D] Turn mismatch: snapshot.isLocalPlayersTurn={snapshot.isLocalPlayersTurn}, activePlayerId={snapshot.activePlayerId}, currentPlayerId={currentPlayerId}, activePlayerIndex={snapshot.activePlayerIndex}");
                }
            }

            return SnapshotTurnAuthority.IsLocalTurn(snapshot);
        }

        private void HighlightPlayableSlots(DuelSnapshotDto snapshot, bool isLocalTurn)
        {
            // Clear all glows
            for (int p = 0; p < 2; p++)
            {
                foreach (BoardSlot slot in System.Enum.GetValues(typeof(BoardSlot)))
                {
                    var slotComponent = board3DManager.GetSlot(p, slot);
                    if (slotComponent != null)
                    {
                        slotComponent.SetGlow(false);
                    }
                }
            }

            // Si no es el turno del jugador local, no mostrar glows
            if (!isLocalTurn || snapshot?.players == null)
                return;

            // Colectar todos los slots jugables
            var playableSlots = new System.Collections.Generic.HashSet<(int playerIndex, BoardSlot slot)>();

            var localPlayer = snapshot.players[snapshot.localPlayerIndex];
            if (localPlayer?.board != null)
            {
                foreach (var boardCard in localPlayer.board)
                {
                    // Si el slot está vacío o ocupado, es potencialmente jugable (según las reglas del juego)
                    playableSlots.Add((snapshot.localPlayerIndex, boardCard.slot));
                }
            }

            // Resaltar los slots jugables
            foreach (var (playerIndex, slot) in playableSlots)
            {
                var slotComponent = board3DManager.GetSlot(playerIndex, slot);
                if (slotComponent != null)
                {
                    slotComponent.SetGlow(true);
                }
            }
        }

        public void RequestPlayCard(string runtimeCardKey, BoardSlot targetSlot)
        {
            Debug.Log($"[GameplayPresenter3D] RequestPlayCard: {runtimeCardKey} → {targetSlot}");

            if (GameModeManager.Instance.IsLocalMode)
            {
                var coordinator = LocalSinglePlayerCoordinator.Instance;
                if (coordinator == null)
                {
                    Debug.LogError("[GameplayPresenter3D] LocalSinglePlayerCoordinator.Instance is null!");
                    return;
                }

                bool success = coordinator.RequestPlayCard(runtimeCardKey, targetSlot);
                Debug.Log($"[GameplayPresenter3D] RequestPlayCard result: {success}");
                if (success)
                {
                    hud3D?.Log($"Played card to {targetSlot}");
                }
                else
                {
                    Debug.LogWarning("[GameplayPresenter3D] coordinator.RequestPlayCard returned false");
                }
            }
            else
            {
                var coordinator = MatchCoordinatorFactory.Instance.GetCoordinator();
                if (coordinator != null)
                {
                    coordinator.RequestPlayCard(runtimeCardKey, (int)targetSlot);
                    hud3D?.Log($"Played card to {targetSlot}");
                }
                else
                {
                    var netCoordinator = CardDuelNetworkCoordinator.Instance;
                    if (netCoordinator == null)
                    {
                        Debug.LogError("[GameplayPresenter3D] No coordinator found!");
                        return;
                    }

                    netCoordinator.RequestPlayCardServerRpc(runtimeCardKey, (int)targetSlot);
                    hud3D?.Log($"Played card to {targetSlot}");
                }
            }
        }

        public void RequestEndTurn()
        {
            Debug.Log("[GameplayPresenter3D] RequestEndTurn invoked");

            if (_latestSnapshot != null)
            {
                if (!SnapshotTurnAuthority.IsLocalTurn(_latestSnapshot))
                {
                    Debug.LogWarning("[GameplayPresenter3D] RequestEndTurn blocked: it is not the local player's turn.");
                    return;
                }
            }

            if (GameModeManager.Instance.IsLocalMode)
            {
                var coordinator = LocalSinglePlayerCoordinator.Instance;
                if (coordinator != null)
                {
                    bool success = coordinator.RequestEndTurn();
                    if (success)
                    {
                        hud3D?.Log("Turn ended");
                    }
                }
            }
            else
            {
                var coordinator = MatchCoordinatorFactory.Instance.GetCoordinator();
                if (coordinator != null)
                {
                    endTurnButton?.SetEnabled(false);
                    Debug.Log($"[GameplayPresenter3D] Sending EndTurn via {MatchCoordinatorFactory.Instance.CurrentType}");
                    coordinator.RequestEndTurn();
                    hud3D?.Log("Turn ended");
                }
                else
                {
                    var netCoordinator = CardDuelNetworkCoordinator.Instance;
                    if (netCoordinator != null)
                    {
                        endTurnButton?.SetEnabled(false);
                        Debug.Log("[GameplayPresenter3D] Sending EndTurn via legacy CardDuelNetworkCoordinator");
                        netCoordinator.RequestEndTurnServerRpc();
                        hud3D?.Log("Turn ended");
                    }
                    else
                    {
                        Debug.LogError("[GameplayPresenter3D] No coordinator available for EndTurn.");
                    }
                }
            }
        }

        public void RequestDestroyCard(string runtimeCardId)
        {
            if (string.IsNullOrWhiteSpace(runtimeCardId))
            {
                Debug.LogWarning("[GameplayPresenter3D] RequestDestroyCard blocked: runtimeCardId is empty.");
                return;
            }

            if (!IsLocalBoardCard(runtimeCardId))
            {
                Debug.LogWarning($"[GameplayPresenter3D] RequestDestroyCard blocked: '{runtimeCardId}' is not on the local player's board.");
                return;
            }

            if (GameModeManager.Instance.IsLocalMode)
            {
                Debug.LogWarning("[GameplayPresenter3D] DestroyCard is server-authoritative and is not implemented for local AI mode.");
                hud3D?.Log("Destroy card is only available in server matches.");
                return;
            }

            var coordinator = MatchCoordinatorFactory.Instance.GetCoordinator();
            if (coordinator != null)
            {
                Debug.Log($"[GameplayPresenter3D] RequestDestroyCard: {runtimeCardId}");
                coordinator.RequestDestroyCard(runtimeCardId);
                hud3D?.Log("Destroy card requested");
            }
            else
            {
                Debug.LogError("[GameplayPresenter3D] No coordinator available for DestroyCard.");
            }
        }

        private bool IsLocalBoardCard(string runtimeCardId)
        {
            if (_latestSnapshot?.players == null ||
                _latestSnapshot.localPlayerIndex < 0 ||
                _latestSnapshot.localPlayerIndex >= _latestSnapshot.players.Length)
            {
                return false;
            }

            var local = _latestSnapshot.players[_latestSnapshot.localPlayerIndex];
            if (local?.board == null)
            {
                return false;
            }

            foreach (var slot in local.board)
            {
                var occupant = slot?.occupant;
                if (occupant != null &&
                    string.Equals(occupant.runtimeId, runtimeCardId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private System.Collections.IEnumerator ResetCardColor(ICardDisplay card, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (card != null)
                card.ResetColor();
        }

        private System.Collections.IEnumerator ClearBoardSlotAfterDelay(int playerIndex, BoardSlot slot, string expectedRuntimeId, float delay)
        {
            yield return new WaitForSeconds(delay);

            var currentCard = board3DManager.GetCardInSlot(playerIndex, slot);
            if (currentCard == null)
            {
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(expectedRuntimeId) &&
                currentCard.CardData != null &&
                !string.Equals(currentCard.CardData.runtimeId, expectedRuntimeId, System.StringComparison.Ordinal))
            {
                yield break;
            }

            board3DManager.ClearSlot(playerIndex, slot);
        }

        private System.Collections.IEnumerator RemoveBoardCardViewAfterDelay(ICardDisplay cardView, int playerIndex, BoardSlot slot, string expectedRuntimeId, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (cardView is MonoBehaviour behaviour && behaviour == null)
            {
                yield break;
            }

            var currentCard = board3DManager.GetCardInSlot(playerIndex, slot);
            if (currentCard == cardView)
            {
                board3DManager.RemoveCardReference(playerIndex, slot);
            }

            if (cardView?.CardData != null &&
                !string.IsNullOrWhiteSpace(expectedRuntimeId) &&
                !string.Equals(cardView.CardData.runtimeId, expectedRuntimeId, StringComparison.Ordinal))
            {
                yield break;
            }

            var go = cardView.GetGameObject();
            if (go != null)
            {
                Destroy(go);
            }
        }

        private void DetectAndAnimateRepositioning(DuelSnapshotDto snapshot)
        {
            if (_previousSnapshot == null || snapshot?.players == null || _previousSnapshot.players == null)
            {
                _previousSnapshot = snapshot;
                return;
            }

            // Detectar cartas que cambiaron de slot
            for (int playerIndex = 0; playerIndex < snapshot.players.Length && playerIndex < _previousSnapshot.players.Length; playerIndex++)
            {
                var currentPlayer = snapshot.players[playerIndex];
                var previousPlayer = _previousSnapshot.players[playerIndex];

                if (currentPlayer?.board == null || previousPlayer?.board == null)
                    continue;

                // Mapear cartas por runtime ID en ambos snapshots
                var currentCards = new System.Collections.Generic.Dictionary<string, (BoardSlot slot, BoardCardDto card)>();
                var previousCards = new System.Collections.Generic.Dictionary<string, (BoardSlot slot, BoardCardDto card)>();

                foreach (var slotSnapshot in currentPlayer.board)
                {
                    if (slotSnapshot.occupied && slotSnapshot.occupant != null)
                    {
                        currentCards[slotSnapshot.occupant.runtimeId] = (slotSnapshot.slot, slotSnapshot.occupant);
                    }
                }

                foreach (var slotSnapshot in previousPlayer.board)
                {
                    if (slotSnapshot.occupied && slotSnapshot.occupant != null)
                    {
                        previousCards[slotSnapshot.occupant.runtimeId] = (slotSnapshot.slot, slotSnapshot.occupant);
                    }
                }

                // Detectar cartas que cambiaron de slot
                foreach (var runtimeId in currentCards.Keys)
                {
                    if (previousCards.TryGetValue(runtimeId, out var prev))
                    {
                        var current = currentCards[runtimeId];
                        if (prev.slot != current.slot)
                        {
                            // Carta se movió
                            var card = board3DManager.GetCardInSlot(playerIndex, current.slot);
                            if (card != null && card.TryGetTransform(out var cardTransform))
                            {
                                var currSlot = board3DManager.GetSlot(playerIndex, current.slot);
                                if (currSlot != null)
                                {
                                    var startPos = cardTransform.position;
                                    var endPos = currSlot.transform.position;

                                    StartCoroutine(AnimateCardMovement(card, startPos, endPos, 0.4f));
                                    hud3D?.Log($"{current.card.displayName} repositioned to {current.slot}");
                                }
                            }
                        }
                    }
                }
            }

            _previousSnapshot = snapshot;
        }

        public void SaveOriginalCardPositions(int playerIndex)
        {
            _originalCardPositions.Clear();

            // Get actual board state from board manager (not snapshot - avoids stale data)
            foreach (var slot in System.Enum.GetValues(typeof(BoardSlot)) as BoardSlot[])
            {
                var card = board3DManager.GetCardInSlot(playerIndex, slot);
                if (card != null && card.CardData != null && card.TryGetTransform(out var cardTransform))
                {
                    _originalCardPositions[card] = cardTransform.position;
                    Debug.Log($"[GameplayPresenter3D] Saved original position for {card.CardData.displayName} at {slot}");
                }
            }

            Debug.Log($"[GameplayPresenter3D] Saved {_originalCardPositions.Count} original card positions");
        }

        public void PreviewCardDisplacement(int playerIndex, BoardSlot targetSlot)
        {
            if (_originalCardPositions.Count == 0)
                return;

            // Stop current animation
            if (_previewAnimCoroutine != null)
                StopCoroutine(_previewAnimCoroutine);

            // Snap cards back to originals before new preview
            foreach (var card in _originalCardPositions.Keys)
            {
                if (card != null && card.TryGetTransform(out var cardTransform))
                {
                    cardTransform.position = _originalCardPositions[card];
                }
            }

            // Calculate displacement (uses board manager state, not snapshot)
            var displacements = CalculateDisplacementsFromBoardManager(playerIndex, targetSlot);

            // Animate to preview positions
            _previewAnimCoroutine = AnimateDisplacements(displacements, 0.3f);
            StartCoroutine(_previewAnimCoroutine);
        }

        public void CancelCardDisplacement(int playerIndex)
        {
            if (_previewAnimCoroutine != null)
                StopCoroutine(_previewAnimCoroutine);

            // Animate back to original positions
            if (_originalCardPositions.Count > 0)
            {
                _previewAnimCoroutine = AnimateRestorePositions(0.3f);
                StartCoroutine(_previewAnimCoroutine);
            }
        }

        public void ClearPreviewPositions()
        {
            if (_previewAnimCoroutine != null)
                StopCoroutine(_previewAnimCoroutine);

            _originalCardPositions.Clear();
            Debug.Log("[GameplayPresenter3D] Cleared preview positions");
        }

        private Dictionary<ICardDisplay, Vector3> CalculateDisplacementsFromBoardManager(int playerIndex, BoardSlot targetSlot)
        {
            var displacements = new Dictionary<ICardDisplay, Vector3>();

            // Get current board state from board manager (not snapshot - avoids stale data)
            var frontCard = board3DManager.GetCardInSlot(playerIndex, BoardSlot.Front);
            var leftCard = board3DManager.GetCardInSlot(playerIndex, BoardSlot.BackLeft);
            var rightCard = board3DManager.GetCardInSlot(playerIndex, BoardSlot.BackRight);

            Debug.Log($"[CalcDisp] targetSlot={targetSlot}, Front={frontCard?.CardData.displayName}({frontCard!=null}), Left={leftCard?.CardData.displayName}({leftCard!=null}), Right={rightCard?.CardData.displayName}({rightCard!=null})");
            Debug.Log($"[CalcDisp] Condition check: targetSlot==Front? {targetSlot == BoardSlot.Front}, frontCard!=null? {frontCard!=null}");

            // Calculate shifts based on target slot
            if (targetSlot == BoardSlot.Front && frontCard != null)
            {
                Debug.Log($"[CalcDisp] ✓ ENTERING: Placing on Front (Front occupied)");
                // Place at Front: Front→Left, Left→Right
                var leftSlot = board3DManager.GetSlot(playerIndex, BoardSlot.BackLeft);
                if (leftSlot != null)
                {
                    displacements[frontCard] = leftSlot.transform.position;
                    Debug.Log($"[CalcDisp] ✓ Front {frontCard.CardData.displayName} → Left");
                }

                if (leftCard != null)
                {
                    var rightSlot = board3DManager.GetSlot(playerIndex, BoardSlot.BackRight);
                    if (rightSlot != null)
                    {
                        displacements[leftCard] = rightSlot.transform.position;
                        Debug.Log($"[CalcDisp] ✓ Left {leftCard.CardData.displayName} → Right");
                    }
                }
            }
            else if (targetSlot == BoardSlot.BackLeft && leftCard != null)
            {
                Debug.Log($"[CalcDisp] ✓ ENTERING: Placing on BackLeft (Left occupied)");
                // Place at Left: Left→Right
                var rightSlot = board3DManager.GetSlot(playerIndex, BoardSlot.BackRight);
                if (rightSlot != null)
                {
                    displacements[leftCard] = rightSlot.transform.position;
                    Debug.Log($"[CalcDisp] ✓ Left {leftCard.CardData.displayName} → Right");
                }
            }
            else
            {
                Debug.Log($"[CalcDisp] ✗ NO CONDITION: targetSlot={targetSlot}, frontCard={frontCard}, leftCard={leftCard}");
            }

            Debug.Log($"[CalcDisp] RESULT: {displacements.Count} displacements");
            return displacements;
        }

        private System.Collections.IEnumerator AnimateDisplacements(Dictionary<ICardDisplay, Vector3> displacements, float duration)
        {
            float elapsed = 0f;
            var startPositions = new Dictionary<ICardDisplay, Vector3>();

            foreach (var card in displacements.Keys)
            {
                if (card != null && card.TryGetTransform(out var cardTransform))
                {
                    startPositions[card] = cardTransform.position;
                }
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = 1f - Mathf.Pow(1f - t, 3f); // Ease-out cubic

                foreach (var card in displacements.Keys)
                {
                    if (card != null && startPositions.ContainsKey(card) && card.TryGetTransform(out var cardTransform))
                    {
                        cardTransform.position = Vector3.Lerp(startPositions[card], displacements[card], t);
                    }
                }

                yield return null;
            }

            foreach (var card in displacements.Keys)
            {
                if (card != null && card.TryGetTransform(out var cardTransform))
                {
                    cardTransform.position = displacements[card];
                }
            }
        }

        private System.Collections.IEnumerator AnimateRestorePositions(float duration)
        {
            float elapsed = 0f;
            var startPositions = new Dictionary<ICardDisplay, Vector3>();

            // Save start positions
            foreach (var card in _originalCardPositions.Keys)
            {
                if (card != null && card.TryGetTransform(out var cardTransform))
                {
                    startPositions[card] = cardTransform.position;
                }
            }

            while (elapsed < duration && _originalCardPositions.Count > 0)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = 1f - Mathf.Pow(1f - t, 3f); // Ease-out cubic

                foreach (var card in _originalCardPositions.Keys)
                {
                    if (card != null && startPositions.ContainsKey(card) && card.TryGetTransform(out var cardTransform))
                    {
                        // Lerp from saved start position to original
                        cardTransform.position = Vector3.Lerp(startPositions[card], _originalCardPositions[card], t);
                    }
                }

                yield return null;
            }

            // Snap to original positions
            foreach (var card in _originalCardPositions.Keys)
            {
                if (card != null && card.TryGetTransform(out var cardTransform))
                {
                    cardTransform.position = _originalCardPositions[card];
                }
            }
        }

        private void ReparentCardToSlot(ICardDisplay cardView, int playerIndex, BoardSlot oldSlot, BoardSlot newSlot)
        {
            board3DManager.MoveCardBetweenSlots(playerIndex, oldSlot, newSlot, cardView);
        }

        private void CreateBoardCard(BoardCardDto cardData, int playerIndex, BoardSlot slot)
        {
            var cardGo = Instantiate(boardCardPlayedPrefab ?? boardCardPrefab);
            cardGo.name = $"Card3D_{cardData.runtimeId}_{cardData.displayName}_unittype:{cardData.unitType}";
            cardGo.transform.SetParent(transform);

            var cardPlayed = cardGo.GetComponent<Card3DPlayed>();
            if (cardPlayed == null)
                cardPlayed = cardGo.AddComponent<Card3DPlayed>();

            cardPlayed.Initialize(cardData, playerIndex);

            board3DManager.SetCardInSlot(playerIndex, slot, cardPlayed);
        }

        private System.Collections.IEnumerator AnimateCardMovement(ICardDisplay card, Vector3 startPos, Vector3 endPos, float duration)
        {
            if (card == null || !card.TryGetTransform(out var cardTransform))
                yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (cardTransform == null)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Ease-out cubic
                float easeT = 1f - Mathf.Pow(1f - t, 3f);
                cardTransform.position = Vector3.Lerp(startPos, endPos, easeT);

                yield return null;
            }

            if (cardTransform != null)
            {
                cardTransform.position = endPos;
            }
        }
    }
}
