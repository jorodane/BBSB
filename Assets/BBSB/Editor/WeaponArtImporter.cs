using System;
using BBSB.Runtime.UI;
using UnityEditor;
using UnityEngine;

namespace BBSB.Editor
{
    public sealed class WeaponArtImporter : AssetPostprocessor
    {
        private bool IsWeapon => assetPath.StartsWith("Assets/BBSB/Resources/" + WeaponArtLayout.Root,
            StringComparison.Ordinal) && assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
        private void OnPreprocessTexture()
        {
            if (!IsWeapon) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false; importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            // Preserve the source alpha through platform import.
            var platform = importer.GetDefaultPlatformTextureSettings();
            platform.format = TextureImporterFormat.RGBA32; platform.maxTextureSize = 512; importer.SetPlatformTextureSettings(platform);
            importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(.5f, .5f); importer.SetTextureSettings(settings);
        }
        private void OnPostprocessTexture(Texture2D texture)
        {
            if (!IsWeapon) return;
            bool transparent = false;
            foreach (var pixel in texture.GetPixels32())
                if (pixel.a == 0) { transparent = true; break; }
            if (!transparent) Debug.LogError("Weapon artwork must be a PNG with real alpha transparency: " + assetPath);
        }
        public override uint GetVersion() => 1;
    }
}
