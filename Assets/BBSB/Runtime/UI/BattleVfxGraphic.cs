using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleVfxGraphic : MaskableGraphic
    {
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private string key;
        private Sprite artwork;
        public bool HasArtwork => artwork != null;
        public override Texture mainTexture => artwork != null ? artwork.texture : base.mainTexture;
        internal void Bind(string id, Color tint)
        {
            if (key != id)
            {
                key = id;
                if (!sprites.TryGetValue(id, out artwork))
                { artwork = Resources.Load<Sprite>(BattleVfxCatalog.Root + id); sprites[id] = artwork; }
                SetAllDirty();
            }
            color = tint; raycastTarget = false;
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = GetPixelAdjustedRect();
            if (artwork != null)
            {
                var uv = UnityEngine.Sprites.DataUtility.GetOuterUV(artwork);
                vh.AddVert(new Vector2(r.xMin, r.yMin), color, new Vector2(uv.x, uv.y));
                vh.AddVert(new Vector2(r.xMin, r.yMax), color, new Vector2(uv.x, uv.w));
                vh.AddVert(new Vector2(r.xMax, r.yMax), color, new Vector2(uv.z, uv.w));
                vh.AddVert(new Vector2(r.xMax, r.yMin), color, new Vector2(uv.z, uv.y));
                vh.AddTriangle(0, 1, 2); vh.AddTriangle(2, 3, 0); return;
            }
            // Useful geometry remains visible while the separate transparent-art ZIP is being installed.
            Vector2 center = r.center;
            if (key == "arrow" || key == "bolt" || key == "pierce")
            {
                Triangle(vh, center + new Vector2(r.width * .42f, 0), center + new Vector2(r.width * .12f, r.height * .13f), center + new Vector2(r.width * .12f, -r.height * .13f));
                Triangle(vh, center + new Vector2(-r.width * .4f, 0), center + new Vector2(r.width * .2f, r.height * .04f), center + new Vector2(r.width * .2f, -r.height * .04f));
                return;
            }
            bool ring = key == "ring" || key == "slash" || key == "dodge";
            int segments = ring ? 36 : 16;
            for (int i = 0; i < segments; i++)
            {
                float sweep = key == "slash" || key == "dodge" ? 4.5f : Mathf.PI * 2;
                float a = i * sweep / segments, b = (i + 1) * sweep / segments;
                Vector2 first = Vector2.Scale(new Vector2(Mathf.Cos(a), Mathf.Sin(a)), r.size) * (ring || i % 2 == 0 ? .44f : .19f);
                Vector2 next = Vector2.Scale(new Vector2(Mathf.Cos(b), Mathf.Sin(b)), r.size) * (ring || i % 2 == 1 ? .44f : .19f);
                if (ring)
                {
                    Triangle(vh, center + first, center + next, center + next * .8f);
                    Triangle(vh, center + first, center + next * .8f, center + first * .8f);
                }
                else Triangle(vh, center, center + first, center + next);
            }
        }
        private void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c)
        {
            int start = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero); vh.AddVert(c, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
        }
    }
}
