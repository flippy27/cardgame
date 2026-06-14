using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;
using Flippy.CardDuelMobile.Networking.ApiClients;
using Flippy.CardDuelMobile.Battle;
using TMPro;

namespace Flippy.CardDuelMobile.UI.DeckBuilding
{
    /// <summary>
    /// CARD LEVELING / upgrade-tree view. Opened when the player taps an OWNED card in the deck-builder
    /// "My Cards" tab. Shows the big framed card (composite + stat badges, reusing the in-game system)
    /// with its current ★level, the applied stat deltas + derived skills, and the NEXT level: its
    /// option(s) (presented as selectable buttons describing each), the material REQUIREMENTS
    /// (icon + have/need per item, red when short — same Resources/Art/items/{key} lookup as
    /// <see cref="CardCraftOverviewPanel"/>), and a "Level Up" button (disabled at cap / can't afford /
    /// no option chosen when a choice is required). On apply → POST, then refresh the state.
    ///
    /// Built entirely in code (no prefab wiring), following the KenneyUiSkin pattern of the other
    /// deck-builder panels. The host (<see cref="DeckBuilderShell"/>) creates one instance, wires
    /// <see cref="OnLeveledUp"/>, and calls <see cref="Show"/>.
    /// </summary>
    public sealed class CardUpgradeView : MonoBehaviour
    {
        private static readonly string[] RarityNames = { "Common", "Rare", "Epic", "Legendary" };

        // Same rarity palette as CardCraftOverviewPanel / CardCollectionItem.
        private static readonly Color[] RarityColors =
        {
            new Color(0.65f, 0.65f, 0.65f), // Common    — grey
            new Color(0.20f, 0.50f, 1.00f), // Rare      — blue
            new Color(0.60f, 0.10f, 0.90f), // Epic      — purple
            new Color(1.00f, 0.78f, 0.10f), // Legendary — gold
        };

        private static readonly Color OkColor = new Color(0.2f, 0.85f, 0.3f);
        private static readonly Color ShortColor = new Color(0.95f, 0.35f, 0.35f);
        private static readonly Color OptionSelected = new Color(0.20f, 0.85f, 1.00f, 1f);

        // ---- UI (built in code) ----
        private RectTransform _root;
        private Image _cardArtImage;
        private RectTransform _statsOverlay;
        private GameObject _levelBadge;
        private TextMeshProUGUI _levelBadgeText;
        private TextMeshProUGUI _nameText;
        private TextMeshProUGUI _rarityText;
        private TextMeshProUGUI _currentText;       // level + applied deltas + skills summary
        private TextMeshProUGUI _nextHeader;
        private Transform _optionsContainer;        // next-level option buttons
        private Transform _reqListContainer;        // material requirement rows
        private Button _levelUpButton;
        private TextMeshProUGUI _levelUpButtonText;
        private Button _closeButton;
        private TextMeshProUGUI _statusText;
        private GameObject _loadingOverlay;
        private bool _built;

        // ---- Data ----
        private string _playerCardId;
        private CardUpgradeApiClient.PlayerCardUpgradeStateDto _state;
        private Dictionary<string, InventoryApiClient.PlayerItemDto> _inventory = new();
        private string _selectedOptionId;          // chosen next-level option (null = none picked yet)
        private readonly List<Button> _optionButtons = new();
        private readonly List<string> _optionButtonIds = new();

        private CardUpgradeApiClient _upgradeApi;
        private InventoryService _inventoryService;
        private CardCatalogCache _catalog;

        /// <summary>Fired after a successful level-up (so the host can invalidate caches / refresh the
        /// owned grid + dust). Carries the playerCardId and the new state.</summary>
        public event Action<string, CardUpgradeApiClient.PlayerCardUpgradeStateDto> OnLeveledUp;

        /// <summary>Show the leveling view for an owned card instance. Fetches the state from the server.</summary>
        public void Show(string playerCardId)
        {
            _playerCardId = playerCardId;
            _selectedOptionId = null;
            gameObject.SetActive(true);
            EnsureBuilt();
            LoadStateAsync();
        }

        public void Hide() => gameObject.SetActive(false);

        private void ResolveServices()
        {
            if (_upgradeApi == null && (!ServiceLocator.TryResolve(out _upgradeApi) || _upgradeApi == null))
                _upgradeApi = new CardUpgradeApiClient();
            if (_inventoryService == null) ServiceLocator.TryResolve(out _inventoryService);
            if (_catalog == null) ServiceLocator.TryResolve(out _catalog);
        }

        // ---- Build the UI once, in code (mirrors CardCraftOverviewPanel layout) ----
        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            var canvas = GetComponentInParent<Canvas>();
            var canvasRT = canvas != null ? (RectTransform)canvas.rootCanvas.transform : null;

            _root = transform as RectTransform;
            if (_root == null) { Debug.LogError("[CardUpgradeView] Root is not a RectTransform."); return; }
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;
            if (canvasRT != null)
            {
                _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
                _root.pivot = new Vector2(0.5f, 0.5f);
                _root.anchoredPosition = Vector2.zero;
                _root.sizeDelta = canvasRT.rect.size;
            }

            // Dimmer behind everything.
            var dim = new GameObject("Dimmer", typeof(RectTransform), typeof(Image));
            var dimRT = (RectTransform)dim.transform;
            dimRT.SetParent(_root, false);
            KenneyUiSkin.Fill(dimRT);
            var dimImg = dim.GetComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.72f);
            dimImg.raycastTarget = true;

            if (KenneyUiSkin.Available) KenneyUiSkin.EnsureWindowBackdrop(this);

            // ---- Big framed card (left, vertically centred) ----
            var cardGo = new GameObject("BigCard", typeof(RectTransform), typeof(Image));
            var cardRT = (RectTransform)cardGo.transform;
            cardRT.SetParent(_root, false);
            cardRT.anchorMin = new Vector2(0f, 0.5f);
            cardRT.anchorMax = new Vector2(0f, 0.5f);
            cardRT.pivot = new Vector2(0f, 0.5f);
            cardRT.anchoredPosition = new Vector2(70f, 30f);
            cardRT.sizeDelta = new Vector2(520f, 780f);
            _cardArtImage = cardGo.GetComponent<Image>();
            _cardArtImage.preserveAspect = false; // 2:3 so badges land on the frame sockets
            _cardArtImage.raycastTarget = false;

            var overlayGo = new GameObject("StatsOverlay", typeof(RectTransform));
            _statsOverlay = (RectTransform)overlayGo.transform;
            _statsOverlay.SetParent(cardRT, false);
            KenneyUiSkin.Fill(_statsOverlay);

            // ★level badge (top-left over the card).
            _levelBadge = new GameObject("LevelBadge", typeof(RectTransform), typeof(Image));
            var lvRt = (RectTransform)_levelBadge.transform;
            lvRt.SetParent(cardRT, false);
            lvRt.anchorMin = new Vector2(0f, 1f);
            lvRt.anchorMax = new Vector2(0f, 1f);
            lvRt.pivot = new Vector2(0f, 1f);
            lvRt.anchoredPosition = new Vector2(16f, -16f);
            lvRt.sizeDelta = new Vector2(150f, 60f);
            _levelBadge.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);
            _levelBadge.GetComponent<Image>().raycastTarget = false;
            _levelBadgeText = MakeText("LevelText", lvRt, DeckBuilderTextScale.Role.Label, TextAlignmentOptions.Center);
            KenneyUiSkin.Fill(_levelBadgeText, 6f, 6f, 2f, 2f);
            _levelBadgeText.color = new Color(1f, 0.85f, 0.25f); // gold

            // ---- Name + rarity (right column top) ----
            _nameText = MakeText("NameText", _root, DeckBuilderTextScale.Role.Header, TextAlignmentOptions.TopLeft);
            KenneyUiSkin.SetRect(_nameText, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(660f, -70f), new Vector2(-720f, 90f));
            _nameText.enableWordWrapping = false;
            _nameText.overflowMode = TextOverflowModes.Ellipsis;

            _rarityText = MakeText("RarityText", _root, DeckBuilderTextScale.Role.Label, TextAlignmentOptions.TopLeft);
            KenneyUiSkin.SetRect(_rarityText, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(660f, -160f), new Vector2(-720f, 60f));

            // Current state summary (level + applied deltas + skills).
            _currentText = MakeText("CurrentText", _root, DeckBuilderTextScale.Role.Status, TextAlignmentOptions.TopLeft);
            KenneyUiSkin.SetRect(_currentText, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(660f, -224f), new Vector2(-720f, 150f));
            _currentText.enableWordWrapping = true;
            _currentText.richText = true;

            // ---- Next-level header ----
            _nextHeader = MakeText("NextHeader", _root, DeckBuilderTextScale.Role.Label, TextAlignmentOptions.TopLeft);
            _nextHeader.text = "Next Level";
            KenneyUiSkin.SetRect(_nextHeader, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(660f, -384f), new Vector2(-720f, 52f));

            // ---- Options row (selectable buttons describing each next-level option) ----
            var optGo = new GameObject("OptionsContainer", typeof(RectTransform));
            var optRT = (RectTransform)optGo.transform;
            optRT.SetParent(_root, false);
            optRT.anchorMin = new Vector2(0f, 1f);
            optRT.anchorMax = new Vector2(1f, 1f);
            optRT.pivot = new Vector2(0.5f, 1f);
            optRT.offsetMin = new Vector2(660f, 0f);
            optRT.offsetMax = new Vector2(-60f, -440f);
            optRT.sizeDelta = new Vector2(optRT.sizeDelta.x, 150f);
            _optionsContainer = optRT;
            var hlg = optGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14f;
            hlg.childAlignment = TextAnchor.UpperLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            // ---- Requirements header + list ----
            var reqHeader = MakeText("RequirementsHeader", _root, DeckBuilderTextScale.Role.Label, TextAlignmentOptions.TopLeft);
            reqHeader.text = "Requirements";
            KenneyUiSkin.SetRect(reqHeader, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(660f, -604f), new Vector2(-720f, 52f));

            var listGo = new GameObject("RequirementsList", typeof(RectTransform));
            var listRT = (RectTransform)listGo.transform;
            listRT.SetParent(_root, false);
            listRT.anchorMin = new Vector2(0f, 0f);
            listRT.anchorMax = new Vector2(1f, 1f);
            listRT.pivot = new Vector2(0.5f, 1f);
            listRT.offsetMin = new Vector2(660f, 240f);   // left, above the action bar
            listRT.offsetMax = new Vector2(-60f, -664f);  // right, under the requirements header
            _reqListContainer = listRT;
            KenneyUiSkin.EnsureVerticalList(_reqListContainer, spacing: 12f, padding: 8);

            // ---- Action bar: Level Up (primary) + Close ----
            _levelUpButton = MakeButton("LevelUpButton", _root, KenneyUiSkin.ButtonStyle.Primary, "Level Up", out _levelUpButtonText);
            KenneyUiSkin.SetRect(_levelUpButton, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(250f, 70f), new Vector2(440f, 132f));
            _levelUpButton.onClick.AddListener(OnLevelUpClicked);

            _closeButton = MakeButton("CloseButton", _root, KenneyUiSkin.ButtonStyle.Nav, "Close", out _);
            KenneyUiSkin.SetRect(_closeButton, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-250f, 70f), new Vector2(400f, 132f));
            _closeButton.onClick.AddListener(Hide);

            var xClose = MakeButton("CloseX", _root, KenneyUiSkin.ButtonStyle.Icon, "X", out _);
            KenneyUiSkin.SetRect(xClose, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-40f, -40f), new Vector2(140f, 120f));
            xClose.onClick.AddListener(Hide);

            _statusText = MakeText("StatusText", _root, DeckBuilderTextScale.Role.Status, TextAlignmentOptions.Center);
            KenneyUiSkin.SetRect(_statusText, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 224f), new Vector2(900f, 50f));

            _loadingOverlay = new GameObject("LoadingOverlay", typeof(RectTransform), typeof(Image));
            var loRT = (RectTransform)_loadingOverlay.transform;
            loRT.SetParent(_root, false);
            KenneyUiSkin.Fill(loRT);
            _loadingOverlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
            _loadingOverlay.SetActive(false);

            if (KenneyUiSkin.Available) KenneyUiSkin.ApplyFontUnder(this);
        }

        // ---- Data loading ----

        private async void LoadStateAsync()
        {
            ResolveServices();
            if (_upgradeApi == null) { ShowStatus("Upgrade service unavailable."); return; }
            SetLoading(true);
            ShowStatus(string.Empty);
            try
            {
                var stateTask = _upgradeApi.FetchState(_playerCardId);
                // Inventory drives have/need + affordability; reuse the cached map.
                Task<Dictionary<string, InventoryApiClient.PlayerItemDto>> invTask =
                    _inventoryService != null ? _inventoryService.GetInventoryAsync() : Task.FromResult(_inventory);
                await Task.WhenAll(stateTask, invTask);
                _state = stateTask.Result;
                _inventory = invTask.Result ?? new Dictionary<string, InventoryApiClient.PlayerItemDto>();

                if (_state == null) { ShowStatus("Could not load card state."); return; }
                _selectedOptionId = null;
                Rebuild();
            }
            catch (Exception ex)
            {
                ShowStatus($"Error: {ex.Message}");
                Debug.LogError($"[CardUpgradeView] LoadState: {ex}");
            }
            finally { SetLoading(false); }
        }

        private void Rebuild()
        {
            if (_state == null) return;

            if (_nameText != null) _nameText.text = _state.displayName ?? _state.cardId;
            if (_rarityText != null)
            {
                int idx = Mathf.Clamp(_state.cardRarity, 0, RarityNames.Length - 1);
                _rarityText.text = RarityNames[idx];
                _rarityText.color = RarityColors[Mathf.Clamp(_state.cardRarity, 0, RarityColors.Length - 1)];
            }

            if (_levelBadgeText != null) _levelBadgeText.text = $"★{Mathf.Max(1, _state.currentLevel)}";

            ApplyBigCard(_state.cardId);
            BuildCurrentSummary();
            BuildNextLevel();
        }

        // Big framed composite + stat badges (the EFFECTIVE stats), mirroring CardCraftOverviewPanel.
        private void ApplyBigCard(string cardId)
        {
            if (_catalog == null) ServiceLocator.TryResolve(out _catalog);
            ServerCardDefinition d = null;
            if (_catalog != null) _catalog.TryGetCard(cardId, out d);

            if (_cardArtImage != null)
            {
                Sprite spr = null;
                if (d != null)
                    spr = CardArtLibrary.GetCardComposite(cardId, d.cardType, d.cardRarity, d.cardFaction, d.unitType, d.armor > 0, "hand");
                if (spr == null || spr == CardArtLibrary.Missing)
                    spr = CardArtLibrary.GetCardArt(cardId);
                if (spr != null && spr != CardArtLibrary.Missing)
                {
                    _cardArtImage.sprite = spr;
                    _cardArtImage.color = Color.white;
                }
                else
                {
                    _cardArtImage.sprite = null;
                    _cardArtImage.color = new Color(0.30f, 0.32f, 0.38f);
                }
            }

            // Badges show the EFFECTIVE (leveled) stats from the server state. Mana/unitType come from the
            // catalog (not part of the upgrade state). Force a layout pass first (overlay-rect gotcha).
            if (_statsOverlay != null)
            {
                var dto = new BoardCardDto
                {
                    cardId = cardId,
                    manaCost = d != null ? d.manaCost : 0,
                    attack = _state.effectiveAttack,
                    currentHealth = _state.effectiveHealth,
                    maxHealth = _state.effectiveHealth,
                    armor = _state.effectiveArmor,
                    unitType = d != null ? d.unitType : 0
                };
                Canvas.ForceUpdateCanvases();
                CardStatBadges.Apply(_statsOverlay, dto, isBoard: false);
            }

            if (_levelBadge != null) _levelBadge.transform.SetAsLastSibling();
        }

        // Current level + applied deltas + derived skills.
        private void BuildCurrentSummary()
        {
            if (_currentText == null) return;
            var sb = new StringBuilder();
            sb.Append($"<b>Level {_state.currentLevel} / {_state.maxLevel}</b>\n");
            sb.Append($"ATK {_state.effectiveAttack}  HP {_state.effectiveHealth}");
            if (_state.effectiveArmor > 0) sb.Append($"  ARM {_state.effectiveArmor}");
            sb.Append('\n');

            string deltas = FormatDeltas(_state.appliedAttackDelta, _state.appliedHealthDelta, _state.appliedArmorDelta);
            if (!string.IsNullOrEmpty(deltas)) sb.Append($"<color=#A0C8FF>Applied: {deltas}</color>\n");

            if (_state.skillLevels != null && _state.skillLevels.Length > 0)
            {
                sb.Append("Skills: ");
                for (int i = 0; i < _state.skillLevels.Length; i++)
                {
                    var s = _state.skillLevels[i];
                    if (s == null) continue;
                    if (i > 0) sb.Append(", ");
                    sb.Append($"{PrettyAbility(s.abilityId)} Lv{s.level}");
                    if (s.added) sb.Append(" (new)");
                }
            }
            _currentText.text = sb.ToString();
        }

        private void BuildNextLevel()
        {
            ClearOptions();
            ClearChildren(_reqListContainer);

            // Level cap reached → no next level.
            if (_state.isMaxLevel)
            {
                if (_nextHeader != null) _nextHeader.text = "Max Level Reached";
                var capRow = MakeText("MaxRow", _reqListContainer, DeckBuilderTextScale.Role.Status, TextAlignmentOptions.MidlineLeft);
                capRow.text = "This card is fully leveled.";
                KenneyUiSkin.EnsureRowHeight(capRow, 64f);
                if (_levelUpButton != null) _levelUpButton.interactable = false;
                if (_levelUpButtonText != null) _levelUpButtonText.text = "Maxed";
                return;
            }

            if (_nextHeader != null) _nextHeader.text = $"Next: Level {_state.nextLevel}";

            // Options. 0 → no-choice (auto) level; 1 → single option (auto-selected); 2 → pick one.
            var options = _state.nextLevelOptions ?? Array.Empty<CardUpgradeApiClient.CardUpgradeOptionDto>();
            BuildOptionButtons(options);

            // Requirements.
            var reqs = _state.nextLevelRequirements ?? Array.Empty<CardUpgradeApiClient.CardUpgradeRequirementDto>();
            if (reqs.Length == 0)
            {
                var row = MakeText("NoReqs", _reqListContainer, DeckBuilderTextScale.Role.Status, TextAlignmentOptions.MidlineLeft);
                row.text = "No materials required.";
                KenneyUiSkin.EnsureRowHeight(row, 64f);
            }
            else
            {
                foreach (var req in reqs) BuildOneRequirementRow(req);
            }

            if (KenneyUiSkin.Available) KenneyUiSkin.ApplyFontUnder(this);
            RefreshLevelUpButton();
        }

        private void BuildOptionButtons(CardUpgradeApiClient.CardUpgradeOptionDto[] options)
        {
            if (_optionsContainer == null) return;

            if (options.Length == 0)
            {
                // No-choice level: a static info chip, no selection needed (chosenOptionId stays null).
                _selectedOptionId = null;
                var info = MakeText("NoChoice", _optionsContainer, DeckBuilderTextScale.Role.Status, TextAlignmentOptions.Center);
                info.text = "Automatic upgrade — no choice.";
                return;
            }

            // Single option auto-selects; two options require an explicit pick.
            if (options.Length == 1) _selectedOptionId = options[0]?.id;

            foreach (var opt in options)
            {
                if (opt == null) continue;
                string capturedId = opt.id;
                var btn = MakeButton("Option", _optionsContainer, KenneyUiSkin.ButtonStyle.Nav, DescribeOption(opt), out var lbl);
                if (lbl != null) { lbl.enableWordWrapping = true; lbl.overflowMode = TextOverflowModes.Truncate; }
                btn.onClick.AddListener(() => OnOptionClicked(capturedId));
                _optionButtons.Add(btn);
                _optionButtonIds.Add(capturedId);
            }
            UpdateOptionSelectionVisuals();
        }

        private void OnOptionClicked(string optionId)
        {
            _selectedOptionId = optionId;
            UpdateOptionSelectionVisuals();
            RefreshLevelUpButton();
        }

        private void UpdateOptionSelectionVisuals()
        {
            for (int i = 0; i < _optionButtons.Count; i++)
            {
                var btn = _optionButtons[i];
                if (btn == null) continue;
                bool sel = _optionButtonIds[i] == _selectedOptionId;
                var img = btn.targetGraphic as Image ?? btn.GetComponent<Image>();
                if (img != null)
                    img.color = sel ? OptionSelected : new Color(0.30f, 0.32f, 0.40f, 1f);
            }
        }

        // Human-readable option label, e.g. "+1 ATK", "+2 HP / +1 ARM", "New skill: Immolate",
        // "Upgrade: Strike".
        private static string DescribeOption(CardUpgradeApiClient.CardUpgradeOptionDto opt)
        {
            if (!string.IsNullOrWhiteSpace(opt.displayName)) return opt.displayName;
            switch (opt.kind)
            {
                case 1: // stat
                {
                    string d = FormatDeltas(opt.attackDelta, opt.healthDelta, opt.armorDelta);
                    return string.IsNullOrEmpty(d) ? "Stat change" : d;
                }
                case 2: return $"New skill: {PrettyAbility(opt.addAbilityId)}";
                case 3: return $"Upgrade: {PrettyAbility(opt.upgradeAbilityId)}";
                default: return "Upgrade"; // 0 = fixed/auto bump
            }
        }

        private static string FormatDeltas(int atk, int hp, int arm)
        {
            var parts = new List<string>();
            if (atk != 0) parts.Add($"{(atk > 0 ? "+" : "")}{atk} ATK");
            if (hp != 0) parts.Add($"{(hp > 0 ? "+" : "")}{hp} HP");
            if (arm != 0) parts.Add($"{(arm > 0 ? "+" : "")}{arm} ARM");
            return string.Join(" / ", parts);
        }

        private static string PrettyAbility(string abilityId)
        {
            if (string.IsNullOrWhiteSpace(abilityId)) return "—";
            var s = abilityId.Replace('_', ' ').Trim();
            return s.Length > 0 ? char.ToUpperInvariant(s[0]) + s.Substring(1) : abilityId;
        }

        // One material row: [rarity-tinted icon] [name] ......... [have/need] (red when short). Same
        // icon lookup + layout as CardCraftOverviewPanel.BuildOneRequirementRow.
        private void BuildOneRequirementRow(CardUpgradeApiClient.CardUpgradeRequirementDto req)
        {
            int have = 0;
            if (_inventory.TryGetValue(req.itemTypeKey ?? string.Empty, out var bal)) have = bal?.quantity ?? 0;
            int need = req.quantityRequired;
            bool met = have >= need;

            int rarity = Mathf.Clamp(req.itemTypeRarity, 0, RarityColors.Length - 1);
            Color rarityColor = RarityColors[rarity];

            var rowGo = new GameObject("Req_" + (req.itemTypeKey ?? "?"), typeof(RectTransform), typeof(Image));
            var rowRT = (RectTransform)rowGo.transform;
            rowRT.SetParent(_reqListContainer, false);
            KenneyUiSkin.EnsureRowHeight(rowRT, 84f);
            var rowBg = rowGo.GetComponent<Image>();
            rowBg.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.18f);
            rowBg.raycastTarget = false;

            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(14, 16, 8, 8);
            hlg.spacing = 16f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(rowRT, false);
            var icon = iconGo.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var iconSprite = ResolveMaterialIcon(req);
            if (iconSprite != null) { icon.sprite = iconSprite; icon.color = Color.white; }
            else { icon.sprite = null; icon.color = rarityColor; }
            KenneyUiSkin.SetLayoutElement(icon, preferredWidth: 64f, preferredHeight: 64f);

            var nameText = MakeText("MatName", rowRT, DeckBuilderTextScale.Role.Label, TextAlignmentOptions.MidlineLeft);
            nameText.text = req.itemTypeDisplayName ?? req.itemTypeKey ?? "Material";
            nameText.color = rarityColor;
            nameText.enableWordWrapping = false;
            nameText.overflowMode = TextOverflowModes.Ellipsis;
            KenneyUiSkin.SetLayoutElement(nameText, preferredWidth: 280f, preferredHeight: 64f, flexibleWidth: 1f);

            var qtyText = MakeText("MatQty", rowRT, DeckBuilderTextScale.Role.Label, TextAlignmentOptions.MidlineRight);
            qtyText.text = $"{have}/{need}";
            qtyText.color = met ? OkColor : ShortColor;
            qtyText.enableWordWrapping = false;
            KenneyUiSkin.SetLayoutElement(qtyText, preferredWidth: 150f, preferredHeight: 64f);
        }

        // Best-effort material icon (same lookup as CardCraftOverviewPanel): Resources/Art/items/{key},
        // else the dust stat-icon for dust, else null (caller draws a rarity chip).
        private Sprite ResolveMaterialIcon(CardUpgradeApiClient.CardUpgradeRequirementDto req)
        {
            if (req == null) return null;
            var key = req.itemTypeKey ?? string.Empty;
            var spr = Resources.Load<Sprite>($"Art/items/{key}");
            if (spr != null) return spr;
            if (key.ToLowerInvariant().Contains("dust"))
            {
                var dust = CardArtLibrary.GetStatIconSprite("stat_mana");
                if (dust != null) return dust;
            }
            return null;
        }

        // The Level Up button is enabled only when: not maxed, affordable (server flag), and an option is
        // chosen IF the level requires a choice (2 options). 0 options → no choice; 1 option → auto-picked.
        private void RefreshLevelUpButton()
        {
            if (_levelUpButton == null) return;
            int optionCount = _state.nextLevelOptions?.Length ?? 0;
            bool choiceRequired = optionCount >= 2;
            bool optionChosen = !choiceRequired || !string.IsNullOrEmpty(_selectedOptionId);
            bool affordable = _state.nextLevelAffordable;

            bool canApply = !_state.isMaxLevel && affordable && optionChosen;
            _levelUpButton.interactable = canApply;
            if (_levelUpButtonText != null)
            {
                if (!affordable) _levelUpButtonText.text = "Can't Afford";
                else if (!optionChosen) _levelUpButtonText.text = "Pick an Option";
                else _levelUpButtonText.text = "Level Up";
            }
        }

        private async void OnLevelUpClicked()
        {
            if (_state == null || _state.isMaxLevel) return;
            ResolveServices();
            if (_upgradeApi == null) { ShowStatus("Upgrade service unavailable."); return; }

            int optionCount = _state.nextLevelOptions?.Length ?? 0;
            // chosenOptionId is null only for a no-choice (0-option) level.
            string chosen = optionCount == 0 ? null : _selectedOptionId;
            if (optionCount >= 1 && string.IsNullOrEmpty(chosen)) { ShowStatus("Pick an option first."); return; }

            SetLoading(true);
            ShowStatus("Leveling up...");
            try
            {
                var res = await _upgradeApi.ApplyLevel(_playerCardId, chosen);
                if (res != null && res.success)
                {
                    // Apply the consumed-materials delta to the cached inventory so have/need is fresh.
                    if (res.updatedInventory != null) _inventoryService?.ApplyPartialUpdate(res.updatedInventory);

                    _state = res.state;
                    _selectedOptionId = null;

                    // Refresh inventory map for the requirements display.
                    if (_inventoryService != null)
                    {
                        try { _inventory = await _inventoryService.GetInventoryAsync(); }
                        catch (Exception ex) { Debug.LogWarning($"[CardUpgradeView] inv refresh: {ex.Message}"); }
                    }

                    if (_state != null)
                    {
                        Rebuild();
                        ShowStatus(_state.isMaxLevel ? "Max level reached!" : $"Now level {_state.currentLevel}!");
                        OnLeveledUp?.Invoke(_playerCardId, _state);
                    }
                    else
                    {
                        // Server didn't echo the new state — reload it.
                        OnLeveledUp?.Invoke(_playerCardId, null);
                        LoadStateAsync();
                    }
                }
                else
                {
                    ShowStatus(res?.message ?? "Level up failed.");
                }
            }
            catch (Exception ex)
            {
                ShowStatus($"Error: {ex.Message}");
                Debug.LogError($"[CardUpgradeView] ApplyLevel: {ex}");
            }
            finally { SetLoading(false); }
        }

        // ---- Small helpers ----

        private void ClearOptions()
        {
            _optionButtons.Clear();
            _optionButtonIds.Clear();
            ClearChildren(_optionsContainer);
        }

        private static void ClearChildren(Transform container)
        {
            if (container == null) return;
            foreach (Transform child in container) Destroy(child.gameObject);
        }

        private TextMeshProUGUI MakeText(string goName, Transform parent, DeckBuilderTextScale.Role role, TextAlignmentOptions align)
        {
            var go = new GameObject(goName, typeof(RectTransform));
            var t = go.AddComponent<TextMeshProUGUI>();
            t.transform.SetParent(parent, false);
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            DeckBuilderTextScale.Apply(t, role);
            return t;
        }

        private Button MakeButton(string goName, Transform parent, KenneyUiSkin.ButtonStyle style, string label, out TextMeshProUGUI labelText)
        {
            var go = new GameObject(goName, typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.30f, 0.32f, 0.40f, 1f);

            var lblGo = new GameObject("Label", typeof(RectTransform));
            labelText = lblGo.AddComponent<TextMeshProUGUI>();
            var lrt = (RectTransform)lblGo.transform;
            lrt.SetParent(rt, false);
            KenneyUiSkin.Fill(labelText);
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = Color.white;
            labelText.raycastTarget = false;
            DeckBuilderTextScale.Apply(labelText, DeckBuilderTextScale.Role.Button);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            if (KenneyUiSkin.Available) KenneyUiSkin.SkinButtonWithLabel(btn, style, label);
            else labelText.text = label;
            return btn;
        }

        private void ShowStatus(string msg) { if (_statusText != null) _statusText.text = msg; }
        public void SetLoading(bool on) { if (_loadingOverlay != null) _loadingOverlay.SetActive(on); }
    }
}
