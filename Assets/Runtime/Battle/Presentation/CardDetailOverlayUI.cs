using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.UI.DeckBuilding;

namespace Flippy.CardDuelMobile.UI
{
    public class CardDetailOverlayUI : MonoBehaviour
    {
        // Runtime-created card art rect; the stat numbers are parented to it so they
        // float over the previewed card regardless of scene wiring.
        private RectTransform cardArtRect;

        // Runtime-created right-side panel that lists one block per ability/skill (name + description).
        private RectTransform skillsContent;     // the vertical-layout content the blocks parent to
        private GameObject skillsScrollRoot;     // the whole right column (scroll view), toggled with empty state
        private TextMeshProUGUI skillsEmptyText; // shown when the card has no abilities

        // Layout constants (mobile-portrait friendly). The card sits on the left, smaller; the skills
        // column fills the right. Tune here.
        private const float CardWidth = 360f;       // was 520 (centred); now smaller + left-aligned
        private const float CardHeight = 540f;      // keeps the 2:3 hand ratio (360x540)
        private const float CardLeftMargin = 40f;
        private const float ColumnGap = 28f;
        private const float SkillsRightMargin = 40f;
        private const float SkillBlockSpacing = 14f;
        private const float SkillBlockPadding = 14f;
        private const float SkillIconSize = 56f;

        [Header("Visibility")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private CardSurfaceVisualRenderer visualRenderer;

        [Header("Texts")]
        [SerializeField] private TextMeshProUGUI titleText;
        // Built in code (no prefab wiring): a small "★N" upgrade-level badge shown next to the title.
        private TextMeshProUGUI levelBadgeText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private GameObject costRoot;
        [SerializeField] private TextMeshProUGUI attackText;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI armorText;
        [SerializeField] private GameObject armorRoot;
        [SerializeField] private Image attackTypeImage;
        [SerializeField] private GameObject attackTypeRoot;
        [SerializeField] private Sprite meleeAttackTypeSprite;
        [SerializeField] private Sprite rangedAttackTypeSprite;
        [SerializeField] private Sprite magicAttackTypeSprite;
        [SerializeField] private TextMeshProUGUI ownerText;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private TextMeshProUGUI legacyStatsText;

        [Header("Runtime Icon Groups")]
        [SerializeField] private CardIconGroup abilityIconGroup;
        [SerializeField] private CardIconGroup statusIconGroup;

        [Header("Legacy Ability Icon Slots")]
        [SerializeField] private CardStateIconSlot[] abilityIconSlots;

        [Header("Legacy Buff/Debuff Icon Slots")]
        [SerializeField] private CardStateIconSlot[] statusIconSlots;

        [Header("Legacy State Icons")]
        [SerializeField] private CardStateIconSlot[] stateIconSlots;

        public bool IsVisible { get; private set; }
        public ICardDisplay CurrentSource { get; private set; }
        public Card3DView CurrentHandCardSource => CurrentSource as Card3DView;

        private void Awake()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (visualRenderer == null)
            {
                visualRenderer = panelRoot.GetComponent<CardSurfaceVisualRenderer>() ??
                                 GetComponent<CardSurfaceVisualRenderer>() ??
                                 GetComponentInChildren<CardSurfaceVisualRenderer>(true);
            }

            if (visualRenderer == null)
            {
                visualRenderer = panelRoot.AddComponent<CardSurfaceVisualRenderer>();
            }

            var detailImage = FindPreferredDetailImage();
            // FindPreferredDetailImage only finds a real art image when the scene has one named
            // art/visual/card; otherwise it falls back to an unrelated icon panel image (which made the
            // preview render into a tiny wrong rect under the full-screen dim panel). Guarantee a proper
            // left-aligned 2:3 art surface by creating a dedicated one when none is named.
            if (detailImage == null || !detailImage.gameObject.name.ToLowerInvariant().Contains("art"))
            {
                detailImage = CreateDetailArtImage();
            }
            if (visualRenderer != null && detailImage != null)
            {
                visualRenderer.EnsureDefaultImageBinding(detailImage, "played");
                detailImage.preserveAspect = true; // 2:3 card, never stretched across the overlay
            }
            cardArtRect = detailImage != null ? detailImage.rectTransform : null;

            // Place the (possibly scene-provided) card art on the LEFT side, smaller, so the right
            // half is free for the per-skill blocks. Safe even when the art image came from the scene.
            LayoutCardArtLeft(cardArtRect);

            // Build the right-side scrollable skills column (one block per ability is filled in Show()).
            BuildSkillsColumn();

            attackTypeImage ??= FindPreferredAttackTypeImage();
            if (attackTypeRoot == null && attackTypeImage != null)
            {
                attackTypeRoot = attackTypeImage.gameObject;
            }

            // Bug (a): a full-screen background Image on the panelRoot was rendering as an opaque
            // grey wash behind the preview. Drop its alpha so the preview floats over the game
            // instead of greying it out, while keeping it a raycast target for click-to-dismiss.
            DimBackdropImage();

            SetVisible(false);
        }

        public void Show(ICardDisplay source, string ownerLabel = null)
        {
            // Default entry point (used in-battle): no upgrade level is available on the board/hand
            // BoardCardDto, so the "★N" badge stays hidden. NOTE: showing card level in-battle needs the
            // upgrade level plumbed through the server MatchSnapshot — that's a follow-up (see report).
            Show(source, ownerLabel, cardLevel: 0);
        }

        /// <summary>
        /// Preview overload that also shows the owned card's upgrade level as a "★N" badge next to the
        /// title (collection / preview callers pass the resolved level; pass ≤ 1 to hide it).
        /// </summary>
        public void Show(ICardDisplay source, string ownerLabel, int cardLevel)
        {
            if (source?.CardData == null)
            {
                return;
            }

            CurrentSource = source;
            var card = source.CardData;

            // Stat numbers are overlay badges (CardStatBadges) built on the card-art rect — consistent
            // with the hand/board cards. Only the TITLE is driven through ApplyCardTexts here; passing
            // null for the stat TMPs keeps the legacy combined-stats string hidden (titleText != null).
            CardVisualCommon.ApplyCardTexts(
                card,
                titleText,
                null,
                costRoot,
                null,
                null,
                null,
                armorRoot,
                legacyStatsText);

            // Upgrade-level "★N" badge next to the title (hidden for ≤ 1 / in-battle calls).
            UpdateLevelBadge(cardLevel);

            // The preview always shows the full HAND-style card, so use hand sockets (with cost).
            CardStatBadges.Apply(cardArtRect, card, isBoard: false);

            if (ownerText != null)
            {
                ownerText.text = ownerLabel ?? string.Empty;
                ownerText.gameObject.SetActive(!string.IsNullOrWhiteSpace(ownerLabel));
            }

            CardVisualCommon.ApplyDescriptionText(card, bodyText);
            // Old unit-type square is redundant (the attack badge shows sword/bow/magic) — hide it.
            if (attackTypeImage != null) attackTypeImage.enabled = false;
            if (attackTypeRoot != null) attackTypeRoot.SetActive(false);

            // The big preview always shows the full hand-style card, even for board (played) cards.
            visualRenderer?.ApplyCard(card.cardId, "hand");
            CardVisualCommon.ApplyAbilityIcons(card, abilityIconGroup, abilityIconSlots);
            var resolvedStatusSlots = statusIconSlots != null && statusIconSlots.Length > 0 ? statusIconSlots : stateIconSlots;
            CardVisualCommon.ApplyStatusIcons(card, statusIconGroup, resolvedStatusSlots);

            // NEW: populate the right-side per-skill blocks (name + description).
            BuildSkillBlocks(card);

            SetVisible(true);
        }

        public void Hide()
        {
            CurrentSource = null;
            SetVisible(false);
        }

        public bool IsShowing(ICardDisplay source)
        {
            return IsVisible && CurrentSource != null && ReferenceEquals(CurrentSource, source);
        }

        // Lazily builds and updates a small "★N" badge anchored to the right of the title. Hidden when
        // level ≤ 1 (unupgraded) or when there is no titleText to anchor against.
        private void UpdateLevelBadge(int cardLevel)
        {
            bool show = cardLevel > 1 && titleText != null;
            if (levelBadgeText == null)
            {
                if (!show)
                {
                    return;
                }

                var go = new GameObject("LevelBadge", typeof(RectTransform));
                go.transform.SetParent(titleText.transform.parent, false);
                levelBadgeText = go.AddComponent<TextMeshProUGUI>();
                ApplyThemeFont(levelBadgeText);
                levelBadgeText.fontSize = 34f;
                levelBadgeText.fontStyle = FontStyles.Bold;
                levelBadgeText.color = new Color(1f, 0.85f, 0.25f); // gold
                levelBadgeText.alignment = TextAlignmentOptions.MidlineLeft;
                levelBadgeText.enableWordWrapping = false;
                levelBadgeText.raycastTarget = false;

                // Sit just to the right of the title's rect.
                var titleRt = titleText.rectTransform;
                var rt = levelBadgeText.rectTransform;
                rt.anchorMin = titleRt.anchorMin;
                rt.anchorMax = titleRt.anchorMax;
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(120f, titleRt.sizeDelta.y);
                rt.anchoredPosition = titleRt.anchoredPosition + new Vector2(titleRt.sizeDelta.x + 12f, 0f);
            }

            levelBadgeText.gameObject.SetActive(show);
            if (show)
            {
                levelBadgeText.text = $"★{cardLevel}"; // "★N"
            }
        }

        private void OnDestroy()
        {
            CardStatBadges.Release(cardArtRect);
        }

        private void SetVisible(bool visible)
        {
            IsVisible = visible;

            if (panelRoot != null)
            {
                panelRoot.SetActive(visible);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Right-side per-skill blocks
        // ─────────────────────────────────────────────────────────────────────────────

        // Builds a scrollable right-hand column (ScrollRect + vertical content) that will hold one
        // block per ability. Created once in Awake; populated per-card in BuildSkillBlocks.
        private void BuildSkillsColumn()
        {
            var parent = (panelRoot != null ? panelRoot.transform : transform) as RectTransform;
            if (parent == null)
            {
                return;
            }

            // The scroll view spans the right half of the overlay, leaving the left half for the card.
            var scrollGo = new GameObject("SkillsScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            var scrollRect = scrollGo.GetComponent<RectTransform>();
            scrollRect.SetParent(parent, false);
            scrollRect.anchorMin = new Vector2(0.5f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.pivot = new Vector2(0.5f, 0.5f);
            // Left edge starts just right of the card column; right/top/bottom keep a margin.
            scrollRect.offsetMin = new Vector2(ColumnGap, 80f);
            scrollRect.offsetMax = new Vector2(-SkillsRightMargin, -80f);
            scrollGo.transform.SetAsLastSibling();

            var scrollBg = scrollGo.GetComponent<Image>();
            scrollBg.color = new Color(0f, 0f, 0f, 0.28f); // subtle panel so blocks read on busy art
            scrollBg.raycastTarget = true;

            // Viewport (mask) so blocks clip when scrolled.
            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            var viewport = viewportGo.GetComponent<RectTransform>();
            viewport.SetParent(scrollRect, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            var viewportImg = viewportGo.GetComponent<Image>();
            viewportImg.color = new Color(1f, 1f, 1f, 0f); // mask needs a graphic; keep invisible
            viewportImg.raycastTarget = false;

            // Content: top-anchored vertical layout that grows downward as blocks are added.
            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            skillsContent = contentGo.GetComponent<RectTransform>();
            skillsContent.SetParent(viewport, false);
            skillsContent.anchorMin = new Vector2(0f, 1f);
            skillsContent.anchorMax = new Vector2(1f, 1f);
            skillsContent.pivot = new Vector2(0.5f, 1f);
            skillsContent.offsetMin = new Vector2(0f, skillsContent.offsetMin.y);
            skillsContent.offsetMax = new Vector2(0f, skillsContent.offsetMax.y);

            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.spacing = SkillBlockSpacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = skillsContent;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            // Empty-state label (shown when the card has no abilities). Lives in the content area.
            var emptyGo = new GameObject("SkillsEmpty", typeof(RectTransform));
            var emptyRect = emptyGo.GetComponent<RectTransform>();
            emptyRect.SetParent(skillsContent, false);
            skillsEmptyText = emptyGo.AddComponent<TextMeshProUGUI>();
            ApplyThemeFont(skillsEmptyText);
            skillsEmptyText.text = "No skills.";
            skillsEmptyText.fontSize = 30f;
            skillsEmptyText.color = new Color(1f, 1f, 1f, 0.75f);
            skillsEmptyText.alignment = TextAlignmentOptions.TopLeft;
            skillsEmptyText.enableWordWrapping = true;
            var emptyLe = emptyGo.AddComponent<LayoutElement>();
            emptyLe.minHeight = 40f;

            skillsScrollRoot = scrollGo;
        }

        // Clears the previous blocks and rebuilds one block per ability for the given card.
        private void BuildSkillBlocks(BoardCardDto card)
        {
            if (skillsContent == null)
            {
                return;
            }

            // Remove previously built blocks (keep the empty-state label, which we toggle).
            for (var i = skillsContent.childCount - 1; i >= 0; i--)
            {
                var child = skillsContent.GetChild(i);
                if (skillsEmptyText != null && child == skillsEmptyText.transform)
                {
                    continue;
                }
                Destroy(child.gameObject);
            }

            var abilities = ResolveAbilities(card);
            var built = 0;
            if (abilities != null)
            {
                foreach (var ability in abilities)
                {
                    if (ability == null || string.IsNullOrWhiteSpace(ability.abilityId))
                    {
                        continue;
                    }
                    CreateSkillBlock(ability);
                    built++;
                }
            }

            if (skillsEmptyText != null)
            {
                skillsEmptyText.gameObject.SetActive(built == 0);
                // Keep the empty label first so it shows at the top when present.
                skillsEmptyText.transform.SetAsFirstSibling();
            }
        }

        // One block = a dark rounded-ish panel containing [icon] + name (bold) + wrapped description.
        private void CreateSkillBlock(CardAbilityDto ability)
        {
            var blockGo = new GameObject("SkillBlock", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            var blockRect = blockGo.GetComponent<RectTransform>();
            blockRect.SetParent(skillsContent, false);

            var blockBg = blockGo.GetComponent<Image>();
            blockBg.color = new Color(0f, 0f, 0f, 0.45f);
            blockBg.raycastTarget = false;

            var blockLayout = blockGo.GetComponent<HorizontalLayoutGroup>();
            blockLayout.padding = new RectOffset(SkillBlockPaddingInt, SkillBlockPaddingInt, SkillBlockPaddingInt, SkillBlockPaddingInt);
            blockLayout.spacing = 12f;
            blockLayout.childAlignment = TextAnchor.UpperLeft;
            blockLayout.childControlWidth = true;
            blockLayout.childControlHeight = true;
            blockLayout.childForceExpandWidth = false;
            blockLayout.childForceExpandHeight = false;

            // Icon (optional — only when the pack actually has skill_{abilityId}).
            var icon = CardArtLibrary.GetSkillIcon(ability.abilityId);
            if (icon != null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                iconGo.transform.SetParent(blockRect, false);
                var iconImg = iconGo.GetComponent<Image>();
                iconImg.sprite = icon;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                var iconLe = iconGo.GetComponent<LayoutElement>();
                iconLe.preferredWidth = SkillIconSize;
                iconLe.preferredHeight = SkillIconSize;
                iconLe.minWidth = SkillIconSize;
                iconLe.minHeight = SkillIconSize;
            }

            // Text column: name (bold) on top, description (wrapped) below.
            var textColGo = new GameObject("Text", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            textColGo.transform.SetParent(blockRect, false);
            var textLayout = textColGo.GetComponent<VerticalLayoutGroup>();
            textLayout.padding = new RectOffset(0, 0, 0, 0);
            textLayout.spacing = 4f;
            textLayout.childAlignment = TextAnchor.UpperLeft;
            textLayout.childControlWidth = true;
            textLayout.childControlHeight = true;
            textLayout.childForceExpandWidth = true;
            textLayout.childForceExpandHeight = false;
            var textColLe = textColGo.GetComponent<LayoutElement>();
            textColLe.flexibleWidth = 1f; // take the remaining width next to the icon

            var nameGo = new GameObject("Name", typeof(RectTransform));
            nameGo.transform.SetParent(textColGo.transform, false);
            var nameText = nameGo.AddComponent<TextMeshProUGUI>();
            ApplyThemeFont(nameText);
            nameText.text = ResolveAbilityName(ability);
            nameText.fontSize = 32f;
            nameText.fontStyle = FontStyles.Bold;
            nameText.color = Color.white;
            nameText.alignment = TextAlignmentOptions.TopLeft;
            nameText.enableWordWrapping = true;

            var descGo = new GameObject("Description", typeof(RectTransform));
            descGo.transform.SetParent(textColGo.transform, false);
            var descText = descGo.AddComponent<TextMeshProUGUI>();
            ApplyThemeFont(descText);
            descText.text = ResolveAbilityDescription(ability);
            descText.fontSize = 26f;
            descText.color = new Color(1f, 1f, 1f, 0.88f);
            descText.alignment = TextAlignmentOptions.TopLeft;
            descText.enableWordWrapping = true;
        }

        private static int SkillBlockPaddingInt => Mathf.RoundToInt(SkillBlockPadding);

        // ─────────────────────────────────────────────────────────────────────────────
        // Ability resolution + human-readable descriptions
        // ─────────────────────────────────────────────────────────────────────────────

        // Same resolution order CardVisualCommon uses: prefer the snapshot's per-card abilities,
        // else look the card up in the catalog and use its serverDefinition.abilities.
        private static CardAbilityDto[] ResolveAbilities(BoardCardDto card)
        {
            if (card == null)
            {
                return System.Array.Empty<CardAbilityDto>();
            }

            if (card.abilities != null && card.abilities.Length > 0)
            {
                return card.abilities;
            }

            if (!string.IsNullOrWhiteSpace(card.cardId) && GameService.Instance?.CardCatalog != null)
            {
                if (GameService.Instance.CardCatalog.TryGetCard(card.cardId, out var serverDefinition) &&
                    serverDefinition?.abilities != null)
                {
                    return serverDefinition.abilities;
                }
            }

            return System.Array.Empty<CardAbilityDto>();
        }

        private static string ResolveAbilityName(CardAbilityDto ability)
        {
            if (ability == null)
            {
                return "Skill";
            }

            if (!string.IsNullOrWhiteSpace(ability.displayName))
            {
                return ability.displayName;
            }

            return Prettify(ability.abilityId);
        }

        // The wire DTO (CardAbilityDto) has no free-text description field, so we derive a readable line
        // from the ability's effects (effectKind + amount + duration). If there are no effects, fall
        // back to a humanized ability id.
        private static string ResolveAbilityDescription(CardAbilityDto ability)
        {
            if (ability == null)
            {
                return string.Empty;
            }

            if (ability.effects != null && ability.effects.Length > 0)
            {
                var parts = new List<string>();
                foreach (var effect in ability.effects)
                {
                    if (effect == null)
                    {
                        continue;
                    }
                    var line = DescribeEffect(effect);
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        parts.Add(line);
                    }
                }

                if (parts.Count > 0)
                {
                    var sb = new StringBuilder();
                    for (var i = 0; i < parts.Count; i++)
                    {
                        if (i > 0)
                        {
                            sb.Append("  ");
                        }
                        sb.Append(parts[i]);
                        if (!parts[i].EndsWith("."))
                        {
                            sb.Append('.');
                        }
                    }
                    return sb.ToString();
                }
            }

            // No usable effects — a humanized id is still more useful than blank.
            return Prettify(ability.abilityId);
        }

        // Turns one CardEffectDto into a short readable sentence using the shared EffectKind enum
        // (mirror of the server). Amount/duration are woven in when meaningful.
        private static string DescribeEffect(CardEffectDto effect)
        {
            var kind = (EffectKind)effect.effectKind;
            var amount = effect.amount;
            var duration = effect.durationTurns;
            string durationSuffix = duration > 0 ? $" for {duration} turn{(duration == 1 ? string.Empty : "s")}" : string.Empty;

            switch (kind)
            {
                case EffectKind.Damage: return $"Deal {amount} damage";
                case EffectKind.Heal: return $"Restore {amount} health";
                case EffectKind.GainArmor:
                case EffectKind.Armor: return $"Gain {amount} armor";
                case EffectKind.BuffAttack: return $"Increase attack by {amount}{durationSuffix}";
                case EffectKind.HitHero: return $"Deal {amount} damage to the enemy hero";
                case EffectKind.Stun:
                case EffectKind.ApplyStun: return $"Stun the target{durationSuffix}";
                case EffectKind.Poison:
                case EffectKind.ApplyPoison: return $"Poison the target for {amount}{durationSuffix}";
                case EffectKind.Leech: return $"Leech {amount} health from the target";
                case EffectKind.Evasion: return "Has a chance to evade attacks";
                case EffectKind.Shield:
                case EffectKind.AddShield: return $"Add a {amount}-point shield";
                case EffectKind.Reflection: return "Reflects part of incoming damage";
                case EffectKind.Dodge: return "Can dodge the next attack";
                case EffectKind.Enrage: return $"Enrages, gaining {amount} attack when damaged";
                case EffectKind.ManaBurn: return $"Burns {amount} of the enemy's mana";
                case EffectKind.Regenerate:
                case EffectKind.ApplyRegeneration: return $"Regenerate {amount} health each turn{durationSuffix}";
                case EffectKind.Execute: return "Instantly destroys low-health targets";
                case EffectKind.DiagonalAttack: return "Can attack diagonally";
                case EffectKind.Fly: return "Flying: ignores ground blockers";
                case EffectKind.Chain: return "Attacks chain to nearby targets";
                case EffectKind.Charge: return "Charge: can attack the turn it is played";
                case EffectKind.Cleave: return "Cleaves adjacent enemies";
                case EffectKind.LastStand: return "Survives the killing blow once";
                case EffectKind.MeleeRange: return "Attacks at melee range";
                case EffectKind.Ricochet: return "Attacks ricochet to extra targets";
                case EffectKind.Taunt: return "Taunt: forces enemies to attack this unit";
                case EffectKind.Trample: return "Excess damage tramples through";
                case EffectKind.Haste: return "Haste: ready to act immediately";
                case EffectKind.ApplyBurn: return $"Burn the target for {amount}{durationSuffix}";
                case EffectKind.ApplyParalyze: return $"Paralyze the target{durationSuffix}";
                case EffectKind.ApplyConfuse: return $"Confuse the target{durationSuffix}";
                case EffectKind.ApplySilence: return $"Silence the target{durationSuffix}";
                case EffectKind.ApplyVulnerable: return $"Make the target vulnerable{durationSuffix}";
                case EffectKind.ApplyWeaken: return $"Weaken the target{durationSuffix}";
                case EffectKind.ApplyWard: return "Ward: blocks the next debuff";
                case EffectKind.Cleanse: return "Cleanse all debuffs from the target";
                case EffectKind.Dispel: return "Dispel all buffs from the target";
                default:
                    return amount != 0 ? $"{Prettify(kind.ToString())} ({amount})" : Prettify(kind.ToString());
            }
        }

        // "fire_burst" / "FireBurst" -> "Fire Burst".
        private static string Prettify(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "Skill";
            }

            var sb = new StringBuilder(raw.Length + 4);
            var prevLower = false;
            for (var i = 0; i < raw.Length; i++)
            {
                var c = raw[i];
                if (c == '_' || c == '-')
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != ' ')
                    {
                        sb.Append(' ');
                    }
                    prevLower = false;
                    continue;
                }

                if (char.IsUpper(c) && prevLower)
                {
                    sb.Append(' ');
                }

                sb.Append(c);
                prevLower = char.IsLower(c) || char.IsDigit(c);
            }

            var result = sb.ToString().Trim();
            if (result.Length == 0)
            {
                return "Skill";
            }

            // Capitalize the first letter of each word for a tidy title.
            var words = result.Split(' ');
            for (var i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
                }
            }
            return string.Join(" ", words);
        }

        private static void ApplyThemeFont(TextMeshProUGUI text)
        {
            if (text == null)
            {
                return;
            }

            // Match the deck-builder/battle chrome when a theme font is available; otherwise TMP keeps
            // its clean default (LiberationSans). ThemeFont is null-safe by design.
            var font = KenneyUiSkin.ThemeFont;
            if (font != null)
            {
                text.font = font;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Card art (now left-aligned + smaller)
        // ─────────────────────────────────────────────────────────────────────────────

        // Builds a dedicated left-aligned 2:3 card-art Image under the panel (behind the stat/text
        // children, above the dim backdrop) so the composite shows as a card, not a stretched wash.
        private Image CreateDetailArtImage()
        {
            var parent = (panelRoot != null ? panelRoot.transform : transform) as RectTransform;
            var go = new GameObject("CardArt", typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.SetAsLastSibling(); // render above the dim backdrop and the semi-transparent icon panels

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.color = Color.white;
            return image;
        }

        // Pins the card art to the LEFT, vertically centred, at the smaller preview size.
        private void LayoutCardArtLeft(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(CardWidth, CardHeight); // smaller 2:3 card
            rect.anchoredPosition = new Vector2(CardLeftMargin, 0f);
        }

        // Finds the panelRoot's own (typically full-screen) background Image and lowers its alpha so
        // it stops washing the whole screen grey. Kept as a raycast target so click-to-dismiss still
        // works; the CanvasGroup.blocksRaycasts in SetVisible also keeps the overlay interactive.
        private void DimBackdropImage()
        {
            var root = panelRoot != null ? panelRoot : gameObject;
            var backdrop = root.GetComponent<Image>();
            if (backdrop == null)
            {
                return;
            }

            var color = backdrop.color;
            color.a = Mathf.Min(color.a, 0.12f); // near-transparent dim, never a solid grey wash
            backdrop.color = color;
            backdrop.raycastTarget = true; // preserve click-to-dismiss
        }

        private Image FindPreferredDetailImage()
        {
            var searchRoot = panelRoot != null ? panelRoot.transform : transform;
            var images = searchRoot.GetComponentsInChildren<Image>(true);
            var panelImage = panelRoot != null ? panelRoot.GetComponent<Image>() : null;
            Image fallback = null;

            // Prefer an explicit art/visual image; never bind the panel's own (usually full-screen)
            // background image, which would stretch the card art across the whole overlay.
            foreach (var image in images)
            {
                if (image == null || image == panelImage)
                {
                    continue;
                }

                var objectName = image.gameObject.name.ToLowerInvariant();
                if (objectName.Contains("art") || objectName.Contains("visual"))
                {
                    return image;
                }
            }

            foreach (var image in images)
            {
                if (image == null || image == panelImage)
                {
                    continue;
                }

                if (image.gameObject.name.ToLowerInvariant().Contains("card"))
                {
                    return image;
                }

                fallback ??= image;
            }

            return fallback;
        }

        private Image FindPreferredAttackTypeImage()
        {
            var searchRoot = panelRoot != null ? panelRoot.transform : transform;
            foreach (var image in searchRoot.GetComponentsInChildren<Image>(true))
            {
                if (image == null)
                {
                    continue;
                }

                var objectName = image.gameObject.name.ToLowerInvariant();
                if (objectName.Contains("attacktype") || objectName.Contains("delivery") || objectName.Contains("combat"))
                {
                    return image;
                }
            }

            return null;
        }
    }
}
