using UnityEngine;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Perfil visual de una carta.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Visual Profile", fileName = "CardVisualProfile")]
    public sealed class CardVisualProfile : ScriptableObject
    {
        [Header("Art")]
        public Sprite artwork;
        public Sprite frame;
        public Sprite icon;

        [Header("Palette")]
        public Color primaryColor = Color.white;
        public Color secondaryColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        public Color glowColor = new Color(0.8f, 0.9f, 1f, 1f);

        [Header("FX")]
        public Material cardMaterial;
        public Material highlightMaterial;
        public CardRarity rarity;
    }
}
