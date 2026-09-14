using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PatternOverviewGraphic : MaskableGraphic
    {
        private RhythmPattern pattern;
        public float MaximumMarkerRadius { get; set; } = 8;
        public void Bind(RhythmPattern value) { pattern = value; raycastTarget = false; SetVerticesDirty(); }
        public static Color ActionColor(GestureKind kind) => RunUI.Hex(GestureIconCatalog.ColorHex(kind));
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (pattern == null) return;
            var rect = rectTransform.rect; int last = 0;
            foreach (var step in pattern.Steps) last = Mathf.Max(last, step.OffsetTick + step.DurationTicks);
            int span = last + RhythmTime.TicksPerBeat;
            float radius = Mathf.Min(MaximumMarkerRadius, rect.height * .48f);
            float left = rect.xMin + radius, width = Mathf.Max(1, rect.width - radius * 2), y = rect.center.y;
            Quad(vh, left, y - .5f, width, 1, RunUI.Muted);
            for (int tick = 0; tick <= span; tick += RhythmTime.TicksPerBeat)
                Quad(vh, left + width * tick / span - .5f, y - 3, 1, 6, RunUI.Muted);
            foreach (var step in pattern.Steps)
            {
                float x = left + width * step.OffsetTick / span, end = left + width * (step.OffsetTick + step.DurationTicks) / span;
                var tint = ActionColor(step.Kind);
                if (step.DurationTicks > 0) Quad(vh, x, y - 2, Mathf.Max(1, end - x), 4, tint);
                GestureIconMesh.Badge(vh, new Vector2(x,y), Vector2.one * radius, step.Kind);
                if (step.DurationTicks > 0) Quad(vh, end - 1.5f, y - 5, 3, 10, tint);
            }
        }
        private static void Quad(VertexHelper vh, float x, float y, float width, float height, Color tint)
        {
            int start = vh.currentVertCount;
            vh.AddVert(new Vector3(x, y, 0), tint, Vector2.zero); vh.AddVert(new Vector3(x, y + height, 0), tint, Vector2.zero);
            vh.AddVert(new Vector3(x + width, y + height, 0), tint, Vector2.zero); vh.AddVert(new Vector3(x + width, y, 0), tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
