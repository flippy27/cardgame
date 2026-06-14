using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.UI
{
    public class Card3DPlayed : MonoBehaviour, ICardDisplay
    {
        [Header("References")]
        [SerializeField] private Renderer meshRenderer;
        [SerializeField] private Collider meshCollider;
        [SerializeField] private CardSurfaceVisualRenderer visualRenderer;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI attackText;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI armorText;
        [SerializeField] private GameObject armorRoot;
        [SerializeField] private Image attackTypeImage;
        [SerializeField] private GameObject attackTypeRoot;
        [SerializeField] private Sprite meleeAttackTypeSprite;
        [SerializeField] private Sprite rangedAttackTypeSprite;
        [SerializeField] private Sprite magicAttackTypeSprite;
        [SerializeField] private TextMeshProUGUI statsText;

        [Header("Runtime Icon Groups")]
        [SerializeField] private CardIconGroup abilityIconGroup;
        [SerializeField] private CardIconGroup statusIconGroup;

        [Header("Legacy Ability Icon Slots")]
        [SerializeField] private CardStateIconSlot[] abilityIconSlots;

        [Header("Legacy Buff/Debuff Icon Slots")]
        [SerializeField] private CardStateIconSlot[] statusIconSlots;

        [Header("Legacy State Icons")]
        [SerializeField] private CardStateIconSlot[] stateIconSlots;

        [Header("Colors")]
        [SerializeField] private Color baseColor = new Color(0.1f, 0.1f, 0.15f, 0.9f);

        public BoardCardDto CardData { get; private set; }
        public int PlayerIndex { get; private set; }

        private Material _cardMaterial;
        private CardEffectController _effects;

        // StatusEffectKind ints (mirror Core.StatusEffectKind / server MatchEngine.StatusEffectKind).
        private const int ShieldStatusKind = 2;   // divine-shield shimmer
        private const int StunStatusKind = 1;     // frozen-in-place look
        private const int ParalyzeStatusKind = 6; // frozen-in-place look
        private const int BurnStatusKind = 4;     // ember/heat shimmer

        // Status -> sustained shader effect mapping, evaluated in priority order (FIRST match wins).
        // The controller only renders ONE sustained surface effect at a time, so priority decides which
        // status "wins" when a card carries several. Priority: Shield > Freeze(Stun/Paralyze) > Burn.
        // To extend later, add a row (status kind -> effect) at the right priority position.
        private static readonly (int statusKind, CardEffectController.CardEffect effect)[] StatusEffectMap =
        {
            (ShieldStatusKind,   CardEffectController.CardEffect.Shield),
            (StunStatusKind,     CardEffectController.CardEffect.Freeze),
            (ParalyzeStatusKind, CardEffectController.CardEffect.Freeze),
            (BurnStatusKind,     CardEffectController.CardEffect.Burn),
        };

        // CardRarity ints (mirror Core.CardRarity / server). Legendary/Epic get a sustained foil sweep.
        private const int RarityEpic = 2;
        private const int RarityLegendary = 3;

        // Rarity foil sweep. Because the controller renders only ONE sustained surface effect at a time,
        // the rarity Holo is the BASE sustained effect: it shows whenever no status effect is active, and
        // a status effect (shield/freeze/burn) temporarily overrides it (RefreshStatusEffects falls back
        // to this when no status is present). None = this card has no rarity flair.
        private CardEffectController.CardEffect _rarityEffect = CardEffectController.CardEffect.None;
        private float _rarityIntensity = 1f;

        // Card-surface effect controller for this token (divine-shield shimmer, summon glow, ...).
        // Created lazily and bound to the same quad renderer the composite is drawn on.
        // Surface shader effects (shield/freeze/burn/holo shimmer, summon glow, dissolve) via
        // CardEffectController are TEMPORARILY DISABLED: the controller's shader-swap blanked the card
        // for the effect's duration (the post-landing "disappears then reappears"). Returning null here
        // turns every Effects?.* call into a no-op. Re-enable after the controller's material binding is
        // fixed so it can't blank the composite.
        private const bool SurfaceEffectsEnabled = false;

        private CardEffectController Effects
        {
            get
            {
                if (!SurfaceEffectsEnabled)
                {
                    return null;
                }
                if (_effects == null && meshRenderer != null)
                {
                    _effects = CardEffectController.GetOrAdd(gameObject, meshRenderer);
                }
                return _effects;
            }
        }

        private void Awake()
        {
            AutoAssignReferences();
            EnsureRuntimeMaterial();
        }

        private void Reset()
        {
            AutoAssignReferences();
        }

        private void OnValidate()
        {
            AutoAssignReferences();
        }

        public void Initialize(BoardCardDto card, int playerIndex)
        {
            CardData = card;
            PlayerIndex = playerIndex;

            AutoAssignReferences();
            EnsureRuntimeMaterial();

            if (meshCollider != null)
            {
                meshCollider.enabled = true;
                meshCollider.isTrigger = false;
            }

            // Resolve the world-space StatsOverlay canvas (the badges are parented under it and the
            // LateUpdate nudge moves it). Find by name, falling back to a stat text's parent.
            EnsureStatsOverlay();

            // Position the name strip + floating ability/status panels, THEN fill texts/badges. The
            // board prefab ships the stat texts at old hand-tuned anchored positions; the stat numbers
            // are now overlay badges (CardStatBadges), so LayoutStatsOverlay only handles name + panels.
            LayoutStatsOverlay();
            // Resolve rarity foil BEFORE the first stats refresh so RefreshStatusEffects can use it as
            // the base sustained effect (Legendary/Epic shimmer when no status overrides it).
            InitRarityEffect();
            UpdateStatsDisplay();
            visualRenderer?.ApplyCard(card.cardId, "played");

            GameLogger.Info("Card3DPlayed", $"Initialized {card.displayName}");
        }

        private Transform _statsOverlay;

        private void EnsureStatsOverlay()
        {
            if (_statsOverlay != null)
            {
                return;
            }
            var overlay = transform.Find("StatsOverlay");
            _statsOverlay = overlay != null
                ? overlay
                : (attackText != null ? attackText.transform.parent : null);
        }

        private RectTransform OverlayRect => _statsOverlay as RectTransform;

        // The board card is rotated/scaled so the stat-overlay canvas (a child at a fixed local z) ends
        // up BEHIND the card quad relative to the camera, so the opaque card occludes the numbers (the
        // baked icons show because they're on the quad itself). ZTest tricks don't work on the Mobile
        // TMP shader, so instead pin the overlay just IN FRONT of the card, toward the camera, every
        // frame. The numbers stay at their canvas-corner anchors; only the canvas plane is nudged
        // cameraward so it's never occluded. Robust to the board's odd rotation/scale and to attack
        // animations (follows the live mesh centre).
        private void LateUpdate()
        {
            if (_statsOverlay == null || meshRenderer == null)
            {
                return;
            }
            var cam = Camera.main;
            if (cam == null)
            {
                return;
            }
            var center = meshRenderer.bounds.center;
            var toCam = (cam.transform.position - center);
            var dist = toCam.magnitude;
            if (dist < 0.0001f)
            {
                return;
            }
            // Push the overlay IN FRONT of the card quad ALONG THE VIEW RAY. The board quad is tilted,
            // so its bounds have real Z depth (bounds.size.z ~2), and a tiny 0.25 nudge left the badges
            // BEHIND the card (occluded — "debajo" = behind in Z). Offsetting by the quad's half-depth
            // (extents.z) + margin clears it. Moving along the exact ray to the camera keeps the badges
            // at the SAME screen position (no parallax) — they just stop being occluded.
            var clear = meshRenderer.bounds.extents.z + 0.6f;
            _statsOverlay.position = center + (toCam / dist) * clear;
        }

        // Cell size for the floating skill/status icons above the board card. Square cells.
        private const float FloatCell = 130f;

        // The board token's stat numbers are now overlay-UI badges (CardStatBadges) built under the
        // 1000x1000 StatsOverlay at the board socket FRACTIONS (shared with CardArtLibrary). This
        // method only positions the NAME strip and the floating ability/status panels. No mana cost
        // on the board. Tune the sockets/sizes in CardArtLibrary / CardStatBadges, not here.
        private void LayoutStatsOverlay()
        {
            // The prefab wires statsText to the same object as nameText; blank the dedicated stat TMPs
            // (attack/health/armor) so a stray prefab number never shows behind the overlay badges.
            HideLegacyStat(attackText);
            HideLegacyStat(healthText);
            HideLegacyStat(armorText);

            PlaceStat(nameText, new Vector2(0.5f, 1f), new Vector2(0f, -180f), 65f);   // name strip (top)

            // Skills + buffs/debuffs float as a row of SQUARE cells just ABOVE the card's top edge,
            // CENTERED. Anchor (0.5,1) = top-centre of the 1000x1000 overlay; a SMALL positive y hugs
            // the edge (the overlay maps ~1000u to the card height, so 1u ~ 0.001 card; +70 just clears
            // the frame). Abilities sit a touch left of centre, status a touch right, so both rows read
            // as one centred cluster above the token.
            abilityIconGroup?.SetCellSize(new Vector2(FloatCell, FloatCell));
            statusIconGroup?.SetCellSize(new Vector2(FloatCell, FloatCell));
            var panelSize = new Vector2(FloatCell * 3f, FloatCell);
            PlaceIconPanel(abilityIconGroup, new Vector2(0.5f, 1f), new Vector2(-(FloatCell * 0.55f), 70f), panelSize);
            PlaceIconPanel(statusIconGroup, new Vector2(0.5f, 1f), new Vector2(FloatCell * 0.55f, 70f), panelSize);
        }

        // Blank a dedicated stat TMP (not the shared name object) so a leftover prefab number doesn't
        // show behind the new overlay badges.
        private void HideLegacyStat(TextMeshProUGUI text)
        {
            if (text == null || (nameText != null && ReferenceEquals(text.gameObject, nameText.gameObject)))
            {
                return;
            }
            text.text = string.Empty;
        }

        private static void PlaceIconPanel(CardIconGroup group, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            if (group == null || group.transform is not RectTransform rect)
            {
                return;
            }
            rect.localScale = Vector3.one;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
        }

        // Builds an icon group at runtime when the prefab didn't wire one, so unit skills + statuses
        // always show. Parented under the world-space StatsOverlay (same canvas as the stat badges).
        private CardIconGroup CreateRuntimeIconGroup(string groupName)
        {
            var parent = OverlayRect;
            if (parent == null)
            {
                return null;
            }
            var go = new GameObject(groupName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.AddComponent<CardIconGroup>();
        }

        // Positions the NAME strip (the only TMP this view still drives directly; stat numbers are
        // overlay badges now). White/bold/black-outline so it reads over the frame.
        private static void PlaceStat(TextMeshProUGUI text, Vector2 anchor, Vector2 anchoredPosition, float fontSize)
        {
            if (text == null)
            {
                return;
            }

            // The board prefab wires statsText to the SAME GameObject as nameText; force it active so a
            // stray SetActive can never hide the name.
            if (!text.gameObject.activeSelf)
            {
                text.gameObject.SetActive(true);
            }

            var rect = text.rectTransform;
            rect.localScale = Vector3.one;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(320f, 320f);
            // Force local Z to 0. The prefab ships the texts with a non-zero local z; multiplied by the
            // slot's huge z-scale (lossy z ~7) it flings the text far. anchoredPosition3D with z=0 fixes
            // it (anchoredPosition (Vector2) never touches z).
            rect.anchoredPosition3D = new Vector3(anchoredPosition.x, anchoredPosition.y, 0f);

            text.enableAutoSizing = false;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.color = Color.white;
            text.fontStyle = FontStyles.Bold;
            var mat = text.fontMaterial; // per-instance material so the outline doesn't leak to the shared one
            mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
            // NO ZTest hack: the overlay (with all badge elements coplanar) is nudged slightly toward
            // the camera by LateUpdate, so each card's badges render on that card and are correctly
            // occluded by cards physically in front — no cross-card bleed. The Mobile TMP distance-field
            // shader ignores _ZTestMode anyway; occlusion is solved purely by POSITION.
        }

        public void UpdateStatsDisplay()
        {
            if (CardData == null)
            {
                return;
            }

            // Only the NAME strip is driven here; the stat numbers are overlay badges (CardStatBadges).
            // nameText != null keeps hasDedicatedStats true so the legacy combined-stats string stays
            // hidden. Pass null for the stat TMPs so a stray prefab number never appears behind a badge.
            CardVisualCommon.ApplyCardTexts(
                CardData,
                nameText,
                null,
                null,
                null,
                null,
                null,
                armorRoot,
                statsText);

            // statsText shares the name GameObject; ApplyCardTexts may SetActive(false) it, blanking the
            // name strip. Re-enable so the card name shows at the top of the token.
            if (nameText != null && statsText != null && ReferenceEquals(nameText.gameObject, statsText.gameObject))
            {
                nameText.gameObject.SetActive(true);
            }

            // Build/refresh the overlay stat badges (circle + icon + number per stat) under the overlay.
            EnsureStatsOverlay();
            CardStatBadges.Apply(OverlayRect, CardData, isBoard: true);

            // Old unit-type square (the stray "AttackType" indicator) is REDUNDANT now — the attack
            // badge already shows the sword/bow/magic icon. Hide it (it was the pink/grey square).
            if (attackTypeImage != null) attackTypeImage.enabled = false;
            if (attackTypeRoot != null) attackTypeRoot.SetActive(false);

            // Build the icon groups at runtime if the prefab didn't wire them (skills above-left,
            // buffs/debuffs above-right), so unit skills + statuses always show on the board token.
            if (abilityIconGroup == null)
            {
                abilityIconGroup = CreateRuntimeIconGroup("AbilityIcons");
                abilityIconGroup?.SetCellSize(new Vector2(FloatCell, FloatCell));
                PlaceIconPanel(abilityIconGroup, new Vector2(0.5f, 1f), new Vector2(-(FloatCell * 0.55f), 70f), new Vector2(FloatCell * 3f, FloatCell));
            }
            if (statusIconGroup == null)
            {
                statusIconGroup = CreateRuntimeIconGroup("StatusIcons");
                statusIconGroup?.SetCellSize(new Vector2(FloatCell, FloatCell));
                PlaceIconPanel(statusIconGroup, new Vector2(0.5f, 1f), new Vector2(FloatCell * 0.55f, 70f), new Vector2(FloatCell * 3f, FloatCell));
            }
            CardVisualCommon.ApplyAbilityIcons(CardData, abilityIconGroup, abilityIconSlots);
            CardVisualCommon.ApplyStatusIcons(CardData, statusIconGroup, statusIconSlots != null && statusIconSlots.Length > 0 ? statusIconSlots : stateIconSlots);

            // Sustained card-surface effects driven by status. Refreshed on every snapshot update so
            // the shimmer/freeze/burn turns on/off as the underlying statuses come and go.
            RefreshStatusEffects();
        }

        // Maps the card's current statusEffects to the single sustained surface effect (Shield shimmer,
        // Freeze for Stun/Paralyze, Burn ember). Only ONE sustained effect renders at a time, so the
        // highest-priority status in StatusEffectMap wins (Shield > Freeze > Burn). When no mapped
        // status is present the sustained effect is cleared. Sustained: the shader animates from _Time;
        // SetSustained only flips the keyword on state change (idempotent), so calling it each snapshot
        // refresh is cheap (no per-frame work, no allocations).
        private void RefreshStatusEffects()
        {
            if (Effects == null)
            {
                return;
            }

            var desired = CardEffectController.CardEffect.None;
            foreach (var (statusKind, effect) in StatusEffectMap)
            {
                if (HasStatus(statusKind))
                {
                    desired = effect;
                    break; // priority order: first match wins.
                }
            }

            // No status effect? Fall back to the card's rarity foil (Holo) so a Legendary/Epic still
            // shimmers between status effects. A status effect always overrides the rarity flair.
            var intensity = 1f;
            if (desired == CardEffectController.CardEffect.None && _rarityEffect != CardEffectController.CardEffect.None)
            {
                desired = _rarityEffect;
                intensity = _rarityIntensity;
            }

            // Drive the controller's single sustained surface effect. SetSustained(on=true) for the
            // winner replaces any other sustained effect; passing None turns the sustained effect off.
            Effects?.SetSustained(desired, desired != CardEffectController.CardEffect.None, null, intensity);
        }

        // Resolves the card's rarity from the catalog (BoardCardDto carries no rarity; same lookup
        // CardStatBadges uses) and sets the rarity foil sweep. Legendary = full Holo; Epic = a subtler
        // Holo. Called once at init; RefreshStatusEffects then keeps it on whenever no status overrides
        // it. Safe to call before the catalog is ready (leaves _rarityEffect = None).
        private void InitRarityEffect()
        {
            _rarityEffect = CardEffectController.CardEffect.None;
            _rarityIntensity = 1f;

            var cardId = CardData?.cardId;
            if (string.IsNullOrWhiteSpace(cardId))
            {
                return;
            }

            try
            {
                var catalog = GameService.Instance?.CardCatalog;
                if (catalog != null &&
                    catalog.TryGetCard(cardId, out ServerCardDefinition definition) && definition != null)
                {
                    if (definition.cardRarity == RarityLegendary)
                    {
                        _rarityEffect = CardEffectController.CardEffect.Holo;
                        _rarityIntensity = 0.85f;
                    }
                    else if (definition.cardRarity == RarityEpic)
                    {
                        _rarityEffect = CardEffectController.CardEffect.Holo;
                        _rarityIntensity = 0.45f; // subtler foil for Epic
                    }
                }
            }
            catch (System.Exception)
            {
                // Catalog not ready — no rarity flair (RefreshStatusEffects still drives statuses).
            }
        }

        private bool HasStatus(int statusKind)
        {
            var statuses = CardData?.statusEffects;
            if (statuses == null)
            {
                return false;
            }
            foreach (var status in statuses)
            {
                if (status != null && status.kind == statusKind)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>On-play summon flash (rim glow that swells then fades). Call when the token lands.</summary>
        public void PlaySummonGlow(Color? color = null, float duration = 0.6f)
        {
            Effects?.PlaySummonGlow(color, duration);
        }

        public void SetStateIcons(CardStateVisualData[] states)
        {
            CardVisualCommon.ApplyStateIcons(statusIconGroup, stateIconSlots, states);
        }

        private void OnDestroy()
        {
            CardStatBadges.Release(OverlayRect);
        }

        public void SetColor(Color color)
        {
            if (_cardMaterial != null)
            {
                _cardMaterial.color = color;
            }
        }

        public void ResetColor()
        {
            SetColor(Color.white);
        }

        public void AnimateDrop(Vector3 targetPos, float duration = 0.3f)
        {
            StartCoroutine(AnimateDropCoroutine(targetPos, duration));
        }

        /// <summary>
        /// Drops the card "from the sky": starts above the slot, slightly larger than its resting
        /// size, and eases down to the target position and real scale (with a tiny settle overshoot).
        /// </summary>
        public void AnimateDropFromSky(Vector3 targetPos, float duration = 0.32f, float startScaleMultiplier = 1.25f, float skyHeight = 2.2f)
        {
            StartCoroutine(AnimateDropFromSkyCoroutine(targetPos, duration, startScaleMultiplier, skyHeight));
        }

        private System.Collections.IEnumerator AnimateDropFromSkyCoroutine(Vector3 targetPos, float duration, float startScaleMultiplier, float skyHeight)
        {
            var restScale = transform.localScale;
            var startScale = restScale * Mathf.Max(1f, startScaleMultiplier);
            var startPos = targetPos + Vector3.up * skyHeight;
            transform.position = startPos;
            transform.localScale = startScale;

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var ease = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic (fast then settle)
                transform.position = Vector3.Lerp(startPos, targetPos, ease);
                transform.localScale = Vector3.Lerp(startScale, restScale, ease);
                yield return null;
            }

            transform.position = targetPos;
            transform.localScale = restScale;
        }

        public void AnimateAttack(Vector3 targetPos, float returnDuration = 0.4f)
        {
            StartCoroutine(AnimateAttackCoroutine(targetPos, returnDuration));
        }

        public void AnimateDeath(float duration = 0.5f)
        {
            StartCoroutine(AnimateDeathCoroutine(duration));
        }

        private void AutoAssignReferences()
        {
            if (meshRenderer == null || !meshRenderer.gameObject.activeSelf)
            {
                meshRenderer = FindPreferredRenderer();
            }

            if (meshCollider == null || !meshCollider.gameObject.activeSelf)
            {
                meshCollider = FindPreferredCollider();
            }

            if (statsText == null)
            {
                statsText = GetComponentInChildren<TextMeshProUGUI>(true);
            }

            attackTypeImage ??= FindImageByName("attacktype", "delivery", "range", "combat");
            if (attackTypeRoot == null && attackTypeImage != null)
            {
                attackTypeRoot = attackTypeImage.gameObject;
            }

            if (visualRenderer == null)
            {
                visualRenderer = GetComponent<CardSurfaceVisualRenderer>() ?? GetComponentInChildren<CardSurfaceVisualRenderer>(true);
            }

            if (visualRenderer == null && Application.isPlaying)
            {
                visualRenderer = gameObject.AddComponent<CardSurfaceVisualRenderer>();
            }

            if (visualRenderer != null && meshRenderer != null)
            {
                visualRenderer.EnsureDefaultMaterialBinding(meshRenderer, "played");
                HideUnboundCardRenderers();
            }
        }

        // The card prefab carries a few legacy mesh quads; only the bound one shows the composited
        // card. Disable the others at runtime so their leftover material (a red quad behind the
        // card) stops showing. Runtime-only: the prefab is untouched.
        private void HideUnboundCardRenderers()
        {
            if (!Application.isPlaying || meshRenderer == null)
            {
                return;
            }

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer != meshRenderer)
                {
                    renderer.enabled = false;
                }
            }
        }

        private void EnsureRuntimeMaterial()
        {
            if (meshRenderer == null || _cardMaterial != null)
            {
                return;
            }

            var sourceMaterial = meshRenderer.sharedMaterial != null
                ? meshRenderer.sharedMaterial
                : new Material(Shader.Find("Standard"));

            // White base so the card art texture shows untinted (the binding sets the texture +
            // forces white; baseColor is only kept for highlight/death-fade effects).
            _cardMaterial = new Material(sourceMaterial)
            {
                color = Color.white
            };
            meshRenderer.material = _cardMaterial;
        }

        private Renderer FindPreferredRenderer()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var candidate in renderers)
            {
                if (candidate.gameObject.activeSelf)
                {
                    return candidate;
                }
            }

            return renderers.Length > 0 ? renderers[0] : null;
        }

        private Collider FindPreferredCollider()
        {
            var colliders = GetComponentsInChildren<Collider>(true);
            foreach (var candidate in colliders)
            {
                if (candidate.gameObject.activeSelf)
                {
                    return candidate;
                }
            }

            return colliders.Length > 0 ? colliders[0] : null;
        }

        private Image FindImageByName(params string[] keywords)
        {
            foreach (var image in GetComponentsInChildren<Image>(true))
            {
                var objectName = image.gameObject.name.ToLowerInvariant();
                foreach (var keyword in keywords)
                {
                    if (objectName.Contains(keyword))
                    {
                        return image;
                    }
                }
            }

            return null;
        }

        private System.Collections.IEnumerator AnimateDropCoroutine(Vector3 targetPos, float duration)
        {
            var startPos = transform.position;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                t = 1f - Mathf.Pow(1f - t, 3f);
                transform.position = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }

            transform.position = targetPos;
        }

        private System.Collections.IEnumerator AnimateAttackCoroutine(Vector3 targetPos, float duration)
        {
            var startPos = transform.position;
            var elapsed = 0f;

            while (elapsed < duration * 0.5f)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / (duration * 0.5f));
                transform.position = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }

            elapsed = 0f;

            while (elapsed < duration * 0.5f)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / (duration * 0.5f));
                transform.position = Vector3.Lerp(targetPos, startPos, t);
                yield return null;
            }

            transform.position = startPos;
        }

        private System.Collections.IEnumerator AnimateDeathCoroutine(float duration)
        {
            // Edge-burn dissolve so the death reads as the card disintegrating, not just fading. The
            // dissolve is a phase-driven one-shot on the card surface that runs over (roughly) the same
            // window as the scale/alpha fade below — both play together so the silhouette burns away as
            // it shrinks. Clears any sustained status effect first so it doesn't fight the dissolve.
            Effects?.ClearAll();
            Effects?.PlayDissolve(duration: duration);

            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);

                if (_cardMaterial != null)
                {
                    var color = _cardMaterial.color;
                    color.a = Mathf.Lerp(baseColor.a, 0f, t);
                    _cardMaterial.color = color;
                }

                transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
