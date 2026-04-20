using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.SinglePlayer;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;
using UnityEngine.Serialization;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Orquestador principal del gameplay 3D.
    /// Se suscribe a BattleSnapshotBus y actualiza la presentación 3D.
    /// </summary>
    public class GameplayPresenter3D : MonoBehaviour
    {
        [SerializeField] private Board3DManager board3DManager;
        [SerializeField] private Hand3DManager hand3DManager;
        [SerializeField] private HUD3D hud3D;
        [SerializeField] private DragHandler3D dragHandler;
        [SerializeField] private EndTurnButton3D endTurnButton;
        [SerializeField] public GameObject boardCardPrefab;

        public Board3DManager Board3DManager
        {
            get => board3DManager;
            set => board3DManager = value;
        }

        public Hand3DManager Hand3DManager
        {
            get => hand3DManager;
            set => hand3DManager = value;
        }

        public HUD3D Hud3D
        {
            get => hud3D;
            set => hud3D = value;
        }

        public DragHandler3D DragHandler
        {
            get => dragHandler;
            set => dragHandler = value;
        }

        public EndTurnButton3D EndTurnButton
        {
            get => endTurnButton;
            set => endTurnButton = value;
        }

        private DuelSnapshotDto _latestSnapshot;
        private DuelSnapshotDto _previousSnapshot;
        private bool _subscribed;
        private int _lastLogCount;

        public static GameplayPresenter3D Instance { get; private set; }

        public static DuelSnapshotDto GetLatestSnapshot() => Instance?._latestSnapshot;

        private void Awake()
        {
            Debug.Log("[GameplayPresenter3D] Awake");
            Instance = this;

            // Board manager
            bool boardManagerCreated = false;
            if (board3DManager == null)
            {
                var boardGo = new GameObject("Board3D");
                boardGo.transform.SetParent(transform);
                board3DManager = boardGo.AddComponent<Board3DManager>();
                boardManagerCreated = true;
            }

            // Hand manager
            bool handManagerCreated = false;
            if (hand3DManager == null)
            {
                var handGo = new GameObject("Hand3D");
                handGo.transform.SetParent(transform);
                hand3DManager = handGo.AddComponent<Hand3DManager>();
                handManagerCreated = true;
            }

            // HUD
            if (hud3D == null)
            {
                hud3D = FindFirstObjectByType<HUD3D>();
            }

            // Drag handler
            if (dragHandler == null)
            {
                dragHandler = FindFirstObjectByType<DragHandler3D>();
                if (dragHandler == null && Camera.main != null)
                {
                    dragHandler = Camera.main.gameObject.AddComponent<DragHandler3D>();
                    dragHandler.enabled = true;
                }
            }

            // End turn button
            if (endTurnButton == null)
            {
                endTurnButton = FindFirstObjectByType<EndTurnButton3D>();
                if (endTurnButton == null)
                {
                    var canvas = FindFirstObjectByType<Canvas>();
                    if (canvas != null)
                    {
                        var btnGo = new GameObject("EndTurnButton");
                        btnGo.transform.SetParent(canvas.transform);
                        btnGo.transform.localPosition = Vector3.zero;

                        var btnRect = btnGo.AddComponent<RectTransform>();
                        btnRect.anchoredPosition = new Vector2(0, -50);
                        btnRect.sizeDelta = new Vector2(200, 60);

                        var btn = btnGo.AddComponent<UnityEngine.UI.Button>();
                        btn.targetGraphic = btnGo.AddComponent<Image>();

                        var txt = btnGo.AddComponent<TextMeshProUGUI>();
                        txt.text = "END TURN";
                        txt.alignment = TextAlignmentOptions.Center;

                        endTurnButton = btnGo.AddComponent<EndTurnButton3D>();
                        endTurnButton.enabled = true;

                        Debug.Log("[GameplayPresenter3D] Created EndTurnButton");
                    }
                }
            }

            // Always initialize managers (idempotent)
            if (board3DManager != null)
                board3DManager.Initialize();
            if (hand3DManager != null)
                hand3DManager.Initialize();
        }

        private void Start()
        {
            // Si estamos en local mode pero el match no está iniciado, iniciarlo
            if (GameModeManager.Instance != null && GameModeManager.Instance.IsLocalMode)
            {
                var coordinator = LocalSinglePlayerCoordinator.Instance;
                if (coordinator != null && !coordinator.IsActive)
                {
                    Debug.Log("[GameplayPresenter3D] Starting local match from MainGame");
                    coordinator.StartMatch();
                }
            }
        }

        private void OnEnable()
        {
            if (!_subscribed)
            {
                BattleSnapshotBus.SubscribeAndGetLast(HandleSnapshot);
                _subscribed = true;
                Debug.Log("[GameplayPresenter3D] Subscribed to BattleSnapshotBus");
            }
        }

        private void OnDisable()
        {
            BattleSnapshotBus.SnapshotReceived -= HandleSnapshot;
            _subscribed = false;
        }

        private void HandleSnapshot(string snapshotJson)
        {
            if (string.IsNullOrEmpty(snapshotJson))
                return;

            var snapshot = JsonUtility.FromJson<DuelSnapshotDto>(snapshotJson);
            if (snapshot == null)
                return;

            _latestSnapshot = snapshot;

            // Procesar nuevos logs para detectar ataques
            ProcessAttackLogs(snapshot);

            // Actualizar board: mostrar cartas
            UpdateBoard(snapshot);

            // Detectar y animar reposicionamiento
            DetectAndAnimateRepositioning(snapshot);

            // Actualizar mano del jugador local
            UpdateLocalHand(snapshot);

            // Actualizar HUD
            UpdateHUD(snapshot);

            // Detectar fin de partida
            if (snapshot.duelEnded && snapshot.winnerPlayerIndex >= 0)
            {
                HandleMatchCompletion(snapshot);
            }
        }

        private void ProcessAttackLogs(DuelSnapshotDto snapshot)
        {
            if (snapshot?.logs == null || snapshot.logs.Count == 0)
                return;

            // Procesar logs nuevos
            for (int i = _lastLogCount; i < snapshot.logs.Count; i++)
            {
                var log = snapshot.logs[i];
                if (log.type == BattleLogType.Attack && log.message != null)
                {
                    // Buscar "→" para separar attacker y defender
                    int arrowIdx = log.message.IndexOf("→");
                    if (arrowIdx > 0)
                    {
                        // Extraer nombre del atacante y el defensor
                        string beforeArrow = log.message.Substring(0, arrowIdx).Trim();
                        string afterArrow = log.message.Substring(arrowIdx + 1).Trim();

                        // Extraer el nombre después de "] "
                        int nameStart = beforeArrow.LastIndexOf("] ") + 2;
                        if (nameStart > 1)
                        {
                            string attackerName = beforeArrow.Substring(nameStart).Split('(')[0].Trim();
                            string defenderName = afterArrow.Split(':')[0].Split('→')[0].Trim();

                            // Buscar las cartas en el board
                            Card3DView attacker = FindCardByName(attackerName);
                            Card3DView defender = FindCardByName(defenderName);

                            if (attacker != null && defender != null)
                            {
                                AttackEffectSystem.PlayAttackEffect(attacker, defender);
                            }
                        }
                    }
                }
            }

            _lastLogCount = snapshot.logs.Count;
        }

        private Card3DView FindCardByName(string displayName)
        {
            if (_latestSnapshot?.players == null)
                return null;

            for (int playerIndex = 0; playerIndex < _latestSnapshot.players.Length; playerIndex++)
            {
                var playerSnapshot = _latestSnapshot.players[playerIndex];
                if (playerSnapshot?.board == null)
                    continue;

                foreach (var slotSnapshot in playerSnapshot.board)
                {
                    if (slotSnapshot.occupied && slotSnapshot.occupant != null && slotSnapshot.occupant.displayName == displayName)
                    {
                        // Buscar la Card3DView correspondiente en el board manager
                        var cardView = board3DManager.GetCardInSlot(playerIndex, slotSnapshot.slot);
                        if (cardView != null && cardView.CardData.displayName == displayName)
                        {
                            return cardView;
                        }
                    }
                }
            }

            return null;
        }

        private void HandleMatchCompletion(DuelSnapshotDto snapshot)
        {
            var isLocalWin = snapshot.winnerPlayerIndex == snapshot.localPlayerIndex;
            var opponentIndex = 1 - snapshot.localPlayerIndex;
            var opponentName = snapshot.players?[opponentIndex]?.playerId ?? "Opponent";

            var completionScreen = FindFirstObjectByType<MatchCompletionScreen>();
            if (completionScreen != null)
            {
                if (isLocalWin)
                {
                    completionScreen.ShowVictory(opponentName, snapshot.turnNumber);
                    AudioManager.Instance?.PlayVictory();
                }
                else
                {
                    completionScreen.ShowDefeat(opponentName, snapshot.turnNumber);
                    AudioManager.Instance?.PlayDefeat();
                }
            }
        }

        private void UpdateBoard(DuelSnapshotDto snapshot)
        {
            if (snapshot?.players == null || snapshot.players.Length < 2)
                return;

            // Actualizar ambos lados del board
            for (int playerIndex = 0; playerIndex < 2; playerIndex++)
            {
                var playerSnapshot = snapshot.players[playerIndex];
                if (playerSnapshot?.board == null)
                    continue;

                // Para cada slot del jugador
                foreach (var boardCard in playerSnapshot.board)
                {
                    var slot = boardCard.slot;
                    var existing = board3DManager.GetCardInSlot(playerIndex, slot);

                    if (boardCard.occupied && boardCard.occupant != null)
                    {
                        // Card existe en snapshot
                        if (existing == null)
                        {
                            CreateBoardCard(boardCard.occupant, playerIndex, slot);
                            Debug.Log($"[GameplayPresenter3D] Created card {boardCard.occupant.displayName} at P{playerIndex} {slot}");
                        }
                        else if (existing.CardData.runtimeId != boardCard.occupant.runtimeId)
                        {
                            // Card cambió, reemplazar
                            board3DManager.ClearSlot(playerIndex, slot);
                            CreateBoardCard(boardCard.occupant, playerIndex, slot);
                        }
                        else
                        {
                            // Update stats - detectar si murió
                            var oldHP = existing.CardData.currentHealth;
                            var newHP = boardCard.occupant.currentHealth;

                            existing.CardData.currentHealth = newHP;
                            existing.UpdateStatsDisplay();

                            // Animar daño
                            if (newHP < oldHP && newHP > 0)
                            {
                                // Daño (flash)
                                existing.SetColor(Color.red);
                                StartCoroutine(ResetCardColor(existing, 0.2f));
                            }
                            else if (newHP <= 0)
                            {
                                // Muerte
                                existing.AnimateDeath();
                                board3DManager.ClearSlot(playerIndex, slot);
                            }
                        }
                    }
                    else
                    {
                        // Slot vacío - detectar si carta murió
                        if (existing != null)
                        {
                            // Animar muerte si no fue hecha ya
                            if (existing.CardData.currentHealth > 0)
                            {
                                existing.AnimateDeath();
                            }
                            board3DManager.ClearSlot(playerIndex, slot);
                        }
                    }
                }
            }
        }

        private void UpdateLocalHand(DuelSnapshotDto snapshot)
        {
            if (snapshot?.players == null || snapshot.players.Length == 0)
                return;

            var localPlayer = snapshot.players[snapshot.localPlayerIndex];
            if (localPlayer?.hand != null)
            {
                hand3DManager.RefreshHand(localPlayer.hand);
            }
        }

        private void UpdateHUD(DuelSnapshotDto snapshot)
        {
            if (hud3D == null)
                return;

            var local = snapshot.players[snapshot.localPlayerIndex];
            var remote = snapshot.players[1 - snapshot.localPlayerIndex];

            if (local != null)
            {
                // Hero max health es constante (20 según reglas)
                hud3D.UpdateLocalHeroInfo(local.heroHealth, 20, local.mana, local.maxMana);
            }

            if (remote != null)
            {
                hud3D.UpdateRemoteHeroInfo(remote.heroHealth, 20, remote.mana, remote.maxMana);
            }

            bool isLocalTurn = snapshot.activePlayerIndex == snapshot.localPlayerIndex;
            hud3D.UpdateTurnInfo(snapshot.turnNumber, snapshot.activePlayerIndex, isLocalTurn);

            // Actualizar botón End Turn
            if (endTurnButton != null)
            {
                endTurnButton.SetEnabled(isLocalTurn);
            }

            // Actualizar glow en slots jugables
            HighlightPlayableSlots(snapshot, isLocalTurn);
        }

        private void HighlightPlayableSlots(DuelSnapshotDto snapshot, bool isLocalTurn)
        {
            // Clear all glows
            for (int p = 0; p < 2; p++)
            {
                foreach (BoardSlot slot in System.Enum.GetValues(typeof(BoardSlot)))
                {
                    var slotComponent = board3DManager.GetSlot(p, slot);
                    if (slotComponent != null)
                    {
                        slotComponent.SetGlow(false);
                    }
                }
            }

            // Si no es el turno del jugador local, no mostrar glows
            if (!isLocalTurn || snapshot?.players == null)
                return;

            // Colectar todos los slots jugables
            var playableSlots = new System.Collections.Generic.HashSet<(int playerIndex, BoardSlot slot)>();

            var localPlayer = snapshot.players[snapshot.localPlayerIndex];
            if (localPlayer?.board != null)
            {
                foreach (var boardCard in localPlayer.board)
                {
                    // Si el slot está vacío o ocupado, es potencialmente jugable (según las reglas del juego)
                    playableSlots.Add((snapshot.localPlayerIndex, boardCard.slot));
                }
            }

            // Resaltar los slots jugables
            foreach (var (playerIndex, slot) in playableSlots)
            {
                var slotComponent = board3DManager.GetSlot(playerIndex, slot);
                if (slotComponent != null)
                {
                    slotComponent.SetGlow(true);
                }
            }
        }

        public void RequestPlayCard(string runtimeCardKey, BoardSlot targetSlot)
        {
            Debug.Log($"[GameplayPresenter3D] RequestPlayCard: {runtimeCardKey} → {targetSlot}");
            var coordinator = LocalSinglePlayerCoordinator.Instance;
            if (coordinator == null)
            {
                Debug.LogError("[GameplayPresenter3D] LocalSinglePlayerCoordinator.Instance is null!");
                return;
            }

            bool success = coordinator.RequestPlayCard(runtimeCardKey, targetSlot);
            Debug.Log($"[GameplayPresenter3D] RequestPlayCard result: {success}");
            if (success)
            {
                hud3D?.Log($"Played card to {targetSlot}");
            }
            else
            {
                Debug.LogWarning("[GameplayPresenter3D] coordinator.RequestPlayCard returned false");
            }
        }

        public void RequestEndTurn()
        {
            var coordinator = LocalSinglePlayerCoordinator.Instance;
            if (coordinator != null)
            {
                bool success = coordinator.RequestEndTurn();
                if (success)
                {
                    hud3D?.Log("Turn ended");
                }
            }
        }

        private System.Collections.IEnumerator ResetCardColor(Card3DView card, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (card != null)
                card.SetColor(new Color(0.2f, 0.2f, 0.3f));
        }

        private void DetectAndAnimateRepositioning(DuelSnapshotDto snapshot)
        {
            if (_previousSnapshot == null || snapshot?.players == null || _previousSnapshot.players == null)
            {
                _previousSnapshot = snapshot;
                return;
            }

            // Detectar cartas que cambiaron de slot
            for (int playerIndex = 0; playerIndex < snapshot.players.Length && playerIndex < _previousSnapshot.players.Length; playerIndex++)
            {
                var currentPlayer = snapshot.players[playerIndex];
                var previousPlayer = _previousSnapshot.players[playerIndex];

                if (currentPlayer?.board == null || previousPlayer?.board == null)
                    continue;

                // Mapear cartas por runtime ID en ambos snapshots
                var currentCards = new System.Collections.Generic.Dictionary<string, (BoardSlot slot, BoardCardDto card)>();
                var previousCards = new System.Collections.Generic.Dictionary<string, (BoardSlot slot, BoardCardDto card)>();

                foreach (var slotSnapshot in currentPlayer.board)
                {
                    if (slotSnapshot.occupied && slotSnapshot.occupant != null)
                    {
                        currentCards[slotSnapshot.occupant.runtimeId] = (slotSnapshot.slot, slotSnapshot.occupant);
                    }
                }

                foreach (var slotSnapshot in previousPlayer.board)
                {
                    if (slotSnapshot.occupied && slotSnapshot.occupant != null)
                    {
                        previousCards[slotSnapshot.occupant.runtimeId] = (slotSnapshot.slot, slotSnapshot.occupant);
                    }
                }

                // Detectar cartas que cambiaron de slot
                foreach (var runtimeId in currentCards.Keys)
                {
                    if (previousCards.TryGetValue(runtimeId, out var prev))
                    {
                        var current = currentCards[runtimeId];
                        if (prev.slot != current.slot)
                        {
                            // Carta se movió
                            var card = board3DManager.GetCardInSlot(playerIndex, current.slot);
                            if (card != null)
                            {
                                // Obtener la posición del slot anterior
                                var prevSlot = board3DManager.GetSlot(playerIndex, prev.slot);
                                var currSlot = board3DManager.GetSlot(playerIndex, current.slot);

                                if (prevSlot != null && currSlot != null)
                                {
                                    var startPos = prevSlot.transform.position;
                                    var endPos = currSlot.transform.position;

                                    // Animar movimiento
                                    StartCoroutine(AnimateCardMovement(card, startPos, endPos, 0.4f));
                                    hud3D?.Log($"{current.card.displayName} repositioned to {current.slot}");
                                }
                            }
                        }
                    }
                }
            }

            _previousSnapshot = snapshot;
        }

        private void CreateBoardCard(BoardCardDto cardData, int playerIndex, BoardSlot slot)
        {
            GameObject cardGo;

            if (boardCardPrefab != null)
            {
                cardGo = Instantiate(boardCardPrefab);
                cardGo.name = $"Card3D_{cardData.runtimeId}";
            }
            else
            {
                cardGo = new GameObject($"Card3D_{cardData.runtimeId}");
                cardGo.AddComponent<Card3DView>();
            }

            cardGo.transform.SetParent(transform);

            var cardView = cardGo.GetComponent<Card3DView>();
            if (cardView == null)
                cardView = cardGo.AddComponent<Card3DView>();

            cardView.Initialize(cardData, playerIndex);

            if (cardGo.GetComponent<CardTooltip>() == null)
                cardGo.AddComponent<CardTooltip>();

            board3DManager.SetCardInSlot(playerIndex, slot, cardView);
        }

        private System.Collections.IEnumerator AnimateCardMovement(Card3DView card, Vector3 startPos, Vector3 endPos, float duration)
        {
            if (card == null)
                yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Ease-out cubic
                float easeT = 1f - Mathf.Pow(1f - t, 3f);
                card.transform.position = Vector3.Lerp(startPos, endPos, easeT);

                yield return null;
            }

            card.transform.position = endPos;
        }
    }
}
