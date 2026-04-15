using System;

namespace Flippy.CardDuelMobile.Core
{
    /// <summary>
    /// Excepción base para errores del juego.
    /// </summary>
    public class GameException : Exception
    {
        public GameException(string message) : base(message) { }
        public GameException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Error cuando el estado del juego es inválido para una acción.
    /// </summary>
    public class InvalidGameStateException : GameException
    {
        public InvalidGameStateException(string message) : base(message) { }
    }

    /// <summary>
    /// Error cuando una validación falla (deck, carta, etc).
    /// </summary>
    public class ValidationException : GameException
    {
        public ValidationException(string message) : base(message) { }

        public static void ThrowIf(bool condition, string message)
        {
            if (condition) throw new ValidationException(message);
        }
    }

    /// <summary>
    /// Error cuando una carta o recurso no se encuentra.
    /// </summary>
    public class CardNotFoundException : GameException
    {
        public string CardId { get; }

        public CardNotFoundException(string cardId)
            : base($"Card not found: {cardId}")
        {
            CardId = cardId;
        }
    }

    /// <summary>
    /// Error cuando no hay suficientes recursos (mana, deck, etc).
    /// </summary>
    public class InsufficientResourcesException : GameException
    {
        public string ResourceType { get; }
        public int Required { get; }
        public int Available { get; }

        public InsufficientResourcesException(string resourceType, int required, int available)
            : base($"Insufficient {resourceType}: required {required}, available {available}")
        {
            ResourceType = resourceType;
            Required = required;
            Available = available;
        }
    }

    /// <summary>
    /// Error cuando una acción es ilegal en el contexto actual.
    /// </summary>
    public class IllegalActionException : GameException
    {
        public IllegalActionException(string action, string reason)
            : base($"Illegal action '{action}': {reason}") { }
    }
}
