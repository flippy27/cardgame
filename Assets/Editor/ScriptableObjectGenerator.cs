using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using Flippy.CardDuelMobile.Data;
using Flippy.CardDuelMobile.Core;

namespace Flippy.CardDuelMobile.Editor
{
    public static class ScriptableObjectGenerator
    {
        private const string MOCK_SKILLS_PATH = "Mock/Skills/skills.json";
        private const string MOCK_ABILITIES_PATH = "Mock/Abilities/abilities.json";
        private const string MOCK_CARDS_PATH = "Mock/Cards/cards.json";
        private const string MOCK_DECKS_PATH = "Mock/Decks/decks.json";

        private const string OUTPUT_ABILITIES_PATH = "Assets/Runtime/Data/Abilities";
        private const string OUTPUT_CARDS_PATH = "Assets/Runtime/Data/Cards";
        private const string OUTPUT_DECKS_PATH = "Assets/Runtime/Data/Decks";

        [MenuItem("Tools/Data/Generate All")]
        public static void GenerateAll()
        {
            Debug.Log("[ScriptableObjectGenerator] Starting full generation");
            GenerateAbilities();
            GenerateCards();
            GenerateDecks();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Success", "All assets generated successfully!", "OK");
        }

        [MenuItem("Tools/Data/Generate Abilities")]
        public static void GenerateAbilities()
        {
            Debug.Log("[ScriptableObjectGenerator] Generating abilities from JSON");
            string jsonPath = Path.Combine(Application.dataPath, MOCK_ABILITIES_PATH);

            if (!File.Exists(jsonPath))
            {
                EditorUtility.DisplayDialog("Error", $"Abilities JSON not found at {jsonPath}", "OK");
                return;
            }

            string jsonContent = File.ReadAllText(jsonPath);
            AbilitiesData abilitiesData = JsonUtility.FromJson<AbilitiesData>(jsonContent);

            if (abilitiesData?.abilities == null || abilitiesData.abilities.Length == 0)
            {
                Debug.LogWarning("[ScriptableObjectGenerator] No abilities found in JSON");
                return;
            }

            EnsureDirectoryExists(OUTPUT_ABILITIES_PATH);

            foreach (var abilityData in abilitiesData.abilities)
            {
                CreateAbilityAsset(abilityData);
            }

            Debug.Log($"[ScriptableObjectGenerator] Generated {abilitiesData.abilities.Length} abilities");
        }

        [MenuItem("Tools/Data/Generate Cards")]
        public static void GenerateCards()
        {
            Debug.Log("[ScriptableObjectGenerator] Generating cards from JSON");
            string jsonPath = Path.Combine(Application.dataPath, MOCK_CARDS_PATH);

            if (!File.Exists(jsonPath))
            {
                EditorUtility.DisplayDialog("Error", $"Cards JSON not found at {jsonPath}", "OK");
                return;
            }

            string jsonContent = File.ReadAllText(jsonPath);
            CardsData cardsData = JsonUtility.FromJson<CardsData>(jsonContent);

            if (cardsData?.cards == null || cardsData.cards.Length == 0)
            {
                Debug.LogWarning("[ScriptableObjectGenerator] No cards found in JSON");
                return;
            }

            EnsureDirectoryExists(OUTPUT_CARDS_PATH);

            // Load all ability assets for reference
            var abilityAssets = new Dictionary<string, AbilityDefinition>();
            LoadAbilityAssets(abilityAssets);

            foreach (var cardData in cardsData.cards)
            {
                CreateCardAsset(cardData, abilityAssets);
            }

            Debug.Log($"[ScriptableObjectGenerator] Generated {cardsData.cards.Length} cards");
        }

        [MenuItem("Tools/Data/Generate Decks")]
        public static void GenerateDecks()
        {
            Debug.Log("[ScriptableObjectGenerator] Generating decks from JSON");
            string jsonPath = Path.Combine(Application.dataPath, MOCK_DECKS_PATH);

            if (!File.Exists(jsonPath))
            {
                EditorUtility.DisplayDialog("Error", $"Decks JSON not found at {jsonPath}", "OK");
                return;
            }

            string jsonContent = File.ReadAllText(jsonPath);
            DecksData decksData = JsonUtility.FromJson<DecksData>(jsonContent);

            if (decksData?.decks == null || decksData.decks.Length == 0)
            {
                Debug.LogWarning("[ScriptableObjectGenerator] No decks found in JSON");
                return;
            }

            EnsureDirectoryExists(OUTPUT_DECKS_PATH);

            // Load all card assets for reference
            var cardAssets = new Dictionary<string, CardDefinition>();
            LoadCardAssets(cardAssets);

            foreach (var deckData in decksData.decks)
            {
                CreateDeckAsset(deckData, cardAssets);
            }

            Debug.Log($"[ScriptableObjectGenerator] Generated {decksData.decks.Length} decks");
        }

        private static void CreateAbilityAsset(AbilityJsonData abilityData)
        {
            string assetPath = Path.Combine(OUTPUT_ABILITIES_PATH, $"Ability_{abilityData.abilityId}.asset");

            AbilityDefinition ability = ScriptableObject.CreateInstance<AbilityDefinition>();
            ability.abilityId = abilityData.abilityId;
            ability.displayName = abilityData.displayName;
            ability.description = abilityData.description;

            // Parse trigger type
            if (System.Enum.TryParse<AbilityTrigger>(abilityData.trigger, out var trigger))
            {
                ability.trigger = trigger;
            }

            // Create asset first
            AssetDatabase.CreateAsset(ability, assetPath);

            // Create effects from JSON data and add them to the ability asset
            if (abilityData.effects != null && abilityData.effects.Length > 0)
            {
                var effectsList = new List<EffectDefinition>();
                for (int i = 0; i < abilityData.effects.Length; i++)
                {
                    var effect = CreateEffectAsset(abilityData.effects[i], abilityData.abilityId, i);
                    if (effect != null)
                    {
                        AssetDatabase.AddObjectToAsset(effect, assetPath);
                        effectsList.Add(effect);
                    }
                }
                ability.effects = effectsList.ToArray();
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ScriptableObjectGenerator] Created ability asset: {assetPath}");
        }

        private static EffectDefinition CreateEffectAsset(EffectJsonData effectData, string abilityId, int index)
        {
            EffectDefinition effect = null;

            switch (effectData.effectType)
            {
                case "BuffAttackEffectDefinition":
                    var buffEffect = ScriptableObject.CreateInstance<BuffAttackEffectDefinition>();
                    buffEffect.amount = effectData.amount > 0 ? effectData.amount : 1;
                    buffEffect.name = $"Effect_{index}_{effectData.effectType}";
                    effect = buffEffect;
                    break;

                case "DamageEffectDefinition":
                    var dmgEffect = ScriptableObject.CreateInstance<DamageEffectDefinition>();
                    dmgEffect.amount = effectData.amount > 0 ? effectData.amount : 1;
                    dmgEffect.ignoreArmor = effectData.ignoreArmor;
                    dmgEffect.name = $"Effect_{index}_{effectData.effectType}";
                    effect = dmgEffect;
                    break;

                case "HealEffectDefinition":
                    var healEffect = ScriptableObject.CreateInstance<HealEffectDefinition>();
                    healEffect.amount = effectData.amount > 0 ? effectData.amount : 1;
                    healEffect.name = $"Effect_{index}_{effectData.effectType}";
                    effect = healEffect;
                    break;

                case "GainArmorEffectDefinition":
                    var armorEffect = ScriptableObject.CreateInstance<GainArmorEffectDefinition>();
                    armorEffect.amount = effectData.amount > 0 ? effectData.amount : 1;
                    armorEffect.name = $"Effect_{index}_{effectData.effectType}";
                    effect = armorEffect;
                    break;

                case "HitHeroEffectDefinition":
                    var heroEffect = ScriptableObject.CreateInstance<HitHeroEffectDefinition>();
                    heroEffect.amount = effectData.amount > 0 ? effectData.amount : 1;
                    heroEffect.name = $"Effect_{index}_{effectData.effectType}";
                    effect = heroEffect;
                    break;

                default:
                    Debug.LogWarning($"[ScriptableObjectGenerator] Unknown effect type: {effectData.effectType}");
                    return null;
            }

            return effect;
        }

        private static void CreateCardAsset(CardJsonData cardData, Dictionary<string, AbilityDefinition> abilityAssets)
        {
            string assetPath = Path.Combine(OUTPUT_CARDS_PATH, $"Card_{cardData.cardId}.asset");

            CardDefinition card = ScriptableObject.CreateInstance<CardDefinition>();
            card.cardId = cardData.cardId;
            card.displayName = cardData.displayName;
            card.description = cardData.description;
            card.manaCost = cardData.manaCost;
            card.attack = cardData.attack;
            card.health = cardData.health;
            card.armor = cardData.armor;

            // Parse faction
            if (System.Enum.TryParse<CardFaction>(cardData.faction, out var faction))
            {
                card.faction = faction;
            }

            // Parse rarity
            if (System.Enum.TryParse<CardRarity>(cardData.rarity, out var rarity))
            {
                card.rarity = rarity;
            }

            // Parse card type
            if (System.Enum.TryParse<CardType>(cardData.cardType, out var cardType))
            {
                card.cardType = cardType;
            }

            // Parse unit type
            if (System.Enum.TryParse<UnitType>(cardData.unitType, out var unitType))
            {
                card.unitType = unitType;
            }

            // Wire up abilities
            if (cardData.abilities != null && cardData.abilities.Length > 0)
            {
                var abilitiesList = new List<AbilityDefinition>();
                foreach (var abilityId in cardData.abilities)
                {
                    if (abilityAssets.TryGetValue(abilityId, out var abilityAsset))
                    {
                        abilitiesList.Add(abilityAsset);
                    }
                    else
                    {
                        Debug.LogWarning($"[ScriptableObjectGenerator] Ability '{abilityId}' not found for card '{cardData.cardId}'");
                    }
                }
                card.abilities = abilitiesList.ToArray();
            }

            AssetDatabase.CreateAsset(card, assetPath);
            Debug.Log($"[ScriptableObjectGenerator] Created card asset: {assetPath}");
        }

        private static void LoadAbilityAssets(Dictionary<string, AbilityDefinition> abilityAssets)
        {
            EnsureDirectoryExists(OUTPUT_ABILITIES_PATH);
            var guids = AssetDatabase.FindAssets("t:AbilityDefinition", new[] { OUTPUT_ABILITIES_PATH });

            foreach (var guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(assetPath);
                if (ability != null && !string.IsNullOrEmpty(ability.abilityId))
                {
                    abilityAssets[ability.abilityId] = ability;
                }
            }
        }

        private static void LoadCardAssets(Dictionary<string, CardDefinition> cardAssets)
        {
            EnsureDirectoryExists(OUTPUT_CARDS_PATH);
            var guids = AssetDatabase.FindAssets("t:CardDefinition", new[] { OUTPUT_CARDS_PATH });

            foreach (var guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<CardDefinition>(assetPath);
                if (card != null && !string.IsNullOrEmpty(card.cardId))
                {
                    cardAssets[card.cardId] = card;
                }
            }
        }

        private static void CreateDeckAsset(DeckJsonData deckData, Dictionary<string, CardDefinition> cardAssets)
        {
            string assetPath = Path.Combine(OUTPUT_DECKS_PATH, $"Deck_{deckData.deckId}.asset");

            DeckDefinition deck = ScriptableObject.CreateInstance<DeckDefinition>();
            deck.deckId = deckData.deckId;
            deck.displayName = deckData.displayName;
            deck.description = deckData.description;
            deck.deckType = deckData.deckType;

            // Parse faction
            if (System.Enum.TryParse<CardFaction>(deckData.faction, out var faction))
            {
                deck.faction = faction;
            }

            // Wire up cards
            if (deckData.cards != null && deckData.cards.Length > 0)
            {
                var deckCardsList = new List<DeckDefinition.DeckCard>();
                foreach (var cardRef in deckData.cards)
                {
                    if (cardAssets.TryGetValue(cardRef.cardId, out var cardAsset))
                    {
                        deckCardsList.Add(new DeckDefinition.DeckCard
                        {
                            card = cardAsset,
                            quantity = cardRef.quantity
                        });
                    }
                    else
                    {
                        Debug.LogWarning($"[ScriptableObjectGenerator] Card '{cardRef.cardId}' not found for deck '{deckData.deckId}'");
                    }
                }
                deck.cards = deckCardsList.ToArray();
            }

            AssetDatabase.CreateAsset(deck, assetPath);
            Debug.Log($"[ScriptableObjectGenerator] Created deck asset: {assetPath}");
        }

        private static void EnsureDirectoryExists(string path)
        {
            string fullPath = Path.Combine(Application.dataPath, path.Replace("Assets/", ""));
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }
        }
    }

    [System.Serializable]
    public class AbilitiesData
    {
        public AbilityJsonData[] abilities;
    }

    [System.Serializable]
    public class AbilityJsonData
    {
        public string abilityId;
        public string displayName;
        public string description;
        public string trigger;
        public EffectJsonData[] effects;
    }

    [System.Serializable]
    public class EffectJsonData
    {
        public string effectType;
        public int amount;
        public int damage;
        public bool ignoreArmor;
    }

    [System.Serializable]
    public class CardsData
    {
        public CardJsonData[] cards;
    }

    [System.Serializable]
    public class CardJsonData
    {
        public string cardId;
        public string displayName;
        public string description;
        public string faction;
        public string rarity;
        public string cardType;
        public int manaCost;
        public int attack;
        public int health;
        public int armor;
        public string unitType;
        public string[] abilities;
    }

    [System.Serializable]
    public class DecksData
    {
        public DeckJsonData[] decks;
    }

    [System.Serializable]
    public class DeckJsonData
    {
        public string deckId;
        public string displayName;
        public string description;
        public string deckType;
        public string faction;
        public DeckCardRef[] cards;
    }

    [System.Serializable]
    public class DeckCardRef
    {
        public string cardId;
        public int quantity;
    }
}
