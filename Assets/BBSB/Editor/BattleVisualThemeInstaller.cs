using System;
using System.IO;
using BBSB.Runtime.UI;
using UnityEditor;
using UnityEngine;

namespace BBSB.Editor
{
    // PNGs are delivered separately. Import once into normal serialized references;
    // subsequent reimports preserve an artist's edits and the source asset GUIDs.
    public static class BattleVisualThemeInstaller
    {
        public const string ArtRoot = "Assets/BBSB/Art/CathedralBattle";
        public const string ThemePath = "Assets/BBSB/Resources/BBSB/Presentation/CathedralBattle.asset";
        private static readonly string[] Names = { "cathedral-arena", "sky-clouds", "note-light", "note-dark", "health-frame",
            "combo-crest", "beat-ring", "weapon-halo", "judgment-flash" };
        private static bool queued, installing;
        [InitializeOnLoadMethod] private static void Initialize()
        {
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            EditorApplication.playModeStateChanged += PlayModeChanged;
            Schedule();
        }
        private static void PlayModeChanged(PlayModeStateChange state)
        { if (state == PlayModeStateChange.EnteredEditMode) Schedule(); }
        internal static void Schedule()
        {
            if (queued || installing) return;
            queued = true; EditorApplication.delayCall += AutoInstall;
        }
        private static void AutoInstall()
        {
            queued = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { Schedule(); return; }
            if (AssetDatabase.LoadAssetAtPath<BattleVisualTheme>(ThemePath) != null) return;
            foreach (var name in Names) if (!File.Exists(PathFor(name))) return;
            if (!TryInstall(out var problem)) Debug.LogError(problem);
        }
        [MenuItem("BBSB/Presentation/Install cathedral battle art")]
        private static void MenuInstall()
        {
            if (!TryInstall(out var problem)) { Debug.LogError(problem); return; }
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<BattleVisualTheme>(ThemePath);
        }
        public static bool TryInstall(out string problem)
        {
            problem = null;
            if (installing || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            { problem = "Stop Play Mode and wait for compilation before importing the cathedral art pack."; return false; }
            if (AssetDatabase.LoadAssetAtPath<BattleVisualTheme>(ThemePath) != null) return true;
            foreach (var name in Names)
                if (!File.Exists(PathFor(name))) { problem = "Copy the art ZIP's Assets folder into the Unity project. Missing: " + PathFor(name); return false; }
            installing = true;
            try
            {
                foreach (var name in Names)
                {
                    string path = PathFor(name);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) throw new InvalidOperationException("Could not import " + path);
                    Configure(importer, name == "cathedral-arena" || name == "sky-clouds"); importer.SaveAndReimport();
                }
                var theme = ScriptableObject.CreateInstance<BattleVisualTheme>();
                theme.arena = AssetDatabase.LoadAssetAtPath<Texture2D>(PathFor("cathedral-arena"));
                theme.skyClouds = AssetDatabase.LoadAssetAtPath<Texture2D>(PathFor("sky-clouds"));
                theme.lightNote = Sprite("note-light"); theme.darkNote = Sprite("note-dark");
                theme.healthFrame = Sprite("health-frame"); theme.comboCrest = Sprite("combo-crest");
                theme.beatRing = Sprite("beat-ring"); theme.weaponHalo = Sprite("weapon-halo");
                theme.judgmentFlash = Sprite("judgment-flash");
                EnsureFolder(Path.GetDirectoryName(ThemePath).Replace('\\', '/'));
                AssetDatabase.CreateAsset(theme, ThemePath); AssetDatabase.SaveAssets();
                Debug.Log("BBSB cathedral battle art connected: arena, clouds, crystal notes and five HUD/effect sprites.");
                return true;
            }
            catch (Exception e) { problem = e.ToString(); return false; }
            finally { installing = false; }
        }
        private static string PathFor(string name) => ArtRoot + "/" + name + ".png";
        private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(PathFor(name)) ??
            throw new InvalidOperationException("Missing imported sprite: " + name);
        internal static void Configure(TextureImporter importer, bool arena)
        {
            importer.textureType = arena ? TextureImporterType.Default : TextureImporterType.Sprite;
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false; importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear; importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = arena ? 4096 : 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            if (arena) return;
            importer.spriteImportMode = SpriteImportMode.Single; importer.spritePixelsPerUnit = 100;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(.5f, .5f); importer.SetTextureSettings(settings);
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
    internal sealed class BattleVisualArtPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(BattleVisualThemeInstaller.ArtRoot + "/", StringComparison.Ordinal))
                BattleVisualThemeInstaller.Configure((TextureImporter)assetImporter,
                    Path.GetFileNameWithoutExtension(assetPath) == "cathedral-arena" || Path.GetFileNameWithoutExtension(assetPath) == "sky-clouds");
        }
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
                if (path.StartsWith(BattleVisualThemeInstaller.ArtRoot + "/", StringComparison.Ordinal))
                { BattleVisualThemeInstaller.Schedule(); return; }
        }
    }
}
