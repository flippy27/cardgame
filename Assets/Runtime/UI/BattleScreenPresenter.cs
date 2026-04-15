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
        public Text battleLogText;
        public Text turnInfoText;
        public Text heroInfoText;
        public Text selectedCardText;
        public Button endTurnButton;

        [Header("Prefabs")]
        public HandCardButton handCardPrefab;
        public CardViewWidget boardCardPrefab;
        public CardViewWidget dragGhostPrefab;

        private DuelSnapshotDto _latestSnapshot;
        private CardInHandDto _selectedCard;
        private CardInHandDto _draggedCard;
        private HandCardButton _dragSource;
        private CardViewWidget _dragGhost;
        private bool _dragDropCommitted;
        private BoardSlotButton _dragOverSlot;

        private bool _matchCompletionHandled;
        private string _matchId;
        private string _playerId;
        private string _opponentId;
        private int _matchStartTime;

        private readonly List<GameObject> _spawnedHandCards = new();
        private readonly Dictionary<BoardSlot, BoardSlotButton> _localSlots = new();
        private readonly Dictionary<BoardSlot, BoardSlotButton> _remoteSlots = new();

        public CardViewWidget BoardCardPrefab => boardCardPrefab;
        public bool HasDraggedCard => _draggedCard != null;

        private void OnEnable()
        {
            BattleSnapshotBus.SnapshotReceived += HandleSnapshot;

            if (endTurnButton != null)
            {
                endTurnButton.onClick.AddListener(HandleEndTurnPressed);
            }

            CacheBoardSlots();
            RefreshAllSlotVisuals();
            RefreshSelectionLabel();
        }

        private void OnDisable()
        {
            BattleSnapshotBus.SnapshotReceived -= HandleSnapshot;

            if (endTurnButton != null)
            {
                endTurnButton.onClick.RemoveListener(HandleEndTurnPressed);
            }
        }

        public void NotifyCardClicked(CardInHandDto dto)
        {
            if (dto == null)
            {
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

        public void BeginDrag(CardInHandDto dto, HandCardButton source, Vector2 screenPosition)
        {
            if (dto == null)
            {
                return;
            }

            if (!CanStartInteractionWithCard(dto))
            {
                return;
            }

            _selectedCard = dto;
            _draggedCard = dto;
            _dragSource = source;
            _dragDropCommitted = false;

            Debug.Log($"[Drag] BEGIN: {dto.displayName} at {screenPosition}");

            CreateDragGhost(dto, screenPosition);
            RefreshSelectionLabel();
            RefreshAllSlotVisuals();
            // Don't RebuildHandOnly here - it destroys the HandCardButton being dragged
        }

        public void UpdateDrag(Vector2 screenPosition)
        {
            if (_dragGhost != null)
            {
                var ghostRect = _dragGhost.transform as RectTransform;
                if (ghostRect != null && dragLayer != null)
                {
                    // Convert screen position to local position in dragLayer
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        dragLayer, screenPosition, null, out var localPos))
                    {
                        ghostRect.anchoredPosition = localPos;
                    }
                }
                else if (ghostRect != null)
                {
                    // Fallback: use world position if no dragLayer
                    ghostRect.position = screenPosition;
                }
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
            Debug.Log($"[Drag] END: dragOverSlot={slotInfo}, targetSlot={targetSlot}, card={cardInfo}");

            if (!_dragDropCommitted && _draggedCard != null && targetSlot != null)
            {
                Debug.Log($"[Drag] PLAYING: {_draggedCard.displayName} to {targetSlot.slot}");
                TryPlayDraggedCardTo(targetSlot.slot, targetSlot.CardAnchor);
            }

            if (!_dragDropCommitted)
            {
                DestroyDragGhostImmediate();
            }

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

            return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition);
        }

        public void SetDragOverSlot(BoardSlotButton slot)
        {
            _dragOverSlot = slot;
        }

        public void TryPlayDraggedCardTo(BoardSlot slot, RectTransform targetAnchor)
        {
            if (_draggedCard == null)
            {
                return;
            }

            var cardToPlay = _draggedCard;
            if (!TryPlayCardToSlot(cardToPlay, slot))
            {
                DestroyDragGhostImmediate();
                _draggedCard = null;
                _dragSource = null;
                RefreshAllSlotVisuals();
                RebuildHandOnly();
                return;
            }

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

        private void HandleSnapshot(string json)
        {
            _latestSnapshot = JsonUtility.FromJson<DuelSnapshotDto>(json);

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
        }

        private void Rebuild()
        {
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

            RebuildHandOnly();

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
                HandleMatchCompletion(local, remote);
            }

            RefreshSelectionLabel();
        }

        private void RebuildHandOnly()
        {
            ClearHandCards();

            if (_latestSnapshot == null || _latestSnapshot.players == null || _latestSnapshot.players.Length <= _latestSnapshot.localPlayerIndex)
            {
                return;
            }

            var local = _latestSnapshot.players[_latestSnapshot.localPlayerIndex];
            var isLocalTurn = _latestSnapshot.matchPhase == MatchPhase.InProgress
                              && _latestSnapshot.activePlayerIndex == _latestSnapshot.localPlayerIndex
                              && !_latestSnapshot.duelEnded;

            if (localHandRoot == null || handCardPrefab == null || local.hand == null)
            {
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
            if (dto == null || !CanCardBePlayedTo(dto, slot))
            {
                return false;
            }

            var played = false;

            if (LocalSinglePlayerCoordinator.Instance != null && LocalSinglePlayerCoordinator.Instance.IsActive)
            {
                played = LocalSinglePlayerCoordinator.Instance.RequestPlayCard(dto.runtimeCardKey, slot);
            }
            else if (CardDuelNetworkCoordinator.Instance != null)
            {
                CardDuelNetworkCoordinator.Instance.RequestPlayCardServerRpc(dto.runtimeCardKey, (int)slot);
                played = true;
            }

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

            if (local.board != null)
            {
                var slotSnapshot = local.board.FirstOrDefault(x => x.slot == slot);
                if (slotSnapshot != null && slotSnapshot.occupied)
                {
                    return false;
                }
            }

            return slot == BoardSlot.Front
                ? dto.canBePlayedInFront
                : dto.canBePlayedInBack;
        }

        private void HandleEndTurnPressed()
        {
            DestroyDragGhostImmediate();
            _draggedCard = null;
            _dragSource = null;

            if (_latestSnapshot != null && _latestSnapshot.matchPhase != MatchPhase.InProgress)
            {
                return;
            }

            if (LocalSinglePlayerCoordinator.Instance != null && LocalSinglePlayerCoordinator.Instance.IsActive)
            {
                LocalSinglePlayerCoordinator.Instance.RequestEndTurn();
                return;
            }

            if (CardDuelNetworkCoordinator.Instance != null)
            {
                CardDuelNetworkCoordinator.Instance.RequestEndTurnServerRpc();
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

            var parts = new List<string>(2);
            if (dto.canBePlayedInFront)
            {
                parts.Add("Front");
            }

            if (dto.canBePlayedInBack)
            {
                parts.Add("Back");
            }

            return parts.Count == 0 ? "NoSlot" : string.Join("/", parts);
        }

        private string BuildTurnLine(bool isLocalTurn)
        {
            if (_latestSnapshot == null)
            {
                return "Waiting snapshot...";
            }

            if (_latestSnapshot.duelEnded)
            {
                return _latestSnapshot.winnerPlayerIndex == _latestSnapshot.localPlayerIndex
                    ? "Match ended - Victory"
                    : "Match ended - Defeat";
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

            var prefab = dragGhostPrefab != null ? dragGhostPrefab : boardCardPrefab;
            if (prefab == null)
            {
                return;
            }

            var parent = dragLayer != null ? dragLayer : transform as RectTransform;
            _dragGhost = Instantiate(prefab, parent);
            _dragGhost.Bind(dto);
            _dragGhost.transform.position = screenPosition;

            var canvasGroup = _dragGhost.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = _dragGhost.gameObject.AddComponent<CanvasGroup>();
            }

            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0.92f;

            var rectTransform = _dragGhost.transform as RectTransform;
            if (rectTransform != null)
            {
                rectTransform.localScale = Vector3.one * 0.95f;
                rectTransform.SetAsLastSibling();
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
                Destroy(_dragGhost.gameObject);
                _dragGhost = null;
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

                // Get GameService for match completion
                var gameService = new GameService();
                var completionService = new MatchCompletionService(gameService);

                var result = await completionService.CompleteMatch(
                    _matchId,
                    _playerId,
                    _opponentId,
                    local.rating,
                    remote.rating,
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
