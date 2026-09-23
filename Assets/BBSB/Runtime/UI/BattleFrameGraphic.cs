using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleFrameGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect;
            Gradient(vh, r, 0, 0, .23f, 1, new Color(.015f, .015f, .03f, .78f), Color.clear, true);
            Gradient(vh, r, .77f, 0, 1, 1, Color.clear, new Color(.015f, .015f, .03f, .78f), true);
            Gradient(vh, r, 0, .81f, 1, 1, Color.clear, new Color(.015f, .015f, .03f, .58f), false);
            Gradient(vh, r, 0, 0, 1, .03f, new Color(.015f, .015f, .025f, .92f), new Color(.015f, .015f, .025f, .92f), false);
        }
        private static void Gradient(VertexHelper vh, Rect r, float x0, float y0, float x1, float y1, Color from, Color to, bool horizontal)
        {
            int i = vh.currentVertCount;
            vh.AddVert(new Vector2(r.xMin + r.width * x0, r.yMin + r.height * y0), from, Vector2.zero);
            vh.AddVert(new Vector2(r.xMin + r.width * x0, r.yMin + r.height * y1), horizontal ? from : to, Vector2.zero);
            vh.AddVert(new Vector2(r.xMin + r.width * x1, r.yMin + r.height * y1), to, Vector2.zero);
            vh.AddVert(new Vector2(r.xMin + r.width * x1, r.yMin + r.height * y0), horizontal ? to : from, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
