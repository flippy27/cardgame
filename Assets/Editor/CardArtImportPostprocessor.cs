#if UNITY_EDITOR
using UnityEditor;

namespace Flippy.CardDuelMobile.EditorTools
{
    /// <summary>
    /// Forces card-art and art-pack textures to import as readable Sprites so
    /// <see cref="Flippy.CardDuelMobile.UI.CardArtLibrary"/> can composite them at runtime
    /// (art + frame + faction overlay/crest) into a single sprite.
    ///
    /// Applies to everything under Assets/Resources/CardArt and Assets/Resources/Art.
    /// </summary>
    public sealed class CardArtImportPostprocessor : AssetPostprocessor
    {
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
        }
    }
}
#endif
