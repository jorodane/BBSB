using System;
using BBSB.Core;
using UnityEditor;
using UnityEngine;

namespace BBSB.Editor
{
    public sealed class StageAssetImporter : AssetPostprocessor
    {
        private const string Root = "Assets/BBSB/Resources/BBSB/StageAssets/";
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root, StringComparison.Ordinal) || !assetImporter.importSettingsMissing) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default; importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048; importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear; importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Root, StringComparison.Ordinal) || !assetImporter.importSettingsMissing) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = false; importer.loadInBackground = false; importer.preloadAudioData = true;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis; settings.quality = .85f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
        }
        [MenuItem("BBSB/Validate stage asset pack")]
        public static void ValidatePack()
        {
            int missing = 0, invalid = 0;
            foreach (var stage in StageCatalog.All)
            {
                var image = Resources.Load<Texture2D>(stage.BackgroundPath);
                var audio = Resources.Load<AudioClip>(stage.AudioPath);
                if (image == null || audio == null) { Debug.LogWarning("Missing stage assets: " + stage.Id); missing++; continue; }
                if (Math.Abs(audio.length - stage.Music.DurationSeconds) > .05)
                { Debug.LogError("Stage audio duration does not match the score: " + stage.Id); invalid++; }
                Resources.UnloadAsset(image); Resources.UnloadAsset(audio);
            }
            Debug.Log("BBSB stage assets: " + (StageCatalog.All.Count - missing) + "/80 pairs; " + invalid + " duration errors.");
        }
    }
}
