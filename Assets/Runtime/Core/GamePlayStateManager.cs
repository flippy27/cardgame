using System.Collections.Generic;
using UnityEngine;

namespace Flippy.CardDuelMobile.Core
{
    /// <summary>
    /// Gestiona estado global del juego entre pantallas.
    /// Mantiene: deck seleccionado, cartas en mano, datos del match, etc.
    /// Singleton que persiste entre escenas.
    /// </summary>
    public sealed class GamePlayStateManager : MonoBehaviour
    {
        public static GamePlayStateManager Instance { get; private set; }

        // Estado actual
        [SerializeField] private string selectedDeckId;
        [SerializeField] private List<string> selectedDeckCards = new();

        // Información del match
        [SerializeField] private string matchId;
        [SerializeField] private string currentPlayerId;
        [SerializeField] private string opponentPlayerId;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Setea el deck seleccionado para el próximo match.
        /// </summary>
        public void SetSelectedDeck(string deckId, List<string> cardIds)
        {
            selectedDeckId = deckId;
            selectedDeckCards = new List<string>(cardIds);
            GameLogger.Info("PlayState", $"Selected deck: {deckId} with {cardIds.Count} cards");
        }

        /// <summary>
        /// Obtiene el deck seleccionado.
        /// </summary>
        public (string deckId, List<string> cardIds) GetSelectedDeck()
        {
            return (selectedDeckId, selectedDeckCards);
        }

        /// <summary>
        /// Setea información del match (matchId, players).
        /// </summary>
        public void SetMatchInfo(string matchId, string playerId, string opponentId)
        {
            this.matchId = matchId;
            currentPlayerId = playerId;
            opponentPlayerId = opponentId;
            GameLogger.Info("PlayState", $"Match info set: {matchId}");
        }

        /// <summary>
        /// Obtiene información del match.
        /// </summary>
        public (string matchId, string playerId, string opponentId) GetMatchInfo()
        {
            return (matchId, currentPlayerId, opponentPlayerId);
        }

        /// <summary>
        /// Limpia todo el estado (ej: al volver al menú).
        /// </summary>
        public void Reset()
        {
            selectedDeckId = null;
            selectedDeckCards.Clear();
            matchId = null;
            currentPlayerId = null;
            opponentPlayerId = null;
            GameLogger.Info("PlayState", "State cleared");
        }

        /// <summary>
        /// Verifica si hay un deck seleccionado válido.
        /// </summary>
        public bool HasValidDeck => !string.IsNullOrWhiteSpace(selectedDeckId) && selectedDeckCards.Count > 0;
    }
}
