using System;
using BBSB.Runtime.UI;
using UnityEditor;
using UnityEngine;

namespace BBSB.Editor
{
    public sealed class GestureIconImporter : AssetPostprocessor
    {
        private bool IsIcon => assetPath.StartsWith("Assets/BBSB/Resources/" + GestureIconCatalog.Root, StringComparison.Ordinal)
            && assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
        private void OnPreprocessTexture()
        {
            if (!IsIcon) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false; importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 512; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
            var platform = importer.GetDefaultPlatformTextureSettings(); platform.format = TextureImporterFormat.RGBA32;
            platform.maxTextureSize = 512; importer.SetPlatformTextureSettings(platform);
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(.5f,.5f); importer.SetTextureSettings(settings);
        }
        private void OnPostprocessTexture(Texture2D texture)
        {
            if (!IsIcon) return;
            if (texture.width != texture.height) Debug.LogError("Gesture icons must be square: " + assetPath);
            foreach (var pixel in texture.GetPixels32()) if (pixel.a == 0) return;
            Debug.LogError("Gesture icons require genuine transparent PNG alpha: " + assetPath);
        }
    }
}
