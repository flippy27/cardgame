using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Flippy.CardDuelMobile.Data;
using Flippy.CardDuelMobile.Core;
using Flippy.CardDuelMobile.Networking;

namespace Flippy.CardDuelMobile.EditorTools
{
    /// <summary>
    /// Genera assets de prototipo de forma determinista.
    /// </summary>
    public static class CardsPrototypeContentGenerator
    {
        [System.Serializable]
        private sealed class CardSeed
        {
            public string id;
            public string name;
            public string description;
            public CardFaction faction;
            public CardRarity rarity;
            public int mana;
            public int atk;
            public int hp;
            public int armor;
            public bool front = true;
            public bool back = true;
            public string attackSelector;
            public string[] abilities;
            public string visual;
        }

        public static void EnsureFolders()
        {
            foreach (var folder in CardsEditorPaths.AllFolders)
            {
                EnsureFolderRecursive(folder);
            }

            AssetDatabase.Refresh();
            Debug.Log("Cards folder structure ensured.");
        }

        public static void GenerateAll()
        {
            EnsureFolders();

            var visuals = GenerateVisualProfiles();
            var selectors = GenerateSelectors();
            var effects = GenerateEffects();
            var abilities = GenerateAbilities(selectors, effects);

            GenerateRules();
            GenerateMatchmakerConfig();
            GenerateCards(visuals, selectors, abilities);
            GenerateDecks();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Prototype card content generated.");
        }

        private static void GenerateRules()
        {
            var rules = CreateOrLoadAsset<DuelRulesProfile>(CardsEditorPaths.Rules + "/DefaultDuelRules.asset");
            rules.startingHeroHealth = 20;
            rules.startingMana = 1;
            rules.manaPerTurn = 1;
            rules.maxMana = 10;
            rules.startingHandSize = 4;
            rules.drawAtTurnStart = true;
            EditorUtility.SetDirty(rules);
        }

        private static void GenerateMatchmakerConfig()
        {
            var config = CreateOrLoadAsset<MatchmakerConfig>(CardsEditorPaths.Config + "/DefaultMatchmakerConfig.asset");
            config.sessionType = "CardDuel";
            config.advancedQueueName = "Friendly";
            config.useAdvancedMatchmaker = false;
            config.createSessionIfQuickJoinFails = true;
            config.quickJoinTimeoutSeconds = 5f;
            EditorUtility.SetDirty(config);
        }

        private static Dictionary<string, CardVisualProfile> GenerateVisualProfiles()
        {
            var dictionary = new Dictionary<string, CardVisualProfile>();
            dictionary["Ember"] = MakeVisual("VP_Ember", new Color(0.92f, 0.34f, 0.20f), new Color(0.22f, 0.08f, 0.08f));
            dictionary["Tidal"] = MakeVisual("VP_Tidal", new Color(0.20f, 0.62f, 0.90f), new Color(0.06f, 0.12f, 0.20f));
            dictionary["Grove"] = MakeVisual("VP_Grove", new Color(0.32f, 0.78f, 0.33f), new Color(0.06f, 0.16f, 0.08f));
            dictionary["Alloy"] = MakeVisual("VP_Alloy", new Color(0.70f, 0.70f, 0.75f), new Color(0.16f, 0.16f, 0.18f));
            dictionary["Void"] = MakeVisual("VP_Void", new Color(0.62f, 0.28f, 0.95f), new Color(0.10f, 0.06f, 0.16f));
            return dictionary;
        }

        private static Dictionary<string, TargetSelectorDefinition> GenerateSelectors()
        {
            return new Dictionary<string, TargetSelectorDefinition>
            {
                ["FrontlineFirst"] = CreateAssetIfMissing<FrontlineFirstTargetSelector>(CardsEditorPaths.Selectors + "/TS_FrontlineFirst.asset"),
                ["BacklineFirst"] = CreateAssetIfMissing<BacklineFirstTargetSelector>(CardsEditorPaths.Selectors + "/TS_BacklineFirst.asset"),
                ["AllEnemies"] = CreateAssetIfMissing<AllEnemiesTargetSelector>(CardsEditorPaths.Selectors + "/TS_AllEnemies.asset"),
                ["LowestHealthAlly"] = CreateAssetIfMissing<LowestHealthAllyTargetSelector>(CardsEditorPaths.Selectors + "/TS_LowestHealthAlly.asset")
            };
        }

        private static Dictionary<string, EffectDefinition> GenerateEffects()
        {
            var result = new Dictionary<string, EffectDefinition>();

            var damage1 = CreateAssetIfMissing<DamageEffectDefinition>(CardsEditorPaths.Effects + "/FX_Damage1.asset");
            damage1.amount = 1;
            result["Damage1"] = damage1;

            var damage2 = CreateAssetIfMissing<DamageEffectDefinition>(CardsEditorPaths.Effects + "/FX_Damage2.asset");
            damage2.amount = 2;
            result["Damage2"] = damage2;

            var damage3 = CreateAssetIfMissing<DamageEffectDefinition>(CardsEditorPaths.Effects + "/FX_Damage3.asset");
            damage3.amount = 3;
            result["Damage3"] = damage3;

            var heal2 = CreateAssetIfMissing<HealEffectDefinition>(CardsEditorPaths.Effects + "/FX_Heal2.asset");
            heal2.amount = 2;
            result["Heal2"] = heal2;

            var armor2 = CreateAssetIfMissing<GainArmorEffectDefinition>(CardsEditorPaths.Effects + "/FX_Armor2.asset");
            armor2.amount = 2;
            result["Armor2"] = armor2;

            var buff1 = CreateAssetIfMissing<BuffAttackEffectDefinition>(CardsEditorPaths.Effects + "/FX_BuffAttack1.asset");
            buff1.amount = 1;
            result["BuffAttack1"] = buff1;

            var hero2 = CreateAssetIfMissing<HitHeroEffectDefinition>(CardsEditorPaths.Effects + "/FX_HitHero2.asset");
            hero2.amount = 2;
            result["HitHero2"] = hero2;

            foreach (var item in result.Values)
            {
                EditorUtility.SetDirty(item);
            }

            return result;
        }

        private static Dictionary<string, AbilityDefinition> GenerateAbilities(
            Dictionary<string, TargetSelectorDefinition> selectors,
            Dictionary<string, EffectDefinition> effects)
        {
            var result = new Dictionary<string, AbilityDefinition>();

            result["BattleHeal"] = MakeAbility(
                "AB_BattleHeal",
                "Battle Heal",
                AbilityTrigger.OnTurnEnd,
                selectors["LowestHealthAlly"],
                effects["Heal2"]);

            result["BattleArmor"] = MakeAbility(
                "AB_BattleArmor",
                "Armor Up",
                AbilityTrigger.OnPlay,
                null,
                effects["Armor2"]);

            result["BattleBuff"] = MakeAbility(
                "AB_BattleBuff",
                "Sharpen",
                AbilityTrigger.OnTurnStart,
                null,
                effects["BuffAttack1"]);

            result["SplashFront"] = MakeAbility(
                "AB_SplashFront",
                "Volley",
                AbilityTrigger.OnBattlePhase,
                selectors["AllEnemies"],
                effects["Damage1"]);

            result["HeroPing"] = MakeAbility(
                "AB_HeroPing",
                "Burning Echo",
                AbilityTrigger.OnTurnEnd,
                null,
                effects["HitHero2"]);

            return result;
        }

        private static void GenerateCards(
            Dictionary<string, CardVisualProfile> visuals,
            Dictionary<string, TargetSelectorDefinition> selectors,
            Dictionary<string, AbilityDefinition> abilities)
        {
            var seeds = new[]
            {
                new CardSeed{ id="ember_vanguard", name="Ember Vanguard", description="Frontline beater.", faction=CardFaction.Ember, rarity=CardRarity.Common, mana=2, atk=3, hp=3, attackSelector="FrontlineFirst", front=true, back=false, visual="Ember"},
                new CardSeed{ id="ember_archer", name="Ember Archer", description="Backline ranged attacker.", faction=CardFaction.Ember, rarity=CardRarity.Common, mana=2, atk=2, hp=2, attackSelector="BacklineFirst", front=false, back=true, visual="Ember"},
                new CardSeed{ id="ember_burnseer", name="Burnseer", description="Pings enemy hero.", faction=CardFaction.Ember, rarity=CardRarity.Rare, mana=3, atk=2, hp=3, attackSelector="BacklineFirst", abilities=new[]{"HeroPing"}, front=false, back=true, visual="Ember"},

                new CardSeed{ id="tidal_priest", name="Tidal Priest", description="Heals ally at turn end.", faction=CardFaction.Tidal, rarity=CardRarity.Common, mana=2, atk=1, hp=3, attackSelector="BacklineFirst", abilities=new[]{"BattleHeal"}, front=false, back=true, visual="Tidal"},
                new CardSeed{ id="tidal_lancer", name="Tidal Lancer", description="Fast frontline unit.", faction=CardFaction.Tidal, rarity=CardRarity.Common, mana=2, atk=3, hp=2, attackSelector="FrontlineFirst", front=true, back=false, visual="Tidal"},
                new CardSeed{ id="tidal_sniper", name="Tidal Sniper", description="Targets backline first.", faction=CardFaction.Tidal, rarity=CardRarity.Rare, mana=3, atk=3, hp=2, attackSelector="BacklineFirst", front=false, back=true, visual="Tidal"},

                new CardSeed{ id="grove_guardian", name="Grove Guardian", description="Durable frontline body.", faction=CardFaction.Grove, rarity=CardRarity.Common, mana=3, atk=2, hp=5, armor=1, attackSelector="FrontlineFirst", front=true, back=false, visual="Grove"},
                new CardSeed{ id="grove_shaper", name="Grove Shaper", description="Buffs allies over time.", faction=CardFaction.Grove, rarity=CardRarity.Rare, mana=3, atk=1, hp=4, attackSelector="BacklineFirst", abilities=new[]{"BattleBuff"}, front=false, back=true, visual="Grove"},
                new CardSeed{ id="grove_raincaller", name="Raincaller", description="Support caster.", faction=CardFaction.Grove, rarity=CardRarity.Common, mana=2, atk=1, hp=3, attackSelector="BacklineFirst", abilities=new[]{"BattleHeal"}, front=false, back=true, visual="Grove"},

                new CardSeed{ id="alloy_bulwark", name="Alloy Bulwark", description="Armor on play.", faction=CardFaction.Alloy, rarity=CardRarity.Common, mana=3, atk=2, hp=4, attackSelector="FrontlineFirst", abilities=new[]{"BattleArmor"}, front=true, back=false, visual="Alloy"},
                new CardSeed{ id="alloy_ballista", name="Alloy Ballista", description="Ranged siege weapon.", faction=CardFaction.Alloy, rarity=CardRarity.Rare, mana=4, atk=4, hp=2, attackSelector="BacklineFirst", front=false, back=true, visual="Alloy"},
                new CardSeed{ id="alloy_hoplite", name="Alloy Hoplite", description="Solid frontliner.", faction=CardFaction.Alloy, rarity=CardRarity.Common, mana=2, atk=2, hp=3, attackSelector="FrontlineFirst", front=true, back=false, visual="Alloy"},

                new CardSeed{ id="void_stalker", name="Void Stalker", description="Aggressive melee.", faction=CardFaction.Void, rarity=CardRarity.Common, mana=2, atk=3, hp=2, attackSelector="FrontlineFirst", front=true, back=false, visual="Void"},
                new CardSeed{ id="void_caller", name="Void Caller", description="Wide pressure.", faction=CardFaction.Void, rarity=CardRarity.Epic, mana=4, atk=2, hp=3, attackSelector="FrontlineFirst", abilities=new[]{"SplashFront"}, front=false, back=true, visual="Void"},
                new CardSeed{ id="void_magus", name="Void Magus", description="Late scaler.", faction=CardFaction.Void, rarity=CardRarity.Rare, mana=4, atk=3, hp=4, attackSelector="FrontlineFirst", abilities=new[]{"BattleBuff"}, front=false, back=true, visual="Void"},

                new CardSeed{ id="ember_colossus", name="Ember Colossus", description="Heavy front body.", faction=CardFaction.Ember, rarity=CardRarity.Epic, mana=5, atk=5, hp=6, attackSelector="FrontlineFirst", front=true, back=false, visual="Ember"},
                new CardSeed{ id="tidal_waveblade", name="Waveblade", description="Aggressive skirmisher.", faction=CardFaction.Tidal, rarity=CardRarity.Common, mana=1, atk=2, hp=1, attackSelector="BacklineFirst", front=false, back=true, visual="Tidal"},
                new CardSeed{ id="grove_myr", name="Grove Myr", description="Cheap filler.", faction=CardFaction.Grove, rarity=CardRarity.Common, mana=1, atk=1, hp=2, attackSelector="FrontlineFirst", front=true, back=false, visual="Grove"}
            };

            foreach (var seed in seeds)
            {
                var card = CreateOrLoadAsset<CardDefinition>($"{CardsEditorPaths.Cards}/{seed.id}.asset");
                card.cardId = seed.id;
                card.displayName = seed.name;
                card.description = seed.description;
                card.faction = seed.faction;
                card.rarity = seed.rarity;
                card.manaCost = seed.mana;
                card.attack = seed.atk;
                card.health = seed.hp;
                card.armor = seed.armor;
                card.defaultAttackTargetSelector = selectors[seed.attackSelector];
                card.visualProfile = visuals[seed.visual];

                if (seed.abilities != null && seed.abilities.Length > 0)
                {
                    var mapped = new List<AbilityDefinition>();
                    foreach (var abilityKey in seed.abilities)
                    {
                        if (abilities.TryGetValue(abilityKey, out var ability))
                        {
                            mapped.Add(ability);
                        }
                    }

                    card.abilities = mapped.ToArray();
                }
                else
                {
                    card.abilities = null;
                }

                EditorUtility.SetDirty(card);
            }
        }

        private static void GenerateDecks()
        {
            var cardPaths = new[]
            {
                "ember_vanguard", "ember_archer", "ember_burnseer", "tidal_priest", "tidal_lancer",
                "tidal_sniper", "grove_guardian", "grove_shaper", "grove_raincaller", "alloy_bulwark",
                "alloy_ballista", "alloy_hoplite", "void_stalker", "void_caller", "void_magus",
                "ember_colossus", "tidal_waveblade", "grove_myr"
            };

            var cards = new List<CardDefinition>();
            foreach (var id in cardPaths)
            {
                var card = AssetDatabase.LoadAssetAtPath<CardDefinition>($"{CardsEditorPaths.Cards}/{id}.asset");
                if (card != null)
                {
                    cards.Add(card);
                }
            }

            var deckA = CreateOrLoadAsset<DeckDefinition>(CardsEditorPaths.Decks + "/Deck_PlayerA.asset");
            deckA.deckId = "player_a";
            deckA.displayName = "Prototype Deck A";
            deckA.cards = new[]
            {
                new DeckDefinition.DeckCard { card = Find(cards, "ember_vanguard"), quantity = 1 },
                new DeckDefinition.DeckCard { card = Find(cards, "ember_archer"), quantity = 2 },
                new DeckDefinition.DeckCard { card = Find(cards, "ember_burnseer"), quantity = 1 },
                new DeckDefinition.DeckCard { card = Find(cards, "tidal_priest"), quantity = 1 },
                new DeckDefinition.DeckCard { card = Find(cards, "tidal_lancer"), quantity = 1 },
                new DeckDefinition.DeckCard { card = Find(cards, "tidal_sniper"), quantity = 1 },
                new DeckDefinition.DeckCard { card = Find(cards, "grove_guardian"), quantity = 1 },
                new DeckDefinition.DeckCard { card = Find(cards, "grove_shaper"), quantity = 1 },
                new DeckDefinition.DeckCard { card = Find(cards, "grove_raincaller"), quantity = 1 },
                new DeckDefinition.DeckCard { card = Find(cards, "alloy_bulwark"), quantity = 1 },
                new DeckDefinition.DeckCard { card = Find(cards, "alloy_hoplite"), quantity = 1 },
                new DeckDefinition.DeckCard { card = Find(cards, "void_stalker"), quantity = 2 },
                new DeckDefinition.DeckCard { card = Find(cards, "void_magus"), quantity = 1 },
                new DeckDefinition.DeckCard { card = Find(cards, "tidal_waveblade"), quantity = 1 },
                new DeckDefinition.DeckCard { card = Find(cards, "grove_myr"), quantity = 2 },
                new DeckDefinition.DeckCard { card = Find(cards, "ember_colossus"), quantity = 1 }
            };
            EditorUtility.SetDirty(deckA);

            var deckB = CreateOrLoadAsset<DeckDefinition>(CardsEditorPaths.Decks + "/Deck_PlayerB.asset");
            deckB.deckId = "player_b";
            deckB.displayName = "Prototype Deck B";
            deckB.cards = new[]
            {
                new DeckDefinition.DeckCard { card = Find(cards, "void_caller"), quantity = 1 },
                new DeckDefinition.DeckCard { card = Find(cards, "void_stalker"), quantity = 2 },
                new DeckDefinition.DeckCard { card = Find(cards, "void_magus"), quantity = 1 },
                new DeckDefinition.DeckCard { card = Find(cards, "alloy_ballista"), quantity = 1 },
                new DeckDefinition.DeckCard { card = Find(cards, "alloy_bulwark"), quantity = 1 },
                new DeckDefinition.DeckCard { card = Find(cards, "alloy_hoplite"), quantity = 2 },
                new DeckDefinition.DeckCard { card = Find(cards, "tidal_priest"), quantity = 1 },
                new DeckDefinition.DeckCard { card = Find(cards, "tidal_lancer"), quantity = 1 },
                new DeckDefinition.DeckCard { card = Find(cards, "tidal_sniper"), quantity = 1 },
                new DeckDefinition.DeckCard { card = Find(cards, "ember_archer"), quantity = 1 },
                new DeckDefinition.DeckCard { card = Find(cards, "ember_vanguard"), quantity = 1 },
                new DeckDefinition.DeckCard { card = Find(cards, "ember_burnseer"), quantity = 1 },
                new DeckDefinition.DeckCard { card = Find(cards, "grove_guardian"), quantity = 1 },
                new DeckDefinition.DeckCard { card = Find(cards, "grove_raincaller"), quantity = 1 },
                new DeckDefinition.DeckCard { card = Find(cards, "grove_shaper"), quantity = 1 },
                new DeckDefinition.DeckCard { card = Find(cards, "grove_myr"), quantity = 1 },
                new DeckDefinition.DeckCard { card = Find(cards, "ember_colossus"), quantity = 1 },
                new DeckDefinition.DeckCard { card = Find(cards, "tidal_waveblade"), quantity = 1 }
            };
            EditorUtility.SetDirty(deckB);
        }

        private static AbilityDefinition MakeAbility(string fileName, string displayName, AbilityTrigger trigger, TargetSelectorDefinition selector, params EffectDefinition[] effects)
        {
            var ability = CreateOrLoadAsset<AbilityDefinition>($"{CardsEditorPaths.Abilities}/{fileName}.asset");
            ability.abilityId = fileName;
            ability.displayName = displayName;
            ability.trigger = trigger;
            ability.targetSelector = selector;
            ability.effects = effects;
            EditorUtility.SetDirty(ability);
            return ability;
        }

        private static Sprite PrepareSprite(string relativePath)
        {
            var importer = AssetImporter.GetAtPath(relativePath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(relativePath);
        }

        private static Material GetOrCreateMaterial(string name, Color baseColor)
        {
            var path = $"{CardsEditorPaths.Materials}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/CardPulseUnlit.shader");
                if (shader == null)
                {
                    shader = Shader.Find("UI/Default");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", baseColor);
            }

            if (material.HasProperty("_GlowColor"))
            {
                material.SetColor("_GlowColor", baseColor * 1.2f);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static CardVisualProfile MakeVisual(string name, Color primary, Color secondary)
        {
            var visual = CreateOrLoadAsset<CardVisualProfile>($"{CardsEditorPaths.Visuals}/{name}.asset");
            visual.primaryColor = primary;
            visual.secondaryColor = secondary;
            visual.glowColor = primary * 1.2f;

            visual.artwork = PrepareSprite("Assets/Art/Prototype/" + name.Replace("VP_", "").ToLowerInvariant() + "_art.png");
            visual.frame = PrepareSprite("Assets/Art/Prototype/card_frame.png");
            visual.icon = PrepareSprite("Assets/Art/Prototype/" + name.Replace("VP_", "").ToLowerInvariant() + "_icon.png");

            visual.cardMaterial = GetOrCreateMaterial(name + "_Card", primary);
            visual.highlightMaterial = GetOrCreateMaterial(name + "_Highlight", primary * 1.15f);

            EditorUtility.SetDirty(visual);
            return visual;
        }

        private static T CreateAssetIfMissing<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static T CreateOrLoadAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static CardDefinition Find(List<CardDefinition> cards, string id)
        {
            return cards.Find(x => x != null && x.cardId == id);
        }

        private static void EnsureFolderRecursive(string assetFolderPath)
        {
            var parts = assetFolderPath.Split('/');
            var current = parts[0];

            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}