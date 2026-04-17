using System;

namespace Flippy.CardDuelMobile.Core
{
    public static class RequestValidator
    {
        public static void ValidateEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ValidationException("Email required");
            if (!email.Contains("@") || email.Length < 3)
                throw new ValidationException("Invalid email format");
        }

        public static void ValidatePassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 3)
                throw new ValidationException("Password must be 3+ chars");
        }

        public static void ValidatePlayerId(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId) || playerId.Length < 1)
                throw new ValidationException("PlayerId required");
        }

        public static void ValidateMatchId(string matchId)
        {
            if (string.IsNullOrWhiteSpace(matchId))
                throw new ValidationException("MatchId required");
        }

        public static void ValidateDeckId(string deckId)
        {
            if (string.IsNullOrWhiteSpace(deckId))
                throw new ValidationException("DeckId required");
        }

        public static void ValidateCardIds(int[] cardIds, int minSize = 20, int maxSize = 30)
        {
            if (cardIds == null || cardIds.Length < minSize)
                throw new ValidationException($"Deck must have at least {minSize} cards");
            if (cardIds.Length > maxSize)
                throw new ValidationException($"Deck cannot exceed {maxSize} cards");
        }
    }
}
