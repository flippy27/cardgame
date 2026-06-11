#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Flippy.CardDuelMobile.EditorTools
{
    /// <summary>
    /// Generates placeholder card art (one colored PNG per cardId) so the game looks
    /// production-ready before real illustrations exist. Each file is named by its cardId,
    /// so replacing the real art later is a 1:1 drop-in: overwrite Assets/Resources/CardArt/{cardId}.png.
    ///
    /// Reads the live card catalog from the API to know every cardId + faction + rarity.
    /// Server must be running (default http://127.0.0.1:5000). Use 127.0.0.1, NOT localhost
    /// (localhost resolves to IPv6 ::1 and the Docker/WSL2 port-forward hangs on it).
    ///
    /// Output:
    ///   Assets/Resources/CardArt/{cardId}.png        — faction-colored card with rarity border
    ///   Assets/Resources/CardArt/frames/frame_{0..3}.png — rarity frames (transparent center)
    /// These paths match <c>CardArtLibrary</c>'s Resources lookups.
    /// </summary>
    public static class PlaceholderArtGenerator
    {
        private const string ApiBaseUrl = "http://127.0.0.1:5000";
        private const string OutputDir = "Assets/Resources/CardArt";
        private const string FramesDir = "Assets/Resources/CardArt/frames";
        private const int CardWidth = 512;
        private const int CardHeight = 768;
        private const int BorderPx = 16;

        // [PROPOSAL] palettes — see CARD_ART_BIBLE.md. Free to retune.
        private static readonly Color32[] FactionColors =
        {
            new Color32(0xC0, 0x39, 0x2B, 0xFF), // 0 Ember  red
            new Color32(0x2E, 0x86, 0xC1, 0xFF), // 1 Tidal  blue
            new Color32(0x27, 0xAE, 0x60, 0xFF), // 2 Grove  green
            new Color32(0x7F, 0x8C, 0x8D, 0xFF), // 3 Alloy  gray
            new Color32(0x8E, 0x44, 0xAD, 0xFF), // 4 Void   purple
        };

        private static readonly Color32[] RarityColors =
        {
            new Color32(0x95, 0xA5, 0xA6, 0xFF), // 0 Common    silver
            new Color32(0x34, 0x98, 0xDB, 0xFF), // 1 Rare      blue
            new Color32(0x9B, 0x59, 0xB6, 0xFF), // 2 Epic      purple
            new Color32(0xF1, 0xC4, 0x0F, 0xFF), // 3 Legendary gold
        };

        [Serializable]
        private sealed class CardLite
        {
            public string cardId;
            public int cardFaction = -1;
            public int cardRarity = -1;
        }

        [Serializable]
        private sealed class CardListWrapper
        {
            public List<CardLite> items;
        }

        [MenuItem("Tools/CardDuel/Generate Placeholder Card Art")]
        public static void Generate()
        {
            var json = FetchCatalogJson();
            if (string.IsNullOrEmpty(json))
            {
                EditorUtility.DisplayDialog("Placeholder Art",
                    $"Could not fetch the card catalog from {ApiBaseUrl}/api/v1/cards.\nStart the server and try again.", "OK");
                return;
            }

            // JsonUtility can't parse a top-level array; wrap it.
            var wrapper = JsonUtility.FromJson<CardListWrapper>("{\"items\":" + json + "}");
            if (wrapper?.items == null || wrapper.items.Count == 0)
            {
                EditorUtility.DisplayDialog("Placeholder Art", "Catalog returned no cards.", "OK");
                return;
            }

            Directory.CreateDirectory(OutputDir);
            Directory.CreateDirectory(FramesDir);

            try
            {
                for (var i = 0; i < wrapper.items.Count; i++)
                {
                    var card = wrapper.items[i];
                    if (string.IsNullOrWhiteSpace(card.cardId))
                    {
                        continue;
                    }

                    EditorUtility.DisplayProgressBar("Generating placeholder art",
                        $"{card.cardId} ({i + 1}/{wrapper.items.Count})", (float)i / wrapper.items.Count);

                    var path = $"{OutputDir}/{card.cardId}.png";
                    if (File.Exists(path))
                    {
                        continue; // never overwrite real/existing art
                    }

                    var tex = BuildCardTexture(card.cardFaction, card.cardRarity);
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(tex);
                }

                for (var r = 0; r < RarityColors.Length; r++)
                {
                    var framePath = $"{FramesDir}/frame_{r}.png";
                    if (File.Exists(framePath))
                    {
                        continue;
                    }

                    var tex = BuildFrameTexture(r);
                    File.WriteAllBytes(framePath, tex.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(tex);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
            ApplySpriteImportSettings(OutputDir);
            ApplySpriteImportSettings(FramesDir);

            EditorUtility.DisplayDialog("Placeholder Art",
                $"Done. Generated placeholders for {wrapper.items.Count} cards in {OutputDir}.\n\n" +
                "Replace any file with the real illustration (keep the same {cardId}.png name).", "OK");
        }

        private static Texture2D BuildCardTexture(int faction, int rarity)
        {
            var baseColor = FactionColors[Mathf.Clamp(faction < 0 ? 3 : faction, 0, FactionColors.Length - 1)];
            var borderColor = RarityColors[Mathf.Clamp(rarity < 0 ? 0 : rarity, 0, RarityColors.Length - 1)];

            var tex = new Texture2D(CardWidth, CardHeight, TextureFormat.RGBA32, false);
            var pixels = new Color32[CardWidth * CardHeight];

            // darker header band (top 18%) for a card-like silhouette
            var headerTop = (int)(CardHeight * 0.82f);
            var header = Multiply(baseColor, 0.65f);

            for (var y = 0; y < CardHeight; y++)
            {
                for (var x = 0; x < CardWidth; x++)
                {
                    Color32 c;
                    var onBorder = x < BorderPx || x >= CardWidth - BorderPx || y < BorderPx || y >= CardHeight - BorderPx;
                    if (onBorder)
                    {
                        c = borderColor;
                    }
                    else if (y >= headerTop)
                    {
                        c = header;
                    }
                    else
                    {
                        c = baseColor;
                    }
                    pixels[y * CardWidth + x] = c;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static Texture2D BuildFrameTexture(int rarity)
        {
            var borderColor = RarityColors[Mathf.Clamp(rarity, 0, RarityColors.Length - 1)];
            var tex = new Texture2D(CardWidth, CardHeight, TextureFormat.RGBA32, false);
            var pixels = new Color32[CardWidth * CardHeight];
            var clear = new Color32(0, 0, 0, 0);

            for (var y = 0; y < CardHeight; y++)
            {
                for (var x = 0; x < CardWidth; x++)
                {
                    var onBorder = x < BorderPx || x >= CardWidth - BorderPx || y < BorderPx || y >= CardHeight - BorderPx;
                    pixels[y * CardWidth + x] = onBorder ? borderColor : clear;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static Color32 Multiply(Color32 c, float f)
        {
            return new Color32((byte)(c.r * f), (byte)(c.g * f), (byte)(c.b * f), c.a);
        }

        private static string FetchCatalogJson()
        {
            using var req = UnityWebRequest.Get($"{ApiBaseUrl}/api/v1/cards");
            req.timeout = 30;
            var op = req.SendWebRequest();

            var start = DateTime.UtcNow;
            while (!op.isDone)
            {
                if ((DateTime.UtcNow - start).TotalSeconds > 35)
                {
                    return null;
                }
                System.Threading.Thread.Sleep(20);
            }

            return req.result == UnityWebRequest.Result.Success ? req.downloadHandler.text : null;
        }

        private static void ApplySpriteImportSettings(string dir)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.SaveAndReimport();
                }
            }
        }
    }
}
#endif
