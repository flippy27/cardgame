using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Flippy.CardDuelMobile.Data;

namespace Flippy.CardDuelMobile.Networking
{
    /// <summary>
    /// Hash simple y determinista del mazo para validación mínima.
    /// </summary>
    public static class DeckHashUtility
    {
        public static string ComputeHash(DeckDefinition deck)
        {
            if (deck == null || deck.cards == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.Append(deck.deckId).Append('|');

            foreach (var deckCard in deck.cards.Where(card => card != null && card.card != null))
            {
                var card = deckCard.card;
                builder
                    .Append(card.cardId).Append(':')
                    .Append(card.manaCost).Append(':')
                    .Append(card.attack).Append(':')
                    .Append(card.health).Append(':')
                    .Append((int)card.unitType).Append(':')
                    .Append(deckCard.quantity)
                    .Append(';');
            }

            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
            return string.Concat(bytes.Select(byteValue => byteValue.ToString("x2")));
        }
    }
}
