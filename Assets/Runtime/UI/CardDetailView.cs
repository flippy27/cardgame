using UnityEngine;
using System;
using Flippy.CardDuelMobile.Battle;

namespace Flippy.CardDuelMobile.UI
{
    public class CardDetailView : MonoBehaviour
    {
        public CardViewWidget cardViewWidget;

        public event Action OnDestroyed;

        private CardInHandDto _cardDto;

        private void Start()
        {
            if (cardViewWidget == null)
                cardViewWidget = GetComponentInChildren<CardViewWidget>();
        }

        public void SetCard(CardInHandDto dto)
        {
            _cardDto = dto;
            if (cardViewWidget != null)
            {
                cardViewWidget.Bind(dto);
            }
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
