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
        [Tooltip("Allow dragging cards already in play (destroy-by-drag). Off: played cards aren't draggable.")]
        [SerializeField] private bool allowBoardCardDrag = false;

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
        private ICardDisplay _targetCard;             // board card highlighted as a spell/equipment target
        private GameObject _targetOutline;            // generated soft-glow quad hugging _targetCard's border
        private static readonly Color FriendlyTargetColor = new Color(0.2f, 1f, 0.3f);
        private static readonly Color EnemyTargetColor = new Color(1f, 0.25f, 0.2f);
        // --- Soft rim-glow tuning (all in the card's local space, where the card is ~1 unit) ---
        private const float targetGlowScale = 1.22f;     // glow quad size relative to the card (outer reach of the halo)
        private const float targetGlowBorder = 0.16f;    // glow band thickness as a fraction of the quad (rim hugging the edge)
        private const float targetGlowFeather = 0.55f;   // 0..1 softness of the outward fade (higher = softer/blurrier)
        private const float targetGlowIntensity = 0.9f;  // peak alpha of the glow rim
        private const float targetGlowLocalZ = 0.02f;    // push BEHIND the card so the halo peeks out around the edges
        private const float targetGlowPulseAmount = 0.18f; // alpha swing of the gentle pulse (fraction of intensity)
        private const float targetGlowPulseScale = 0.03f;  // scale swing of the gentle pulse (fraction of size)
        private const float targetGlowPulseSpeed = 3.2f;    // pulse cycles speed (radians/sec feed)
        private const int targetGlowTextureSize = 256;      // resolution of the cached glow texture
        private const float hoverMaxDistance = 3.2f; // max world distance from a slot centre to count as "over board"
        private const float hoverHysteresis = 1.0f;  // the nearest slot must be this much closer to switch away
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
        // Renderers + canvases of the hand card being dragged; hidden so only the drag ghost shows.
        private Renderer[] _draggedCardHiddenRenderers;
        private Canvas[] _draggedCardHiddenCanvases;
        private BoardCardDestroyDropZone _hoveredDestroyZone;
        private Vector2 _pressStartScreenPos;
        private Vector2 _detailInteractionStartScreenPos;

        private void Start()
        {
            EnsureReferences();
        }

        private void OnDisable()
        {
            // Don't leave a target halo behind if the handler is torn down / disabled mid-drag.
            ClearTargetOutline();
            _targetCard = null;
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

            // Cards already in play are not draggable (no destroy-by-drag) unless explicitly enabled.
            if (allowBoardCardDrag &&
                _pressedBoardCard != null &&
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

            // Hide the original hand card so only the ghost is visible while dragging (avoids the
            // duplicate). Capture ONLY the renderers that are currently enabled, so restoring on cancel
            // doesn't switch on stray disabled quads (e.g. the red placeholder sibling).
            _draggedCardHiddenRenderers = cardView.GetComponentsInChildren<Renderer>(true)
                .Where(r => r != null && r.enabled).ToArray();
            SetRenderersEnabled(_draggedCardHiddenRenderers, false);
            // Also hide the source card's stat canvases, or its numbers stay visible at the hand
            // position while the card itself is hidden (looked like detached numbers).
            _draggedCardHiddenCanvases = cardView.GetComponentsInChildren<Canvas>(true)
                .Where(c => c != null && c.enabled).ToArray();
            foreach (var canvas in _draggedCardHiddenCanvases)
            {
                canvas.enabled = false;
            }

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

            // Pick the hovered slot by NEAREST slot centre to the ghost (not a physics raycast, which
            // flips at slot boundaries and gets intercepted by displaced cards' colliders). Hysteresis
            // keeps the current slot until another is clearly closer, so it never oscillates.
            var ghostPos = _dragGhostInstance != null ? _dragGhostInstance.transform.position : Vector3.zero;

            // Non-unit cards (spell/equipment/utility) don't take a slot — they pick a TARGET board
            // card (highlighted) instead of doing slot displacement.
            if (DraggedCardIsNonUnit())
            {
                SetHoveredSlot(null);
                UpdateTargetHover(ghostPos);
            }
            else
            {
                SetTargetCard(null);
                SetHoveredSlot(ResolveHoverSlotByDistance(ghostPos));
            }
        }

        // True when the dragged hand card is Utility/Equipment/Spell (cardType != Unit), resolved from
        // the catalog (the hand DTO carries no cardType).
        private bool DraggedCardIsNonUnit()
        {
            var cardId = _draggedCard?.CardData?.cardId;
            if (string.IsNullOrEmpty(cardId))
            {
                return false;
            }
            var catalog = Networking.GameService.Instance?.CardCatalog;
            if (catalog != null && catalog.TryGetCard(cardId, out var def) && def != null)
            {
                return def.cardType != 0; // 0 = Unit
            }
            return false;
        }

        // Mirrors the server's EnsureLegalPlacement for the LOCAL player's board. Placement priority is
        // Front -> BackLeft -> BackRight; a back slot is blocked until its prerequisite(s) are occupied,
        // and an occupied slot stays playable (placing there shifts the chain down) until all three are
        // full. Reads occupancy from the latest snapshot's local board.
        //   - Front:     legal unless all three slots are occupied (full board).
        //   - BackLeft:  legal only if Front is occupied AND not (BackLeft AND BackRight both occupied).
        //   - BackRight: legal only if Front AND BackLeft are occupied AND BackRight is empty.
        // Only applies to UNIT placement; callers gate non-units out before calling this.
        private bool IsSlotLegalForPlacement(BoardSlot slot)
        {
            var snapshot = GameplayPresenter3D.GetLatestSnapshot();
            if (snapshot?.players == null ||
                snapshot.localPlayerIndex < 0 ||
                snapshot.localPlayerIndex >= snapshot.players.Length)
            {
                // Without a snapshot we can't validate; fail closed so we never highlight/send an
                // illegal slot. (Front would be the only ever-safe choice, but be conservative.)
                return false;
            }

            var board = snapshot.players[snapshot.localPlayerIndex]?.board;
            var frontOccupied = IsSlotOccupied(board, BoardSlot.Front);
            var backLeftOccupied = IsSlotOccupied(board, BoardSlot.BackLeft);
            var backRightOccupied = IsSlotOccupied(board, BoardSlot.BackRight);

            switch (slot)
            {
                case BoardSlot.Front:
                    return !(frontOccupied && backLeftOccupied && backRightOccupied);
                case BoardSlot.BackLeft:
                    return frontOccupied && !(backLeftOccupied && backRightOccupied);
                case BoardSlot.BackRight:
                    return frontOccupied && backLeftOccupied && !backRightOccupied;
                default:
                    return false;
            }
        }

        private static bool IsSlotOccupied(BoardSlotSnapshotDto[] board, BoardSlot slot)
        {
            if (board == null)
            {
                return false;
            }
            foreach (var entry in board)
            {
                if (entry != null && entry.slot == slot)
                {
                    return entry.occupied;
                }
            }
            return false;
        }

        // Highlights the board card nearest the ghost as the spell/equipment target.
        private void UpdateTargetHover(Vector3 ghostPos)
        {
            if (board3DManager == null)
            {
                SetTargetCard(null);
                return;
            }
            ICardDisplay nearest = null;
            var nearestDist = float.MaxValue;
            foreach (var p in new[] { 0, 1 })
            {
                foreach (var s in new[] { BoardSlot.Front, BoardSlot.BackLeft, BoardSlot.BackRight })
                {
                    var card = board3DManager.GetCardInSlot(p, s);
                    var slot = board3DManager.GetSlot(p, s);
                    if (card == null || slot == null)
                    {
                        continue;
                    }
                    var sp = slot.transform.position;
                    var d = Vector2.Distance(new Vector2(sp.x, sp.y), new Vector2(ghostPos.x, ghostPos.y));
                    if (d < nearestDist)
                    {
                        nearestDist = d;
                        nearest = card;
                    }
                }
            }
            SetTargetCard(nearestDist <= hoverMaxDistance ? nearest : null);
        }

        private void SetTargetCard(ICardDisplay card)
        {
            if (_targetCard == card)
            {
                return;
            }

            ClearTargetOutline();
            _targetCard = card;

            if (_targetCard != null)
            {
                // Green outline = friendly/ally target (local player owns it, PlayerIndex 0),
                // red outline = enemy target. Ownership comes straight off the board card itself.
                var color = _targetCard.PlayerIndex == 0 ? FriendlyTargetColor : EnemyTargetColor;
                ShowTargetOutline(_targetCard, color);
            }
        }

        // Renders a soft RIM-GLOW that hugs the targeted card's silhouette: a single quad, sitting just
        // behind the card and a little larger, textured with a cached soft-edged rounded-rectangle halo
        // (transparent centre, coloured rim feathered outward). Tinted green (friendly) / red (enemy) and
        // gently pulsed. Reads as an emissive glow tracing the border, not a hard bar. Generated in code
        // (no prefab/shader asset needed) and re-created each time the target changes; cleaned up by
        // ClearTargetOutline. This replaces the old hollow green frame.
        private void ShowTargetOutline(ICardDisplay card, Color color)
        {
            if (card == null || !card.TryGetTransform(out var cardTransform))
            {
                return;
            }

            _targetOutline = new GameObject("SpellTargetGlow")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _targetOutline.transform.SetParent(cardTransform, worldPositionStays: false);
            // Push slightly BEHIND the card (+Z, away from camera) so only the feathered halo peeks out
            // around the edges instead of overlapping the card art.
            _targetOutline.transform.localPosition = new Vector3(0f, 0f, targetGlowLocalZ);
            _targetOutline.transform.localRotation = Quaternion.identity;
            _targetOutline.transform.localScale = Vector3.one * targetGlowScale;

            var filter = _targetOutline.AddComponent<MeshFilter>();
            filter.sharedMesh = GetGlowQuadMesh();

            var renderer = _targetOutline.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Per-instance material (tinted + pulsed alpha) over a shared transparent-unlit shader and the
            // shared white glow texture. Unlit transparent reads as a flat emissive halo regardless of the
            // scene lighting / render pipeline.
            var material = new Material(GetGlowShader())
            {
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = GetGlowTexture()
            };
            ApplyGlowTint(material, color, targetGlowIntensity);
            renderer.sharedMaterial = material;

            // Gentle pulse (alpha + scale) so the highlight feels alive without being distracting.
            var pulse = _targetOutline.AddComponent<TargetGlowPulse>();
            pulse.Configure(material, color, targetGlowScale, targetGlowIntensity,
                targetGlowPulseAmount, targetGlowPulseScale, targetGlowPulseSpeed);
        }

        private void ClearTargetOutline()
        {
            if (_targetOutline == null)
            {
                return;
            }

            var renderer = _targetOutline.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                Destroy(renderer.sharedMaterial);
            }

            Destroy(_targetOutline);
            _targetOutline = null;
        }

        // Tints the glow material to the given colour at the given peak alpha. White texture * colour gives
        // the rim its hue; alpha rides the texture's feathered falloff.
        private static void ApplyGlowTint(Material material, Color color, float intensity)
        {
            if (material == null)
            {
                return;
            }
            var tint = color;
            tint.a = Mathf.Clamp01(intensity);
            material.color = tint;
            if (material.HasProperty(GlowColorId))
            {
                material.SetColor(GlowColorId, tint);
            }
        }

        private static readonly int GlowColorId = Shader.PropertyToID("_Color");

        // A simple unit quad (1x1 in XY, facing -Z toward the camera) carrying the glow texture. Scaled up
        // by targetGlowScale on the instance. Built once and reused for every target glow.
        private static Mesh _glowQuadMesh;
        private static Mesh GetGlowQuadMesh()
        {
            if (_glowQuadMesh != null)
            {
                return _glowQuadMesh;
            }

            const float h = 0.5f;
            var vertices = new[]
            {
                new Vector3(-h, -h, 0f),
                new Vector3(-h, h, 0f),
                new Vector3(h, h, 0f),
                new Vector3(h, -h, 0f)
            };
            var uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f)
            };
            // Wound so the quad faces -Z (toward the camera).
            var triangles = new[] { 0, 1, 2, 0, 2, 3 };

            _glowQuadMesh = new Mesh
            {
                name = "SpellTargetGlowQuad",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = vertices,
                uv = uv,
                triangles = triangles
            };
            _glowQuadMesh.RecalculateNormals();
            _glowQuadMesh.RecalculateBounds();
            return _glowQuadMesh;
        }

        // Picks a transparent unlit shader that works in built-in + URP. "Sprites/Default" multiplies
        // texture * vertex/_Color and blends alpha — ideal for a tinted, feathered glow. Falls back to the
        // particle additive shader, then plain unlit transparent.
        private static Shader _glowShader;
        private static Shader GetGlowShader()
        {
            if (_glowShader != null)
            {
                return _glowShader;
            }
            _glowShader = Shader.Find("Sprites/Default")
                ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended")
                ?? Shader.Find("Unlit/Transparent");
            return _glowShader;
        }

        // Cached soft glow texture: a rounded-rectangle "rim" in white with a transparent centre and an
        // alpha that feathers to zero outward, so tinting it produces a soft halo hugging the card edge.
        // The band peaks near the card silhouette and fades both inward (so it doesn't cover the art) and
        // outward (so the edge is soft, not a hard line).
        private static Texture2D _glowTexture;
        private static Texture2D GetGlowTexture()
        {
            if (_glowTexture != null)
            {
                return _glowTexture;
            }

            var size = Mathf.Max(16, targetGlowTextureSize);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false, linear: false)
            {
                name = "SpellTargetGlowTex",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            // The card silhouette occupies the quad inset by half the (scaled) overshoot. In UV terms the
            // glow quad is targetGlowScale across, so the card edge sits at inset = (1 - 1/scale) * 0.5.
            var edgeInset = Mathf.Clamp01((1f - 1f / Mathf.Max(0.0001f, targetGlowScale)) * 0.5f);
            // Half-thickness of the bright band, as a fraction of the quad (UV space).
            var band = Mathf.Max(0.001f, targetGlowBorder * 0.5f);
            var feather = Mathf.Clamp01(targetGlowFeather);

            for (var y = 0; y < size; y++)
            {
                var v = (y + 0.5f) / size;
                for (var x = 0; x < size; x++)
                {
                    var u = (x + 0.5f) / size;
                    // Distance from the nearest quad edge (0 at the border, 0.5 at the centre).
                    var distToEdge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                    // Signed distance from the card silhouette line: 0 on the line, grows inward.
                    var d = distToEdge - edgeInset;

                    float alpha;
                    if (d <= 0f)
                    {
                        // Outside the card silhouette (toward the quad border): feather outward to 0.
                        // -band reaches full edge of the soft outer falloff.
                        var t = Mathf.Clamp01(1f + d / (band * (1f + feather * 2f)));
                        alpha = t * t; // ease so the outer fade is gentle
                    }
                    else
                    {
                        // Inside the card: fade quickly to 0 so the centre/art stays clear.
                        var t = Mathf.Clamp01(1f - d / band);
                        alpha = t * t;
                    }

                    var a = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false);
            _glowTexture = tex;
            return _glowTexture;
        }

        // Subtly animates the glow's alpha and scale so the target highlight breathes. Lives on the glow
        // GameObject and is destroyed with it; it only drives the per-instance material/transform.
        private sealed class TargetGlowPulse : MonoBehaviour
        {
            private Material _material;
            private Color _baseColor;
            private float _baseScale;
            private float _baseIntensity;
            private float _alphaAmount;
            private float _scaleAmount;
            private float _speed;
            private float _phase;

            public void Configure(Material material, Color color, float baseScale, float baseIntensity,
                float alphaAmount, float scaleAmount, float speed)
            {
                _material = material;
                _baseColor = color;
                _baseScale = baseScale;
                _baseIntensity = baseIntensity;
                _alphaAmount = alphaAmount;
                _scaleAmount = scaleAmount;
                _speed = speed;
                _phase = 0f;
            }

            private void Update()
            {
                if (_material == null)
                {
                    return;
                }

                _phase += Time.deltaTime * _speed;
                // 0..1 breathing curve.
                var wave = (Mathf.Sin(_phase) + 1f) * 0.5f;

                var intensity = _baseIntensity * (1f - _alphaAmount * (1f - wave));
                ApplyGlowTint(_material, _baseColor, intensity);

                var scale = _baseScale * (1f + _scaleAmount * (wave - 0.5f) * 2f);
                transform.localScale = Vector3.one * scale;
            }
        }

        private Board3DSlot ResolveHoverSlotByDistance(Vector3 worldPos)
        {
            if (board3DManager == null)
            {
                return null;
            }

            Board3DSlot nearest = null;
            var nearestDist = float.MaxValue;
            foreach (var slotEnum in new[] { BoardSlot.Front, BoardSlot.BackLeft, BoardSlot.BackRight })
            {
                // Only the local player's LEGAL slots can be hovered/highlighted for a unit placement
                // (Front -> BackLeft -> BackRight priority). Illegal slots are skipped entirely so the
                // nearest LEGAL slot wins and the displacement preview never fires on a blocked slot.
                if (!IsSlotLegalForPlacement(slotEnum))
                {
                    continue;
                }
                var slot = board3DManager.GetSlot(0, slotEnum);
                if (slot == null)
                {
                    continue;
                }
                var p = slot.transform.position;
                var d = Vector2.Distance(new Vector2(p.x, p.y), new Vector2(worldPos.x, worldPos.y)); // ignore depth
                if (d < nearestDist)
                {
                    nearestDist = d;
                    nearest = slot;
                }
            }

            // Off the board area entirely -> no hover (clears the preview).
            if (nearest == null || nearestDist > hoverMaxDistance)
            {
                return null;
            }

            // Hysteresis: keep the current slot unless the new nearest is at least hoverHysteresis closer.
            // Only stick to the current slot while it is itself still a legal placement target.
            if (_hoveredSlot != null && _hoveredSlot != nearest && IsSlotLegalForPlacement(_hoveredSlot.Slot))
            {
                var cp = _hoveredSlot.transform.position;
                var currentDist = Vector2.Distance(new Vector2(cp.x, cp.y), new Vector2(worldPos.x, worldPos.y));
                if (currentDist <= nearestDist + hoverHysteresis)
                {
                    return _hoveredSlot;
                }
            }

            return nearest;
        }

        private void EndDrag()
        {
            if (_draggedCard == null)
            {
                return;
            }

            Debug.Log($"[DragHandler3D] EndDrag - WorldDistance: {_dragDistance}, ScreenDistance: {_dragScreenDistance}, HoveredSlot: {_hoveredSlot?.Slot}");

            var played = false;
            var movedEnough = _dragDistance >= dragMinDistance || _dragScreenDistance >= dragMinScreenDistance;
            if (movedEnough)
            {
                if (DraggedCardIsNonUnit())
                {
                    if (_targetCard != null)
                    {
                        played = TryPlayCardOnTarget(_targetCard);
                    }
                }
                else if (_hoveredSlot != null)
                {
                    played = TryPlayCard();
                }
            }
            SetTargetCard(null); // clear any target highlight

            if (_dragGhostInstance != null)
            {
                // The card "dissolves" into particles at the point it was released; the board card then
                // falls from the sky a beat later (GameplayPresenter3D.AnimateBoardCardEntry).
                if (played)
                {
                    CardFeedbackVfx.Disperse(_dragGhostInstance.transform.position, new Color(0.9f, 0.95f, 1f, 1f));
                }
                Destroy(_dragGhostInstance);
                _dragGhostInstance = null;
                _dragGhost = null;
                Debug.Log("[DragHandler3D] Drag ghost destroyed");
            }

            // If the card was played, keep it hidden (no flash of the hand card at its old position):
            // the snapshot either removes it from the hand (accepted) or RefreshHand re-shows it
            // (rejected — it re-enables hand card renderers, so it is never left "consumed but hidden").
            // If the drag was aborted, restore it now.
            if (!played)
            {
                SetRenderersEnabled(_draggedCardHiddenRenderers, true);
                if (_draggedCardHiddenCanvases != null)
                {
                    foreach (var canvas in _draggedCardHiddenCanvases)
                    {
                        if (canvas != null)
                        {
                            canvas.enabled = true;
                        }
                    }
                }
            }
            _draggedCardHiddenRenderers = null;
            _draggedCardHiddenCanvases = null;

            _draggedCard = null;
            _isDragging = false;

            if (played)
            {
                // Leave the displaced cards at their preview positions — the play snapshot confirms them
                // in place. Calling SetHoveredSlot(null) here would CancelCardDisplacement and animate
                // them back to their original slots, then the snapshot would re-animate them forward
                // (the "go and come back" bounce). Just drop the highlight.
                if (_hoveredSlot != null)
                {
                    _hoveredSlot.SetHighlight(false);
                }
                _hoveredSlot = null;
            }
            else
            {
                SetHoveredSlot(null); // aborted drag: animate displaced cards back to their original slots
            }
        }

        private static void SetRenderersEnabled(Renderer[] renderers, bool enabled)
        {
            if (renderers == null)
            {
                return;
            }
            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = enabled;
                }
            }
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

        private bool TryPlayCard()
        {
            if (_draggedCard == null || _hoveredSlot == null)
            {
                Debug.LogWarning($"[DragHandler3D] TryPlayCard failed: card={_draggedCard}, slot={_hoveredSlot}");
                return false;
            }

            var targetSlot = _hoveredSlot.Slot;
            Debug.Log($"[DragHandler3D] TryPlayCard: {_draggedCard.CardData.displayName} -> {targetSlot}");

            // Final client-side guard: units may only be placed on a LEGAL slot (Front -> BackLeft ->
            // BackRight priority). This prevents sending a PlayCard the server would reject with
            // "left_slot_required" / "front_slot_required". Non-units never reach here (they go through
            // TryPlayCardOnTarget), but scope the check to units defensively all the same.
            if (!DraggedCardIsNonUnit() && !IsSlotLegalForPlacement(targetSlot))
            {
                Debug.LogWarning($"[DragHandler3D] Illegal slot {targetSlot} for unit placement; returning card to hand.");
                return false;
            }

            var snapshot = GameplayPresenter3D.GetLatestSnapshot();
            if (snapshot == null)
            {
                Debug.LogWarning("[DragHandler3D] Snapshot unavailable; cannot play card.");
                return false;
            }

            var isLocalTurn = SnapshotTurnAuthority.IsLocalTurn(snapshot);
            if (!isLocalTurn)
            {
                Debug.LogWarning("[DragHandler3D] No es turno del jugador local");
                return false;
            }

            var localPlayer = snapshot.players[snapshot.localPlayerIndex];
            if (localPlayer == null)
            {
                Debug.LogWarning("[DragHandler3D] Local player snapshot missing.");
                return false;
            }

            var handCard = localPlayer.hand?.FirstOrDefault(c => c.runtimeCardKey == _draggedCard.CardData.runtimeId);
            if (handCard == null)
            {
                Debug.LogWarning("[DragHandler3D] Card is not present in the latest local hand snapshot.");
                return false;
            }

            if (presenter == null)
            {
                Debug.LogError("[DragHandler3D] presenter is null!");
                return false;
            }

            Debug.Log($"[DragHandler3D] Playing {_draggedCard.CardData.displayName} (ID: {_draggedCard.CardData.runtimeId}) to {targetSlot}");
            presenter.RequestPlayCard(_draggedCard.CardData.runtimeId, targetSlot);
            return true;
        }

        // Plays a non-unit card (spell/equipment/utility) against a chosen board card. Slot is
        // irrelevant server-side for these; the target runtimeId drives the effect.
        private bool TryPlayCardOnTarget(ICardDisplay target)
        {
            if (_draggedCard?.CardData == null || target?.CardData == null || presenter == null)
            {
                return false;
            }

            var snapshot = GameplayPresenter3D.GetLatestSnapshot();
            if (snapshot == null || !SnapshotTurnAuthority.IsLocalTurn(snapshot))
            {
                return false;
            }

            var localPlayer = snapshot.players != null && snapshot.localPlayerIndex >= 0 && snapshot.localPlayerIndex < snapshot.players.Length
                ? snapshot.players[snapshot.localPlayerIndex]
                : null;
            var handCard = localPlayer?.hand?.FirstOrDefault(c => c.runtimeCardKey == _draggedCard.CardData.runtimeId);
            if (handCard == null)
            {
                return false;
            }

            Debug.Log($"[DragHandler3D] Playing {_draggedCard.CardData.displayName} on target {target.CardData.runtimeId}");
            presenter.RequestPlayCard(_draggedCard.CardData.runtimeId, BoardSlot.Front, target.CardData.runtimeId);
            return true;
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

            // Pickup sparkle where the card lifts off the hand.
            if (sourceCard != null)
            {
                CardFeedbackVfx.PickupSparkle(sourceCard.transform.position, new Color(0.85f, 0.92f, 1f, 1f));
            }

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
