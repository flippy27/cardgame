using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Networking.ApiClients
{
    public sealed class CardInventoryApiClient
    {
        private readonly string _baseUrl;

        public CardInventoryApiClient(string baseUrl = null)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? ApiConfig.BaseUrl : baseUrl.TrimEnd('/');
        }

        public async Task<PlayerCardListDto> FetchInventory()
        {
            var json = await HttpClientHelper.GetAsync($"{_baseUrl}/api/v1/cards/my-cards");
            return JsonUtility.FromJson<PlayerCardListDto>($"{{\"items\":{json}}}");
        }

        [System.Serializable]
        public sealed class PlayerCardListDto
        {
            public PlayerCardDto[] items;
        }

        [System.Serializable]
        public sealed class PlayerCardDto
        {
            public string cardId;
            public string displayName;
            public int quantity;
            public int rarity;
        }
    }
}
