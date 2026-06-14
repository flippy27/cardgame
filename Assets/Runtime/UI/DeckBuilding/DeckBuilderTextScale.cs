using TMPro;
using UnityEngine;

namespace Flippy.CardDuelMobile.UI.DeckBuilding
{
    /// <summary>
    /// Single source of truth for deck-builder text legibility.
    ///
    /// The deck-builder screens historically set TMP text at tiny / inconsistent
    /// sizes (status and validation labels were ~14pt, well below a comfortable
    /// mobile reading size). This helper centralizes a consistent, readable type
    /// scale so future tweaks live in exactly one place.
    ///
    /// Usage:
    ///   DeckBuilderTextScale.Apply(statusText, DeckBuilderTextScale.Role.Status);
    ///
    /// For variable-length labels prefer the auto-sizing overload, which keeps
    /// text legible while letting long strings shrink to fit:
    ///   DeckBuilderTextScale.ApplyAutoSize(cardNameText, DeckBuilderTextScale.Role.CardName);
    /// </summary>
    public static class DeckBuilderTextScale
    {
        /// <summary>Semantic text roles mapped to point sizes.</summary>
        public enum Role
        {
            /// <summary>Screen / panel titles. Bold.</summary>
            Header,
            /// <summary>Section labels, dropdown labels, page labels.</summary>
            Label,
            /// <summary>Button captions.</summary>
            Button,
            /// <summary>Card names (collection cells, deck rows, catalog rows).</summary>
            CardName,
            /// <summary>Status / validation / feedback messages.</summary>
            Status
        }

        // ---- Type scale (point sizes) ----
        // Sized for a PHONE-portrait canvas. The scene's prominent labels are authored at
        // ~57-87pt, so the old 28-48pt scale rendered tiny by comparison and was illegible on
        // a handset. Bumped up substantially — this is the single knob for deck-builder type.
        public const float Header   = 64f;
        public const float Label    = 46f;
        public const float Button   = 46f;
        public const float CardName = 42f;
        public const float Status   = 40f;

        /// <summary>Absolute readability floor (pt). No deck-builder text should render below
        /// this on a phone — used by the theme font pass to catch text that bypasses the role API.</summary>
        public const float MinReadable = 38f;

        /// <summary>Returns the point size for a role.</summary>
        public static float SizeFor(Role role) => role switch
        {
            Role.Header   => Header,
            Role.Label    => Label,
            Role.Button   => Button,
            Role.CardName => CardName,
            Role.Status   => Status,
            _             => Label
        };

        /// <summary>
        /// Applies an explicit, fixed font size for the role (and bold for headers).
        /// Null-safe — does nothing if <paramref name="text"/> is null.
        /// </summary>
        public static void Apply(TMP_Text text, Role role)
        {
            if (text == null) return;

            text.enableAutoSizing = false;
            text.fontSize = SizeFor(role);

            if (role == Role.Header)
            {
                text.fontStyle |= FontStyles.Bold;
            }
        }

        /// <summary>
        /// Applies auto-sizing for labels whose length varies (card names, status,
        /// validation, feedback). Text renders at the role's size and only shrinks
        /// to a sensible minimum when it would otherwise overflow.
        /// Null-safe.
        /// </summary>
        public static void ApplyAutoSize(TMP_Text text, Role role)
        {
            if (text == null) return;

            float max = SizeFor(role);
            // Keep the shrink floor high enough to stay readable on a phone even when
            // a long string forces auto-size down (was 12pt / 0.66 — too small).
            float min = Mathf.Max(18f, max * 0.75f);

            text.enableAutoSizing = true;
            text.fontSizeMin = min;
            text.fontSizeMax = max;
            text.fontSize = max;

            if (role == Role.Header)
            {
                text.fontStyle |= FontStyles.Bold;
            }
        }
    }
}
