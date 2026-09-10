using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    /// <summary>A small independent HUD control, with circular rendering and hit testing.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoundMenuGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect;
            Vector2 center = r.center; float radius = Mathf.Min(r.width, r.height) * .5f;
            if (radius <= 0) return;
            Disc(vh, center, radius, RunUI.Muted);
            Disc(vh, center, radius - 2, RunUI.Panel);
            for (int i = 0; i < 3; i++)
            {
                float y = r.yMin + r.height * (.55f + i * .105f);
                Quad(vh, new Vector2(center.x - radius * .34f, y),
                    new Vector2(center.x + radius * .34f, y + 3), RunUI.TextColor);
            }
        }

        public override bool Raycast(Vector2 point, Camera eventCamera)
        {
            if (!base.Raycast(point, eventCamera) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, point, eventCamera, out var local)) return false;
            float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .5f;
            return (local - rectTransform.rect.center).sqrMagnitude <= radius * radius;
        }

        private static void Disc(VertexHelper vh, Vector2 center, float radius, Color tint)
        {
            int start = vh.currentVertCount; vh.AddVert(center, tint, Vector2.zero);
            const int segments = 48;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint, Vector2.zero);
                if (i > 0) vh.AddTriangle(start, start + i, start + i + 1);
            }
        }

        private static void Quad(VertexHelper vh, Vector2 min, Vector2 max, Color tint)
        {
            int n = vh.currentVertCount;
            vh.AddVert(min, tint, Vector2.zero); vh.AddVert(new Vector2(min.x, max.y), tint, Vector2.zero);
            vh.AddVert(max, tint, Vector2.zero); vh.AddVert(new Vector2(max.x, min.y), tint, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
        }
    }
}
