using System;
using System.Collections.Generic;
using UnityEngine;
using Flippy.CardDuelMobile.Networking.ApiClients;

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
        [SerializeField] private string currentRulesetId;
        private GameRulesDto _currentRules;

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
            var hasChanged =
                !string.Equals(this.matchId, matchId, StringComparison.Ordinal) ||
                !string.Equals(currentPlayerId, playerId, StringComparison.Ordinal) ||
                !string.Equals(opponentPlayerId, opponentId, StringComparison.Ordinal);

            this.matchId = matchId;
            currentPlayerId = playerId;
            opponentPlayerId = opponentId;
            if (hasChanged)
            {
                GameLogger.Info("PlayState", $"Match info set: {matchId}");
            }
        }

        /// <summary>
        /// Obtiene información del match.
        /// </summary>
        public (string matchId, string playerId, string opponentId) GetMatchInfo()
        {
            return (matchId, currentPlayerId, opponentPlayerId);
        }

        /// <summary>
        /// Cachea las reglas efectivas del match actual (fuente backend).
        /// </summary>
        public void SetMatchRules(string rulesetId, GameRulesDto rules)
        {
            currentRulesetId = rulesetId;
            _currentRules = rules;

            if (rules != null)
            {
                GameLogger.Info("PlayState", $"Rules cached: {rules.displayName} ({rules.rulesetId})");
            }
        }

        public (string rulesetId, GameRulesDto rules) GetMatchRules()
        {
            return (currentRulesetId, _currentRules);
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
            currentRulesetId = null;
            _currentRules = null;
            GameLogger.Info("PlayState", "State cleared");
        }

        /// <summary>
        /// Verifica si hay un deck seleccionado válido.
        /// </summary>
        public bool HasValidDeck => !string.IsNullOrWhiteSpace(selectedDeckId) && selectedDeckCards.Count > 0;
    }
}
