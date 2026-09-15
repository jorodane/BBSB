using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace BBSB.Runtime.UI
{
    /// <summary>Shared TMP font; the editor persists it for direct prefab authoring.</summary>
    public static class PresentationFonts
    {
        public const string ResourcePath = "BBSB/Fonts/BBSBUI SDF";
        public const string SourcePath = "BBSB/Fonts/BBSBUI";
        private static TMP_FontAsset runtimeFont;

        public static TMP_FontAsset Load()
        {
            var saved = Resources.Load<TMP_FontAsset>(ResourcePath);
            if (saved != null) return saved;
            if (runtimeFont != null) return runtimeFont;
            runtimeFont = Create();
            if (runtimeFont != null) return runtimeFont;
            Debug.LogError("BBSB UI font is missing. Restore Assets/BBSB/Resources/BBSB/Fonts/BBSBUI.otf.");
            return TMP_Settings.defaultFontAsset;
        }

        public static TMP_FontAsset Create()
        {
            var source = Resources.Load<Font>(SourcePath);
            if (source == null) return null;
            var font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA,
                1024, 1024, AtlasPopulationMode.Dynamic);
            if (font == null) return null;
            font.name = "BBSBUI SDF";
            font.isMultiAtlasTexturesEnabled = true;
            return font;
        }

        // Only used until the editor has completed the one-time prefab font setup.
        public static bool NeedsProjectFont(TMP_Text text) => text.font == null || text.font.name == "LiberationSans SDF";
    }
}
