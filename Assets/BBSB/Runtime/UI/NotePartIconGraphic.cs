using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class NotePartIconGraphic : MaskableGraphic
    {
        private NotePartDefinition part;
        public void Bind(NotePartDefinition value) { part = value; raycastTarget = false; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (part == null) return;
            var r = rectTransform.rect; var c = r.center; float s = Mathf.Min(r.width, r.height) * .33f;
            var tint = part.Kind == NotePartKind.Frame ? RunUI.Gold : RunUI.Teal;
            NoteWorkshopMesh.Diamond(vh, c, s * 1.35f, new Color(tint.r, tint.g, tint.b, .12f));
            switch (part.Effect)
            {
                case NotePartEffect.Power:
                    NoteWorkshopMesh.Diamond(vh, c, s * .75f, tint);
                    NoteWorkshopMesh.Line(vh, c + new Vector2(-s, -s), c + new Vector2(s, s), 3, RunUI.TextColor); break;
                case NotePartEffect.Mend:
                    NoteWorkshopMesh.Quad(vh, c.x - 3, c.y - s, 6, s * 2, tint);
                    NoteWorkshopMesh.Quad(vh, c.x - s, c.y - 3, s * 2, 6, tint); break;
                case NotePartEffect.Pierce:
                    NoteWorkshopMesh.Line(vh, c - Vector2.up * s, c + Vector2.up * s, 4, tint);
                    NoteWorkshopMesh.Line(vh, c + new Vector2(-s * .65f, .15f * s), c + Vector2.up * s, 4, tint);
                    NoteWorkshopMesh.Line(vh, c + new Vector2(s * .65f, .15f * s), c + Vector2.up * s, 4, tint); break;
                case NotePartEffect.ExtraTap:
                    NoteWorkshopMesh.Diamond(vh, c - Vector2.right * s * .6f, s * .42f, tint);
                    NoteWorkshopMesh.Diamond(vh, c + Vector2.right * s * .6f, s * .42f, tint); break;
                case NotePartEffect.Hold:
                    NoteWorkshopMesh.Quad(vh, c.x - 6, c.y - s, 12, s * 2, new Color(tint.r, tint.g, tint.b, .4f));
                    NoteWorkshopMesh.Quad(vh, c.x - 11, c.y - s, 22, 5, tint);
                    NoteWorkshopMesh.Quad(vh, c.x - 11, c.y + s - 5, 22, 5, tint); break;
                case NotePartEffect.CrossTap:
                    NoteWorkshopMesh.Line(vh, c + new Vector2(-s, -s), c + new Vector2(s, s), 4, tint);
                    NoteWorkshopMesh.Line(vh, c + new Vector2(-s, s), c + new Vector2(s, -s), 4, tint); break;
            }
        }
    }

    internal static class NoteWorkshopMesh
    {
        internal static void Quad(VertexHelper vh, float x, float y, float w, float h, Color tint)
        {
            Polygon(vh, new Vector2(x, y), new Vector2(x, y + h), new Vector2(x + w, y + h), new Vector2(x + w, y), tint);
        }
        internal static void Diamond(VertexHelper vh, Vector2 c, float radius, Color tint) =>
            Polygon(vh, c + Vector2.left * radius, c + Vector2.up * radius, c + Vector2.right * radius, c + Vector2.down * radius, tint);
        internal static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 side = new Vector2(a.y - b.y, b.x - a.x).normalized * width * .5f;
            Polygon(vh, a - side, a + side, b + side, b - side, tint);
        }
        private static void Polygon(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int start = vh.currentVertCount;
            vh.AddVert(a, tint, Vector2.zero); vh.AddVert(b, tint, Vector2.zero);
            vh.AddVert(c, tint, Vector2.zero); vh.AddVert(d, tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
