using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    internal static class StageScenery
    {
        public static bool Add(RunUI ui, RectTransform parent, MusicDefinition music, string name,
            ShoulderViewPresentation presentation = null)
        {
            var stage = StageCatalog.Find(music.Id);
            if (presentation == null) presentation = Resources.Load<ShoulderViewPresentation>(ShoulderViewPresentation.ResourcePath);
            var setting = presentation != null ? presentation.FindStage(music.Id, stage?.MapId) : null;
            var texture = setting != null ? setting.backdrop : stage == null ? null : Resources.Load<Texture2D>(stage.BackgroundPath);
            if (texture == null) return false;
            if (setting != null)
            {
                var sky = ui.Rect(name + " sky", parent); RunUI.Stretch(sky); ui.Background(sky, setting.sky);
            }
            var art = ui.Rect(name, parent); RunUI.Stretch(art);
            var image = art.gameObject.AddComponent<RawImage>(); image.texture = texture; image.raycastTarget = false;
            var fit = art.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectRatio = (float)texture.width / texture.height;
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            return true;
        }
    }
}
