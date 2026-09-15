using TMPro;
using System.Collections.Generic;
using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GestureIconGraphic : MaskableGraphic
    {
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private Sprite artwork;
        private TextMeshProUGUI fallbackLabel;
        public GestureKind Kind { get; private set; }
        public bool Large { get; private set; }
        public bool HasArtwork => artwork != null;
        public override Texture mainTexture => artwork != null ? artwork.texture : base.mainTexture;

        public void Bind(GestureKind kind, bool large = false, TMP_FontAsset font = null)
        {
            Kind = kind; Large = large; raycastTarget = false;
            string path = GestureIconCatalog.ResourcePath(kind, large);
            if (!cache.TryGetValue(path, out artwork) || artwork == null)
            {
                artwork = Resources.Load<Sprite>(path);
                if (artwork != null) cache[path] = artwork;
            }
            if (large && artwork == null && fallbackLabel == null && font != null)
            {
                var ui = new RunUI(font);
                fallbackLabel = ui.Label(transform, "", 16, RunUI.Gold, 22, TextAlignmentOptions.Center);
                RunUI.Overlay(fallbackLabel.rectTransform, new Vector2(.14f, .10f), new Vector2(.86f, .31f), Vector2.zero, Vector2.zero);
                fallbackLabel.enableAutoSizing = true; fallbackLabel.fontSizeMin = 6; fallbackLabel.fontSizeMax = 48;
                fallbackLabel.raycastTarget = false;
            }
            if (fallbackLabel != null)
            { fallbackLabel.text = GestureIconCatalog.Name(kind); fallbackLabel.gameObject.SetActive(large && artwork == null); }
            SetAllDirty();
        }

        internal static GestureIconGraphic Create(Transform parent, GestureKind kind, bool large, TMP_FontAsset font = null)
        {
            var go = new GameObject("Gesture " + GestureIconCatalog.Name(kind), typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            var icon = go.AddComponent<GestureIconGraphic>(); icon.Bind(kind, large, font); return icon;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = GetPixelAdjustedRect();
            if (artwork == null)
            {
                GestureIconMesh.Badge(vh, r.center, r.size * .5f, Kind, Large); return;
            }
            var uv = UnityEngine.Sprites.DataUtility.GetOuterUV(artwork);
            vh.AddVert(new Vector3(r.xMin, r.yMin), color, new Vector2(uv.x, uv.y));
            vh.AddVert(new Vector3(r.xMin, r.yMax), color, new Vector2(uv.x, uv.w));
            vh.AddVert(new Vector3(r.xMax, r.yMax), color, new Vector2(uv.z, uv.w));
            vh.AddVert(new Vector3(r.xMax, r.yMin), color, new Vector2(uv.z, uv.y));
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(2, 3, 0);
        }
    }

    /// <summary>Recognizable symbols also survive a missing PNG pack and tiny timeline markers.</summary>
    internal static class GestureIconMesh
    {
        internal static void Badge(VertexHelper vh, Vector2 center, Vector2 radius, GestureKind kind, bool large = false)
        {
            var tint = RunUI.Hex(GestureIconCatalog.ColorHex(kind));
            Disc(vh, center, radius, RunUI.Ink);
            Disc(vh, center, radius * .93f, RunUI.Gold);
            Disc(vh, center, radius * .82f, Color.Lerp(RunUI.Ink, tint, .70f));
            var origin = large ? center + new Vector2(0, radius.y * .16f) : center;
            var size = radius * (large ? .58f : .64f);
            Symbol(vh, origin, size, kind, large, RunUI.TextColor);
        }
        private static void Symbol(VertexHelper vh, Vector2 c, Vector2 r, GestureKind kind, bool large, Color tint)
        {
            if (kind == GestureKind.Tap)
            {
                Triangle(vh,P(c,r,-.85f,.52f),P(c,r,.85f,.52f),P(c,r,0,-.80f),tint);
                if (large)
                {
                    // Two concentric downward triangles, never two vertically stacked arrows.
                    Triangle(vh,P(c,r,-.61f,.38f),P(c,r,.61f,.38f),P(c,r,0,-.57f),RunUI.Ink);
                    Triangle(vh,P(c,r,-.36f,.23f),P(c,r,.36f,.23f),P(c,r,0,-.34f),tint);
                }
            }
            else if (kind == GestureKind.Hold)
            {
                Disc(vh,c,r * .51f,tint);
                if (large) { Disc(vh,c,r*.34f,RunUI.Ink); Disc(vh,c,r*.19f,tint);
                    Line(vh,P(c,r,0,-.85f),c,r.x*.19f,tint); }
            }
            else if (kind == GestureKind.Flick)
            {
                // A single lower-left to upper-right stroke communicates outward motion.
                Line(vh,P(c,r,-.64f,-.66f),P(c,r,.64f,.66f),r.x*.24f,tint);
                if (large)
                {
                    Disc(vh,P(c,r,-.65f,-.65f),r*.20f,tint);
                    Triangle(vh,P(c,r,.83f,.85f),P(c,r,.16f,.68f),P(c,r,.65f,.16f),tint);
                }
            }
            else if (kind == GestureKind.Dive)
            {
                Line(vh,P(c,r,-.64f,.70f),P(c,r,-.64f,0),r.x*.23f,tint);
                for(int i=0;i<14;i++) { float a=Mathf.PI+i*Mathf.PI/14, b=Mathf.PI+(i+1)*Mathf.PI/14;
                    Line(vh,P(c,r,.64f*Mathf.Cos(a),.64f*Mathf.Sin(a)),P(c,r,.64f*Mathf.Cos(b),.64f*Mathf.Sin(b)),r.x*.23f,tint); }
                Line(vh,P(c,r,.64f,0),P(c,r,.64f,.70f),r.x*.23f,tint);
                if(large) Triangle(vh,P(c,r,.64f,.88f),P(c,r,.30f,.43f),P(c,r,.98f,.43f),tint);
            }
            else if (kind == GestureKind.Shake)
            {
                Line(vh,P(c,r,-.73f,.34f),P(c,r,.73f,.34f),r.x*.24f,tint);
                Line(vh,P(c,r,-.73f,-.34f),P(c,r,.73f,-.34f),r.x*.24f,tint);
                if(large) { Triangle(vh,P(c,r,.92f,.34f),P(c,r,.40f,.70f),P(c,r,.40f,-.02f),tint);
                    Triangle(vh,P(c,r,-.92f,-.34f),P(c,r,-.40f,.02f),P(c,r,-.40f,-.70f),tint); }
            }
        }
        private static Vector2 P(Vector2 c,Vector2 r,float x,float y) => c + new Vector2(r.x*x,r.y*y);
        internal static void Disc(VertexHelper vh, Vector2 c, Vector2 r, Color tint)
        {
            const int n=32; int first=vh.currentVertCount; vh.AddVert(c,tint,Vector2.zero);
            for(int i=0;i<=n;i++) { float a=i*Mathf.PI*2/n;
                vh.AddVert(c+new Vector2(Mathf.Cos(a)*r.x,Mathf.Sin(a)*r.y),tint,Vector2.zero);
                if(i>0) vh.AddTriangle(first,first+i,first+i+1); }
        }
        private static void Triangle(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Color tint)
        { int i=vh.currentVertCount; vh.AddVert(a,tint,Vector2.zero); vh.AddVert(b,tint,Vector2.zero); vh.AddVert(c,tint,Vector2.zero); vh.AddTriangle(i,i+1,i+2); }
        private static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color tint)
        { var d=(b-a).normalized; var n=new Vector2(-d.y,d.x)*width*.5f;
            Triangle(vh,a-n,a+n,b+n,tint); Triangle(vh,b+n,b-n,a-n,tint); }
    }
}
