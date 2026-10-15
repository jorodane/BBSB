using BBSB.Runtime.UI;
using UnityEditor;
using UnityEngine;

namespace BBSB.Editor
{
    public sealed class PlayerDefenseArtImporter : AssetPostprocessor
    {
        private bool IsDefense => assetPath == "Assets/BBSB/Resources/" + PlayerDefenseView.ResourcePath + ".png";
        private void OnPreprocessTexture()
        {
            if (!IsDefense) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 4096;
            importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
            var platform = importer.GetDefaultPlatformTextureSettings();
            platform.format = TextureImporterFormat.RGBA32; platform.maxTextureSize = 4096; importer.SetPlatformTextureSettings(platform);
        }
        private void OnPostprocessTexture(Texture2D texture)
        {
            if (!IsDefense) return;
            bool transparent = false;
            foreach (var pixel in texture.GetPixels32()) if (pixel.a == 0) { transparent = true; break; }
            if (!transparent) Debug.LogError("Defense motion artwork requires real PNG alpha: " + assetPath);
            if (texture.width != 1254 || texture.height != 1254)
                Debug.LogWarning("Defense motion uses authored bounds for the supplied 1254 x 1254 atlas. Check the pose rectangles after replacing it.", texture);
        }
    }
}
