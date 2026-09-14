using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    /// <summary>Short tapered pink ribbons drawn in the same depth layer as their sampled weapon.</summary>
    public sealed class WeaponTrailGraphic : MaskableGraphic
    {
        private WeaponTrailHistory[] trails;
        private bool inFront;
        private double seconds;
        internal static WeaponTrailGraphic Create(Transform parent, string name, WeaponTrailHistory[] histories, bool front)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            node.transform.SetParent(parent, false);
            var rect = (RectTransform)node.transform; RunUI.Stretch(rect);
            var graphic = node.AddComponent<WeaponTrailGraphic>();
            graphic.trails = histories; graphic.inFront = front; graphic.raycastTarget = false;
            return graphic;
        }
        internal void SetTime(double time) { seconds = time; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (trails == null) return;
            var rect = rectTransform.rect; float unit = Mathf.Min(rect.width, rect.height) / 720;
            foreach (var trail in trails)
            {
                if (trail == null) continue;
                for (int i = 1; i < trail.Count; i++)
                {
                    var a = trail[i - 1]; var b = trail[i];
                    if (a.InFront != inFront || b.InFront != inFront) continue;
                    float fadeA = (float)WeaponTrailHistory.Opacity(seconds - a.Time);
                    float fadeB = (float)WeaponTrailHistory.Opacity(seconds - b.Time);
                    if (fadeB <= 0) continue;
                    var start = new Vector2(rect.xMin + (float)a.Position.X * rect.width, rect.yMin + (float)a.Position.Y * rect.height);
                    var end = new Vector2(rect.xMin + (float)b.Position.X * rect.width, rect.yMin + (float)b.Position.Y * rect.height);
                    Ribbon(vh, start, end, unit * 22, fadeA, fadeB, new Color(1, .12f, .53f, .16f));
                    Ribbon(vh, start, end, unit * 10, fadeA, fadeB, new Color(1, .25f, .64f, .72f));
                    Ribbon(vh, start, end, unit * 3, fadeA, fadeB, new Color(1, .69f, .86f, .8f));
                }
            }
        }
        private static void Ribbon(VertexHelper vh, Vector2 a, Vector2 b, float width, float fadeA, float fadeB, Color tint)
        {
            var delta = b - a; if (delta.sqrMagnitude < .001f) return;
            var normal = new Vector2(-delta.y, delta.x).normalized * width * .5f;
            var first = tint; first.a *= fadeA; var last = tint; last.a *= fadeB;
            float widthA = (float)WeaponFormation.Smooth(fadeA), widthB = (float)WeaponFormation.Smooth(fadeB);
            int index = vh.currentVertCount;
            vh.AddVert(a - normal * widthA, first, Vector2.zero); vh.AddVert(a + normal * widthA, first, Vector2.zero);
            vh.AddVert(b + normal * widthB, last, Vector2.zero); vh.AddVert(b - normal * widthB, last, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2); vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
