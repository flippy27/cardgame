using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.SinglePlayer;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Presenter principal de la pantalla de duelo.
    /// Esta versión unifica:
    /// - board fijo con 3 slots por lado
    /// - drag & drop
    /// - single player / multiplayer
    /// - ready / waiting / in-progress / completed
    /// - abandono / reconnect grace visual
    /// </summary>
    public sealed class BattleScreenPresenter : MonoBehaviour
    {
        [Header("Hand")]
        public Transform localHandRoot;
        public RectTransform dragLayer;

        [Header("Local Board Slots")]
        public BoardSlotButton localFrontSlot;
        public BoardSlotButton localBackLeftSlot;
        public BoardSlotButton localBackRightSlot;

        [Header("Remote Board Slots")]
        public BoardSlotButton remoteFrontSlot;
        public BoardSlotButton remoteBackLeftSlot;
        public BoardSlotButton remoteBackRightSlot;

        [Header("HUD")]
        public TMPro.TextMeshProUGUI battleLogText;
        public TMPro.TextMeshProUGUI turnInfoText;
        public TMPro.TextMeshProUGUI heroInfoText;
        public TMPro.TextMeshProUGUI selectedCardText;
        public TMPro.TextMeshProUGUI localDeckCountText;
        public TMPro.TextMeshProUGUI localDeadPileCountText;
        public TMPro.TextMeshProUGUI remoteDeckCountText;
        public TMPro.TextMeshProUGUI remoteDeadPileCountText;
        public Button endTurnButton;

        [Header("Prefabs")]
        public HandCardButton handCardPrefab;
        public CardViewWidget boardCardPrefab;
        public GameObject dragGhost3DPrefab;

        [Header("Detail View")]
        public CardDetailView detailViewInstance;

        [Header("Toast")]
        public GameObject toastPrefab;
        public Transform toastContainer;

        [Header("Debug")]
        public DebugPanel debugPanel;

        private DuelSnapshotDto _latestSnapshot;
        private CardInHandDto _selectedCard;
        private CardInHandDto _draggedCard;
        private HandCardButton _dragSource;
        private GameObject _dragGhost;
        private DragGhost3D _dragGhost3D;
        private bool _dragDropCommitted;
        private BoardSlotButton _dragOverSlot;

        private bool _matchCompletionHandled;
        private string _matchId;
        private string _playerId;
        private string _opponentId;
        private int _matchStartTime;
        private bool _inDiscardMode;

        private readonly List<GameObject> _spawnedHandCards = new();
        private readonly Dictionary<BoardSlot, BoardSlotButton> _localSlots = new();
        private readonly Dictionary<BoardSlot, BoardSlotButton> _remoteSlots = new();
        private readonly Dictionary<string, BoardSlot> _cardSlotPositions = new();
        private readonly Dictionary<string, (BoardSlot slot, Vector2 anchoredPos)> _cardPreviousStates = new();
        private readonly Dictionary<BoardSlot, Color> _previewSlotColors = new();
        private bool _subscribed;

        public CardViewWidget BoardCardPrefab => boardCardPrefab;
        public bool HasDraggedCard => _draggedCard != null;

#if ODIN_INSPECTOR
        [ShowInInspector, ReadOnly]
        public Dictionary<string, BoardSlot> DebugCardSlotPositions => _cardSlotPositions;

        [ShowInInspector, ReadOnly]
        public Dictionary<string, (BoardSlot slot, Vector2 anchoredPos)> DebugCardPreviousStates => _cardPreviousStates;
#endif

        private void Update()
        {
            // Save card world positions every frame so we have previous frame state when snapshot arrives
            SaveCurrentCardWorldPositions();
        }

        private void Awake()
        {
            Debug.Log($"[BattleScreenPresenter] Awake called");
            if (!_subscribed)
            {
                BattleSnapshotBus.SubscribeAndGetLast(HandleSnapshot);
                _subscribed = true;
                Debug.Log($"[BattleScreenPresenter] Subscribed to BattleSnapshotBus from Awake");
            }
        }

        private void OnEnable()
        {
            if (!_subscribed)
            {
                BattleSnapshotBus.SubscribeAndGetLast(HandleSnapshot);
                _subscribed = true;
                Debug.Log("[BattleScreenPresenter] OnEnable - subscribed to BattleSnapshotBus");
            }

            if (endTurnButton != null)
            {
                endTurnButton.onClick.AddListener(HandleEndTurnPressed);
            }

            // Initialize ToastManager
            if (ToastManager.Instance != null && toastPrefab != null)
            {
                ToastManager.Instance.toastPrefab = toastPrefab;
                ToastManager.Instance.toastContainer = toastContainer ?? transform;
            }

            CacheBoardSlots();
            RefreshAllSlotVisuals();
            RefreshSelectionLabel();
        }

        private void OnDisable()
        {
            if (endTurnButton != null)
            {
                endTurnButton.onClick.RemoveListener(HandleEndTurnPressed);
            }
        }

        private void OnDestroy()
        {
            BattleSnapshotBus.SnapshotReceived -= HandleSnapshot;
        }

        public void NotifyCardClicked(CardInHandDto dto)
        {
            if (dto == null)
            {
                return;
            }

            if (_inDiscardMode)
            {
                GameLogger.Info("UI", $"Discard mode: {dto.displayName} selected for discard");
                if (LocalSinglePlayerCoordinator.Instance != null && LocalSinglePlayerCoordinator.Instance.IsActive)
                {
                    LocalSinglePlayerCoordinator.Instance.RequestDiscardCard(dto.runtimeCardKey);
                }
                _inDiscardMode = false;
                RefreshAllSlotVisuals();
                RebuildHandOnly();
                return;
            }

            if (!CanStartInteractionWithCard(dto))
            {
                return;
            }

            if (_selectedCard != null && _selectedCard.runtimeCardKey == dto.runtimeCardKey)
            {
                _selectedCard = null;
            }
            else
            {
                _selectedCard = dto;
            }

            RefreshSelectionLabel();
            RefreshAllSlotVisuals();
            RebuildHandOnly();
        }

        public bool CanCurrentCardBePlayedTo(BoardSlot slot)
        {
            return CanCardBePlayedTo(_selectedCard, slot);
        }

        public void ShowDetailView(CardInHandDto dto)
        {
            if (dto == null || detailViewInstance == null)
            {
                return;
            }

            detailViewInstance.SetCard(dto);
            detailViewInstance.gameObject.SetActive(true);
        }

        public void HideDetailView()
        {
            if (detailViewInstance != null)
            {
                detailViewInstance.gameObject.SetActive(false);
            }
        }

        public void BeginDrag(CardInHandDto dto, HandCardButton source, Vector2 screenPosition)
        {


            if (dto == null || _inDiscardMode)
            {
                Debug.Log($"[BattleScreenPresenter] BeginDrag rejected: dto null={dto == null}, inDiscardMode={_inDiscardMode}");
                return;
            }

            if (!CanStartInteractionWithCard(dto))
            {
                Debug.Log($"[BattleScreenPresenter] BeginDrag rejected: CanStartInteractionWithCard returned false");
                return;
            }

            HideDetailView();
            ClearDragPreview();

            _selectedCard = dto;
            _draggedCard = dto;
            _dragSource = source;
            _dragDropCommitted = false;



            CreateDragGhost(dto, screenPosition);
            RefreshSelectionLabel();
            RefreshAllSlotVisuals();
            // Don't RebuildHandOnly here - it destroys the HandCardButton being dragged
        }

        public void UpdateDrag(Vector2 screenPosition)
        {
            if (_dragGhost3D != null)
            {
                _dragGhost3D.SetTargetPosition(screenPosition, Camera.main);
            }

            // Continuous raycast to find any slot under mouse (for visual feedback)
            if (_draggedCard != null && _dragSource != null)
            {
                var anySlot = RaycastForAnySlot(screenPosition);
                if (anySlot != null && anySlot.isLocalSide)
                {
                    // Track slot for visual feedback (whether valid or not)
                    _dragOverSlot = anySlot;
                }
                else
                {
                    _dragOverSlot = null;
                }
            }
        }

        public void EndDrag()
        {
            // Try using tracked slot first
            var targetSlot = _dragOverSlot;

            // Fallback: raycast from mouse position if tracking didn't work
            if (targetSlot == null && !_dragDropCommitted && _draggedCard != null)
            {
                var mouse = Mouse.current;
                if (mouse != null)
                {
                    var mousePos = mouse.position.ReadValue();
                    targetSlot = RaycastForSlot(mousePos);
                }
            }

            var slotInfo = _dragOverSlot != null ? _dragOverSlot.slot.ToString() : "NULL";
            var cardInfo = _draggedCard != null ? _draggedCard.displayName : "NULL";


            if (!_dragDropCommitted && _draggedCard != null && targetSlot != null)
            {
                
                TryPlayDraggedCardTo(targetSlot.slot, targetSlot.CardAnchor);
            }

            if (!_dragDropCommitted)
            {
                DestroyDragGhostImmediate();
            }

            ClearDragPreview();
            _draggedCard = null;
            _dragSource = null;
            _dragOverSlot = null;
            RefreshAllSlotVisuals();
            RebuildHandOnly();
        }

        private BoardSlotButton RaycastForAnySlot(Vector2 screenPosition)
        {
            // Check all slots to see which one is under the mouse
            foreach (var slot in _localSlots.Values)
            {
                if (slot != null && IsScreenPointOverSlot(slot, screenPosition))
                {
                    return slot;
                }
            }

            // Check remote slots too
            foreach (var slot in _remoteSlots.Values)
            {
                if (slot != null && IsScreenPointOverSlot(slot, screenPosition))
                {
                    return slot;
                }
            }

            return null;
        }

        private BoardSlotButton RaycastForSlot(Vector2 screenPosition)
        {
            // Check local slots for valid drop targets
            foreach (var slot in _localSlots.Values)
            {
                if (slot != null && slot.isLocalSide && IsScreenPointOverSlot(slot, screenPosition))
                {
                    if (CanCardBePlayedTo(_draggedCard, slot.slot))
                    {
                        return slot;
                    }
                }
            }

            return null;
        }

        private bool IsScreenPointOverSlot(BoardSlotButton slot, Vector2 screenPosition)
        {
            var rect = slot.transform as RectTransform;
            if (rect == null)
            {
                return false;
            }

            var canvas = slot.GetComponentInParent<Canvas>();
            var cam = canvas != null ? canvas.worldCamera : Camera.main;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, cam);
        }

        public void SetDragOverSlot(BoardSlotButton slot)
        {
            if (_dragOverSlot != slot)
            {
                ClearDragPreview();
                _dragOverSlot = slot;

                if (slot != null && _draggedCard != null)
                {
                    ShowDragPreview(slot.slot);
                }
            }
        }

        private void ShowDragPreview(BoardSlot targetSlot)
        {
            if (_latestSnapshot == null || _draggedCard == null)
            {
                return;
            }

            var local = _latestSnapshot.players[_latestSnapshot.localPlayerIndex];

            // Check if target slot is occupied
            var targetSnapshot = local.board?.FirstOrDefault(x => x.slot == targetSlot);
            if (targetSnapshot == null || !targetSnapshot.occupied || targetSnapshot.occupant == null)
            {
                return;  // No displacement needed
            }

            // Compute displacement chain
            var chain = new List<(BoardSlot slot, BoardCardDto card)>();
            var currentSlot = targetSlot;
            var currentCard = targetSnapshot.occupant;

            // Simulate displacement: Front → BackLeft → BackRight
            if (currentSlot == BoardSlot.Front)
            {
                chain.Add((BoardSlot.BackLeft, currentCard));
                var backLeftSlot = local.board?.FirstOrDefault(x => x.slot == BoardSlot.BackLeft);
                if (backLeftSlot != null && backLeftSlot.occupied && backLeftSlot.occupant != null)
                {
                    currentSlot = BoardSlot.BackLeft;
                    currentCard = backLeftSlot.occupant;
                    chain.Add((BoardSlot.BackRight, currentCard));
                }
            }
            else if (currentSlot == BoardSlot.BackLeft)
            {
                chain.Add((BoardSlot.BackRight, currentCard));
            }

            // Highlight slots in the displacement chain with a preview color
            _previewSlotColors.Clear();
            foreach (var (slot, card) in chain)
            {
                if (_localSlots.TryGetValue(slot, out var slotButton))
                {
                    var image = slotButton.placeholderImage;
                    if (image != null)
                    {
                        _previewSlotColors[slot] = image.color;
                        image.color = Color.yellow * 0.5f;  // Semi-transparent yellow
                        Debug.Log($"[Preview] {card.displayName} → {slot}");
                    }
                }
            }

            // Highlight target slot differently
            if (_localSlots.TryGetValue(targetSlot, out var targetSlotButton))
            {
                var image = targetSlotButton.placeholderImage;
                if (image != null && !_previewSlotColors.ContainsKey(targetSlot))
                {
                    _previewSlotColors[targetSlot] = image.color;
                    image.color = Color.green * 0.5f;  // Semi-transparent green for target
                }
            }
        }

        private void ClearDragPreview()
        {
            // Restore original colors
            foreach (var (slot, originalColor) in _previewSlotColors)
            {
                if (_localSlots.TryGetValue(slot, out var slotButton))
                {
                    var image = slotButton.placeholderImage;
                    if (image != null)
                    {
                        image.color = originalColor;
                    }
                }
            }
            _previewSlotColors.Clear();
        }

        public void TryPlayDraggedCardTo(BoardSlot slot, RectTransform targetAnchor)
        {
            if (_draggedCard == null)
            {
                GameLogger.Warning("UI", "TryPlayDraggedCardTo: no dragged card");
                return;
            }

            GameLogger.Info("UI", $"TryPlayDraggedCardTo: {_draggedCard.displayName} to {slot}");
            var cardToPlay = _draggedCard;
            if (!TryPlayCardToSlot(cardToPlay, slot))
            {
                GameLogger.Warning("UI", "TryPlayDraggedCardTo: TryPlayCardToSlot failed");
                ClearDragPreview();
                DestroyDragGhostImmediate();
                _draggedCard = null;
                _dragSource = null;
                RefreshAllSlotVisuals();
                RebuildHandOnly();
                return;
            }

            GameLogger.Info("UI", "TryPlayDraggedCardTo: card played successfully");
            ClearDragPreview();
            _dragDropCommitted = true;
            _selectedCard = null;
            _draggedCard = null;
            _dragSource = null;

            RefreshSelectionLabel();
            RefreshAllSlotVisuals();
            RebuildHandOnly();

            if (_dragGhost != null && targetAnchor != null)
            {
                StartCoroutine(AnimateGhostToTargetAndDestroy(targetAnchor));
            }
            else
            {
                DestroyDragGhostImmediate();
            }
        }

        public void TryPlaySelectedCardTo(BoardSlot slot, RectTransform targetAnchor = null)
        {
            if (_selectedCard == null)
            {
                return;
            }

            var cardToPlay = _selectedCard;
            if (!TryPlayCardToSlot(cardToPlay, slot))
            {
                return;
            }

            _selectedCard = null;
            RefreshSelectionLabel();
            RefreshAllSlotVisuals();
            RebuildHandOnly();

            if (_dragGhost != null && targetAnchor != null)
            {
                StartCoroutine(AnimateGhostToTargetAndDestroy(targetAnchor));
            }
        }

        private void DetectAndAnimateCardMovements()
        {
            Debug.Log("[DetectAndAnimate] Called");
            if (_latestSnapshot == null || _latestSnapshot.players == null || _latestSnapshot.players.Length < 2)
            {
                Debug.Log("[DetectAndAnimate] Snapshot incomplete, returning");
                return;
            }

            Debug.Log($"[DetectAndAnimate] Snapshot OK, previous card positions: {_cardSlotPositions.Count}");

            // Save world positions BEFORE updating slots
            SaveCurrentCardWorldPositions();
            Debug.Log($"[DetectAndAnimate] Saved current positions, now have: {_cardPreviousStates.Count}");

            // Detect movements and prepare animation data
            var movementsToAnimate = new List<(string cardId, BoardSlot fromSlot, BoardSlot toSlot, bool isLocal)>();

            // Check local player board
            var local = _latestSnapshot.players[_latestSnapshot.localPlayerIndex];
            var localNewPositions = new Dictionary<string, BoardSlot>();
            if (local.board != null)
            {
                Debug.Log($"[DetectAndAnimate] Local board has {local.board.Length} slots");
                foreach (var slotSnapshot in local.board)
                {
                    if (slotSnapshot.occupant != null && !string.IsNullOrEmpty(slotSnapshot.occupant.runtimeId))
                    {
                        localNewPositions[slotSnapshot.occupant.runtimeId] = slotSnapshot.slot;
                        Debug.Log($"[DetectAndAnimate] Local card {slotSnapshot.occupant.runtimeId} in slot {slotSnapshot.slot}");
                    }
                }

                Debug.Log($"[DetectAndAnimate] New positions: {localNewPositions.Count}, previous: {_cardSlotPositions.Count}");
                foreach (var cardId in localNewPositions.Keys)
                {
                    if (_cardSlotPositions.TryGetValue(cardId, out var oldSlot))
                    {
                        var newSlot = localNewPositions[cardId];
                        if (oldSlot != newSlot)
                        {
                            Debug.Log($"[DetectAndAnimate] MOVEMENT DETECTED: {cardId} {oldSlot} → {newSlot}");
                            movementsToAnimate.Add((cardId, oldSlot, newSlot, true));
                        }
                    }
                    else
                    {
                        Debug.Log($"[DetectAndAnimate] Card {cardId} is new (not in previous state)");
                    }
                }
            }

            // Check remote player board
            var remoteNewPositions = new Dictionary<string, BoardSlot>();
            var remote = _latestSnapshot.players[1 - _latestSnapshot.localPlayerIndex];
            if (remote.board != null)
            {
                foreach (var slotSnapshot in remote.board)
                {
                    if (slotSnapshot.occupant != null && !string.IsNullOrEmpty(slotSnapshot.occupant.runtimeId))
                    {
                        remoteNewPositions[slotSnapshot.occupant.runtimeId] = slotSnapshot.slot;
                    }
                }

                foreach (var cardId in remoteNewPositions.Keys)
                {
                    if (_cardSlotPositions.TryGetValue(cardId, out var oldSlot))
                    {
                        var newSlot = remoteNewPositions[cardId];
                        if (oldSlot != newSlot)
                        {
                            Debug.Log($"[DetectAndAnimate] MOVEMENT DETECTED (remote): {cardId} {oldSlot} → {newSlot}");
                            movementsToAnimate.Add((cardId, oldSlot, newSlot, false));
                        }
                    }
                }
            }

            // Update card positions for next detection
            _cardSlotPositions.Clear();
            if (local.board != null)
            {
                foreach (var kvp in localNewPositions ?? new Dictionary<string, BoardSlot>())
                {
                    _cardSlotPositions[kvp.Key] = kvp.Value;
                }
            }
            foreach (var kvp in remoteNewPositions)
            {
                _cardSlotPositions[kvp.Key] = kvp.Value;
            }

            // Animate all movements
            Debug.Log($"[DetectAndAnimate] Total movements to animate: {movementsToAnimate.Count}");
            foreach (var (cardId, fromSlot, toSlot, isLocal) in movementsToAnimate)
            {
                Debug.Log($"[DetectAndAnimate] Calling AnimateCardMovement for {cardId}");
                AnimateCardMovement(cardId, fromSlot, toSlot, isLocal);
            }
        }

        private void SaveCurrentCardWorldPositions()
        {
            var allSlots = new List<(BoardSlot slot, BoardSlotButton button)>();
            foreach (var kvp in _localSlots)
                allSlots.Add((kvp.Key, kvp.Value));
            foreach (var kvp in _remoteSlots)
                allSlots.Add((kvp.Key, kvp.Value));

            foreach (var (slot, button) in allSlots)
            {
                var card = button.GetSpawnedCard();
                if (card != null && card.transform is RectTransform cardRect)
                {
                    var runtimeId = button.GetCurrentOccupantRuntimeId();
                    if (!string.IsNullOrEmpty(runtimeId))
                    {
                        _cardPreviousStates[runtimeId] = (slot, cardRect.anchoredPosition);
                    }
                }
            }
        }

        private void AnimateCardMovement(string cardRuntimeId, BoardSlot fromSlot, BoardSlot toSlot, bool isLocalSide)
        {
            Debug.Log($"[AnimateCardMovement] START: {cardRuntimeId} {fromSlot} → {toSlot} isLocal={isLocalSide}");
            var slots = isLocalSide ? _localSlots : _remoteSlots;

            if (!slots.TryGetValue(toSlot, out var toSlotButton) || !slots.TryGetValue(fromSlot, out var fromSlotButton))
            {
                Debug.Log($"[AnimateCardMovement] Slot not found");
                return;
            }

            // Find card widget by searching all slots (it may have been moved)
            CardViewWidget cardWidget = null;
            foreach (var slot in slots.Values)
            {
                var card = slot.GetSpawnedCard();
                if (card != null && card.TryGetComponent<CardViewWidget>(out _))
                {
                    // Check if this is our card by comparing position or other means
                    if (slot.GetCurrentOccupantRuntimeId() == cardRuntimeId)
                    {
                        cardWidget = card;
                        break;
                    }
                }
            }

            Debug.Log($"[AnimateCardMovement] Found cardWidget: {cardWidget}");
            if (cardWidget == null || !(cardWidget.transform is RectTransform cardRect))
            {
                Debug.Log($"[AnimateCardMovement] Card not found or not RectTransform");
                return;
            }

            // Get slot rect positions
            var fromSlotRect = fromSlotButton.GetComponent<RectTransform>();
            var toSlotRect = toSlotButton.GetComponent<RectTransform>();
            if (fromSlotRect == null || toSlotRect == null)
            {
                Debug.Log($"[AnimateCardMovement] Slot rects NULL");
                return;
            }

            var fromPos = fromSlotRect.position;
            var cardAnchor = toSlotButton.CardAnchor as RectTransform;
            var toPos = cardAnchor != null ? cardAnchor.position : toSlotRect.position;

            Debug.Log($"[AnimateCardMovement] ANIMATING: {cardRuntimeId} {fromSlot} ({fromPos}) → {toSlot} ({toPos})");

            // Animate to the CardAnchor's world position
            toSlotButton.PlayWorldPositionTransition(cardWidget.transform, fromPos, toPos);
        }

        private void HandleSnapshot(string json)
        {
            Debug.Log($"[BattleScreenPresenter] HandleSnapshot received, matchPhase will be checked");
            _latestSnapshot = JsonUtility.FromJson<DuelSnapshotDto>(json);

            if (_latestSnapshot != null)
            {
                Debug.Log($"[BattleScreenPresenter] Snapshot received: matchPhase={_latestSnapshot.matchPhase}, duelEnded={_latestSnapshot.duelEnded}");
            }

            if (_selectedCard != null && !IsCardStillInLocalHand(_selectedCard.runtimeCardKey))
            {
                _selectedCard = null;
            }

            if (_draggedCard != null && !IsCardStillInLocalHand(_draggedCard.runtimeCardKey))
            {
                _draggedCard = null;
                _dragSource = null;
                DestroyDragGhostImmediate();
            }

            Rebuild();
            DetectAndAnimateCardMovements();
        }

        private void Rebuild()
        {
            Debug.Log($"[BattleScreenPresenter] Rebuild called");
            // Panel visibility is handled by UIManager

            // Initialize debug panel on first rebuild
            if (debugPanel != null && _latestSnapshot != null && _latestSnapshot.players != null && _latestSnapshot.players.Length >= 2)
            {
                var coordinator = LocalSinglePlayerCoordinator.Instance;
                if (coordinator != null && coordinator.DuelRuntime != null)
                {
                    debugPanel.Initialize(this, coordinator.DuelRuntime, coordinator.DuelState);
                }
            }

            // Rebuild hand as early as possible, even with incomplete snapshots
            if (_latestSnapshot != null && _latestSnapshot.players != null && _latestSnapshot.players.Length >= 2)
            {
                Debug.Log($"[BattleScreenPresenter] Rebuild calling RebuildHandOnly");
                RebuildHandOnly();
            }

            if (_latestSnapshot == null || _latestSnapshot.players == null || _latestSnapshot.players.Length < 2)
            {
                ClearHandCards();
                RefreshSelectionLabel();
                RefreshAllSlotVisuals();

                if (turnInfoText != null)
                {
                    turnInfoText.text = "Waiting snapshot...";
                }

                if (heroInfoText != null)
                {
                    heroInfoText.text = string.Empty;
                }

                if (localDeckCountText != null)
                {
                    localDeckCountText.text = string.Empty;
                }

                if (localDeadPileCountText != null)
                {
                    localDeadPileCountText.text = string.Empty;
                }

                if (remoteDeckCountText != null)
                {
                    remoteDeckCountText.text = string.Empty;
                }

                if (remoteDeadPileCountText != null)
                {
                    remoteDeadPileCountText.text = string.Empty;
                }

                if (battleLogText != null)
                {
                    battleLogText.text = string.Empty;
                }

                if (endTurnButton != null)
                {
                    endTurnButton.interactable = false;
                }

                return;
            }

            // Initialize match info on first snapshot
            if (string.IsNullOrEmpty(_matchId))
            {
                _matchId = _latestSnapshot.matchId;
                _playerId = _latestSnapshot.players[_latestSnapshot.localPlayerIndex].playerId;
                _opponentId = _latestSnapshot.players[1 - _latestSnapshot.localPlayerIndex].playerId;
                _matchStartTime = Time.frameCount; // Simple time tracking
            }

            var local = _latestSnapshot.players[_latestSnapshot.localPlayerIndex];
            var remote = _latestSnapshot.players[1 - _latestSnapshot.localPlayerIndex];
            var isMatchPlayable = _latestSnapshot.matchPhase == MatchPhase.InProgress && !_latestSnapshot.duelEnded;
            var isLocalTurn = isMatchPlayable && _latestSnapshot.activePlayerIndex == _latestSnapshot.localPlayerIndex;

            ApplyBoardState(local, _localSlots, isLocalTurn, isLocalSide: true);
            ApplyBoardState(remote, _remoteSlots, isLocalTurn: false, isLocalSide: false);

            if (turnInfoText != null)
            {
                turnInfoText.text = BuildTurnLine(isLocalTurn);
            }

            if (heroInfoText != null)
            {
                heroInfoText.text = BuildHeroLine(local, remote);
            }

            if (localDeckCountText != null)
            {
                localDeckCountText.text = $"Deck: {local.remainingDeckCount}";
            }

            if (localDeadPileCountText != null)
            {
                localDeadPileCountText.text = $"Dead: {local.deadCardPileCount}";
            }

            if (remoteDeckCountText != null)
            {
                remoteDeckCountText.text = $"Deck: {remote.remainingDeckCount}";
            }

            if (remoteDeadPileCountText != null)
            {
                remoteDeadPileCountText.text = $"Dead: {remote.deadCardPileCount}";
            }

            if (_latestSnapshot.logs != null && battleLogText != null)
            {
                battleLogText.text = string.Join("\n", _latestSnapshot.logs.ConvertAll(x => $"• {x.message}"));
            }

            if (endTurnButton != null)
            {
                endTurnButton.interactable = isLocalTurn;
            }

            // Handle match completion
            if (_latestSnapshot.duelEnded && !_matchCompletionHandled)
            {
                GameLogger.Info("UI", $"Match ended: winner={_latestSnapshot.winnerPlayerIndex}, local={_latestSnapshot.localPlayerIndex}");
                HandleMatchCompletion(local, remote);
            }

            RefreshSelectionLabel();
        }

        private void RebuildHandOnly()
        {
            ClearHandCards();

            if (_latestSnapshot == null || _latestSnapshot.players == null || _latestSnapshot.players.Length <= _latestSnapshot.localPlayerIndex)
            {
                Debug.Log($"[BattleScreenPresenter] RebuildHandOnly early exit: snapshot null={_latestSnapshot == null}, players null={_latestSnapshot?.players == null}");
                return;
            }

            var local = _latestSnapshot.players[_latestSnapshot.localPlayerIndex];
            var isLocalTurn = _latestSnapshot.matchPhase == MatchPhase.InProgress
                              && _latestSnapshot.activePlayerIndex == _latestSnapshot.localPlayerIndex
                              && !_latestSnapshot.duelEnded;

            Debug.Log($"[BattleScreenPresenter] RebuildHandOnly: localHandRoot={localHandRoot != null}, handCardPrefab={handCardPrefab != null}, hand count={local.hand?.Length ?? 0}");

            if (localHandRoot == null || handCardPrefab == null || local.hand == null)
            {
                Debug.Log($"[BattleScreenPresenter] RebuildHandOnly exit: missing refs");
                return;
            }

            foreach (var handCard in local.hand)
            {
                var instance = Instantiate(handCardPrefab, localHandRoot);
                var isSelected = _selectedCard != null && _selectedCard.runtimeCardKey == handCard.runtimeCardKey;
                var canAfford = handCard.manaCost <= local.mana;
                instance.Bind(handCard, this, isSelected, canAfford, isLocalTurn);
                _spawnedHandCards.Add(instance.gameObject);
            }

            Debug.Log($"[BattleScreenPresenter] RebuildHandOnly complete: spawned {_spawnedHandCards.Count} cards");
        }

        private void ApplyBoardState(
            PlayerSnapshotDto playerSnapshot,
            Dictionary<BoardSlot, BoardSlotButton> slotMap,
            bool isLocalTurn,
            bool isLocalSide)
        {
            if (playerSnapshot == null)
            {
                return;
            }

            foreach (var pair in slotMap)
            {
                var slot = pair.Key;
                var slotView = pair.Value;
                var snapshot = playerSnapshot.board != null
                    ? playerSnapshot.board.FirstOrDefault(x => x.slot == slot)
                    : null;
                var legal = isLocalSide && CanCurrentCardBePlayedTo(slot);
                slotView.ApplySnapshot(snapshot, isLocalTurn, _selectedCard != null, legal);
            }
        }

        private void CacheBoardSlots()
        {
            _localSlots.Clear();
            _remoteSlots.Clear();

            AddSlot(_localSlots, localFrontSlot);
            AddSlot(_localSlots, localBackLeftSlot);
            AddSlot(_localSlots, localBackRightSlot);

            AddSlot(_remoteSlots, remoteFrontSlot);
            AddSlot(_remoteSlots, remoteBackLeftSlot);
            AddSlot(_remoteSlots, remoteBackRightSlot);
        }

        private void AddSlot(Dictionary<BoardSlot, BoardSlotButton> map, BoardSlotButton slotButton)
        {
            if (slotButton == null)
            {
                return;
            }

            slotButton.Bind(this);
            map[slotButton.slot] = slotButton;
        }

        private bool TryPlayCardToSlot(CardInHandDto dto, BoardSlot slot)
        {
            if (dto == null)
            {
                return false;
            }

            if (!CanCardBePlayedTo(dto, slot))
            {
                if (dto.isUnit)
                {
                    var local = _latestSnapshot?.players?[_latestSnapshot.localPlayerIndex];
                    var occupiedCount = local?.board?.Count(x => x.occupied) ?? 0;
                    if (occupiedCount >= 3)
                    {
                        ToastManager.Instance?.ShowToast("Board is full!");
                    }
                }
                GameLogger.Warning("UI", $"PlayCard rejected: cannot play {dto.displayName} to {slot}");
                return false;
            }

            var gameModeManager = FindFirstObjectByType<Core.GameModeManager>();
            var isLocalMode = gameModeManager != null && gameModeManager.IsLocalMode;
            var hasLocalCoord = LocalSinglePlayerCoordinator.Instance != null;
            var localActive = hasLocalCoord && LocalSinglePlayerCoordinator.Instance.IsActive;

            GameLogger.Info("UI", $"PlayCard: {dto.displayName} to {slot}, isLocalMode={isLocalMode}, localActive={localActive}, hasNetCoord={CardDuelNetworkCoordinator.Instance != null}");

            var played = false;

            if (isLocalMode && localActive)
            {
                GameLogger.Info("UI", "PlayCard: Using LocalSinglePlayerCoordinator");
                played = LocalSinglePlayerCoordinator.Instance.RequestPlayCard(dto.runtimeCardKey, slot);
            }
            else if (CardDuelNetworkCoordinator.Instance != null)
            {
                GameLogger.Info("UI", "PlayCard: Calling RequestPlayCardServerRpc");
                CardDuelNetworkCoordinator.Instance.RequestPlayCardServerRpc(dto.runtimeCardKey, (int)slot);
                played = true;
            }
            else
            {
                GameLogger.Error("UI", "PlayCard: No coordinator found!");
            }

            GameLogger.Info("UI", $"PlayCard result: {played}");
            return played;
        }

        private bool CanCardBePlayedTo(CardInHandDto dto, BoardSlot slot)
        {
            if (_latestSnapshot == null || dto == null || _latestSnapshot.duelEnded)
            {
                return false;
            }

            if (_latestSnapshot.matchPhase != MatchPhase.InProgress)
            {
                return false;
            }

            if (_latestSnapshot.players == null || _latestSnapshot.players.Length <= _latestSnapshot.localPlayerIndex)
            {
                return false;
            }

            if (_latestSnapshot.activePlayerIndex != _latestSnapshot.localPlayerIndex)
            {
                return false;
            }

            var local = _latestSnapshot.players[_latestSnapshot.localPlayerIndex];
            if (dto.manaCost > local.mana)
            {
                return false;
            }

            // Units can be played in any slot
            if (dto.isUnit)
            {
                return true;
            }

            return true;
        }

        private void HandleEndTurnPressed()
        {
            GameLogger.Info("UI", "End Turn pressed");

            // Validate it's local player's turn
            if (_latestSnapshot == null || _latestSnapshot.matchPhase != MatchPhase.InProgress || _latestSnapshot.activePlayerIndex != _latestSnapshot.localPlayerIndex)
            {
                GameLogger.Warning("UI", "End Turn rejected: not local player's turn");
                return;
            }

            var local = _latestSnapshot.players[_latestSnapshot.localPlayerIndex];
            if (local.hand != null && local.hand.Length > Core.CardConstants.MaxHandSize)
            {
                GameLogger.Info("UI", $"Hand size {local.hand.Length} exceeds max {Core.CardConstants.MaxHandSize}, entering discard mode");
                _inDiscardMode = true;
                RefreshAllSlotVisuals();
                return;
            }

            DestroyDragGhostImmediate();
            _draggedCard = null;
            _dragSource = null;

            var gameModeManager = FindFirstObjectByType<Core.GameModeManager>();
            var isLocalMode = gameModeManager != null && gameModeManager.IsLocalMode;

            GameLogger.Info("UI", $"End Turn: isLocalMode={isLocalMode}, hasLocalCoordinator={LocalSinglePlayerCoordinator.Instance != null}, isLocalActive={LocalSinglePlayerCoordinator.Instance?.IsActive}");

            if (isLocalMode && LocalSinglePlayerCoordinator.Instance != null && LocalSinglePlayerCoordinator.Instance.IsActive)
            {
                GameLogger.Info("UI", "End Turn: local mode confirmed, using LocalSinglePlayerCoordinator");
                LocalSinglePlayerCoordinator.Instance.RequestEndTurn();
                return;
            }

            if (CardDuelNetworkCoordinator.Instance != null)
            {
                GameLogger.Info("UI", "End Turn: online mode, calling RequestEndTurnServerRpc");
                CardDuelNetworkCoordinator.Instance.RequestEndTurnServerRpc();
            }
            else
            {
                GameLogger.Error("UI", "End Turn: CardDuelNetworkCoordinator not found");
            }
        }

        private bool CanStartInteractionWithCard(CardInHandDto dto)
        {
            if (_latestSnapshot == null || dto == null || _latestSnapshot.duelEnded)
            {
                return false;
            }

            if (_latestSnapshot.matchPhase != MatchPhase.InProgress)
            {
                return false;
            }

            if (_latestSnapshot.players == null || _latestSnapshot.players.Length <= _latestSnapshot.localPlayerIndex)
            {
                return false;
            }

            if (_latestSnapshot.activePlayerIndex != _latestSnapshot.localPlayerIndex)
            {
                return false;
            }

            var local = _latestSnapshot.players[_latestSnapshot.localPlayerIndex];
            return dto.manaCost <= local.mana;
        }

        private bool IsCardStillInLocalHand(string runtimeCardKey)
        {
            if (_latestSnapshot == null || _latestSnapshot.players == null || _latestSnapshot.players.Length <= _latestSnapshot.localPlayerIndex)
            {
                return false;
            }

            var local = _latestSnapshot.players[_latestSnapshot.localPlayerIndex];
            return local.hand != null && local.hand.Any(x => x.runtimeCardKey == runtimeCardKey);
        }

        private void RefreshAllSlotVisuals()
        {
            RefreshSlotGroup(_localSlots);
            RefreshSlotGroup(_remoteSlots);
            RefreshSelectionLabel();
        }

        private void RefreshSlotGroup(Dictionary<BoardSlot, BoardSlotButton> slots)
        {
            foreach (var pair in slots)
            {
                var legal = pair.Value.isLocalSide && CanCurrentCardBePlayedTo(pair.Key);
                pair.Value.RefreshOnlyVisual(_selectedCard != null, legal, HasDraggedCard);
            }
        }

        private void RefreshSelectionLabel()
        {
            if (selectedCardText == null)
            {
                return;
            }

            if (_inDiscardMode)
            {
                var localPlayer = _latestSnapshot?.players?[_latestSnapshot.localPlayerIndex];
                var handCount = localPlayer?.hand?.Length ?? 0;
                selectedCardText.text = $"DISCARD MODE: {handCount}/{Core.CardConstants.MaxHandSize} cards - Click card to discard";
                return;
            }

            if (_latestSnapshot == null)
            {
                selectedCardText.text = "Selected: none";
                return;
            }

            if (_latestSnapshot.matchPhase != MatchPhase.InProgress)
            {
                selectedCardText.text = string.IsNullOrWhiteSpace(_latestSnapshot.statusMessage)
                    ? "Waiting..."
                    : _latestSnapshot.statusMessage;
                return;
            }

            if (_selectedCard == null)
            {
                selectedCardText.text = "Selected: none";
                return;
            }

            var local = _latestSnapshot.players != null && _latestSnapshot.players.Length > _latestSnapshot.localPlayerIndex
                ? _latestSnapshot.players[_latestSnapshot.localPlayerIndex]
                : null;

            var currentMana = local != null ? local.mana : 0;
            var affordable = _selectedCard.manaCost <= currentMana ? "OK" : "NO MANA";
            var placement = BuildPlacementLabel(_selectedCard);
            selectedCardText.text = $"Selected: {_selectedCard.displayName} [{_selectedCard.manaCost}] {placement} {affordable}";
        }

        private string BuildPlacementLabel(CardInHandDto dto)
        {
            if (dto == null)
            {
                return string.Empty;
            }

            if (!dto.isUnit)
            {
                return "Any";
            }

            var type = (Data.UnitType)dto.unitType;
            return type switch
            {
                Data.UnitType.Melee => "Front",
                Data.UnitType.Ranged => "Back",
                Data.UnitType.Magic => "Back (Diagonal)",
                _ => "Any"
            };
        }

        private string BuildTurnLine(bool isLocalTurn)
        {
            if (_latestSnapshot == null)
            {
                return "Waiting snapshot...";
            }

            if (_latestSnapshot.duelEnded)
            {
                var isWinner = _latestSnapshot.winnerPlayerIndex == _latestSnapshot.localPlayerIndex;
                return isWinner
                    ? $"🎉 VICTORY! ({_latestSnapshot.endReason})"
                    : $"💀 DEFEAT ({_latestSnapshot.endReason})";
            }

            return _latestSnapshot.matchPhase switch
            {
                MatchPhase.WaitingForPlayers => string.IsNullOrWhiteSpace(_latestSnapshot.statusMessage)
                    ? "Waiting for second player..."
                    : _latestSnapshot.statusMessage,
                MatchPhase.WaitingForReady => $"Lobby - You ready: {(_latestSnapshot.localPlayerReady ? "yes" : "no")} | Opponent ready: {(_latestSnapshot.remotePlayerReady ? "yes" : "no")}",
                MatchPhase.Starting => string.IsNullOrWhiteSpace(_latestSnapshot.statusMessage)
                    ? "Starting match..."
                    : _latestSnapshot.statusMessage,
                MatchPhase.InProgress => $"Turn {_latestSnapshot.turnNumber} - {(isLocalTurn ? "Your turn" : "Opponent turn")}",
                MatchPhase.Completed => string.IsNullOrWhiteSpace(_latestSnapshot.statusMessage)
                    ? "Match completed"
                    : _latestSnapshot.statusMessage,
                MatchPhase.Abandoned => string.IsNullOrWhiteSpace(_latestSnapshot.statusMessage)
                    ? "Match abandoned"
                    : _latestSnapshot.statusMessage,
                _ => string.IsNullOrWhiteSpace(_latestSnapshot.statusMessage)
                    ? "Waiting..."
                    : _latestSnapshot.statusMessage
            };
        }

        private string BuildHeroLine(PlayerSnapshotDto local, PlayerSnapshotDto remote)
        {
            if (_latestSnapshot == null || local == null || remote == null)
            {
                return string.Empty;
            }

            var reconnectLine = _latestSnapshot.reconnectGraceRemainingSeconds > 0f
                ? $" | Reconnect grace {_latestSnapshot.reconnectGraceRemainingSeconds:0}s"
                : string.Empty;

            return $"You {local.heroHealth} HP | Mana {local.mana}/{local.maxMana} | Enemy {remote.heroHealth} HP | Enemy Mana {remote.mana}/{remote.maxMana}{reconnectLine}";
        }

        private void CreateDragGhost(CardInHandDto dto, Vector2 screenPosition)
        {
            DestroyDragGhostImmediate();

            if (dragGhost3DPrefab == null)
            {
                Debug.LogError("[BattleScreenPresenter] dragGhost3DPrefab is null!");
                return;
            }


            _dragGhost = Instantiate(dragGhost3DPrefab);
            _dragGhost.transform.position = new Vector3(0, 0, 0); // Reset to origin
            

            _dragGhost3D = _dragGhost.GetComponent<DragGhost3D>();
            

            if (_dragGhost3D != null)
            {
                _dragGhost3D.SetTargetPosition(screenPosition, Camera.main);
                
            }
            else
            {
                Debug.LogError("[BattleScreenPresenter] Ghost created but no DragGhost3D component!");
            }
        }

        private IEnumerator AnimateGhostToTargetAndDestroy(RectTransform target)
        {
            if (_dragGhost == null || target == null)
            {
                DestroyDragGhostImmediate();
                yield break;
            }

            var ghostRect = _dragGhost.transform as RectTransform;
            if (ghostRect == null)
            {
                DestroyDragGhostImmediate();
                yield break;
            }

            var startPosition = ghostRect.position;
            var startScale = ghostRect.localScale;
            var endPosition = target.position;
            const float duration = 0.16f;
            var elapsed = 0f;

            while (elapsed < duration && ghostRect != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                t = 1f - Mathf.Pow(1f - t, 3f);
                ghostRect.position = Vector3.LerpUnclamped(startPosition, endPosition, t);
                ghostRect.localScale = Vector3.LerpUnclamped(startScale, Vector3.one * 0.76f, t);
                yield return null;
            }

            DestroyDragGhostImmediate();
        }

        private void DestroyDragGhostImmediate()
        {
            if (_dragGhost != null)
            {
                Destroy(_dragGhost);
                _dragGhost = null;
                _dragGhost3D = null;
            }
        }

        private void ClearHandCards()
        {
            foreach (var go in _spawnedHandCards)
            {
                if (go != null)
                {
                    Destroy(go);
                }
            }

            _spawnedHandCards.Clear();
        }

        private async void HandleMatchCompletion(PlayerSnapshotDto local, PlayerSnapshotDto remote)
        {
            _matchCompletionHandled = true;

            try
            {
                var playerWon = _latestSnapshot.winnerPlayerIndex == _latestSnapshot.localPlayerIndex;
                var durationSeconds = Mathf.RoundToInt(Time.time);

                Debug.Log($"[BattleScreen] Match ended. Winner: {_latestSnapshot.winnerPlayerIndex}, " +
                         $"Local player: {_latestSnapshot.localPlayerIndex}, Won: {playerWon}");

                // Create match completion service (GameService can be null, only used for cache clearing)
                var completionService = new MatchCompletionService(null);

                // Use rating from snapshot (or default 1000 if not available)
                var localRating = local?.rating ?? 1000;
                var remoteRating = remote?.rating ?? 1000;

                var result = await completionService.CompleteMatch(
                    _matchId,
                    _playerId,
                    _opponentId,
                    localRating,
                    remoteRating,
                    playerWon,
                    durationSeconds);

                Debug.Log($"[BattleScreen] Match completion result: {result.matchId} | " +
                         $"Won: {result.won} | Rating delta: {(result.ratingDelta > 0 ? "+" : "")}{result.ratingDelta}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BattleScreen] Error completing match: {ex.Message}");
            }
        }
    }
}
