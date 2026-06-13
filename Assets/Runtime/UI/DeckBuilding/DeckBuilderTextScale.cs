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
        public const float Header   = 40f;
        public const float Label    = 28f;
        public const float Button   = 26f;
        public const float CardName = 24f;
        public const float Status   = 24f;

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
            float min = Mathf.Max(12f, max * 0.66f);

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
