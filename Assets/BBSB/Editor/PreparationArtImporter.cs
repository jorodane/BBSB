using System;
using BBSB.Runtime.UI;
using UnityEditor;
using UnityEngine;

namespace BBSB.Editor
{
    public sealed class PreparationArtImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            string root = "Assets/BBSB/Resources/" + BattlePreparationView.ArtRoot;
            if (!assetPath.StartsWith(root, StringComparison.Ordinal) || !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return;
            var importer = (TextureImporter)assetImporter;
            bool background = assetPath == root + "preparation-background.png";
            // Respect later artist adjustments. A dropped portrait starts as a complete alpha sprite.
            if (!importer.importSettingsMissing) return;
            importer.textureType = background ? TextureImporterType.Default : TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = !background;
            importer.mipmapEnabled = false; importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
            if (background) return;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center; settings.spritePivot = new Vector2(.5f, .5f);
            importer.SetTextureSettings(settings);
        }
    }
}
