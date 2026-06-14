using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Flippy.CardDuelMobile.UI.DeckBuilding
{
    /// <summary>
    /// Bottom tab bar for the deck-builder shell: a segmented row of tabs (Decks / Create / Salvage)
    /// pinned to the bottom of the screen. Kenney-skinned via <see cref="KenneyUiSkin.SkinTab"/> (green
    /// = selected, grey = unselected). Built entirely in code; call <see cref="Create"/>.
    ///
    /// Fires <see cref="OnTabSelected"/> with the selected tab index when the user taps a tab.
    /// </summary>
    public sealed class DeckBuilderTabBar : MonoBehaviour
    {
        public const float BarHeight = 184f;

        private Button[] _buttons;
        private Image[] _bgImages;
        private TextMeshProUGUI[] _labels;
        private int _selected = -1;

        /// <summary>Fired with the tab index (0-based) when a tab is selected by the user.</summary>
        public event Action<int> OnTabSelected;

        public int SelectedIndex => _selected;

        /// <summary>Creates a bottom tab bar with the given tab captions under <paramref name="parent"/>
        /// (typically the screen root). Anchored across the bottom edge.</summary>
        public static DeckBuilderTabBar Create(Transform parent, params string[] tabs)
        {
            var go = new GameObject("DeckBuilderTabBar", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(0f, BarHeight);

            // Bar background (a framed strip behind the tabs).
            var barBg = go.GetComponent<Image>();
            barBg.color = new Color(0.10f, 0.09f, 0.12f, 1f);
            barBg.raycastTarget = true;
            if (KenneyUiSkin.Available) KenneyUiSkin.SkinInsetImage(barBg);

            var bar = go.AddComponent<DeckBuilderTabBar>();
            bar.BuildTabs(rt, tabs);
            return bar;
        }

        private void BuildTabs(RectTransform root, string[] tabs)
        {
            int n = tabs != null ? tabs.Length : 0;
            _buttons = new Button[n];
            _bgImages = new Image[n];
            _labels = new TextMeshProUGUI[n];

            float pad = 12f;
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                var go = new GameObject($"Tab_{tabs[i]}", typeof(RectTransform), typeof(Image), typeof(Button));
                var rt = (RectTransform)go.transform;
                rt.SetParent(root, false);
                // Even horizontal split across the bar width.
                float min = i / (float)n;
                float max = (i + 1) / (float)n;
                rt.anchorMin = new Vector2(min, 0f);
                rt.anchorMax = new Vector2(max, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = new Vector2(pad, pad);
                rt.offsetMax = new Vector2(-pad, -pad);

                var img = go.GetComponent<Image>();
                _bgImages[i] = img;

                var lblGo = new GameObject("Label", typeof(RectTransform));
                var lt = lblGo.AddComponent<TextMeshProUGUI>();
                lblGo.transform.SetParent(rt, false);
                KenneyUiSkin.Fill(lt);
                lt.text = tabs[i];
                lt.alignment = TextAlignmentOptions.Center;
                lt.color = Color.white;
                lt.raycastTarget = false;
                DeckBuilderTextScale.Apply(lt, DeckBuilderTextScale.Role.Button);
                lt.fontSize = 60f; // tab captions read larger than ordinary buttons on the taller bar
                _labels[i] = lt;

                var btn = go.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.transition = Selectable.Transition.None; // visual state driven by SkinTab
                btn.onClick.AddListener(() => Select(idx, notify: true));
                _buttons[i] = btn;

                KenneyUiSkin.SkinTab(img, selected: false);
            }
        }

        /// <summary>Selects a tab and updates the visual state. Set <paramref name="notify"/> false to
        /// set the initial tab without firing the event.</summary>
        public void Select(int index, bool notify)
        {
            if (_buttons == null || index < 0 || index >= _buttons.Length) return;
            _selected = index;
            for (int i = 0; i < _buttons.Length; i++)
            {
                bool sel = i == index;
                KenneyUiSkin.SkinTab(_bgImages[i], sel);
                if (_labels[i] != null)
                    _labels[i].fontStyle = sel ? FontStyles.Bold : FontStyles.Normal;
            }
            if (notify) OnTabSelected?.Invoke(index);
        }
    }
}
