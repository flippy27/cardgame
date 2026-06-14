using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.SinglePlayer;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Detailed post-match summary screen (victory/defeat).
    /// Shows the result, derived battle stats (kills, damage dealt, hero HP, mana),
    /// a scrollable event-by-event battle history, and Rematch / Back-to-Menu buttons.
    ///
    /// The screen builds its own full-screen Canvas in code so it works without any
    /// editor wiring (the scene wiring is minimal/missing). If the legacy serialized
    /// references happen to be wired, the simple result/stats texts still get filled.
    /// </summary>
    public class MatchCompletionScreen : MonoBehaviour
    {
        // ---- Legacy serialized refs (optional; kept for backward compatibility) ----
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private TextMeshProUGUI statsText;
        [SerializeField] private Button menuButton;
        [SerializeField] private CanvasGroup canvasGroup;

        // ---- Code-built UI ----
        private bool _uiBuilt;
        private Canvas _canvas;
        private Text _builtTitle;
        private Text _builtStats;
        private Transform _historyContainer;
        private Button _rematchButton;
        private Button _builtMenuButton;
        private bool _allowRematch;

        private static readonly Color BackdropColor = new(0f, 0f, 0f, 0.78f);
        private static readonly Color PanelColor = new(0.08f, 0.09f, 0.13f, 0.98f);
        private static readonly Color StatsColor = new(0.82f, 0.86f, 0.94f, 1f);
        private static readonly Color VictoryColor = new(0.30f, 0.85f, 0.40f, 1f);
        private static readonly Color DefeatColor = new(0.92f, 0.32f, 0.32f, 1f);
        private static readonly Color RematchColor = new(0.22f, 0.50f, 0.72f, 1f);
        private static readonly Color MenuColor = new(0.35f, 0.18f, 0.20f, 1f);
        private static readonly Color HistoryRowEven = new(0.14f, 0.16f, 0.21f, 1f);
        private static readonly Color HistoryRowOdd = new(0.11f, 0.13f, 0.18f, 1f);

        /// <summary>Finds an existing screen in the scene or creates a fresh code-built one.</summary>
        public static MatchCompletionScreen GetOrCreate()
        {
            var existing = FindFirstObjectByType<MatchCompletionScreen>();
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject("MatchCompletionScreen");
            return go.AddComponent<MatchCompletionScreen>();
        }

        private void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            SetVisible(false);

            DisableRaycast(resultText);
            DisableRaycast(statsText);

            if (menuButton != null)
            {
                menuButton.onClick.AddListener(OnMenuClicked);
            }
        }

        // ---- Backward-compatible entry points ----

        public void ShowVictory(string opponentName, int turnsPlayed)
        {
            ShowSimple(true, opponentName, turnsPlayed);
        }

        public void ShowDefeat(string opponentName, int turnsPlayed)
        {
            ShowSimple(false, opponentName, turnsPlayed);
        }

        private void ShowSimple(bool isVictory, string opponentName, int turnsPlayed)
        {
            var stats = $"vs {opponentName}\nTurns: {turnsPlayed}";
            Render(isVictory, stats, history: null, allowRematch: ResolveAllowRematch());
        }

        // ---- Rich entry point ----

        /// <summary>
        /// Renders the full detailed summary from the final snapshot.
        /// Computes kills, damage dealt, hero HP, and (when derivable) mana per side,
        /// plus an ordered, scrollable battle history.
        /// </summary>
        public void ShowSummary(DuelSnapshotDto snapshot)
        {
            if (snapshot == null)
            {
                ShowSimple(false, "Opponent", 0);
                return;
            }

            var localIndex = snapshot.localPlayerIndex;
            var isVictory = ResolveVictory(snapshot, localIndex);
            var summary = ComputeSummary(snapshot, localIndex);
            var statsBlock = BuildStatsText(snapshot, summary, localIndex);
            var history = BuildHistory(snapshot, localIndex);

            Render(isVictory, statsBlock, history, ResolveAllowRematch());
        }

        // The server's winner index can still be -1 in the snapshot that first flips
        // duelEnded; fall back to hero HP so the title reports the right result.
        private static bool ResolveVictory(DuelSnapshotDto snapshot, int localIndex)
        {
            if (snapshot.winnerPlayerIndex >= 0)
            {
                return snapshot.winnerPlayerIndex == localIndex;
            }
            if (snapshot.players != null && snapshot.players.Length >= 2)
            {
                var li = localIndex >= 0 && localIndex < snapshot.players.Length ? localIndex : 0;
                var ei = li == 0 ? 1 : 0;
                var local = snapshot.players[li];
                var enemy = ei < snapshot.players.Length ? snapshot.players[ei] : null;
                if (local != null && local.heroHealth <= 0) return false;
                if (enemy != null && enemy.heroHealth <= 0) return true;
            }
            return false;
        }

        // ---- Stat computation (presentation-derived, client-side) ----

        private struct SideStats
        {
            public int Kills;        // enemy units this side killed
            public int DamageDealt;  // total damage attributed to this side's sources
        }

        private struct MatchSummary
        {
            public SideStats Local;
            public SideStats Enemy;
            public int LocalHeroHp;
            public int EnemyHeroHp;
            public int LocalMaxHp;
            public int EnemyMaxHp;
            public int LocalManaUsed;
            public int EnemyManaUsed;
            public bool ManaDerivable;
        }

        private static MatchSummary ComputeSummary(DuelSnapshotDto snapshot, int localIndex)
        {
            var summary = new MatchSummary();

            if (snapshot.battleEvents != null)
            {
                foreach (var ev in snapshot.battleEvents)
                {
                    if (ev == null)
                    {
                        continue;
                    }

                    // Damage dealt: attributed to the source seat.
                    if (IsDamageKind(ev.kind) && ev.amount > 0)
                    {
                        if (ev.sourceSeatIndex == localIndex)
                        {
                            summary.Local.DamageDealt += ev.amount;
                        }
                        else if (ev.sourceSeatIndex >= 0)
                        {
                            summary.Enemy.DamageDealt += ev.amount;
                        }
                    }

                    // Kills: a "death" event's target belongs to the side that LOST the unit,
                    // so the kill is credited to the opposite side.
                    if (string.Equals(ev.kind, "death", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(ev.kind, "card_destroyed", StringComparison.OrdinalIgnoreCase))
                    {
                        if (ev.targetSeatIndex == localIndex)
                        {
                            summary.Enemy.Kills += 1;
                        }
                        else if (ev.targetSeatIndex >= 0)
                        {
                            summary.Local.Kills += 1;
                        }
                    }
                }
            }

            // Hero HP remaining from the final player snapshots.
            var localPlayer = GetPlayer(snapshot, localIndex);
            var enemyPlayer = GetPlayer(snapshot, 1 - localIndex);
            if (localPlayer != null)
            {
                summary.LocalHeroHp = localPlayer.heroHealth;
            }
            if (enemyPlayer != null)
            {
                summary.EnemyHeroHp = enemyPlayer.heroHealth;
            }
            summary.LocalMaxHp = localIndex == 0 ? snapshot.localHeroMaxHealth : snapshot.remoteHeroMaxHealth;
            summary.EnemyMaxHp = localIndex == 0 ? snapshot.remoteHeroMaxHealth : snapshot.localHeroMaxHealth;

            // Mana used is only derivable if the snapshot exposes per-player maxMana
            // (current mana is reset each turn, so "used this turn" is current vs max).
            if (localPlayer != null && enemyPlayer != null &&
                (localPlayer.maxMana > 0 || enemyPlayer.maxMana > 0))
            {
                summary.ManaDerivable = true;
                summary.LocalManaUsed = Mathf.Max(0, localPlayer.maxMana - localPlayer.mana);
                summary.EnemyManaUsed = Mathf.Max(0, enemyPlayer.maxMana - enemyPlayer.mana);
            }

            return summary;
        }

        private static string BuildStatsText(DuelSnapshotDto snapshot, MatchSummary s, int localIndex)
        {
            var opponent = GetPlayer(snapshot, 1 - localIndex);
            var opponentName = opponent != null && !string.IsNullOrWhiteSpace(opponent.playerId)
                ? opponent.playerId
                : "Opponent";

            var sb = new StringBuilder();
            sb.AppendLine($"Opponent: {opponentName}");
            sb.AppendLine($"Reason: {DescribeEndReason(snapshot.endReason)}");
            sb.AppendLine($"Turns played: {snapshot.turnNumber}");
            sb.AppendLine();
            sb.AppendLine("<b>You          /          Enemy</b>");
            sb.AppendLine($"Hero HP:   {s.LocalHeroHp}/{s.LocalMaxHp}      /      {s.EnemyHeroHp}/{s.EnemyMaxHp}");
            sb.AppendLine($"Kills:        {s.Local.Kills}            /            {s.Enemy.Kills}");
            sb.AppendLine($"Damage:    {s.Local.DamageDealt}          /          {s.Enemy.DamageDealt}");
            if (s.ManaDerivable)
            {
                sb.AppendLine($"Mana used: {s.LocalManaUsed}            /            {s.EnemyManaUsed}");
            }

            return sb.ToString().TrimEnd();
        }

        private static List<string> BuildHistory(DuelSnapshotDto snapshot, int localIndex)
        {
            var lines = new List<string>();

            // Prefer structured battle events (ordered by sequence); fall back to logs.
            if (snapshot.battleEvents != null && snapshot.battleEvents.Length > 0)
            {
                var ordered = new List<BattleEventDto>(snapshot.battleEvents);
                ordered.Sort((a, b) => a.sequence.CompareTo(b.sequence));

                foreach (var ev in ordered)
                {
                    if (ev == null)
                    {
                        continue;
                    }

                    var text = FormatEvent(ev, localIndex);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        lines.Add(text);
                    }
                }
            }

            if (lines.Count == 0 && snapshot.logs != null)
            {
                foreach (var log in snapshot.logs)
                {
                    if (log != null && !string.IsNullOrWhiteSpace(log.message))
                    {
                        lines.Add(log.message.Trim());
                    }
                }
            }

            if (lines.Count == 0)
            {
                lines.Add("No detailed battle events were recorded.");
            }

            return lines;
        }

        private static string FormatEvent(BattleEventDto ev, int localIndex)
        {
            // Server-supplied message wins when present.
            if (!string.IsNullOrWhiteSpace(ev.message))
            {
                return ev.message.Trim();
            }

            var src = ev.sourceSeatIndex == localIndex ? "Your unit" : "Enemy unit";
            var tgt = ev.targetSeatIndex == localIndex ? "your unit" : "enemy unit";

            switch ((ev.kind ?? string.Empty).ToLowerInvariant())
            {
                case "card_damage":
                    return $"{src} hit {tgt} for {ev.amount}.";
                case "card_counterattack":
                    return $"{src} counterattacked {tgt} for {ev.amount}.";
                case "hero_damage":
                    return $"{src} hit {(ev.targetSeatIndex == localIndex ? "your hero" : "enemy hero")} for {ev.amount}.";
                case "heal":
                    return $"{tgt} healed {ev.amount}.";
                case "armor_gain":
                    return $"{tgt} gained {ev.amount} armor.";
                case "attack_buff":
                    return $"{tgt} gained {ev.amount} attack.";
                case "shield_block":
                    return $"{tgt} blocked damage with shield.";
                case "status_applied":
                    return $"Status applied to {tgt}.";
                case "status_expired":
                    return $"Status expired on {tgt}.";
                case "death":
                case "card_destroyed":
                    return $"{(ev.targetSeatIndex == localIndex ? "Your unit" : "Enemy unit")} died.";
                case "card_attack":
                    return $"{src} declared an attack on {tgt}.";
                case "skill_begin":
                    return $"{src} used {ev.abilityId}.";
                default:
                    var amt = ev.amount != 0 ? $" ({ev.amount})" : string.Empty;
                    return $"{ev.kind}{amt}";
            }
        }

        // ---- UI rendering ----

        private void Render(bool isVictory, string statsBlock, List<string> history, bool allowRematch)
        {
            _allowRematch = allowRematch;
            EnsureUiBuilt();

            // Fill legacy serialized texts if they exist (so wired prefabs still show something).
            if (resultText != null)
            {
                resultText.text = isVictory ? "VICTORY!" : "DEFEAT!";
                resultText.color = isVictory ? VictoryColor : DefeatColor;
            }
            if (statsText != null)
            {
                statsText.text = statsBlock;
            }

            if (_builtTitle != null)
            {
                _builtTitle.text = isVictory ? "VICTORY!" : "DEFEAT!";
                _builtTitle.color = isVictory ? VictoryColor : DefeatColor;
            }
            if (_builtStats != null)
            {
                _builtStats.text = statsBlock;
            }

            PopulateHistory(history);

            if (_rematchButton != null)
            {
                _rematchButton.gameObject.SetActive(_allowRematch);
            }

            SetVisible(true);
            GameLogger.Info("UI", isVictory ? "Match won!" : "Match lost!");
        }

        private void EnsureUiBuilt()
        {
            if (_uiBuilt)
            {
                return;
            }
            _uiBuilt = true;

            var canvasGo = new GameObject("SummaryCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 6000;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Dim translucent backdrop (also swallows clicks behind the panel).
            var dim = NewImage("Dim", canvasGo.transform, BackdropColor);
            Stretch(dim.rectTransform);

            // Centered panel.
            var panel = NewImage("Panel", canvasGo.transform, PanelColor);
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0.5f, 0.5f);
            prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(880, 1480);

            // Title.
            _builtTitle = NewText("Title", panel.transform, "RESULT", 72, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(820, 110));

            // Stats block.
            _builtStats = NewText("Stats", panel.transform, string.Empty, 30, TextAnchor.UpperLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -390f), new Vector2(800, 360));
            _builtStats.color = StatsColor;
            _builtStats.supportRichText = true;

            // History header.
            NewText("HistoryHeader", panel.transform, "BATTLE LOG", 30, TextAnchor.MiddleLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-300f, -600f), new Vector2(800, 50));

            // Scrollable history list.
            var scrollGo = NewImage("HistoryScroll", panel.transform, new Color(0f, 0f, 0f, 0.30f));
            var srt = scrollGo.rectTransform;
            srt.anchorMin = new Vector2(0.5f, 0.5f);
            srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(820, 620);
            srt.anchoredPosition = new Vector2(0f, -100f);
            var scrollRect = scrollGo.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollGo.gameObject.AddComponent<RectMask2D>();

            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(scrollGo.transform, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = content;
            scrollRect.viewport = srt;
            _historyContainer = content;

            // Rematch button.
            _rematchButton = NewButton("Rematch", panel.transform, "REMATCH", RematchColor,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-220f, 90f), new Vector2(380, 120));
            _rematchButton.onClick.AddListener(OnRematchClicked);

            // Back-to-menu button.
            _builtMenuButton = NewButton("Menu", panel.transform, "BACK TO MENU", MenuColor,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(220f, 90f), new Vector2(380, 120));
            _builtMenuButton.onClick.AddListener(OnMenuClicked);
        }

        private void PopulateHistory(List<string> history)
        {
            if (_historyContainer == null)
            {
                return;
            }

            // Clear any previous rows.
            for (int i = _historyContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(_historyContainer.GetChild(i).gameObject);
            }

            if (history == null)
            {
                return;
            }

            for (int i = 0; i < history.Count; i++)
            {
                var rowColor = (i % 2 == 0) ? HistoryRowEven : HistoryRowOdd;
                var rowImg = NewImage("Row", _historyContainer, rowColor);
                var le = rowImg.gameObject.AddComponent<LayoutElement>();
                le.minHeight = 56;

                var label = NewText("Line", rowImg.transform, $"{i + 1}. {history[i]}", 24, TextAnchor.MiddleLeft,
                    new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(14f, 0f), new Vector2(-28f, 50f));
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.rectTransform.anchorMin = new Vector2(0f, 0f);
                label.rectTransform.anchorMax = new Vector2(1f, 1f);
                label.rectTransform.offsetMin = new Vector2(14f, 4f);
                label.rectTransform.offsetMax = new Vector2(-14f, -4f);
            }
        }

        // ---- Button handlers ----

        private void OnRematchClicked()
        {
            // Single-player: reloading the battle scene re-runs GameplayPresenter3D.Start(),
            // which spins up a fresh ServerSinglePlayerCoordinator match using the deck that
            // is still cached in GamePlayStateManager. We must re-assert local mode because the
            // previous SP match flipped the session to "online" while it was live.
            if (_allowRematch)
            {
                GameModeManager.Instance?.SetLocalMode();
                SceneBootstrap.LoadBattle();
                return;
            }

            // PvP: a rematch needs a fresh server reservation from the lobby, which can't be
            // done from the battle scene — route back to the menu instead.
            SceneBootstrap.LoadMenu();
        }

        private void OnMenuClicked()
        {
            SceneBootstrap.LoadMenu();
        }

        // ---- Helpers ----

        /// <summary>
        /// Rematch (scene reload restarts the match) is only safe for single-player vs AI.
        /// The SP coordinator instance lives in the battle scene and is only "active" for an
        /// SP match, so it is a reliable signal even after it flipped the session to online.
        /// </summary>
        private static bool ResolveAllowRematch()
        {
            return ServerSinglePlayerCoordinator.Instance != null &&
                   ServerSinglePlayerCoordinator.Instance.IsActive;
        }

        private static bool IsDamageKind(string kind)
        {
            switch ((kind ?? string.Empty).ToLowerInvariant())
            {
                case "card_damage":
                case "card_counterattack":
                case "hero_damage":
                    return true;
                default:
                    return false;
            }
        }

        private static string DescribeEndReason(DuelEndReason reason)
        {
            switch (reason)
            {
                case DuelEndReason.EnemyHeroDefeated:
                    return "Enemy hero defeated";
                case DuelEndReason.LocalHeroDefeated:
                    return "Your hero defeated";
                case DuelEndReason.OpponentDisconnected:
                    return "Opponent disconnected";
                default:
                    return "Match ended";
            }
        }

        private static PlayerSnapshotDto GetPlayer(DuelSnapshotDto snapshot, int index)
        {
            if (snapshot?.players == null || index < 0 || index >= snapshot.players.Length)
            {
                return null;
            }
            return snapshot.players[index];
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null)
            {
                return;
            }
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        private void OnDestroy()
        {
            if (menuButton != null)
            {
                menuButton.onClick.RemoveListener(OnMenuClicked);
            }
        }

        private static void DisableRaycast(TextMeshProUGUI text)
        {
            if (text != null)
            {
                text.raycastTarget = false;
            }
        }

        // ---- tiny uGUI builders (mirrors SinglePlayerDeckSelectOverlay) ----

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Text NewText(string name, Transform parent, string content, int size, TextAnchor anchor,
            Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size2)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<Text>();
            txt.text = content;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = size;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = anchor;
            txt.color = Color.white;
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = txt.rectTransform;
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size2;
            rt.anchoredPosition = pos;
            return txt;
        }

        private static Button NewButton(string name, Transform parent, string label, Color color,
            Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
        {
            var img = NewImage(name, parent, color);
            var rt = img.rectTransform;
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            NewText("Label", img.transform, label, 34, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return btn;
        }
    }
}
