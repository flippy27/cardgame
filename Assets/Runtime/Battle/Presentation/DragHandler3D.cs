using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Handles local hand dragging plus card inspection for both hand and board cards.
    /// Uses the new Input System and supports mouse + touch.
    /// </summary>
    public class DragHandler3D : MonoBehaviour
    {
        [SerializeField] private Hand3DManager hand3DManager;
        [SerializeField] private Board3DManager board3DManager;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private GameplayPresenter3D presenter;
        [SerializeField] private CardDetailOverlayUI cardDetailOverlay;
        public GameObject dragGhost3DPrefab;

        [Header("Drag")]
        public float dragMinDistance = 0.5f;
        public float dragMinScreenDistance = 24f;
        public float quickDragStartScreenDistance = 24f;
        public float detailToDragUpwardDistance = 80f;

        [Header("Inspect")]
        public float inspectHoldDelay = 0.35f;
        public float inspectMovementThreshold = 16f;

        public Hand3DManager Hand3DManager
        {
            get => hand3DManager;
            set => hand3DManager = value;
        }

        public Board3DManager Board3DManager
        {
            get => board3DManager;
            set => board3DManager = value;
        }

        private Card3DView _draggedCard;
        private Vector3 _dragStartWorldPos;
        private Vector2 _dragStartScreenPos;
        private float _dragDistance;
        private float _dragScreenDistance;
        private bool _isDragging;
        private Board3DSlot _hoveredSlot;
        private GameObject _dragGhostInstance;
        private DragGhost3D _dragGhost;

        private ICardDisplay _hoveredCard;
        private ICardDisplay _inspectCandidate;
        private Vector2 _inspectAnchorScreenPos;
        private float _inspectAnchorTime;
        private Card3DView _pressedHandCard;
        private Card3DPlayed _pressedBoardCard;
        private Card3DPlayed _draggedBoardCard;
        private DragGhost3D _boardCardDragMover;
        private Transform _boardCardOriginalParent;
        private Vector3 _boardCardOriginalLocalPosition;
        private Quaternion _boardCardOriginalLocalRotation;
        private Vector3 _boardCardOriginalLocalScale;
        private Board3DSlot _boardCardOriginalSlot;
        private Collider[] _boardCardDisabledColliders;
        private BoardCardDestroyDropZone _hoveredDestroyZone;
        private Vector2 _pressStartScreenPos;
        private Vector2 _detailInteractionStartScreenPos;

        private void Start()
        {
            EnsureReferences();
        }

        private void Update()
        {
            EnsureReferences();

            if (!TryGetPointerState(out var pointerState))
            {
                if (_isDragging)
                {
                    EndDrag();
                }
                else
                {
                    ClearHoverState();
                    ResetInspectCandidate();
                }

                return;
            }

            if (_draggedBoardCard != null)
            {
                UpdateBoardCardDestroyDragging(pointerState);
                return;
            }

            if (_isDragging)
            {
                UpdateDragging(pointerState);
                return;
            }

            var hoveredCard = RaycastCard(pointerState.screenPosition);
            UpdateHoveredCard(hoveredCard, pointerState);

            if (pointerState.pressedThisFrame)
            {
                HandlePointerPressed(pointerState, hoveredCard);
            }

            if (pointerState.isPressed)
            {
                HandlePointerHeld(pointerState, hoveredCard);
            }
            else
            {
                HandlePointerIdle(pointerState, hoveredCard);
            }

            if (pointerState.releasedThisFrame)
            {
                HandlePointerReleased(pointerState, hoveredCard);
            }
        }

        private void HandlePointerPressed(PointerState pointerState, ICardDisplay hoveredCard)
        {
            if (cardDetailOverlay != null &&
                cardDetailOverlay.IsVisible &&
                hoveredCard == null)
            {
                cardDetailOverlay.Hide();
            }

            _pressStartScreenPos = pointerState.screenPosition;
            _detailInteractionStartScreenPos = pointerState.screenPosition;
            _pressedHandCard = hoveredCard as Card3DView;
            _pressedBoardCard = hoveredCard as Card3DPlayed;

            if (hoveredCard == null)
            {
                ResetInspectCandidate();
            }
            else if (_inspectCandidate != hoveredCard)
            {
                StartInspectCandidate(hoveredCard, pointerState.screenPosition);
            }
        }

        private void HandlePointerHeld(PointerState pointerState, ICardDisplay hoveredCard)
        {
            if (TryBeginDragFromDetail(pointerState))
            {
                return;
            }

            if (_pressedBoardCard != null &&
                (cardDetailOverlay == null || !cardDetailOverlay.IsVisible) &&
                Vector2.Distance(pointerState.screenPosition, _pressStartScreenPos) >= quickDragStartScreenDistance)
            {
                BeginBoardCardDestroyDrag(_pressedBoardCard, pointerState.screenPosition);
                return;
            }

            if (_pressedHandCard != null &&
                (cardDetailOverlay == null || !cardDetailOverlay.IsVisible) &&
                Vector2.Distance(pointerState.screenPosition, _pressStartScreenPos) >= quickDragStartScreenDistance)
            {
                BeginDrag(_pressedHandCard, pointerState.screenPosition);
                return;
            }

            UpdateInspectCandidate(pointerState, hoveredCard, requiresPressed: true);
        }

        private void HandlePointerIdle(PointerState pointerState, ICardDisplay hoveredCard)
        {
            ResetInspectCandidate();

            if (cardDetailOverlay != null && cardDetailOverlay.IsVisible)
            {
                cardDetailOverlay.Hide();
            }
        }

        private void HandlePointerReleased(PointerState pointerState, ICardDisplay hoveredCard)
        {
            _pressedHandCard = null;
            _pressedBoardCard = null;
            ResetInspectCandidate();

            if (cardDetailOverlay != null && cardDetailOverlay.IsVisible)
            {
                cardDetailOverlay.Hide();
            }

            if (pointerState.usingTouch && hoveredCard == null)
            {
                ClearHoverState();
            }
        }

        private void UpdateDragging(PointerState pointerState)
        {
            if (pointerState.isPressed)
            {
                UpdateDrag(pointerState.screenPosition);
            }

            if (pointerState.releasedThisFrame || !pointerState.isPressed)
            {
                EndDrag();
            }
        }

        private void BeginDrag(Card3DView cardView, Vector2 screenPosition)
        {
            if (cardView == null)
            {
                return;
            }

            if (cardDetailOverlay != null && cardDetailOverlay.IsShowing(cardView))
            {
                cardDetailOverlay.Hide();
            }

            _draggedCard = cardView;
            _dragStartWorldPos = cardView.transform.position;
            _dragStartScreenPos = screenPosition;
            _dragDistance = 0f;
            _dragScreenDistance = 0f;
            _isDragging = true;
            _pressedHandCard = null;
            ResetInspectCandidate();

            presenter?.SaveOriginalCardPositions(0);
            SpawnDragGhost(screenPosition, cardView);
            UpdateDrag(screenPosition);

            Debug.Log($"[DragHandler3D] Started dragging {cardView.CardData.displayName}");
        }

        private bool TryBeginDragFromDetail(PointerState pointerState)
        {
            if (cardDetailOverlay == null || !cardDetailOverlay.IsVisible)
            {
                return false;
            }

            var handCard = cardDetailOverlay.CurrentHandCardSource;
            if (handCard == null)
            {
                return false;
            }

            var upwardDelta = pointerState.screenPosition.y - _detailInteractionStartScreenPos.y;
            if (upwardDelta < detailToDragUpwardDistance)
            {
                return false;
            }

            BeginDrag(handCard, pointerState.screenPosition);
            return true;
        }

        private void UpdateDrag(Vector2 screenPosition)
        {
            if (_draggedCard == null)
            {
                return;
            }

            _dragScreenDistance = Vector2.Distance(_dragStartScreenPos, screenPosition);

            if (_dragGhost != null)
            {
                _dragGhost.SetTargetPosition(screenPosition, mainCamera);
                var ghostWorldPos = _dragGhostInstance.transform.position;
                _dragDistance = Vector3.Distance(_dragStartWorldPos, ghostWorldPos);
            }

            SetHoveredSlot(RaycastBoardSlot(screenPosition));
        }

        private void EndDrag()
        {
            if (_draggedCard == null)
            {
                return;
            }

            Debug.Log($"[DragHandler3D] EndDrag - WorldDistance: {_dragDistance}, ScreenDistance: {_dragScreenDistance}, HoveredSlot: {_hoveredSlot?.Slot}");

            if ((_dragDistance >= dragMinDistance || _dragScreenDistance >= dragMinScreenDistance) &&
                _hoveredSlot != null)
            {
                TryPlayCard();
            }

            if (_dragGhostInstance != null)
            {
                Destroy(_dragGhostInstance);
                _dragGhostInstance = null;
                _dragGhost = null;
                Debug.Log("[DragHandler3D] Drag ghost destroyed");
            }

            _draggedCard = null;
            _isDragging = false;
            SetHoveredSlot(null);
        }

        private void BeginBoardCardDestroyDrag(Card3DPlayed cardView, Vector2 screenPosition)
        {
            if (cardView == null || cardView.PlayerIndex != 0 || cardView.CardData == null)
            {
                return;
            }

            if (cardDetailOverlay != null && cardDetailOverlay.IsShowing(cardView))
            {
                cardDetailOverlay.Hide();
            }

            _pressedHandCard = null;
            _pressedBoardCard = null;
            _draggedBoardCard = cardView;
            _boardCardOriginalParent = cardView.transform.parent;
            _boardCardOriginalLocalPosition = cardView.transform.localPosition;
            _boardCardOriginalLocalRotation = cardView.transform.localRotation;
            _boardCardOriginalLocalScale = cardView.transform.localScale;
            _boardCardOriginalSlot = board3DManager?.GetSlot(cardView.PlayerIndex, cardView.CardData.slot);
            _boardCardDisabledColliders = cardView.GetComponentsInChildren<Collider>(true);

            if (_boardCardDisabledColliders != null)
            {
                foreach (var collider in _boardCardDisabledColliders)
                {
                    if (collider != null)
                    {
                        collider.enabled = false;
                    }
                }
            }

            cardView.transform.SetParent(null, worldPositionStays: true);
            _boardCardDragMover = cardView.GetComponent<DragGhost3D>() ?? cardView.gameObject.AddComponent<DragGhost3D>();
            _boardCardDragMover.enableVelocityTilt = false;
            _boardCardDragMover.SetTargetPosition(screenPosition, mainCamera);
            SetHoveredDestroyZone(RaycastDestroyDropZone(screenPosition, cardView));
            Debug.Log($"[DragHandler3D] Started destroy drag for board card {cardView.CardData.displayName}");
        }

        private void UpdateBoardCardDestroyDragging(PointerState pointerState)
        {
            if (_draggedBoardCard == null)
            {
                return;
            }

            if (pointerState.isPressed)
            {
                _boardCardDragMover?.SetTargetPosition(pointerState.screenPosition, mainCamera);
                SetHoveredDestroyZone(RaycastDestroyDropZone(pointerState.screenPosition, _draggedBoardCard));
            }

            if (pointerState.releasedThisFrame || !pointerState.isPressed)
            {
                EndBoardCardDestroyDrag();
            }
        }

        private void EndBoardCardDestroyDrag()
        {
            var card = _draggedBoardCard;
            var dropZone = _hoveredDestroyZone;

            if (card == null)
            {
                ClearBoardCardDestroyDragState();
                return;
            }

            if (_boardCardDragMover != null)
            {
                Destroy(_boardCardDragMover);
                _boardCardDragMover = null;
            }

            if (_boardCardDisabledColliders != null)
            {
                foreach (var collider in _boardCardDisabledColliders)
                {
                    if (collider != null)
                    {
                        collider.enabled = true;
                    }
                }
            }

            if (dropZone != null && dropZone.CanAccept(card))
            {
                presenter?.RequestDestroyCard(card.CardData.runtimeId);
            }

            ReturnDraggedBoardCardToOriginalSlot(card);
            ClearBoardCardDestroyDragState();
        }

        private void ReturnDraggedBoardCardToOriginalSlot(Card3DPlayed card)
        {
            if (card == null)
            {
                return;
            }

            var parent = _boardCardOriginalParent != null
                ? _boardCardOriginalParent
                : _boardCardOriginalSlot != null
                    ? _boardCardOriginalSlot.transform
                    : null;

            card.transform.SetParent(parent, worldPositionStays: false);
            card.transform.localPosition = _boardCardOriginalLocalPosition;
            card.transform.localRotation = _boardCardOriginalLocalRotation;
            card.transform.localScale = _boardCardOriginalLocalScale;
        }

        private void ClearBoardCardDestroyDragState()
        {
            SetHoveredDestroyZone(null);
            _draggedBoardCard = null;
            _pressedBoardCard = null;
            _boardCardOriginalParent = null;
            _boardCardOriginalSlot = null;
            _boardCardDisabledColliders = null;
        }

        private void TryPlayCard()
        {
            if (_draggedCard == null || _hoveredSlot == null)
            {
                Debug.LogWarning($"[DragHandler3D] TryPlayCard failed: card={_draggedCard}, slot={_hoveredSlot}");
                return;
            }

            var targetSlot = _hoveredSlot.Slot;
            Debug.Log($"[DragHandler3D] TryPlayCard: {_draggedCard.CardData.displayName} -> {targetSlot}");

            var snapshot = GameplayPresenter3D.GetLatestSnapshot();
            if (snapshot == null)
            {
                Debug.LogWarning("[DragHandler3D] Snapshot unavailable; cannot play card.");
                return;
            }

            var isLocalTurn = SnapshotTurnAuthority.IsLocalTurn(snapshot);
            if (!isLocalTurn)
            {
                Debug.LogWarning("[DragHandler3D] No es turno del jugador local");
                return;
            }

            var localPlayer = snapshot.players[snapshot.localPlayerIndex];
            if (localPlayer == null)
            {
                Debug.LogWarning("[DragHandler3D] Local player snapshot missing.");
                return;
            }

            var handCard = localPlayer.hand?.FirstOrDefault(c => c.runtimeCardKey == _draggedCard.CardData.runtimeId);
            if (handCard == null)
            {
                Debug.LogWarning("[DragHandler3D] Card is not present in the latest local hand snapshot.");
                return;
            }

            if (presenter == null)
            {
                Debug.LogError("[DragHandler3D] presenter is null!");
                return;
            }

            Debug.Log($"[DragHandler3D] Playing {_draggedCard.CardData.displayName} (ID: {_draggedCard.CardData.runtimeId}) to {targetSlot}");
            presenter.RequestPlayCard(_draggedCard.CardData.runtimeId, targetSlot);
        }

        private void UpdateHoveredCard(ICardDisplay hoveredCard, PointerState pointerState)
        {
            _hoveredCard = hoveredCard;

            if (pointerState.usingTouch && !pointerState.isPressed)
            {
                hand3DManager?.ClearHoveredCard();
                return;
            }

            hand3DManager?.SetHoveredCard(hoveredCard as Card3DView);
        }

        private void ClearHoverState()
        {
            _hoveredCard = null;
            hand3DManager?.ClearHoveredCard();
        }

        private void UpdateInspectCandidate(PointerState pointerState, ICardDisplay hoveredCard, bool requiresPressed)
        {
            var canInspect = hoveredCard != null &&
                             (!requiresPressed || pointerState.isPressed) &&
                             (!pointerState.usingTouch || pointerState.isPressed);

            if (!canInspect)
            {
                ResetInspectCandidate();
                return;
            }

            if (cardDetailOverlay != null &&
                cardDetailOverlay.IsVisible &&
                pointerState.isPressed &&
                hoveredCard != null &&
                !cardDetailOverlay.IsShowing(hoveredCard))
            {
                ShowDetail(hoveredCard, pointerState.screenPosition);
                StartInspectCandidate(hoveredCard, pointerState.screenPosition);
                return;
            }

            if (_inspectCandidate != hoveredCard ||
                Vector2.Distance(pointerState.screenPosition, _inspectAnchorScreenPos) > inspectMovementThreshold)
            {
                StartInspectCandidate(hoveredCard, pointerState.screenPosition);
                return;
            }

            if (cardDetailOverlay == null ||
                cardDetailOverlay.IsShowing(hoveredCard) ||
                Time.unscaledTime - _inspectAnchorTime < inspectHoldDelay)
            {
                return;
            }

            ShowDetail(hoveredCard, pointerState.screenPosition);
        }

        private void StartInspectCandidate(ICardDisplay hoveredCard, Vector2 screenPosition)
        {
            _inspectCandidate = hoveredCard;
            _inspectAnchorScreenPos = screenPosition;
            _inspectAnchorTime = Time.unscaledTime;
        }

        private void ResetInspectCandidate()
        {
            _inspectCandidate = null;
            _inspectAnchorScreenPos = Vector2.zero;
            _inspectAnchorTime = 0f;
        }

        private void ShowDetail(ICardDisplay cardDisplay, Vector2 screenPosition)
        {
            if (cardDetailOverlay == null || cardDisplay?.CardData == null)
            {
                return;
            }

            cardDetailOverlay.Show(cardDisplay, BuildOwnerLabel(cardDisplay));
            _detailInteractionStartScreenPos = screenPosition;
        }

        private static string BuildOwnerLabel(ICardDisplay cardDisplay)
        {
            if (cardDisplay is Card3DView)
            {
                return "Your hand";
            }

            return cardDisplay.PlayerIndex == 0 ? "Your board" : "Opponent board";
        }

        private ICardDisplay RaycastCard(Vector2 screenPosition)
        {
            if (mainCamera == null)
            {
                return null;
            }

            var ray = mainCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out var hit, 100f))
            {
                return null;
            }

            var handCard = hit.collider.GetComponentInParent<Card3DView>();
            if (handCard != null)
            {
                return handCard;
            }

            return hit.collider.GetComponentInParent<Card3DPlayed>();
        }

        private Board3DSlot RaycastBoardSlot(Vector2 screenPosition)
        {
            if (mainCamera == null)
            {
                return null;
            }

            var ray = mainCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out var hit, 100f))
            {
                return null;
            }

            if (_dragGhostInstance != null && hit.collider.gameObject == _dragGhostInstance)
            {
                var dragGhostLayer = _dragGhostInstance.layer;
                var layerMask = ~(1 << dragGhostLayer);
                if (!Physics.Raycast(ray, out hit, 100f, layerMask))
                {
                    return null;
                }
            }

            var slot = hit.collider.GetComponentInParent<Board3DSlot>() ?? hit.collider.GetComponent<Board3DSlot>();
            return slot != null && slot.PlayerIndex == 0 ? slot : null;
        }

        private BoardCardDestroyDropZone RaycastDestroyDropZone(Vector2 screenPosition, Card3DPlayed draggedCard)
        {
            var zones = FindObjectsByType<BoardCardDestroyDropZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var zone in zones)
            {
                if (zone != null &&
                    zone.CanAccept(draggedCard) &&
                    zone.ContainsScreenPosition(screenPosition, mainCamera))
                {
                    return zone;
                }
            }

            return null;
        }

        private void SpawnDragGhost(Vector2 screenPosition, Card3DView sourceCard)
        {
            _dragGhostInstance = CreateDragGhostFromSourceCard(sourceCard);
            if (_dragGhostInstance == null && dragGhost3DPrefab != null)
            {
                _dragGhostInstance = Instantiate(dragGhost3DPrefab);
            }

            if (_dragGhostInstance == null)
            {
                Debug.LogWarning("[DragHandler3D] Unable to create drag ghost.");
                return;
            }

            _dragGhost = _dragGhostInstance.GetComponent<DragGhost3D>();
            if (_dragGhost == null)
            {
                _dragGhost = _dragGhostInstance.AddComponent<DragGhost3D>();
            }

            foreach (var collider in _dragGhostInstance.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            _dragGhost.SetTargetPosition(screenPosition, mainCamera);
            Debug.Log($"[DragHandler3D] Spawned drag ghost for {sourceCard?.CardData?.displayName ?? _dragGhostInstance.name}");
        }

        private GameObject CreateDragGhostFromSourceCard(Card3DView sourceCard)
        {
            if (sourceCard == null)
            {
                return null;
            }

            var ghost = Instantiate(sourceCard.gameObject);
            ghost.name = $"DragGhost_{sourceCard.CardData?.displayName ?? sourceCard.name}";

            var clonedCardView = ghost.GetComponent<Card3DView>();
            if (clonedCardView != null && sourceCard.CardData != null)
            {
                clonedCardView.Initialize(CloneBoardCardData(sourceCard.CardData), sourceCard.PlayerIndex);
            }

            return ghost;
        }

        private static BoardCardDto CloneBoardCardData(BoardCardDto sourceCard)
        {
            if (sourceCard == null)
            {
                return null;
            }

            return new BoardCardDto
            {
                runtimeId = sourceCard.runtimeId,
                cardId = sourceCard.cardId,
                displayName = sourceCard.displayName,
                manaCost = sourceCard.manaCost,
                attackMotionLevel = sourceCard.attackMotionLevel,
                attackShakeLevel = sourceCard.attackShakeLevel,
                attackDeliveryType = sourceCard.attackDeliveryType,
                ownerIndex = sourceCard.ownerIndex,
                attack = sourceCard.attack,
                currentHealth = sourceCard.currentHealth,
                maxHealth = sourceCard.maxHealth,
                armor = sourceCard.armor,
                slot = sourceCard.slot,
                canAttack = sourceCard.canAttack,
                unitType = sourceCard.unitType,
                turnsUntilCanAttack = sourceCard.turnsUntilCanAttack,
                statusEffects = sourceCard.statusEffects,
                abilities = sourceCard.abilities
            };
        }

        private void SetHoveredSlot(Board3DSlot slot)
        {
            if (_hoveredSlot == slot)
            {
                return;
            }

            if (_hoveredSlot != null)
            {
                _hoveredSlot.SetHighlight(false);
            }

            _hoveredSlot = slot;

            if (_hoveredSlot != null)
            {
                _hoveredSlot.SetHighlight(true);

                if (presenter != null && _hoveredSlot.PlayerIndex == 0)
                {
                    presenter.PreviewCardDisplacement(0, _hoveredSlot.Slot);
                }
            }
            else if (presenter != null)
            {
                presenter.CancelCardDisplacement(0);
            }
        }

        private void SetHoveredDestroyZone(BoardCardDestroyDropZone zone)
        {
            if (_hoveredDestroyZone == zone)
            {
                return;
            }

            if (_hoveredDestroyZone != null)
            {
                _hoveredDestroyZone.SetHighlighted(false);
            }

            _hoveredDestroyZone = zone;

            if (_hoveredDestroyZone != null)
            {
                _hoveredDestroyZone.SetHighlighted(true);
            }
        }

        private void EnsureReferences()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            if (presenter == null)
            {
                presenter = GameplayPresenter3D.Instance ?? FindFirstObjectByType<GameplayPresenter3D>();
            }

            if (hand3DManager == null)
            {
                hand3DManager = FindFirstObjectByType<Hand3DManager>();
            }

            if (board3DManager == null)
            {
                board3DManager = FindFirstObjectByType<Board3DManager>();
            }

            if (cardDetailOverlay == null)
            {
                var overlays = Resources.FindObjectsOfTypeAll<CardDetailOverlayUI>();
                if (overlays != null)
                {
                    foreach (var overlay in overlays)
                    {
                        if (overlay != null && overlay.gameObject.scene.IsValid())
                        {
                            cardDetailOverlay = overlay;
                            break;
                        }
                    }
                }
            }
        }

        private static bool TryGetPointerState(out PointerState pointerState)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                var touch = touchscreen.primaryTouch;
                if (touch.press.isPressed || touch.press.wasPressedThisFrame || touch.press.wasReleasedThisFrame)
                {
                    pointerState = new PointerState
                    {
                        screenPosition = touch.position.ReadValue(),
                        pressedThisFrame = touch.press.wasPressedThisFrame,
                        releasedThisFrame = touch.press.wasReleasedThisFrame,
                        isPressed = touch.press.isPressed,
                        usingTouch = true
                    };
                    return true;
                }
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                pointerState = new PointerState
                {
                    screenPosition = mouse.position.ReadValue(),
                    pressedThisFrame = mouse.leftButton.wasPressedThisFrame,
                    releasedThisFrame = mouse.leftButton.wasReleasedThisFrame,
                    isPressed = mouse.leftButton.isPressed,
                    usingTouch = false
                };
                return true;
            }

            pointerState = default;
            return false;
        }

        private struct PointerState
        {
            public Vector2 screenPosition;
            public bool pressedThisFrame;
            public bool releasedThisFrame;
            public bool isPressed;
            public bool usingTouch;
        }
    }
}
