using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    internal enum PreparationGraphicKind { Atmosphere, Glass, Start, Health, Versus, Icon }
    internal enum PreparationIcon { None, Weapons, Practice, Book, Menu, Search, Play, Crest }

    /// <summary>Resolution-independent preparation chrome; only the scenery and portraits need textures.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PreparationGraphic : MaskableGraphic
    {
        private PreparationGraphicKind kind;
        private PreparationIcon icon;
        private Color accent;
        public float Value { get; private set; } = 1;

        internal void Configure(PreparationGraphicKind style, Color tint, PreparationIcon symbol = PreparationIcon.None, float value = 1)
        { kind = style; accent = tint; icon = symbol; Value = Mathf.Clamp01(value); raycastTarget = false; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect;
            if (r.width <= 0 || r.height <= 0) return;
            float unit = Mathf.Min(r.width, r.height);
            switch (kind)
            {
                case PreparationGraphicKind.Atmosphere:
                    Quad(vh, r, new Rect(0, .72f, 1, .28f), Clear(RunUI.Ink), Alpha(RunUI.Ink, .97f));
                    Quad(vh, r, new Rect(0, 0, 1, .25f), Alpha(RunUI.Ink, .93f), Clear(RunUI.Ink));
                    for (int i = 0; i < 6; i++)
                    {
                        float y = .04f + i * .11f;
                        Triangle(vh, P(r, 0, y), P(r, .30f, y + .15f), P(r, 0, y + .015f), Alpha(RunUI.Hex("F72C7F"), .12f));
                        Triangle(vh, P(r, 1, y + .04f), P(r, .71f, y + .17f), P(r, 1, y + .065f), Alpha(RunUI.Hex("9775FF"), .14f));
                    }
                    break;
                case PreparationGraphicKind.Glass:
                    Bevel(vh, r, .025f, Color.Lerp(RunUI.Ink, accent, .23f), Color.Lerp(RunUI.Ink, accent, .50f), Alpha(accent, .85f));
                    Quad(vh, r, new Rect(.018f, .49f, .964f, .46f), Clear(Color.white), Alpha(Color.white, .13f));
                    Line(vh, P(r, .03f, .04f), P(r, .97f, .04f), 2, Alpha(accent, .55f));
                    break;
                case PreparationGraphicKind.Start:
                    for (int i = 3; i >= 0; i--)
                        Hex(vh, r, 3 + i * 5, Alpha(accent, .018f + (3 - i) * .015f));
                    Hex(vh, r, 0, accent);
                    var inset = new Rect(r.x + 5, r.y + 5, r.width - 10, r.height - 10);
                    Hex(vh, inset, 0, RunUI.Hex("704319"));
                    inset = new Rect(r.x + 9, r.y + 9, r.width - 18, r.height - 18);
                    Polygon(vh, HexPoints(inset, 0), RunUI.Hex("FFBE35"), RunUI.Hex("FFF1A4"));
                    Line(vh, P(r, .15f, .88f), P(r, .85f, .88f), 2, Alpha(Color.white, .75f));
                    break;
                case PreparationGraphicKind.Health:
                    Bevel(vh, r, .018f, RunUI.Hex("080D19"), RunUI.Hex("1C2A3D"), Alpha(accent, .7f));
                    if (Value > 0)
                    {
                        float end = .02f + .96f * Value;
                        var points = new[] { P(r, .02f, .18f), P(r, end - .008f * Value, .18f),
                            P(r, end, .82f), P(r, .02f + .008f * Value, .82f) };
                        Polygon(vh, points, Color.Lerp(accent, Color.black, .15f), Color.Lerp(accent, Color.white, .55f));
                        Line(vh, P(r, .02f + .013f * Value, .78f), P(r, end - .008f * Value, .78f), 1.5f, Alpha(Color.white, .75f));
                    }
                    Line(vh, P(r, .035f, 0), P(r, .965f, 0), 1, Alpha(accent, .45f));
                    break;
                case PreparationGraphicKind.Versus:
                    var pink = RunUI.Hex("FF337F"); var violet = RunUI.Hex("9872FF");
                    for (int i = 0; i < 14; i++)
                    {
                        float x = .06f + (i * .173f) % .4f, y = .07f + (i * .117f) % .35f;
                        Triangle(vh, P(r, x, y), P(r, .93f - (i % 3) * .04f, .89f - i * .012f),
                            P(r, x + .12f, y + .025f), i < 7 ? Alpha(RunUI.Ink, .9f) : Alpha(i % 2 == 0 ? pink : violet, .75f));
                    }
                    Triangle(vh, P(r, .23f, .1f), P(r, .15f, .88f), P(r, .48f, .53f), Alpha(pink, .55f));
                    Triangle(vh, P(r, .45f, .2f), P(r, .88f, .68f), P(r, .75f, .16f), Alpha(violet, .5f));
                    break;
                case PreparationGraphicKind.Icon: DrawIcon(vh, r, unit); break;
            }
        }

        private void DrawIcon(VertexHelper vh, Rect r, float unit)
        {
            float width = Mathf.Max(1.5f, unit * .075f);
            var c = r.center;
            switch (icon)
            {
                case PreparationIcon.Weapons:
                    foreach (int side in new[] { -1, 1 })
                    {
                        Vector2 a = c + new Vector2(-side * .30f, -.34f) * unit, b = c + new Vector2(side * .28f, .32f) * unit;
                        Line(vh, a, b, width, accent);
                        Line(vh, c + new Vector2(-side * .28f, -.1f) * unit, c + new Vector2(-side * .05f, -.28f) * unit, width, accent);
                        Triangle(vh, b + Vector2.up * unit * .12f, b + Vector2.left * unit * .1f, b + Vector2.right * unit * .1f, accent);
                    }
                    break;
                case PreparationIcon.Practice:
                    Arc(vh, c, unit * .36f, 0, 360, width, accent);
                    Arc(vh, c, unit * .18f, 0, 360, width * 1.35f, accent); break;
                case PreparationIcon.Book:
                    foreach (int side in new[] { -1, 1 })
                    {
                        var points = new[] { c + new Vector2(side * .025f, -.3f) * unit, c + new Vector2(side * .38f, -.23f) * unit,
                            c + new Vector2(side * .38f, .34f) * unit, c + new Vector2(side * .025f, .26f) * unit };
                        Polygon(vh, points, accent, accent);
                        Line(vh, c + new Vector2(side * .45f, -.33f) * unit, c + new Vector2(side * .45f, .27f) * unit, width * .5f, accent);
                    }
                    break;
                case PreparationIcon.Menu:
                    for (int i = -1; i <= 1; i++)
                    {
                        Line(vh, c + new Vector2(-.17f, i * .23f) * unit, c + new Vector2(.39f, i * .23f) * unit, width, accent);
                        Line(vh, c + new Vector2(-.4f, i * .23f) * unit, c + new Vector2(-.33f, i * .23f) * unit, width, accent);
                    }
                    break;
                case PreparationIcon.Search:
                    Arc(vh, c + new Vector2(-.08f, .08f) * unit, unit * .25f, 0, 360, width, accent);
                    Line(vh, c + new Vector2(.1f, -.1f) * unit, c + new Vector2(.37f, -.37f) * unit, width, accent); break;
                case PreparationIcon.Play:
                    Triangle(vh, c + new Vector2(-.23f, -.33f) * unit, c + new Vector2(.33f, 0) * unit,
                        c + new Vector2(-.23f, .33f) * unit, accent); break;
                case PreparationIcon.Crest:
                    var diamond = new[] { c + Vector2.down * unit * .46f, c + Vector2.right * unit * .34f,
                        c + Vector2.up * unit * .46f, c + Vector2.left * unit * .34f };
                    for (int i = 0; i < 4; i++) Line(vh, diamond[i], diamond[(i + 1) % 4], width, accent);
                    Line(vh, c - Vector2.up * unit * .37f, c + Vector2.up * unit * .37f, width, accent);
                    Line(vh, c - Vector2.right * unit * .19f, c + Vector2.right * unit * .19f, width, accent); break;
            }
        }

        private static void Bevel(VertexHelper vh, Rect r, float cut, Color bottom, Color top, Color edge)
        {
            var points = new[] { P(r, cut, 0), P(r, 1 - cut, 0), P(r, 1, .08f), P(r, 1, .92f),
                P(r, 1 - cut, 1), P(r, cut, 1), P(r, 0, .92f), P(r, 0, .08f) };
            Polygon(vh, points, bottom, top);
            for (int i = 0; i < points.Length; i++) Line(vh, points[i], points[(i + 1) % points.Length], 1.3f, edge);
        }
        private static Vector2[] HexPoints(Rect r, float margin) => new[] {
            new Vector2(r.xMin - margin, r.center.y), new Vector2(r.xMin + r.height * .27f, r.yMin - margin),
            new Vector2(r.xMax - r.height * .27f, r.yMin - margin), new Vector2(r.xMax + margin, r.center.y),
            new Vector2(r.xMax - r.height * .27f, r.yMax + margin), new Vector2(r.xMin + r.height * .27f, r.yMax + margin) };
        private static void Hex(VertexHelper vh, Rect r, float margin, Color tint) => Polygon(vh, HexPoints(r, margin), tint, tint);
        private static Vector2 P(Rect r, float x, float y) => new Vector2(r.xMin + x * r.width, r.yMin + y * r.height);
        private static Color Alpha(Color color, float a) { color.a = a; return color; }
        private static Color Clear(Color color) => Alpha(color, 0);
        private static void Quad(VertexHelper vh, Rect r, Rect box, Color bottom, Color top) =>
            Polygon(vh, new[] { P(r, box.xMin, box.yMin), P(r, box.xMax, box.yMin), P(r, box.xMax, box.yMax), P(r, box.xMin, box.yMax) }, bottom, top);
        private static void Polygon(VertexHelper vh, Vector2[] points, Color bottom, Color top)
        {
            float min = points[0].y, max = min;
            foreach (var p in points) { min = Mathf.Min(min, p.y); max = Mathf.Max(max, p.y); }
            int start = vh.currentVertCount;
            foreach (var p in points) vh.AddVert(p, Color.Lerp(bottom, top, Mathf.InverseLerp(min, max, p.y)), Vector2.zero);
            for (int i = 1; i < points.Length - 1; i++) vh.AddTriangle(start, start + i, start + i + 1);
        }
        private static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            int start = vh.currentVertCount;
            vh.AddVert(a, tint, Vector2.zero); vh.AddVert(b, tint, Vector2.zero); vh.AddVert(c, tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
        }
        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
        {
            var direction = b - a; if (direction.sqrMagnitude < .000001f) return;
            var normal = new Vector2(-direction.y, direction.x).normalized * width * .5f;
            Polygon(vh, new[] { a - normal, b - normal, b + normal, a + normal }, tint, tint);
        }
        private static void Arc(VertexHelper vh, Vector2 center, float radius, float start, float sweep, float width, Color tint)
        {
            const int segments = 32;
            for (int i = 0; i < segments; i++)
            {
                float a = (start + sweep * i / segments) * Mathf.Deg2Rad, b = (start + sweep * (i + 1) / segments) * Mathf.Deg2Rad;
                Line(vh, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, width, tint);
            }
        }
    }
}
