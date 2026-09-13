using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    internal static class StageScenery
    {
        public static bool Add(RunUI ui, RectTransform parent, MusicDefinition music, string name)
        {
            var stage = StageCatalog.Find(music.Id);
            var texture = stage == null ? null : Resources.Load<Texture2D>(stage.BackgroundPath);
            if (texture == null) return false;
            var art = ui.Rect(name, parent); RunUI.Stretch(art);
            var image = art.gameObject.AddComponent<RawImage>(); image.texture = texture; image.raycastTarget = false;
            var fit = art.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectRatio = (float)texture.width / texture.height;
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            return true;
        }
    }
}
