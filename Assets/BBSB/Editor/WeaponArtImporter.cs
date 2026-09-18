using System;
using BBSB.Runtime.UI;
using UnityEditor;
using UnityEngine;

namespace BBSB.Editor
{
    public sealed class WeaponArtImporter : AssetPostprocessor
    {
        private bool IsAttributeAtlas => assetPath.StartsWith("Assets/BBSB/Resources/" + WeaponAttributeArtLayout.Root, StringComparison.Ordinal);
        private bool IsWeapon => (IsAttributeAtlas || assetPath.StartsWith("Assets/BBSB/Resources/" + WeaponArtLayout.Root,
            StringComparison.Ordinal)) && assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
        private void OnPreprocessTexture()
        {
            if (!IsWeapon) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = true;
            bool atlas = IsAttributeAtlas || assetPath.Contains("/bow/") || assetPath.Contains("/crossbow/") || assetPath.Contains("/wand/");
            int limit = IsAttributeAtlas ? 2048 : atlas ? 1024 : 512;
            if (atlas) importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false; importer.maxTextureSize = limit;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            // Preserve the source alpha through platform import.
            var platform = importer.GetDefaultPlatformTextureSettings();
            platform.format = TextureImporterFormat.RGBA32; platform.maxTextureSize = limit; importer.SetPlatformTextureSettings(platform);
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
        public override uint GetVersion() => 3;
    }
}
