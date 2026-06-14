#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Flippy.CardDuelMobile.EditorTools
{
    /// <summary>
    /// Forces card-art and art-pack textures to import as readable Sprites so
    /// <see cref="Flippy.CardDuelMobile.UI.CardArtLibrary"/> can composite them at runtime
    /// (art + frame + faction overlay/crest) into a single sprite.
    ///
    /// Applies to everything under Assets/Resources/CardArt and Assets/Resources/Art.
    ///
    /// In addition, textures under Assets/Resources/Art/KenneyUI/ are 9-slice chrome
    /// (panels / buttons / input fields) consumed by
    /// <see cref="Flippy.CardDuelMobile.UI.DeckBuilding.KenneyUiSkin"/> with
    /// <c>Image.Type.Sliced</c>. The Kenney pack ships with NO sprite borders, so
    /// without a border <c>Sliced</c> rendering degenerates to a plain stretch and the
    /// panels/buttons look flat (the bug this addresses). We therefore set a sensible
    /// 9-slice <see cref="TextureImporter.spriteBorder"/> on those textures here so the
    /// corners are preserved and the chrome reads crisp at any size.
    ///
    /// The border logic is scoped ONLY to the KenneyUI path so card frames/icons
    /// (which must NOT be sliced) are unaffected.
    /// </summary>
    public sealed class CardArtImportPostprocessor : AssetPostprocessor
    {
        private const string KenneyUiFolder = "/Resources/Art/KenneyUI/";

        private void OnPreprocessTexture()
        {
            var path = assetPath.Replace('\\', '/');
            if (!path.Contains("/Resources/CardArt/") && !path.Contains("/Resources/Art/"))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.isReadable = true;           // required for runtime CPU compositing
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;

            // Pixel-art icons / item / UI sprites: keep them CRISP (no bilinear blur) and
            // uncompressed so small symbols read cleanly at any scale. Scoped to those folders
            // so the composited card art/frames (read via GetPixels32) keep their defaults.
            if (path.Contains("/Resources/Art/icons/") || path.Contains("/Resources/Art/items/")
                || path.Contains("/Resources/Art/ui/"))
            {
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
            }

            // 9-slice chrome (Kenney UI) needs a sprite border or Image.Type.Sliced
            // renders it as a flat stretch. Set it here from the source dimensions.
            if (path.Contains(KenneyUiFolder))
            {
                ApplyKenneyBorder(importer, path);
            }
        }

        /// <summary>
        /// Chooses and assigns a 9-slice border for a Kenney chrome sprite based on its
        /// pixel size and which family it belongs to. Border is a Vector4 (L, B, R, T).
        ///
        /// Rationale for the fractions:
        /// - Panels (panel_brown 100x100, panelInset_beige 93x94) have thick decorative
        ///   frames, so we keep a large border (~1/3 of the smaller dimension) to protect
        ///   the corner ornamentation when stretched across a full screen.
        /// - Buttons / input fields (192x64 rectangles, 64x64 squares) have a moderate
        ///   rounded-corner + bevel, so ~1/4 of the smaller dimension preserves the corner
        ///   radius and the bottom "depth" lip without eating the flat center.
        ///
        /// Borders are kept symmetric and capped so opposing borders can never overlap
        /// (L+R &lt; width, B+T &lt; height), which would make Unity reject the import.
        /// </summary>
        private static void ApplyKenneyBorder(TextureImporter importer, string path)
        {
            // NOTE: TextureImporter.GetSourceTextureWidthAndHeight throws when called
            // from OnPreprocessTexture (the texture has not finished importing yet), so
            // we read the dimensions straight from the PNG header on disk instead.
            if (!TryReadPngSize(path, out int w, out int h) || w <= 0 || h <= 0)
            {
                return;
            }

            // Panels get a heftier border than buttons / inputs.
            bool isPanel = path.Contains("panel_brown") || path.Contains("panelInset");
            float fraction = isPanel ? (1f / 3f) : (1f / 4f);

            int smaller = Mathf.Min(w, h);
            int border = Mathf.RoundToInt(smaller * fraction);

            // Never let opposing borders meet/overlap — Unity requires L+R < w and B+T < h.
            // Leave at least 2px of un-bordered center on each axis.
            border = Mathf.Max(1, border);
            border = Mathf.Min(border, (w - 2) / 2);
            border = Mathf.Min(border, (h - 2) / 2);

            importer.spriteBorder = new Vector4(border, border, border, border);
        }

        /// <summary>
        /// Reads width/height from a PNG's IHDR chunk without importing the texture.
        /// PNG layout: 8-byte signature, then the IHDR chunk whose data begins at byte
        /// 16 with width (4 bytes, big-endian) followed by height (4 bytes, big-endian).
        /// Returns false for non-PNG / unreadable files.
        /// </summary>
        private static bool TryReadPngSize(string assetPath, out int width, out int height)
        {
            width = 0;
            height = 0;

            try
            {
                using var fs = new FileStream(assetPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var header = new byte[24];
                if (fs.Read(header, 0, header.Length) < header.Length)
                {
                    return false;
                }

                // PNG signature: 89 50 4E 47 0D 0A 1A 0A
                if (header[0] != 0x89 || header[1] != 0x50 || header[2] != 0x4E || header[3] != 0x47)
                {
                    return false;
                }

                width  = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
#endif
