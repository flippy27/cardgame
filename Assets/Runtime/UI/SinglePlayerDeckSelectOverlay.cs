using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Networking.ApiClients;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Code-built, self-contained deck picker shown before the single-player (vs AI) battle.
    /// No prefab/editor wiring required: it builds its own full-screen Canvas, lists the
    /// player's server decks, and reports the chosen <see cref="DeckDto"/> through a callback.
    /// </summary>
    public sealed class SinglePlayerDeckSelectOverlay : MonoBehaviour
    {
        private Action<DeckDto> _onConfirm;
        private Action _onCancel;
        private DeckManagementService _deckService;

        private Transform _listContainer;
        private Text _statusText;
        private Button _playButton;
        private DeckDto _selected;
        private readonly List<(DeckDto deck, Image bg, Button btn)> _items = new();

        private static readonly Color PanelColor = new(0.08f, 0.09f, 0.13f, 0.97f);
        private static readonly Color ItemColor = new(0.16f, 0.18f, 0.24f, 1f);
        private static readonly Color ItemSelected = new(0.20f, 0.45f, 0.30f, 1f);
        private static readonly Color Accent = new(0.25f, 0.62f, 0.42f, 1f);

        /// <summary>Spawns the overlay and returns it. Confirm fires with the selected deck; cancel with nothing.</summary>
        public static SinglePlayerDeckSelectOverlay Show(Action<DeckDto> onConfirm, Action onCancel = null)
        {
            var go = new GameObject("SinglePlayerDeckSelectOverlay");
            var overlay = go.AddComponent<SinglePlayerDeckSelectOverlay>();
            overlay._onConfirm = onConfirm;
            overlay._onCancel = onCancel;
            return overlay;
        }

        private void Start()
        {
            BuildUi();
            LoadDecks();
        }

        private void BuildUi()
        {
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Dim backdrop (also swallows clicks).
            var dim = NewImage("Dim", canvasGo.transform, new Color(0f, 0f, 0f, 0.6f));
            Stretch(dim.rectTransform);

            // Centered panel.
            var panel = NewImage("Panel", canvasGo.transform, PanelColor);
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0.5f, 0.5f);
            prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(820, 1180);

            NewText("Title", panel.transform, "SELECT YOUR DECK", 52, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(740, 90));

            _statusText = NewText("Status", panel.transform, "Loading decks...", 30, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(740, 50));
            _statusText.color = new Color(0.7f, 0.75f, 0.85f, 1f);

            // Scrollable list.
            var scrollGo = NewImage("Scroll", panel.transform, new Color(0f, 0f, 0f, 0.25f));
            var srt = scrollGo.rectTransform;
            srt.anchorMin = new Vector2(0.5f, 0.5f);
            srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(740, 760);
            srt.anchoredPosition = new Vector2(0f, 10f);
            var scrollRect = scrollGo.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollGo.gameObject.AddComponent<RectMask2D>();

            var content = new GameObject("Content").AddComponent<RectTransform>();
            content.SetParent(scrollGo.transform, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 14;
            vlg.padding = new RectOffset(16, 16, 16, 16);
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = content;
            scrollRect.viewport = srt;
            _listContainer = content;

            // Play button.
            _playButton = NewButton("Play", panel.transform, "BATTLE", Accent,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-200f, 90f), new Vector2(340, 110));
            _playButton.onClick.AddListener(OnPlay);
            _playButton.interactable = false;

            // Back button.
            var back = NewButton("Back", panel.transform, "BACK", new Color(0.35f, 0.18f, 0.20f, 1f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(200f, 90f), new Vector2(340, 110));
            back.onClick.AddListener(OnCancel);
        }

        private async void LoadDecks()
        {
            _deckService = ServiceLocator.Get<DeckManagementService>();
            if (_deckService == null)
            {
                SetStatus("Deck service unavailable.", error: true);
                return;
            }

            try
            {
                var decks = await _deckService.GetPlayerDecksAsync();
                if (decks == null || decks.Count == 0)
                {
                    SetStatus("No decks found. Build one first.", error: true);
                    return;
                }

                PopulateList(decks);
                SetStatus("Choose a deck, then Battle.");
            }
            catch (Exception ex)
            {
                SetStatus($"Error: {ex.Message}", error: true);
                Debug.LogError($"[SPDeckSelect] Failed to load decks: {ex}");
            }
        }

        private void PopulateList(List<DeckDto> decks)
        {
            foreach (var deck in decks)
            {
                var rowImg = NewImage("DeckItem", _listContainer, ItemColor);
                var le = rowImg.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 120;
                le.minHeight = 120;

                var name = NewText("Name", rowImg.transform, deck.Name, 36, TextAnchor.MiddleLeft,
                    new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(28f, 18f), new Vector2(-40f, 48f));
                name.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                name.rectTransform.anchorMax = new Vector2(1f, 0.5f);

                var count = NewText("Count", rowImg.transform, $"{deck.CardCount} cards", 26, TextAnchor.MiddleLeft,
                    new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(28f, -30f), new Vector2(-40f, 40f));
                count.color = new Color(0.65f, 0.70f, 0.80f, 1f);
                count.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                count.rectTransform.anchorMax = new Vector2(1f, 0.5f);

                var btn = rowImg.gameObject.AddComponent<Button>();
                btn.targetGraphic = rowImg;
                var captured = deck;
                var capturedImg = rowImg;
                btn.onClick.AddListener(() => SelectDeck(captured, capturedImg));

                _items.Add((deck, rowImg, btn));
            }

            // Default to active deck (or first).
            var def = decks.Find(d => d.isActive) ?? decks[0];
            var defImg = _items.Find(i => i.deck.deckId == def.deckId).bg;
            SelectDeck(def, defImg);
        }

        private void SelectDeck(DeckDto deck, Image rowImg)
        {
            _selected = deck;
            foreach (var item in _items)
            {
                item.bg.color = item.deck.deckId == deck.deckId ? ItemSelected : ItemColor;
            }
            if (_playButton != null)
            {
                _playButton.interactable = true;
            }
        }

        private void OnPlay()
        {
            if (_selected == null)
            {
                SetStatus("Select a deck first.", error: true);
                return;
            }
            var deck = _selected;
            var cb = _onConfirm;
            Destroy(gameObject);
            cb?.Invoke(deck);
        }

        private void OnCancel()
        {
            var cb = _onCancel;
            Destroy(gameObject);
            cb?.Invoke();
        }

        private void SetStatus(string msg, bool error = false)
        {
            if (_statusText == null) return;
            _statusText.text = msg;
            _statusText.color = error ? new Color(0.95f, 0.45f, 0.45f, 1f) : new Color(0.7f, 0.75f, 0.85f, 1f);
        }

        // ---- tiny uGUI builders ----

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
            NewText("Label", img.transform, label, 38, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return btn;
        }
    }
}
