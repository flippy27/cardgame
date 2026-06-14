using System.Collections.Generic;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

namespace Flippy.CardDuelMobile.UI
{
    public enum CardIconGroupLayout
    {
        CenteredHorizontal,
        BottomLeftGrid
    }

    public sealed class CardIconGroup : MonoBehaviour
    {
        private const string RuntimeIconNamePrefix = "__RuntimeCardIcon_";

        [Header("Root")]
        [SerializeField] private Transform iconRoot;

        [Header("Icon Template")]
        [SerializeField] private GameObject iconPrefab;
        [SerializeField] private bool hideTemplateOnAwake = true;
        [SerializeField] private bool createFallbackIconWhenMissing = true;

        [Header("Auto Layout")]
        [SerializeField] private bool autoConfigureGridLayout = true;
        [SerializeField] private CardIconGroupLayout layout = CardIconGroupLayout.CenteredHorizontal;
        [SerializeField] private Vector2 cellSize = new Vector2(18f, 18f);
        [SerializeField] private Vector2 spacing = new Vector2(2f, 2f);

        [Header("Stack Text")]
        [SerializeField] private bool showStackText = true;
        [SerializeField] private TMP_FontAsset stackFont;
        [SerializeField] private float stackFontSize = 10f;
        [SerializeField] private Color stackTextColor = Color.white;

        private readonly List<RuntimeIcon> _icons = new();

        private void Awake()
        {
            EnsureRoot();
            DisableBackgroundImage();
            RemoveGeneratedIconsFromClonedInstances();
            EnsureLayout();
            HideTemplateIfNeeded();
        }

        // The panel ships with a grey backing Image; the skill/buff icons should float over the board
        // with no panel behind them. Disable the panel's own background Image(s) (NOT the icon images,
        // which live on child objects).
        private void DisableBackgroundImage()
        {
            var bg = GetComponent<Image>();
            if (bg != null)
            {
                bg.enabled = false;
            }
            if (iconRoot != null && iconRoot != transform)
            {
                var rootBg = iconRoot.GetComponent<Image>();
                if (rootBg != null)
                {
                    rootBg.enabled = false;
                }
            }
        }

        private void Reset()
        {
            EnsureRoot();
        }

        private void OnValidate()
        {
            EnsureRoot();
            if (autoConfigureGridLayout)
            {
                EnsureLayout();
            }
        }

        /// <summary>Sets the grid cell size at runtime (hand cards use bigger icons than board tokens).</summary>
        public void SetCellSize(Vector2 size)
        {
            cellSize = size;
            EnsureLayout();
        }

        public void Apply(IReadOnlyList<CardStateVisualData> states)
        {
            EnsureRoot();
            EnsureLayout();
            HideTemplateIfNeeded();

            var count = states?.Count ?? 0;
            for (var index = 0; index < count; index++)
            {
                var icon = EnsureIcon(index);
                icon.Apply(states[index]);
            }

            for (var index = count; index < _icons.Count; index++)
            {
                _icons[index].Clear();
            }
        }

        public void Clear()
        {
            foreach (var icon in _icons)
            {
                icon.Clear();
            }
        }

        private RuntimeIcon EnsureIcon(int index)
        {
            while (_icons.Count <= index)
            {
                _icons.Add(CreateRuntimeIcon(_icons.Count));
            }

            return _icons[index];
        }

        private RuntimeIcon CreateRuntimeIcon(int index)
        {
            var parent = GetIconParent();
            GameObject instance;

            if (iconPrefab != null)
            {

                instance = Instantiate(iconPrefab, parent, false);
                var pos = instance.transform.localPosition;
                pos.z = 4f;
                instance.transform.localPosition = pos;

                instance.name = $"{RuntimeIconNamePrefix}{index:00}";
                instance.SetActive(true);
            }
            else if (createFallbackIconWhenMissing)
            {
                instance = CreateFallbackIcon(parent, index);
            }
            else
            {
                instance = new GameObject($"{RuntimeIconNamePrefix}{index:00}", typeof(RectTransform));
                instance.transform.SetParent(parent, false);
            }

            var image = instance.GetComponentInChildren<Image>(true);
            if (image == null)
            {
                image = instance.AddComponent<Image>();
            }

            var stackText = showStackText ? instance.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            if (showStackText && stackText == null)
            {
                stackText = CreateStackText(instance.transform);
            }

            return new RuntimeIcon(instance, image, stackText);
        }

        private GameObject CreateFallbackIcon(Transform parent, int index)
        {
            var instance = new GameObject($"{RuntimeIconNamePrefix}{index:00}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            instance.transform.SetParent(parent, false);

            if (instance.transform is RectTransform rect)
            {
                rect.sizeDelta = cellSize;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
            }

            return instance;
        }

        private TextMeshProUGUI CreateStackText(Transform parent)
        {
            var textGo = new GameObject("StackText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(parent, false);

            if (textGo.transform is RectTransform rect)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            var text = textGo.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.BottomRight;
            text.fontSize = stackFontSize;
            text.color = stackTextColor;
            text.raycastTarget = false;
            if (stackFont != null)
            {
                text.font = stackFont;
            }

            return text;
        }

        private void EnsureRoot()
        {
            if (iconRoot == null)
            {
                iconRoot = transform;
            }
        }

        private Transform GetIconParent()
        {
            EnsureRoot();
            return iconRoot != null ? iconRoot : transform;
        }

        private void EnsureLayout()
        {
            if (!autoConfigureGridLayout)
            {
                return;
            }

            var parent = GetIconParent();
            if (parent == null || parent.GetComponent<RectTransform>() == null)
            {
                return;
            }

            var grid = parent.GetComponent<GridLayoutGroup>();
            if (grid == null)
            {
                grid = parent.gameObject.AddComponent<GridLayoutGroup>();
            }

            grid.cellSize = cellSize;
            grid.spacing = spacing;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            // Centered grid that grows from the bottom-centre: fills up to 3 across, then wraps UPWARD,
            // staying centred (like the reference). Replaces the old left-anchored / flexible layout.
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.startCorner = GridLayoutGroup.Corner.LowerLeft;
            grid.childAlignment = TextAnchor.LowerCenter;
        }

        private void HideTemplateIfNeeded()
        {
            if (!hideTemplateOnAwake || iconPrefab == null)
            {
                return;
            }

            var parent = GetIconParent();
            if (parent != null && iconPrefab.transform.IsChildOf(parent))
            {
                iconPrefab.SetActive(false);
            }
        }

        private void RemoveGeneratedIconsFromClonedInstances()
        {
            var parent = GetIconParent();
            if (parent == null)
            {
                return;
            }

            var transforms = parent.GetComponentsInChildren<Transform>(true);
            foreach (var child in transforms)
            {
                if (child == null ||
                    child == parent ||
                    child.gameObject == iconPrefab ||
                    !child.gameObject.name.StartsWith(RuntimeIconNamePrefix, System.StringComparison.Ordinal))
                {
                    continue;
                }

                child.gameObject.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            _icons.Clear();
        }

        private sealed class RuntimeIcon
        {
            private readonly GameObject _root;
            private readonly Image _iconImage;
            private readonly TextMeshProUGUI _stackText;

            public RuntimeIcon(GameObject root, Image iconImage, TextMeshProUGUI stackText)
            {
                _root = root;
                _iconImage = iconImage;
                _stackText = stackText;
            }

            public void Apply(CardStateVisualData state)
            {
                if (state == null)
                {
                    Debug.LogWarning("Card icon state is NULL, clearing icon.");
                    Clear();
                    return;
                }
                Debug.Log($"Applying card icon. Sprite: {(state.icon != null ? state.icon.name : "NULL")}, stack: {state.stackCount}");
                if (_root != null)
                {
                    _root.SetActive(true);
                }

                if (_iconImage != null)
                {
                    _iconImage.sprite = state.icon;
                    _iconImage.enabled = state.icon != null;
                    _iconImage.color = state.tint.a > 0f ? state.tint : Color.white;
                }

                if (_stackText != null)
                {
                    _stackText.text = state.stackCount > 1 ? state.stackCount.ToString() : string.Empty;
                    _stackText.gameObject.SetActive(!string.IsNullOrEmpty(_stackText.text));
                }
            }

            public void Clear()
            {
                if (_iconImage != null)
                {
                    _iconImage.sprite = null;
                    _iconImage.enabled = false;
                }

                if (_stackText != null)
                {
                    _stackText.text = string.Empty;
                    _stackText.gameObject.SetActive(false);
                }

                if (_root != null)
                {
                    _root.SetActive(false);
                }
            }
        }
    }
}
