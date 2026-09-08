using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    public sealed class MapConnectionsGraphic : MaskableGraphic
    {
        private RunSession session;
        public void Bind(RunSession value) { session = value; raycastTarget = false; SetVerticesDirty(); }
        public static Vector2 Position(StageNode node)
        { return new Vector2(.18f + node.Column * .32f, .14f + node.Row * .24f); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (session == null) return;
            Rect rect = rectTransform.rect;
            foreach (var from in session.Map.Nodes)
                foreach (var targetId in from.Next)
                {
                    var target = session.Map.Find(targetId);
                    var a = Position(from); var b = Position(target);
                    a = new Vector2(rect.xMin + a.x * rect.width, rect.yMin + a.y * rect.height);
                    b = new Vector2(rect.xMin + b.x * rect.width, rect.yMin + b.y * rect.height);
                    Color tint = RunUI.Hex("354058");
                    if (Contains(session.Visited, from.Id) && Contains(session.Visited, targetId)) tint = RunUI.Gold;
                    else if (session.CurrentNode == from && session.CanEnter(targetId)) tint = RunUI.Teal;
                    DrawLine(vh, a, b, tint, tint == RunUI.Hex("354058") ? 2 : 4);
                }
        }

        private static bool Contains(System.Collections.Generic.IReadOnlyList<string> list, string value)
        { foreach (var entry in list) if (entry == value) return true; return false; }

        private static void DrawLine(VertexHelper vh, Vector2 a, Vector2 b, Color tint, float width)
        {
            Vector2 perpendicular = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
            int start = vh.currentVertCount;
            vh.AddVert(a - perpendicular, tint, Vector2.zero); vh.AddVert(a + perpendicular, tint, Vector2.zero);
            vh.AddVert(b + perpendicular, tint, Vector2.zero); vh.AddVert(b - perpendicular, tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
