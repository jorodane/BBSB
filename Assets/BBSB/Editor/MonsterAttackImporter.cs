using System;
using BBSB.Runtime.UI;
using UnityEditor;
using UnityEngine;

namespace BBSB.Editor
{
    /// <summary>New transparent PNGs placed in the documented folders work without manual Inspector setup.</summary>
    public sealed class MonsterAttackImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            const string root = "Assets/BBSB/Resources/" + MonsterAttackDefinition.ResourceRoot;
            if (!assetPath.StartsWith(root, StringComparison.Ordinal) || !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return;
            var importer = (TextureImporter)assetImporter;
            // Preserve the artist's later Sprite Editor slices, pivots, filtering and size choices.
            if (!importer.importSettingsMissing && importer.textureType == TextureImporterType.Sprite) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            bool body = assetPath.Contains("/body/") || assetPath.EndsWith("/idle.png", StringComparison.OrdinalIgnoreCase);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = body ? new Vector2(.5f, 0) : new Vector2(.5f, .5f);
            importer.SetTextureSettings(settings);
        }
    }
}
