using UnityEngine;

namespace Flippy.CardDuelMobile.Data
{
    /// <summary>
    /// Asset que contiene referencia a todas las CardDefinitions.
    /// Sirve como fuente única de verdad para card data.
    /// </summary>
    [CreateAssetMenu(menuName = "Cards/Card Catalog", fileName = "CardCatalog")]
    public class CardCatalog : ScriptableObject
    {
        public CardDefinition[] cards = new CardDefinition[0];
    }
}
