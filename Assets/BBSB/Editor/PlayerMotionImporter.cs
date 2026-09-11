using System;
using System.IO;
using BBSB.Runtime.UI;
using UnityEditor;
using UnityEngine;

namespace BBSB.Editor
{
    /// <summary>Import the supplied PNG alpha and align transparent poses once during import.</summary>
    public sealed class PlayerMotionImporter : AssetPostprocessor
    {
        private bool IsPlayerAtlas => assetPath.StartsWith("Assets/BBSB/Resources/BBSB/BattleArt/PlayerMotion/", StringComparison.Ordinal)
            && assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

        // Reimport cached atlases after removing automatic background/color processing.
        public override uint GetVersion() => 3;

        private void OnPreprocessTexture()
        {
            if (!IsPlayerAtlas) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true; importer.mipmapEnabled = false;
            importer.isReadable = false; importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var platform = importer.GetDefaultPlatformTextureSettings();
            // Preserve the source PNG's alpha rather than inferring transparency from color.
            platform.format = TextureImporterFormat.RGBA32; platform.maxTextureSize = 2048;
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(platform);
        }

        private void OnPostprocessTexture(Texture2D texture)
        {
            if (!IsPlayerAtlas) return;
            var pixels = texture.GetPixels32();
            var rgba = new byte[pixels.Length * 4];
            for (int i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i]; int at = i * 4;
                rgba[at] = p.r; rgba[at + 1] = p.g; rgba[at + 2] = p.b; rgba[at + 3] = p.a;
            }
            string id = Path.GetFileNameWithoutExtension(assetPath);
            int columns = id == "idle" || id.StartsWith("tap-", StringComparison.Ordinal) ? 2 : 3;
            var prepared = PlayerAtlasProcessor.Prepare(rgba, texture.width, texture.height, columns, 2);
            for (int i = 0; i < pixels.Length; i++)
            { int at = i * 4; pixels[i] = new Color32(prepared[at], prepared[at + 1], prepared[at + 2], prepared[at + 3]); }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
        }
    }
}
