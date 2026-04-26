using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Flippy.CardDuelMobile.UI.DeckBuilding
{
    /// <summary>
    /// Presentation row for a server-provided upgrade option.
    /// It intentionally does not know client-side costs or ScriptableObjects.
    /// </summary>
    public sealed class UpgradeOptionItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI upgradeNameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Transform costContainer;
        [SerializeField] private TextMeshProUGUI affordabilityText;
        [SerializeField] private Button applyButton;
        [SerializeField] private TextMeshProUGUI applyButtonText;
        [SerializeField] private Color unavailableColor = new Color(0.9f, 0.55f, 0.25f);

        private Action _onApply;

        public void BindUnavailable(string title, string description)
        {
            _onApply = null;
            SetText(upgradeNameText, title);
            SetText(descriptionText, description);
            SetText(affordabilityText, "Waiting for backend contract");

            if (affordabilityText != null)
            {
                affordabilityText.color = unavailableColor;
            }

            if (applyButton != null)
            {
                applyButton.interactable = false;
                applyButton.onClick.RemoveAllListeners();
            }

            SetText(applyButtonText, "Unavailable");
            ClearChildren(costContainer);
        }

        public void BindServerOption(string title, string description, string affordability, bool canApply, Action onApply)
        {
            _onApply = onApply;
            SetText(upgradeNameText, title);
            SetText(descriptionText, description);
            SetText(affordabilityText, affordability);

            if (applyButton != null)
            {
                applyButton.interactable = canApply;
                applyButton.onClick.RemoveAllListeners();
                applyButton.onClick.AddListener(OnApplyClicked);
            }

            SetText(applyButtonText, canApply ? "Apply" : "Unavailable");
        }

        private void OnApplyClicked()
        {
            _onApply?.Invoke();
        }

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null)
            {
                text.text = value ?? string.Empty;
            }
        }

        private static void ClearChildren(Transform container)
        {
            if (container == null)
            {
                return;
            }

            foreach (Transform child in container)
            {
                Destroy(child.gameObject);
            }
        }
    }
}
