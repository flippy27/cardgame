using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using Flippy.CardDuelMobile.Battle;
using Flippy.CardDuelMobile.Data;

namespace Flippy.CardDuelMobile.UI
{
    public class CardDetailView : MonoBehaviour, IDragHandler
    {
        public CardViewWidget cardViewWidget;
        public CardAbilityDisplay skillDisplay;
        public Image cooldownIcon;
        public Image canAttackIcon;

        public event Action OnDestroyed;

        private CardInHandDto _cardDto;
        private CardDefinition _cardDef;
        private Vector2 _dragStartPos;
        private const float DRAG_DOWN_THRESHOLD = 100f;

        private void Start()
        {
            if (cardViewWidget == null)
                cardViewWidget = GetComponentInChildren<CardViewWidget>();
            if (skillDisplay == null)
                skillDisplay = GetComponentInChildren<CardAbilityDisplay>();

            // Auto-find attack icons
            if (cooldownIcon == null || canAttackIcon == null)
            {
                var images = GetComponentsInChildren<Image>();
                // Buscar por nombre o posición
                foreach (var img in images)
                {
                    if (img.name.Contains("Cooldown"))
                        cooldownIcon = img;
                    if (img.name.Contains("CanAttack") || img.name.Contains("Attack"))
                        canAttackIcon = img;
                }
            }
        }

        public void SetCard(CardInHandDto dto)
        {
            _cardDto = dto;
            if (cardViewWidget != null)
            {
                cardViewWidget.Bind(dto);
            }

            // Cargar CardDefinition completa
            if (!string.IsNullOrEmpty(dto.cardId))
            {
                _cardDef = CardRegistry.GetCard(dto.cardId);

                if (_cardDef != null)
                {
                    if (skillDisplay != null)
                        skillDisplay.DisplaySkills(_cardDef);

                    UpdateAttackIndicators();
                }
                else
                {
                    if (skillDisplay != null)
                        skillDisplay.ClearSkills();
                    HideAttackIndicators();
                }
            }
            else
            {
                if (skillDisplay != null)
                    skillDisplay.ClearSkills();
                HideAttackIndicators();
            }
        }

        public void SetCard(BoardCardDto dto)
        {
            if (cardViewWidget != null)
            {
                cardViewWidget.Bind(dto);
            }

            // Cargar CardDefinition completa
            if (!string.IsNullOrEmpty(dto.cardId))
            {
                _cardDef = CardRegistry.GetCard(dto.cardId);

                if (_cardDef != null)
                {
                    if (skillDisplay != null)
                        skillDisplay.DisplaySkills(_cardDef);

                    UpdateAttackIndicators();
                }
                else
                {
                    if (skillDisplay != null)
                        skillDisplay.ClearSkills();
                    HideAttackIndicators();
                }
            }
            else
            {
                if (skillDisplay != null)
                    skillDisplay.ClearSkills();
                HideAttackIndicators();
            }
        }

        private void UpdateAttackIndicators()
        {
            if (_cardDef == null || _cardDef.cardType != CardType.Unit)
            {
                HideAttackIndicators();
                return;
            }

            // Cooldown icon: mostrar si aún hay cooldown
            // En hand no hay cooldown, siempre será 0, así que ocultamos
            if (cooldownIcon != null)
            {
                cooldownIcon.gameObject.SetActive(false);
            }

            // CanAttack icon: mostrar si el tipo de unidad puede atacar desde ALGÚN slot válido
            // Melee puede atacar desde Front
            // Ranged/Magic pueden atacar desde BackLeft/BackRight
            if (canAttackIcon != null)
            {
                bool canAttackFromSomeSlot = CanAttackAsUnitType(_cardDef.unitType);
                canAttackIcon.gameObject.SetActive(canAttackFromSomeSlot);
            }
        }

        private bool CanAttackAsUnitType(UnitType unitType)
        {
            // Simplemente: cualquier unidad puede atacar desde ALGÚN slot válido
            // Melee desde Front, Ranged/Magic desde Back
            return unitType == UnitType.Melee || unitType == UnitType.Ranged || unitType == UnitType.Magic;
        }

        private void HideAttackIndicators()
        {
            if (cooldownIcon != null)
                cooldownIcon.gameObject.SetActive(false);
            if (canAttackIcon != null)
                canAttackIcon.gameObject.SetActive(false);
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Detectar drag hacia abajo
            if (eventData.position.y < _dragStartPos.y - DRAG_DOWN_THRESHOLD)
            {
                Close();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _dragStartPos = eventData.position;
        }

        public void Close()
        {
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            OnDestroyed?.Invoke();
        }
    }
}
