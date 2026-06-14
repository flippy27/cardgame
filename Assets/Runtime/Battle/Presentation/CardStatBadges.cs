using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Builds and refreshes the card stat BADGES as overlay-UI GameObjects under a single overlay
    /// RectTransform. This REPLACES the old dual system (icons baked into the card texture by
    /// <see cref="CardArtLibrary"/> + separate TMP numbers positioned by each view's PlaceStat).
    ///
    /// Each badge is a parent RectTransform positioned at a stat SOCKET (the same top-left-origin
    /// fractions exposed by <see cref="CardArtLibrary.HandSocket"/>/<see cref="CardArtLibrary.BoardSocket"/>),
    /// holding centred children:
    ///   1. a circle Image  (<see cref="CardArtLibrary.GetSocketBadgeSprite"/>) — the socket disc+ring
    ///   2. an icon Image    (the stat symbol, or the rarity hexagon for the rarity badge)
    ///   3. a TMP number      (white/bold/black-outline) — ON TOP of the circle
    /// LAYOUT (mockup): disc badges (cost/attack/armor/effects) keep the disc + a big CENTRED number,
    /// and the small stat icon pokes out to the card's OUTER corner (see CornerIcon* constants) so it
    /// reads as a little themed badge on the disc edge. HEALTH is special: NO disc — the HEART sprite
    /// itself is the badge background (sized up) with the number centred on it (see heartBackground).
    /// Because the number is a CHILD of the badge root, it can NEVER drift: bg, icon and number are
    /// coplanar and move together. No baked icons, no ZTest hacks — occlusion is solved purely by the
    /// overlay sitting slightly in front of the quad.
    ///
    /// Badges shown: cost (hand only), attack (units only), health (units only), armor (only if
    /// armor &gt; 0), rarity (hexagon+gem, NO number). Call <see cref="Apply"/> on init AND on every
    /// refresh: the first call creates the badge GameObjects, later calls reuse them (no leaks) and
    /// just update numbers/visibility.
    ///
    /// GOTCHA (board): board cards are parented into slots with a large non-uniform scale (lossy z ~7)
    /// and a rotation, so any non-zero local Z gets flung far. Every element here forces local z = 0.
    /// </summary>
    public static class CardStatBadges
    {
        // === TUNABLE SIZES (fractions of the overlay's WIDTH; tweak by screenshot) ===
        // Mirror the old baked fractions: circle ~ SocketBadgeFraction(0.23), icon ~ SocketIconFraction(0.16).
        // The number fills the circle. Rarity uses a slightly larger hexagon and no number.
        private const float CircleFraction = 0.19f;   // socket disc diameter / overlay width
        private const float IconFraction = 0.13f;   // stat symbol size / overlay width
        private const float RarityFraction = 0.15f;   // rarity hexagon size / overlay width
        private const float NumberFraction = 0.16f;   // number box size / overlay width (digit fills circle)
        private const float NumberFontFraction = 0.13f;   // TMP font size / overlay width

        // === CORNER-ICON tuning ===
        // The small stat icon no longer hides behind the number — it pokes out toward the card's OUTER
        // corner so the disc reads as "big centred number + little themed badge on the corner".
        // CornerIconFraction = the poked-out icon size (smaller than the centred icon used before).
        // CornerIconOffsetFraction = how far the icon centre shifts from the disc centre, as a fraction
        // of the overlay WIDTH, along each axis (the per-badge corner direction picks the signs).
        private const float CornerIconFraction = 0.11f;   // corner stat-icon size / overlay width
        private const float CornerIconOffsetFraction = 0.085f; // icon corner shift / overlay width (per axis)

        // === HEALTH HEART tuning (no circle) ===
        // Health uses the heart sprite itself as the badge background (no disc), sized UP a bit so the
        // number reads centred on the heart. HealthHeartFraction = heart diameter / overlay width.
        private const float HealthHeartFraction = 0.24f;   // heart bg size / overlay width
        // The heart's visual mass sits slightly low (the dip is at top); nudge the number UP a hair so
        // it sits on the body of the heart, not over the cleft. Fraction of the heart size.
        private const float HealthNumberYNudge = 0.06f;   // number up-shift inside heart (frac of heart)

        // Outline tuning — matches the old per-instance number styling (white, bold, black outline).
        private const float OutlineWidth = 0.22f;

        private const string RootName = "__CardStatBadges";

        // One reusable set of badge widgets per overlay RectTransform. Keyed by instance id so two
        // overlays (e.g. a hand card and the preview) never share widgets.
        private static readonly Dictionary<int, BadgeSet> _sets = new();

        /// <summary>
        /// Creates (once) and refreshes the stat badges for <paramref name="card"/> under
        /// <paramref name="overlay"/>. <paramref name="isBoard"/> selects board sockets (no cost) vs
        /// hand sockets (with cost). Safe to call every frame/refresh — widgets are reused.
        /// </summary>
        public static void Apply(RectTransform overlay, BoardCardDto card, bool isBoard)
        {
            if (overlay == null || card == null)
            {
                return;
            }

            var key = overlay.GetInstanceID();
            if (!_sets.TryGetValue(key, out var set) || set.Root == null)
            {
                set = BuildSet(overlay);
                _sets[key] = set;
            }

            var meta = ResolveCardMeta(card);
            var width = Mathf.Max(1f, overlay.rect.width);

            // Prefer the catalog cardType (Unit=0) when the lookup succeeded; otherwise fall back to a
            // stat-based heuristic (anything with health/attack reads as a unit).
            var isUnit = meta.resolved ? meta.cardType == 0 : (card.maxHealth > 0 || card.attack != 0);
            var hasArmor = card.armor > 0;

            // Cost — hand only (board tokens show no mana cost). Shown for units AND non-units.
            // Mana sits TOP-LEFT, so its icon pokes to the upper-left corner of the disc.
            var manaCost = CardVisualCommon.ResolveManaCost(card);
            UpdateBadge(set.Cost, !isBoard && manaCost >= 0,
                Socket(isBoard, CardStatSocket.Cost), overlay, width,
                CardArtLibrary.GetStatIconSprite("stat_mana"), manaCost.ToString(),
                IconCorner.UpperLeft);

            // Attack — units only. Icon depends on unit type (sword/bow/magic). Bottom-left socket, so
            // the sword/bow/magic icon pokes to the lower-left corner of the disc.
            UpdateBadge(set.Attack, isUnit,
                Socket(isBoard, CardStatSocket.Attack), overlay, width,
                CardArtLibrary.GetStatIconSprite(CardArtLibrary.AttackIconName(card.unitType)),
                card.attack.ToString(),
                IconCorner.LowerLeft);

            // Health — units only. Heart-as-background (NO circle): the heart sprite IS the badge bg,
            // sized up, with the number centred on it. Show current health.
            UpdateBadge(set.Health, isUnit,
                Socket(isBoard, CardStatSocket.Health), overlay, width,
                CardArtLibrary.GetStatIconSprite("stat_health"),
                card.currentHealth.ToString(),
                IconCorner.None, heartBackground: true);

            // Armor — only when > 0 (units). Sits above health on the OUTER (right) side, so its icon
            // pokes to the upper-right corner of the disc.
            UpdateBadge(set.Armor, isUnit && hasArmor,
                Socket(isBoard, CardStatSocket.Armor), overlay, width,
                CardArtLibrary.GetStatIconSprite("stat_armor"),
                card.armor.ToString(),
                IconCorner.UpperRight);

            // NON-UNIT effect summary (Utility/Equipment/Spell). Units carry attack/health badges;
            // non-units have attack/health = 0, so instead we derive a quick-glance summary from the
            // card's OnPlay ability/effects: a +/- magnitude number per column and a status icon for
            // whatever status the card applies/removes. LEFT = offensive (damage/debuff), RIGHT =
            // defensive/grant (heal/shield/buff/regen/ward), mirroring the unit attack/health layout.
            BuildNonUnitBadges(set, !isUnit, card, overlay, width, isBoard);

            // Rarity — hexagon+gem, NO number. Uses its own (slightly larger) icon and no circle.
            UpdateRarityBadge(set.Rarity, Socket(isBoard, CardStatSocket.Rarity), overlay, width, meta.cardRarity);
        }

        // Builds the non-unit +/- number + status icon badges (or hides them all when the card is a
        // unit / has no resolvable effects). Reuses the same circle+icon+number primitives so it
        // matches the in-game art exactly.
        private static void BuildNonUnitBadges(BadgeSet set, bool isNonUnit, BoardCardDto card,
            RectTransform overlay, float width, bool isBoard)
        {
            EffectSummary summary = isNonUnit ? NonUnitEffectSummary.Resolve(card) : default;
            var has = isNonUnit && summary.HasAny;

            // LEFT column = offensive (damage / debuff). Number carries a "-" prefix.
            UpdateBadge(set.LeftNumber, has && summary.LeftHasNumber,
                Socket(isBoard, CardStatSocket.Attack), overlay, width,
                CardArtLibrary.GetStatIconSprite("attack_sword"), summary.LeftNumberText,
                IconCorner.LowerLeft);
            UpdateStatusBadge(set.LeftStatus, has && summary.LeftHasStatus,
                Socket(isBoard, CardStatSocket.EffectLeft), overlay, width, summary.LeftStatusKind);

            // RIGHT column = defensive / grant (heal / armor / shield / buff / regen / ward / cleanse).
            // Number carries a "+" prefix. Uses the disc style (NOT the heart bg) for effect numbers.
            UpdateBadge(set.RightNumber, has && summary.RightHasNumber,
                Socket(isBoard, CardStatSocket.Health), overlay, width,
                CardArtLibrary.GetStatIconSprite("stat_health"), summary.RightNumberText,
                IconCorner.LowerRight);
            UpdateStatusBadge(set.RightStatus, has && summary.RightHasStatus,
                Socket(isBoard, CardStatSocket.EffectRight), overlay, width, summary.RightStatusKind);
        }

        /// <summary>Removes the badge widgets for an overlay (e.g. when a view is destroyed).</summary>
        public static void Release(RectTransform overlay)
        {
            if (overlay == null)
            {
                return;
            }

            var key = overlay.GetInstanceID();
            if (_sets.TryGetValue(key, out var set))
            {
                if (set.Root != null)
                {
                    Object.Destroy(set.Root.gameObject);
                }
                _sets.Remove(key);
            }
        }

        private static Vector2 Socket(bool isBoard, CardStatSocket socket)
        {
            return isBoard ? CardArtLibrary.BoardSocket(socket) : CardArtLibrary.HandSocket(socket);
        }

        private static BadgeSet BuildSet(RectTransform overlay)
        {
            // A dedicated container fills the overlay so each badge's anchoredPosition is computed in
            // overlay space. Stretched to the overlay; z forced to 0 (board slot z-scale gotcha).
            var rootGo = new GameObject(RootName, typeof(RectTransform));
            var root = rootGo.GetComponent<RectTransform>();
            root.SetParent(overlay, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.localScale = Vector3.one;
            root.anchoredPosition3D = Vector3.zero;
            root.SetAsLastSibling(); // draw above the title/text strip

            return new BadgeSet
            {
                Root = root,
                Cost = CreateBadge(root, "CostBadge", withCircle: true, withNumber: true),
                Attack = CreateBadge(root, "AttackBadge", withCircle: true, withNumber: true),
                Health = CreateBadge(root, "HealthBadge", withCircle: true, withNumber: true),
                Armor = CreateBadge(root, "ArmorBadge", withCircle: true, withNumber: true),
                Rarity = CreateBadge(root, "RarityBadge", withCircle: false, withNumber: false),
                // Non-unit effect badges (circle+icon+number like the stat badges).
                LeftNumber = CreateBadge(root, "EffectLeftNumber", withCircle: true, withNumber: true),
                RightNumber = CreateBadge(root, "EffectRightNumber", withCircle: true, withNumber: true),
                LeftStatus = CreateBadge(root, "EffectLeftStatus", withCircle: true, withNumber: true),
                RightStatus = CreateBadge(root, "EffectRightStatus", withCircle: true, withNumber: true),
            };
        }

        // Builds one badge: a parent RectTransform with a centred circle Image, a centred icon Image,
        // and (optionally) a centred TMP number on top. All children are anchored+pivoted centre with
        // anchoredPosition3D zero so the number is locked to the circle centre forever.
        private static Badge CreateBadge(RectTransform parent, string name, bool withCircle, bool withNumber)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;

            var badge = new Badge { Root = rect };

            if (withCircle)
            {
                badge.Circle = CreateChildImage(rect, "Circle", CardArtLibrary.GetSocketBadgeSprite());
            }

            badge.Icon = CreateChildImage(rect, "Icon", null);

            if (withNumber)
            {
                badge.Number = CreateChildNumber(rect, "Number");
            }

            go.SetActive(false);
            return badge;
        }

        private static Image CreateChildImage(RectTransform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.anchoredPosition3D = Vector3.zero;

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.sprite = sprite;
            image.enabled = sprite != null;
            return image;
        }

        private static TextMeshProUGUI CreateChildNumber(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.anchoredPosition3D = Vector3.zero; // dead-centre on the circle, never drifts
            rect.SetAsLastSibling();                 // digit draws on top of the circle + icon

            var text = go.GetComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.enableAutoSizing = false;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.color = Color.white;
            text.fontStyle = FontStyles.Bold;

            // Per-instance outline so numbers read over any art (matches the old PlaceStat styling).
            // fontMaterial returns a per-instance copy, so this doesn't leak to the shared font material.
            var mat = text.fontMaterial;
            mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineWidth);
            return text;
        }

        // Which OUTER corner of the disc the small stat icon pokes toward. None = icon centred (or, for
        // the health heart, the icon IS the background and the number sits centred on it).
        private enum IconCorner { None, UpperLeft, UpperRight, LowerLeft, LowerRight }

        // Unit (overlay-space, y-up) corner direction for an IconCorner. Multiply by the offset fraction.
        private static Vector2 CornerDir(IconCorner corner) => corner switch
        {
            IconCorner.UpperLeft => new Vector2(-1f, 1f),
            IconCorner.UpperRight => new Vector2(1f, 1f),
            IconCorner.LowerLeft => new Vector2(-1f, -1f),
            IconCorner.LowerRight => new Vector2(1f, -1f),
            _ => Vector2.zero
        };

        // Positions+sizes a stat badge at its socket and updates its number/visibility.
        //   corner          — which outer corner the small stat icon pokes toward (None = centred icon).
        //   heartBackground — health-only: drop the disc and use the (heart) iconSprite as the badge bg,
        //                     sized up, with the number centred on it (icon offset is ignored).
        private static void UpdateBadge(Badge badge, bool visible, Vector2 socketTopLeft,
            RectTransform overlay, float width, Sprite iconSprite, string number,
            IconCorner corner = IconCorner.None, bool heartBackground = false)
        {
            if (badge?.Root == null)
            {
                return;
            }

            badge.Root.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            PlaceBadgeRoot(badge.Root, socketTopLeft, overlay);

            var num = width * NumberFraction;
            var numberOffset = Vector3.zero;

            if (heartBackground)
            {
                // HEALTH: no circle. The heart sprite is the background; the number sits centred on it
                // (nudged up a hair so it lands on the heart's body, not the cleft).
                if (badge.Circle != null)
                {
                    badge.Circle.enabled = false; // hide the disc for health
                }
                var heart = width * HealthHeartFraction;
                SizeChild(badge.Icon, heart, iconSprite);
                numberOffset = new Vector3(0f, heart * HealthNumberYNudge, 0f);
            }
            else
            {
                // DISC badges (cost / attack / armor / effect numbers): keep the disc + centred number,
                // and poke the small stat icon out toward the card's outer corner.
                var circle = width * CircleFraction;
                if (badge.Circle != null)
                {
                    badge.Circle.enabled = true;
                }
                SizeChild(badge.Circle, circle);

                var iconSize = corner == IconCorner.None ? width * IconFraction : width * CornerIconFraction;
                SizeChild(badge.Icon, iconSize, iconSprite);

                if (badge.Icon != null)
                {
                    var dir = CornerDir(corner) * (width * CornerIconOffsetFraction);
                    badge.Icon.rectTransform.anchoredPosition3D = new Vector3(dir.x, dir.y, 0f);
                }
            }

            if (badge.Number != null)
            {
                badge.Number.rectTransform.sizeDelta = new Vector2(num, num);
                badge.Number.rectTransform.anchoredPosition3D = numberOffset;
                badge.Number.fontSize = width * NumberFontFraction;
                badge.Number.text = number ?? string.Empty;
                if (!badge.Number.gameObject.activeSelf)
                {
                    badge.Number.gameObject.SetActive(true);
                }
            }
        }

        // Status circle badge for non-unit cards: a tinted socket circle keyed to the StatusEffectKind
        // the card touches, with a real status sprite on top when one exists, otherwise a short letter
        // placeholder (P/B/W/...) rendered in the number TMP. This is the FIRST PASS — when real status
        // sprites land in Resources/Art/icons/status/status_{key} they show automatically (see
        // CardArtLibrary.GetStatusIcon/StatusIconKey) and the letter is suppressed.
        private static void UpdateStatusBadge(Badge badge, bool visible, Vector2 socketTopLeft,
            RectTransform overlay, float width, int statusKind)
        {
            if (badge?.Root == null)
            {
                return;
            }

            badge.Root.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            PlaceBadgeRoot(badge.Root, socketTopLeft, overlay);

            var circleSize = width * CircleFraction;
            var iconSize = width * IconFraction;

            // Tint the socket circle by status so the badge reads at a glance even with no sprite.
            SizeChild(badge.Circle, circleSize);
            if (badge.Circle != null)
            {
                badge.Circle.color = CardArtLibrary.StatusBadgeTint(statusKind);
            }

            var statusSprite = CardArtLibrary.GetStatusIcon(statusKind);
            SizeChild(badge.Icon, iconSize, statusSprite);

            // Letter placeholder only when there is no real status sprite.
            if (badge.Number != null)
            {
                var showLetter = statusSprite == null;
                var num = width * NumberFraction;
                badge.Number.rectTransform.sizeDelta = new Vector2(num, num);
                badge.Number.rectTransform.anchoredPosition3D = Vector3.zero;
                badge.Number.fontSize = width * NumberFontFraction * 0.8f; // labels can be 2 chars
                badge.Number.text = showLetter ? CardArtLibrary.StatusBadgeLetter(statusKind) : string.Empty;
                if (badge.Number.gameObject.activeSelf != showLetter)
                {
                    badge.Number.gameObject.SetActive(showLetter);
                }
            }
        }

        // Rarity badge: hexagon+gem sprite, no circle, no number.
        private static void UpdateRarityBadge(Badge badge, Vector2 socketTopLeft,
            RectTransform overlay, float width, int cardRarity)
        {
            if (badge?.Root == null)
            {
                return;
            }

            badge.Root.gameObject.SetActive(true);
            PlaceBadgeRoot(badge.Root, socketTopLeft, overlay);
            SizeChild(badge.Icon, width * RarityFraction, CardArtLibrary.GetRarityBadgeSprite(cardRarity));
        }

        // Places the badge parent at the socket fraction (top-left origin) inside the centre-anchored
        // root. anchoredPosition is measured from the overlay centre: x = (fx-0.5)*W, y = (0.5-fy)*H.
        // Forces local z = 0 (board slot z-scale gotcha).
        private static void PlaceBadgeRoot(RectTransform root, Vector2 socketTopLeft, RectTransform overlay)
        {
            var w = overlay.rect.width;
            var h = overlay.rect.height;
            var x = (socketTopLeft.x - 0.5f) * w;
            var y = (0.5f - socketTopLeft.y) * h; // top-left origin -> centre-origin (y up)
            root.anchoredPosition3D = new Vector3(x, y, 0f);
        }

        private static void SizeChild(Image image, float size, Sprite sprite = null)
        {
            if (image == null)
            {
                return;
            }
            image.rectTransform.sizeDelta = new Vector2(size, size);
            image.rectTransform.anchoredPosition3D = Vector3.zero;
            if (sprite != null)
            {
                image.sprite = sprite;
            }
            image.enabled = image.sprite != null;
        }

        // Resolves cardType / cardRarity from the catalog (same approach as
        // CardSurfaceVisualRenderer.ResolveCardMeta) so the badges match the composited frame.
        // resolved=false means the lookup failed (catalog not ready / unknown card).
        private static (int cardType, int cardRarity, bool resolved) ResolveCardMeta(BoardCardDto card)
        {
            try
            {
                var catalog = GameService.Instance?.CardCatalog;
                if (catalog != null && !string.IsNullOrWhiteSpace(card.cardId) &&
                    catalog.TryGetCard(card.cardId, out ServerCardDefinition definition) && definition != null)
                {
                    return (definition.cardType, definition.cardRarity, true);
                }
            }
            catch (System.Exception)
            {
                // Catalog not ready — fall back to defaults below.
            }

            return (0, 0, false);
        }

        private sealed class BadgeSet
        {
            public RectTransform Root;
            public Badge Cost;
            public Badge Attack;
            public Badge Health;
            public Badge Armor;
            public Badge Rarity;
            // Non-unit effect summary badges.
            public Badge LeftNumber;
            public Badge RightNumber;
            public Badge LeftStatus;
            public Badge RightStatus;
        }

        private sealed class Badge
        {
            public RectTransform Root;
            public Image Circle;
            public Image Icon;
            public TextMeshProUGUI Number;
        }
    }

    /// <summary>
    /// What a non-unit card does, distilled to two columns of badges. LEFT = offensive (a "-N" number
    /// and/or a debuff status icon); RIGHT = defensive/grant (a "+N" number and/or a buff status icon).
    /// </summary>
    internal struct EffectSummary
    {
        public bool LeftHasNumber;
        public string LeftNumberText;
        public bool LeftHasStatus;
        public int LeftStatusKind;

        public bool RightHasNumber;
        public string RightNumberText;
        public bool RightHasStatus;
        public int RightStatusKind;

        public bool HasAny => LeftHasNumber || LeftHasStatus || RightHasNumber || RightHasStatus;
    }

    /// <summary>
    /// Derives an <see cref="EffectSummary"/> from a non-unit card's OnPlay ability/effect(s). Effects
    /// arrive as INTEGER <see cref="EffectKind"/> values (mirror of the server enum) on the card's
    /// abilities[].effects[] — taken from the live snapshot DTO if present, else resolved from the
    /// catalog by cardId (HandCardSnapshot/BoardCardDto may not carry abilities). Magnitude = the
    /// effect amount; sign + side are decided per kind.
    /// </summary>
    internal static class NonUnitEffectSummary
    {
        public static EffectSummary Resolve(BoardCardDto card)
        {
            var summary = new EffectSummary();
            var abilities = ResolveAbilities(card);
            if (abilities == null)
            {
                return summary;
            }

            foreach (var ability in abilities)
            {
                if (ability?.effects == null)
                {
                    continue;
                }

                foreach (var effect in ability.effects)
                {
                    if (effect == null || effect.effectKind < 0)
                    {
                        continue;
                    }

                    Accumulate(ref summary, (EffectKind)effect.effectKind, effect.amount);
                }
            }

            return summary;
        }

        // Applies one effect to the summary. The FIRST effect on a side wins the slot (single-ability
        // OnPlay cards are the norm); a second effect of the same side is ignored to keep the badge
        // readable. Tune here if cards start carrying multiple same-side effects.
        private static void Accumulate(ref EffectSummary s, EffectKind kind, int amount)
        {
            var magnitude = Mathf.Abs(amount);

            switch (kind)
            {
                // ---- Pure RIGHT (grant / defensive) numbers ----
                case EffectKind.Heal:
                case EffectKind.GainArmor:
                case EffectKind.Armor:
                    SetRightNumber(ref s, $"+{magnitude}");
                    break;

                // BuffAttack: positive = buff (RIGHT +), negative = curse (LEFT -).
                case EffectKind.BuffAttack:
                    if (amount < 0)
                    {
                        SetLeftNumber(ref s, $"-{magnitude}");
                    }
                    else
                    {
                        SetRightNumber(ref s, $"+{magnitude}");
                    }
                    break;

                // ---- Pure LEFT (offensive) numbers ----
                case EffectKind.Damage:
                case EffectKind.HitHero:
                case EffectKind.Execute:
                case EffectKind.ManaBurn:
                    SetLeftNumber(ref s, $"-{magnitude}");
                    break;

                // ---- RIGHT grant + status icon ----
                case EffectKind.AddShield:
                case EffectKind.Shield:
                    if (magnitude > 0) SetRightNumber(ref s, $"+{magnitude}");
                    SetRightStatus(ref s, (int)StatusEffectKind.Shield);
                    break;
                case EffectKind.ApplyRegeneration:
                case EffectKind.Regenerate:
                    if (magnitude > 0) SetRightNumber(ref s, $"+{magnitude}");
                    SetRightStatus(ref s, (int)StatusEffectKind.Regeneration);
                    break;
                case EffectKind.ApplyWard:
                    SetRightStatus(ref s, (int)StatusEffectKind.Ward);
                    break;

                // ---- LEFT debuff/offensive status icons (with optional magnitude) ----
                case EffectKind.ApplyPoison:
                case EffectKind.Poison:
                    if (magnitude > 0) SetLeftNumber(ref s, $"-{magnitude}");
                    SetLeftStatus(ref s, (int)StatusEffectKind.Poison);
                    break;
                case EffectKind.ApplyBurn:
                    if (magnitude > 0) SetLeftNumber(ref s, $"-{magnitude}");
                    SetLeftStatus(ref s, (int)StatusEffectKind.Burn);
                    break;
                case EffectKind.ApplyStun:
                case EffectKind.Stun:
                    SetLeftStatus(ref s, (int)StatusEffectKind.Stun);
                    break;
                case EffectKind.ApplyParalyze:
                    SetLeftStatus(ref s, (int)StatusEffectKind.Paralyze);
                    break;
                case EffectKind.ApplyConfuse:
                    SetLeftStatus(ref s, (int)StatusEffectKind.Confuse);
                    break;
                case EffectKind.ApplySilence:
                    SetLeftStatus(ref s, (int)StatusEffectKind.Silence);
                    break;
                case EffectKind.ApplyVulnerable:
                    SetLeftStatus(ref s, (int)StatusEffectKind.Vulnerable);
                    break;
                case EffectKind.ApplyWeaken:
                    if (magnitude > 0) SetLeftNumber(ref s, $"-{magnitude}");
                    SetLeftStatus(ref s, (int)StatusEffectKind.Weaken);
                    break;

                // ---- Cleanse / Dispel: status-removal markers (no magnitude). Cleanse cures an ally
                // (RIGHT, show a Ward-like marker); Dispel purges an enemy (LEFT). ----
                case EffectKind.Cleanse:
                    SetRightStatus(ref s, (int)StatusEffectKind.Ward);
                    break;
                case EffectKind.Dispel:
                    SetLeftStatus(ref s, (int)StatusEffectKind.Silence);
                    break;

                default:
                    // Unknown / passive keyword effect with no quick-glance number: skip.
                    break;
            }
        }

        private static void SetLeftNumber(ref EffectSummary s, string text)
        {
            if (s.LeftHasNumber) return;
            s.LeftHasNumber = true;
            s.LeftNumberText = text;
        }

        private static void SetRightNumber(ref EffectSummary s, string text)
        {
            if (s.RightHasNumber) return;
            s.RightHasNumber = true;
            s.RightNumberText = text;
        }

        private static void SetLeftStatus(ref EffectSummary s, int statusKind)
        {
            if (s.LeftHasStatus) return;
            s.LeftHasStatus = true;
            s.LeftStatusKind = statusKind;
        }

        private static void SetRightStatus(ref EffectSummary s, int statusKind)
        {
            if (s.RightHasStatus) return;
            s.RightHasStatus = true;
            s.RightStatusKind = statusKind;
        }

        // Prefer abilities carried on the live DTO; otherwise resolve from the catalog by cardId
        // (the hand/board snapshot may omit abilities — same pattern as CardVisualCommon.ResolveAbilities).
        private static CardAbilityDto[] ResolveAbilities(BoardCardDto card)
        {
            if (card == null)
            {
                return null;
            }

            if (card.abilities != null && card.abilities.Length > 0)
            {
                return card.abilities;
            }

            try
            {
                var catalog = GameService.Instance?.CardCatalog;
                if (catalog != null && !string.IsNullOrWhiteSpace(card.cardId) &&
                    catalog.TryGetCard(card.cardId, out ServerCardDefinition definition) &&
                    definition?.abilities != null)
                {
                    return definition.abilities;
                }
            }
            catch (System.Exception)
            {
                // Catalog not ready — no summary.
            }

            return null;
        }
    }
}
