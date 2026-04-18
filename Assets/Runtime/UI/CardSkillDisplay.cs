using UnityEngine;
using UnityEngine.UI;
using Flippy.CardDuelMobile.Data;

namespace Flippy.CardDuelMobile.UI
{
    /// <summary>
    /// Displays skill icons for a card.
    /// </summary>
    public class CardSkillDisplay : MonoBehaviour
    {
        [SerializeField] private Transform skillIconContainer;
        [SerializeField] private Image skillIconPrefab;
        [SerializeField] private SkillIconDefinition skillIcons;
        [SerializeField] private int maxSkillsDisplayed = 3;

        private Image[] _skillIcons;

        private void OnEnable()
        {
            if (skillIconContainer != null && _skillIcons == null)
            {
                InitializeSkillSlots();
            }
        }

        private void InitializeSkillSlots()
        {
            _skillIcons = new Image[maxSkillsDisplayed];

            for (int i = 0; i < maxSkillsDisplayed; i++)
            {
                var iconObj = Instantiate(skillIconPrefab, skillIconContainer);
                iconObj.gameObject.name = $"SkillIcon_{i}";
                iconObj.gameObject.SetActive(false);
                _skillIcons[i] = iconObj;
            }
        }

        /// <summary>Display skills from a card definition.</summary>
        public void DisplaySkills(CardDefinition card)
        {
            if (card == null || card.abilities == null)
            {
                ClearSkills();
                return;
            }

            if (_skillIcons == null)
            {
                InitializeSkillSlots();
            }

            int displayCount = Mathf.Min(card.abilities.Length, maxSkillsDisplayed);

            for (int i = 0; i < maxSkillsDisplayed; i++)
            {
                if (i < displayCount && card.abilities[i] != null)
                {
                    var ability = card.abilities[i];
                    var icon = skillIcons.GetIcon(ability.abilityId);

                    if (icon != null)
                    {
                        _skillIcons[i].sprite = Sprite.Create(icon, new Rect(0, 0, icon.width, icon.height), Vector2.one * 0.5f);
                        _skillIcons[i].gameObject.SetActive(true);
                    }
                    else
                    {
                        _skillIcons[i].gameObject.SetActive(false);
                    }
                }
                else
                {
                    _skillIcons[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>Clear all displayed skill icons.</summary>
        public void ClearSkills()
        {
            if (_skillIcons == null) return;

            foreach (var icon in _skillIcons)
            {
                if (icon != null)
                {
                    icon.gameObject.SetActive(false);
                }
            }
        }
    }
}
