using UnityEngine;
using UnityEngine.InputSystem;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;
using System.Linq;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Maneja drag & drop en 3D usando raycast.
    /// Detecta cartas en mano, slots válidos, valida drops.
    /// </summary>
    public class DragHandler3D : MonoBehaviour
    {
        [SerializeField] private Hand3DManager hand3DManager;
        [SerializeField] private Board3DManager board3DManager;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private GameplayPresenter3D presenter;
        public GameObject dragGhost3DPrefab;

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

        [Header("Drag")]
        public float dragMinDistance = 0.5f;

        private Card3DView _draggedCard;
        private Vector3 _dragStartPos;
        private float _dragDistance;
        private bool _isDragging;
        private Board3DSlot _hoveredSlot;
        private GameObject _dragGhostInstance;
        private DragGhost3D _dragGhost;
        private float _lastHoveredDistance;

        private void Start()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;
        }

        private void Update()
        {
            HandleMouseInput();
        }

        private void HandleMouseInput()
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;

            // Mouse pressed
            if (mouse.leftButton.wasPressedThisFrame)
            {
                TryStartDrag();
            }

            // Mouse moved
            if (mouse.leftButton.isPressed && _isDragging)
            {
                UpdateDrag();
            }

            // Mouse released
            if (mouse.leftButton.wasReleasedThisFrame && _isDragging)
            {
                EndDrag();
            }
        }

        private void TryStartDrag()
        {
            var ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

            // Raycast para cartas
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                var cardView = hit.collider.GetComponentInParent<Card3DView>();
                if (cardView != null && cardView.PlayerIndex == 0) // Solo cartas locales (mano)
                {
                    _draggedCard = cardView;
                    _dragStartPos = hit.point;
                    _dragDistance = 0;
                    _isDragging = true;

                    // Save original board card positions for preview restore
                    if (presenter != null)
                    {
                        presenter.SaveOriginalCardPositions(0);
                    }

                    // Spawn drag ghost
                    if (dragGhost3DPrefab != null)
                    {
                        _dragGhostInstance = Instantiate(dragGhost3DPrefab);
                        _dragGhost = _dragGhostInstance.GetComponent<DragGhost3D>();

                        // Disable collider so raycast passes through to slots
                        var collider = _dragGhostInstance.GetComponent<Collider>();
                        if (collider != null)
                            collider.enabled = false;

                        _dragGhost.SetTargetPosition(Mouse.current.position.ReadValue(), mainCamera);
                        Debug.Log($"[DragHandler3D] Spawned drag ghost for {cardView.CardData.displayName}");
                    }
                    else
                    {
                        Debug.LogWarning("[DragHandler3D] DragGhost3DPrefab not assigned!");
                    }

                    Debug.Log($"[DragHandler3D] Started dragging {cardView.CardData.displayName}");
                }
            }
        }

        private void UpdateDrag()
        {
            if (_draggedCard == null)
                return;

            var mousePos = Mouse.current.position.ReadValue();
            var ray = mainCamera.ScreenPointToRay(mousePos);

            // Update drag ghost position
            if (_dragGhost != null)
            {
                _dragGhost.SetTargetPosition(mousePos, mainCamera);
                var ghostWorldPos = _dragGhostInstance.transform.position;
                _dragDistance = Vector3.Distance(_dragStartPos, ghostWorldPos);
            }

            // Detectar slot hovereado (ortho camera - single raycast for closest hit only, skip drag ghost)
            RaycastHit hit;
            Board3DSlot detectedSlot = null;

            // First try: raycast and skip drag ghost if we hit it
            if (Physics.Raycast(ray, out hit, 100f))
            {
                if (_dragGhostInstance != null && hit.collider.gameObject == _dragGhostInstance)
                {
                    // Hit drag ghost, raycast again with layer mask excluding it
                    int dragGhostLayer = _dragGhostInstance.layer;
                    int layerMask = ~(1 << dragGhostLayer);
                    if (Physics.Raycast(ray, out hit, 100f, layerMask))
                    {
                        var slot = hit.collider.GetComponentInParent<Board3DSlot>();
                        if (slot == null)
                            slot = hit.collider.GetComponent<Board3DSlot>();
                        if (slot != null && slot.PlayerIndex == 0)
                            detectedSlot = slot;
                    }
                }
                else
                {
                    var slot = hit.collider.GetComponentInParent<Board3DSlot>();
                    if (slot == null)
                        slot = hit.collider.GetComponent<Board3DSlot>();
                    if (slot != null && slot.PlayerIndex == 0)
                        detectedSlot = slot;
                }
            }

            SetHoveredSlot(detectedSlot);
        }

        private void EndDrag()
        {
            if (_draggedCard == null)
                return;

            Debug.Log($"[DragHandler3D] EndDrag - Distance: {_dragDistance}, HoveredSlot: {_hoveredSlot?.Slot}");

            // Si moved enough y hovering slot válido, intentar jugar
            if (_dragDistance >= dragMinDistance && _hoveredSlot != null)
            {
                TryPlayCard();
            }

            // Destroy drag ghost
            if (_dragGhostInstance != null)
            {
                Destroy(_dragGhostInstance);
                _dragGhostInstance = null;
                _dragGhost = null;
                Debug.Log("[DragHandler3D] Drag ghost destroyed");
            }

            // Limpiar
            _draggedCard = null;
            _isDragging = false;

            SetHoveredSlot(null);
        }

        private void TryPlayCard()
        {
            if (_draggedCard == null || _hoveredSlot == null)
            {
                Debug.LogWarning($"[DragHandler3D] TryPlayCard failed: card={_draggedCard}, slot={_hoveredSlot}");
                return;
            }

            var targetSlot = _hoveredSlot.Slot;
            Debug.Log($"[DragHandler3D] TryPlayCard: {_draggedCard.CardData.displayName} → {targetSlot}");

            // Validar: turno local
            var snapshot = GameplayPresenter3D.GetLatestSnapshot();
            if (snapshot == null)
            {
                Debug.LogWarning("[DragHandler3D] Snapshot unavailable; cannot play card.");
                return;
            }

            var isInProgress = snapshot.matchPhase == MatchPhase.InProgress && !snapshot.duelEnded;
            var isLocalTurn = isInProgress &&
                              (snapshot.isLocalPlayersTurn ||
                               snapshot.activePlayerIndex == snapshot.localPlayerIndex);
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

        private void SetHoveredSlot(Board3DSlot slot)
        {
            if (_hoveredSlot != slot)
            {
                if (_hoveredSlot != null)
                    _hoveredSlot.SetHighlight(false);

                _hoveredSlot = slot;

                if (_hoveredSlot != null)
                {
                    _hoveredSlot.SetHighlight(true);

                    // Preview card displacement
                    if (presenter != null && _hoveredSlot.PlayerIndex == 0)
                    {
                        presenter.PreviewCardDisplacement(0, _hoveredSlot.Slot);
                    }
                }
                else
                {
                    // Cancel preview
                    if (presenter != null)
                    {
                        presenter.CancelCardDisplacement(0);
                    }
                }
            }
        }
    }
}
