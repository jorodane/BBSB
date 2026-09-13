using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PreparationPortraitMask : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var rect = rectTransform.rect;
            var center = rect.center; float radius = Mathf.Min(rect.width, rect.height) * .5f;
            vh.AddVert(center, color, new Vector2(.5f, .5f));
            for (int i = 0; i <= 40; i++)
            {
                float angle = i * Mathf.PI * 2 / 40;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vh.AddVert(center + direction * radius, color, direction * .5f + Vector2.one * .5f);
                if (i > 0) vh.AddTriangle(0, i, i + 1);
            }
        }
    }
}
