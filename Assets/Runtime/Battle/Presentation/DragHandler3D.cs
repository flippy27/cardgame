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

                    // Spawn drag ghost
                    if (dragGhost3DPrefab != null)
                    {
                        _dragGhostInstance = Instantiate(dragGhost3DPrefab);
                        _dragGhost = _dragGhostInstance.GetComponent<DragGhost3D>();
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

            // Detectar slot hovereado (skip drag ghost)
            var hits = Physics.RaycastAll(ray, 100f);
            foreach (var hit in hits)
            {
                // Skip drag ghost
                if (_dragGhostInstance != null && hit.collider.gameObject == _dragGhostInstance)
                    continue;

                var slot = hit.collider.GetComponentInParent<Board3DSlot>();
                if (slot == null)
                    slot = hit.collider.GetComponent<Board3DSlot>();

                if (slot != null && slot.PlayerIndex == 0) // Solo slots locales
                {
                    Debug.Log($"[DragHandler3D] Found slot: {slot.Slot} (P{slot.PlayerIndex})");
                    SetHoveredSlot(slot);
                    return;
                }
            }

            SetHoveredSlot(null);
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
            if (snapshot == null || snapshot.activePlayerIndex != snapshot.localPlayerIndex)
            {
                Debug.LogWarning("[DragHandler3D] No es turno del jugador local");
                return;
            }

            // Validar: mana suficiente
            var localPlayer = snapshot.players[snapshot.localPlayerIndex];
            if (localPlayer != null && _draggedCard.CardData.runtimeId != null)
            {
                // Buscar carta en mano para obtener mana cost
                var handCard = localPlayer.hand?.FirstOrDefault(c => c.runtimeCardKey == _draggedCard.CardData.runtimeId);
                if (handCard != null && localPlayer.mana < handCard.manaCost)
                {
                    Debug.LogWarning($"[DragHandler3D] Mana insuficiente: {localPlayer.mana} < {handCard.manaCost}");
                    return;
                }
            }

            // Validar: slot válido según reglas
            if (!IsSlotValidForPlay(targetSlot, snapshot))
            {
                Debug.LogWarning($"[DragHandler3D] Slot {targetSlot} no válido para jugar");
                return;
            }

            if (presenter == null)
            {
                Debug.LogError("[DragHandler3D] presenter is null!");
                return;
            }

            Debug.Log($"[DragHandler3D] ✓ Playing {_draggedCard.CardData.displayName} (ID: {_draggedCard.CardData.runtimeId}) to {targetSlot}");
            presenter.RequestPlayCard(_draggedCard.CardData.runtimeId, targetSlot);
        }

        private bool IsSlotValidForPlay(BoardSlot targetSlot, DuelSnapshotDto snapshot)
        {
            var localPlayer = snapshot.players[snapshot.localPlayerIndex];
            if (localPlayer?.board == null)
                return false;

            // Obtener estado actual del board
            var boardCards = new System.Collections.Generic.Dictionary<BoardSlot, bool>();
            foreach (var slot in localPlayer.board)
            {
                boardCards[slot.slot] = slot.occupied;
            }

            // Reglas según gameplay_explication.md:
            // - Sin cartas: solo Front
            // - Front ocupado, Left vacío: Front (shift) o Left
            // - Front y Left ocupados, Right vacío: Front (shift), Left (shift), o Right
            // - Todo ocupado: invalid

            if (!boardCards[BoardSlot.Front])
            {
                return targetSlot == BoardSlot.Front;
            }

            if (!boardCards[BoardSlot.BackLeft])
            {
                return targetSlot == BoardSlot.Front || targetSlot == BoardSlot.BackLeft;
            }

            if (!boardCards[BoardSlot.BackRight])
            {
                return targetSlot == BoardSlot.Front || targetSlot == BoardSlot.BackLeft || targetSlot == BoardSlot.BackRight;
            }

            // Todo ocupado, no puedes jugar
            return false;
        }

        private void SetHoveredSlot(Board3DSlot slot)
        {
            if (_hoveredSlot != slot)
            {
                if (_hoveredSlot != null)
                    _hoveredSlot.SetHighlight(false);

                _hoveredSlot = slot;

                if (_hoveredSlot != null)
                    _hoveredSlot.SetHighlight(true);
            }
        }
    }
}
