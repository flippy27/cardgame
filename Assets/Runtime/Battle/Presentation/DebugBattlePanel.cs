using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// In-battle DEBUG/TEST panel for manipulating the REAL server match to test mechanics + animations.
    /// Built ENTIRELY in code on its own top-most overlay canvas (no scene editing, no prefab). Every button
    /// sends a gated debug action through MatchSignalRCoordinator.DebugActionAsync, which the server applies
    /// to the authoritative MatchEngine and broadcasts as a normal MatchSnapshot — so the client animates via
    /// the usual battle-event pipeline (same as PlayCard).
    ///
    /// Visibility: EDITOR / development builds only (auto-instantiated via RuntimeInitializeOnLoadMethod).
    /// It is also inert unless the SERVER has match debug enabled (Development env or Debug:EnableMatchDebug
    /// =true) — otherwise the hub throws "debug_disabled" and the action is a logged no-op.
    ///
    /// Toggle: tap the on-screen "DBG" button (top-left) or press F9.
    ///
    /// Status kind ints MUST match the server StatusEffectKind:
    /// poison0 stun1 shield2 enrageCooldown3 burn4 regeneration5 paralyze6 confuse7 silence8
    /// vulnerable9 weaken10 ward11.
    /// </summary>
    public sealed class DebugBattlePanel : MonoBehaviour
    {
        private const int SortingOrder = 32750; // just below the loading overlay, above HUD/battle canvases.

        // Selectable status effects with their server StatusEffectKind int.
        private static readonly (string Label, int Kind)[] Statuses =
        {
            ("Poison", 0), ("Stun", 1), ("Shield", 2), ("Burn", 4), ("Regen", 5),
            ("Paralyze", 6), ("Confuse", 7), ("Silence", 8), ("Vulnerable", 9), ("Weaken", 10), ("Ward", 11),
        };

        private GameObject _panel;
        private TextMeshProUGUI _targetLabel;
        private readonly List<(string runtimeId, string label)> _targets = new();
        private int _targetIndex;

        // ── Auto-instantiation (editor / dev builds only) ──────────────────────────
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindFirstObjectByType<DebugBattlePanel>() != null)
            {
                return;
            }
            var go = new GameObject("DebugBattlePanel");
            DontDestroyOnLoad(go);
            go.AddComponent<DebugBattlePanel>();
        }
#endif

        private void Awake()
        {
            BuildToggleCanvas();
        }

        private void Update()
        {
            if (WasToggleKeyPressed() && _panel != null)
            {
                _panel.SetActive(!_panel.activeSelf);
                if (_panel.activeSelf)
                {
                    RefreshTargets();
                }
            }
        }

        // F9 toggles the panel (new Input System only — the project's active backend).
        private static bool WasToggleKeyPressed()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.f9Key.wasPressedThisFrame;
        }

        // === UI construction ===

        private void BuildToggleCanvas()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // Small "DBG" toggle pinned to the top-left.
            var toggle = NewButton("DBG", transform, new Color(0.6f, 0.15f, 0.15f, 0.9f), () =>
            {
                _panel.SetActive(!_panel.activeSelf);
                if (_panel.activeSelf)
                {
                    RefreshTargets();
                }
            });
            var trt = toggle.GetComponent<RectTransform>();
            trt.anchorMin = trt.anchorMax = new Vector2(0f, 1f);
            trt.pivot = new Vector2(0f, 1f);
            trt.anchoredPosition = new Vector2(12f, -12f);
            trt.sizeDelta = new Vector2(120f, 70f);

            BuildPanel();
            _panel.SetActive(false);
        }

        private void BuildPanel()
        {
            _panel = new GameObject("Panel", typeof(RectTransform));
            var prt = (RectTransform)_panel.transform;
            prt.SetParent(transform, false);
            prt.anchorMin = new Vector2(0f, 0f);
            prt.anchorMax = new Vector2(0f, 1f);
            prt.pivot = new Vector2(0f, 1f);
            prt.anchoredPosition = new Vector2(12f, -92f);
            prt.sizeDelta = new Vector2(360f, -110f);

            var bg = _panel.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.09f, 0.92f);

            // Vertical layout so we don't have to position every row by hand.
            var layout = _panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Header("HERO");
            Row(("HP+ P1", () => Send("healHero", seat: 0, amount: 5)),
                ("HP- P1", () => Send("damageHero", seat: 0, amount: 5)));
            Row(("HP+ P2", () => Send("healHero", seat: 1, amount: 5)),
                ("HP- P2", () => Send("damageHero", seat: 1, amount: 5)));

            Header("MANA");
            Row(("Mana+ P1", () => Send("addMana", seat: 0, amount: 1)),
                ("Mana- P1", () => Send("addMana", seat: 0, amount: -1)));
            Row(("Mana+ P2", () => Send("addMana", seat: 1, amount: 1)),
                ("Mana- P2", () => Send("addMana", seat: 1, amount: -1)));

            Header("TURN");
            Row(("Draw P1", () => Send("drawCard", seat: 0)),
                ("Draw P2", () => Send("drawCard", seat: 1)));
            Row(("End Turn", () => Send("endTurn")),
                ("Refresh", RefreshTargets));

            Header("TARGET CARD");
            // Target selector (cycles through live board cards from the current snapshot).
            var sel = NewButton("(no targets)", _panel.transform, new Color(0.2f, 0.25f, 0.35f, 0.95f), CycleTarget);
            _targetLabel = sel.GetComponentInChildren<TextMeshProUGUI>();
            SetButtonHeight(sel, 56f);

            Row(("Damage 5", () => SendTarget("damageCard", amount: 5)),
                ("Heal 5", () => SendTarget("healCard", amount: 5)));
            Row(("Kill", () => SendTarget("killCard")),
                ("Clear Stat", () => SendTarget("clearStatuses")));
            Row(("Force Atk", () => SendTarget("forceAttack")),
                ("", null));

            Header("APPLY STATUS (dur 2)");
            // Status buttons in rows of two.
            for (var i = 0; i < Statuses.Length; i += 2)
            {
                var a = Statuses[i];
                if (i + 1 < Statuses.Length)
                {
                    var b = Statuses[i + 1];
                    Row((a.Label, () => SendTarget("applyStatus", statusKind: a.Kind, amount: 1, duration: 2)),
                        (b.Label, () => SendTarget("applyStatus", statusKind: b.Kind, amount: 1, duration: 2)));
                }
                else
                {
                    Row((a.Label, () => SendTarget("applyStatus", statusKind: a.Kind, amount: 1, duration: 2)),
                        ("", null));
                }
            }
        }

        // === target handling ===

        private void RefreshTargets()
        {
            _targets.Clear();
            var snapshot = MatchSignalRCoordinator.Instance != null ? MatchSignalRCoordinator.Instance.CurrentSnapshot : null;
            if (snapshot?.seats != null)
            {
                foreach (var seat in snapshot.seats)
                {
                    if (seat?.board == null)
                    {
                        continue;
                    }
                    foreach (var slot in seat.board)
                    {
                        var occ = slot != null ? slot.occupant : null;
                        if (occ != null && !string.IsNullOrEmpty(occ.runtimeId))
                        {
                            _targets.Add((occ.runtimeId, $"P{seat.seatIndex + 1} {occ.displayName} ({occ.currentHealth}/{occ.maxHealth})"));
                        }
                    }
                }
            }

            if (_targetIndex >= _targets.Count)
            {
                _targetIndex = 0;
            }
            UpdateTargetLabel();
        }

        private void CycleTarget()
        {
            if (_targets.Count == 0)
            {
                RefreshTargets();
                return;
            }
            _targetIndex = (_targetIndex + 1) % _targets.Count;
            UpdateTargetLabel();
        }

        private void UpdateTargetLabel()
        {
            if (_targetLabel == null)
            {
                return;
            }
            _targetLabel.text = _targets.Count == 0 || _targetIndex >= _targets.Count
                ? "(no targets - Refresh)"
                : _targets[_targetIndex].label;
        }

        private string CurrentTargetRuntimeId()
            => _targets.Count > 0 && _targetIndex < _targets.Count ? _targets[_targetIndex].runtimeId : null;

        // === send ===

        private void SendTarget(string action, int amount = 0, int statusKind = 0, int duration = 0)
        {
            var runtimeId = CurrentTargetRuntimeId();
            if (string.IsNullOrEmpty(runtimeId))
            {
                Debug.LogWarning("[DebugBattlePanel] No target card selected (tap the target button / Refresh).");
                return;
            }
            Send(action, runtimeId: runtimeId, amount: amount, statusKind: statusKind, duration: duration);
        }

        private void Send(string action, int seat = 0, int amount = 0, int value = 0, int duration = 0,
            int slotIndex = 0, int statusKind = 0, string runtimeId = null, string cardId = null)
        {
            var coordinator = MatchSignalRCoordinator.Instance;
            if (coordinator == null)
            {
                Debug.LogWarning("[DebugBattlePanel] No active match coordinator.");
                return;
            }

            _ = coordinator.DebugActionAsync(new MatchDebugRequestDto
            {
                action = action,
                seatIndex = seat,
                amount = amount,
                value = value,
                duration = duration,
                slotIndex = slotIndex,
                statusKind = statusKind,
                runtimeId = runtimeId,
                cardId = cardId,
            });
        }

        // === UI helpers ===

        private void Header(string text)
        {
            var go = new GameObject("Header", typeof(RectTransform));
            go.transform.SetParent(_panel.transform, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 30f;
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 24f;
            label.color = new Color(0.55f, 0.8f, 1f, 1f);
            label.alignment = TextAlignmentOptions.Left;
        }

        // A horizontal row of (up to) two buttons. Pass an empty label + null action for a spacer.
        private void Row((string label, System.Action action) left, (string label, System.Action action) right)
        {
            var row = new GameObject("Row", typeof(RectTransform));
            row.transform.SetParent(_panel.transform, false);
            var le = row.AddComponent<LayoutElement>();
            le.minHeight = 56f;
            var hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 4f;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = true;
            hl.childForceExpandHeight = true;

            MakeRowButton(row.transform, left);
            MakeRowButton(row.transform, right);
        }

        private void MakeRowButton(Transform parent, (string label, System.Action action) spec)
        {
            if (string.IsNullOrEmpty(spec.label) || spec.action == null)
            {
                // Spacer so the surviving button keeps half-width.
                var spacer = new GameObject("Spacer", typeof(RectTransform));
                spacer.transform.SetParent(parent, false);
                spacer.AddComponent<LayoutElement>();
                return;
            }
            NewButton(spec.label, parent, new Color(0.18f, 0.2f, 0.28f, 0.95f), spec.action);
        }

        private static void SetButtonHeight(GameObject button, float height)
        {
            var le = button.GetComponent<LayoutElement>() ?? button.AddComponent<LayoutElement>();
            le.minHeight = height;
        }

        private static GameObject NewButton(string label, Transform parent, Color color, System.Action onClick)
        {
            var go = new GameObject("Button", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            var button = go.AddComponent<Button>();
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 22f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return go;
        }
    }
}
