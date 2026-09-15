using System;
using System.Collections.Generic;
using System.IO;
using BBSB.Runtime.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BBSB.Editor
{
    /// <summary>Initializes the migrated prefabs once; subsequent artist edits stay authoritative.</summary>
    public sealed class PresentationFontBuilder : IPreprocessBuildWithReport
    {
        private const string FontPath = "Assets/BBSB/Resources/BBSB/Fonts/BBSBUI SDF.asset";
        public int callbackOrder => -100;
        public void OnPreprocessBuild(BuildReport report) => Ensure();

        [MenuItem("BBSB/Presentation/Set up TextMeshPro font")]
        public static void Ensure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
            {
                font = PresentationFonts.Create();
                if (font == null) throw new InvalidOperationException("BBSBUI.otf 폰트가 없어 TMP 폰트를 생성할 수 없어.");
                Directory.CreateDirectory(Path.GetDirectoryName(FontPath));
                AssetDatabase.CreateAsset(font, FontPath);
                // Keep the material and dynamic atlas inside the font asset, including in builds.
                font.material.name = font.name + " Material";
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (var atlas in font.atlasTextures)
                {
                    atlas.name = font.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(atlas, font);
                }
                EditorUtility.SetDirty(font);
                AssetDatabase.SaveAssets();
            }
            var library = Resources.Load<PresentationPrefabs>(PresentationPrefabs.ResourcePath);
            if (library == null || library.textMeshProVersion >= 1) return;
            var paths = new HashSet<string>();
            foreach (var screen in library.screens)
                if (screen != null && screen.prefab != null) paths.Add(AssetDatabase.GetAssetPath(screen.prefab));
            foreach (var template in new UnityEngine.Object[] { library.titleText, library.headingText,
                library.bodyText, library.captionText, library.primaryButton, library.secondaryButton })
                if (template != null) paths.Add(AssetDatabase.GetAssetPath(template));
            foreach (var path in paths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = false;
                    foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                    {
                        if (!PresentationFonts.NeedsProjectFont(text)) continue;
                        text.font = font; changed = true;
                    }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            if (library.defaultFont == null) library.defaultFont = font;
            library.textMeshProVersion = 1;
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }
    }
}
