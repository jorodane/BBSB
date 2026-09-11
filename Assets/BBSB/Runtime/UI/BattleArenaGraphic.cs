using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    internal enum BattleEffectKind { Call, CallStep, CallStomp, CallSweep, CallRise, CallDip, CallSway, CallFlash, Attack, Counter, Guard, Dive, Flick, Shake, Miss }

    internal struct BattleEffect
    {
        public BattleEffectKind Kind;
        public Vector2 From, To;
        public Color Tint;
        public float Progress, Strength;
    }

    internal struct BattleGroundShadow
    {
        public Vector2 Ground, Radius;
        public float Advance;
    }

    /// <summary>Untextured arena, spectral weapons and gesture effects. All time is supplied by the song.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleArenaGraphic : MaskableGraphic
    {
        private bool backdrop;
        private readonly List<BattleGroundShadow> groundShadows = new List<BattleGroundShadow>();
        private double beat;
        private float energy, guard, shake;
        private IReadOnlyList<BattleEffect> effects;
        private Vector2 heroGround = PlayerMotionDisplay.DefaultGround;
        private Vector2 heroImpact = new Vector2(.24f, .49f);

        internal void SetHeroAnchors(Vector2 ground, Vector2 impact)
        {
            if (heroGround == ground && heroImpact == impact) return;
            heroGround = ground; heroImpact = impact; SetVerticesDirty();
        }

        internal void SetBackdrop()
        { backdrop = true; raycastTarget = false; SetVerticesDirty(); }

        internal void SetGroundShadows(IReadOnlyList<BattleGroundShadow> values)
        {
            bool changed = values.Count != groundShadows.Count;
            if (!changed) for (int i = 0; i < values.Count; i++)
                if (values[i].Ground != groundShadows[i].Ground || values[i].Radius != groundShadows[i].Radius ||
                    values[i].Advance != groundShadows[i].Advance) { changed = true; break; }
            if (!changed) return;
            groundShadows.Clear();
            foreach (var value in values) groundShadows.Add(value);
            SetVerticesDirty();
        }

        internal void SetFrame(double songBeat, float weaponEnergy, float guardStrength, float shakeStrength,
            IReadOnlyList<BattleEffect> values)
        {
            backdrop = false; beat = songBeat; energy = weaponEnergy; guard = guardStrength; shake = shakeStrength;
            effects = values; raycastTarget = false; SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect;
            if (r.width <= 0 || r.height <= 0) return;
            if (backdrop) { Stage(vh, r); return; }
            Weapons(vh, r);
            if (effects == null) return;
            foreach (var effect in effects) Effect(vh, r, effect);
        }

        private void Stage(VertexHelper vh, Rect r)
        {
            Quad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin),
                new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax),
                RunUI.Hex("20283D"), RunUI.Hex("0A101E"));
            float horizon = Mathf.Clamp(.49f + heroGround.y - PlayerMotionDisplay.DefaultGround.y, .34f, .70f);
            Quad(vh, Point(r, Vector2.zero), Point(r, Vector2.right),
                Point(r, new Vector2(1, horizon)), Point(r, new Vector2(0, horizon)),
                RunUI.Hex("303A50"), RunUI.Hex("202B3D"));
            Line(vh, Point(r, new Vector2(0, horizon)), Point(r, new Vector2(1, horizon)), 1,
                Alpha(RunUI.Muted, .14f));
            // Shallow parallel floor seams keep the camera side-on instead of pointing into the enemy group.
            for (int i = -1; i < 6; i++)
                Line(vh, Point(r, new Vector2(i * .24f, 0)), Point(r, new Vector2(i * .24f + .12f, horizon)),
                    1, Alpha(RunUI.Muted, .045f));
            for (int i = 1; i < 4; i++)
            {
                float y = horizon * i / 4;
                Line(vh, Point(r, new Vector2(0, y)), Point(r, new Vector2(1, y)), 1, Alpha(RunUI.Muted, .05f));
            }
            foreach (var shadow in groundShadows)
            {
                var p = Point(r, shadow.Ground);
                var radius = Vector2.Scale(shadow.Radius, r.size);
                Ellipse(vh, p, radius * 1.15f, Alpha(Color.black, .10f));
                Ellipse(vh, p, radius, Alpha(Color.black, .28f));
                if (shadow.Advance > 0)
                    Arc(vh, p, radius * 1.13f, 0, 360, 1.5f, Alpha(RunUI.Gold, shadow.Advance * .32f));
            }
        }

        private void Weapons(VertexHelper vh, Rect r)
        {
            var center = Point(r, heroImpact - new Vector2(0, .035f));
            float unit = Mathf.Min(r.width, r.height) / 600;
            float turn = (float)(beat * .24);
            // A small oscillation accelerates the orbit during Shake without introducing a second clock.
            turn += Mathf.Sin((float)beat * 12) * shake * .25f;
            var tint = Color.Lerp(RunUI.Gold, RunUI.Teal, energy * .7f);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2 / 5 + turn;
                var p = center + new Vector2(Mathf.Cos(a) * r.width * .13f, Mathf.Sin(a) * r.height * .085f);
                float size = (20 + energy * 7) * unit;
                var up = new Vector2(Mathf.Sin(a) * .45f, 1).normalized;
                var right = new Vector2(up.y, -up.x);
                Arc(vh, p, Vector2.one * size * 1.3f, 20 + a * Mathf.Rad2Deg, 130, unit,
                    Alpha(tint, .25f + energy * .3f));
                if (i == 1) // Shield.
                {
                    Triangle(vh, p - right * size * .6f + up * size * .5f,
                        p + right * size * .6f + up * size * .5f, p - up * size, Alpha(tint, .8f));
                    Line(vh, p - up * size * .6f, p + up * size * .45f, 2 * unit, Color.white);
                }
                else if (i == 3) // Hammer.
                {
                    Line(vh, p - up * size, p + up * size * .5f, 4 * unit, tint);
                    Line(vh, p + up * size * .5f - right * size * .6f,
                        p + up * size * .5f + right * size * .6f, size * .65f, tint);
                }
                else // Sword, spear and dagger.
                {
                    float length = i == 2 ? 1.5f : i == 4 ? .8f : 1.1f;
                    Triangle(vh, p - right * size * .23f, p + right * size * .23f, p + up * size * length, tint);
                    Line(vh, p - up * size * .65f, p + up * size * length * .65f, 2 * unit, Color.white);
                    Line(vh, p - right * size * .4f, p + right * size * .4f, 3 * unit, tint);
                }
            }
            if (guard > 0)
            {
                var p = Point(r, heroImpact);
                Arc(vh, p, new Vector2(r.width * .09f, r.height * .15f), -75, 150, 5 * unit, Alpha(RunUI.Teal, guard * .85f));
                Arc(vh, p, new Vector2(r.width * .10f, r.height * .17f), -70, 140, unit, Alpha(RunUI.Gold, guard * .6f));
            }
        }

        private void Effect(VertexHelper vh, Rect r, BattleEffect fx)
        {
            var from = Point(r, fx.From); var to = Point(r, fx.To);
            float p = Mathf.Clamp01(fx.Progress), fade = 1 - p;
            float unit = Mathf.Min(r.width, r.height) / 600;
            float size = (18 + 40 * p) * unit * fx.Strength;
            Color tint = Alpha(fx.Tint, fade * fx.Strength);
            switch (fx.Kind)
            {
                case BattleEffectKind.Call:
                    Arc(vh, from, new Vector2(size * 1.5f, size * .48f), 0, 360, 3 * unit, tint);
                    for (int i = -1; i <= 1; i++)
                        Line(vh, from + new Vector2(i * 18, 35) * unit,
                            from + new Vector2(i * 24, 47 + p * 14) * unit, 3 * unit, tint);
                    break;
                case BattleEffectKind.CallStep:
                    for (int i = 0; i < 2; i++)
                        Ellipse(vh, from + new Vector2((i * 2 - 1) * size * .6f, i * size * .2f),
                            new Vector2(size * .25f, size * .12f), tint);
                    break;
                case BattleEffectKind.CallStomp:
                    for (int i = 0; i < 2; i++)
                        Arc(vh, from, new Vector2(size * (1.5f + i * .6f), size * (.3f + i * .15f)), 0, 360, 4 * unit, tint);
                    for (int i = -1; i <= 1; i++)
                        Line(vh, from + new Vector2(i * size, size), from + new Vector2(i * size, 4 * unit), 3 * unit, tint);
                    break;
                case BattleEffectKind.CallSweep:
                    // A broad tail trail accompanies the cutout's twist; it never travels toward the hero.
                    Arc(vh, from + Vector2.up * size * .4f, new Vector2(size * 2.4f, size * .85f),
                        170 - p * 100, 190, 8 * unit, tint);
                    Triangle(vh, from - Vector2.right * size * 2.5f, from + new Vector2(-size * 1.3f, size),
                        from + new Vector2(-size * 1.2f, -size * .3f), tint);
                    break;
                case BattleEffectKind.CallRise:
                case BattleEffectKind.CallDip:
                    float direction = fx.Kind == BattleEffectKind.CallRise ? 1 : -1;
                    for (int i = -1; i <= 1; i++)
                    {
                        var tip = to + new Vector2(i * size * .7f, direction * size * (1 + p));
                        Line(vh, tip - new Vector2(size * .25f, direction * size * .35f), tip, 3 * unit, tint);
                        Line(vh, tip - new Vector2(-size * .25f, direction * size * .35f), tip, 3 * unit, tint);
                    }
                    break;
                case BattleEffectKind.CallSway:
                    Arc(vh, to, new Vector2(size * 1.7f, size), 25 - p * 100, 110, 4 * unit, tint);
                    Arc(vh, to, new Vector2(size * 1.7f, size), 205 - p * 100, 110, 4 * unit, tint);
                    break;
                case BattleEffectKind.CallFlash:
                    Spark(vh, to, p, unit * 1.4f, tint);
                    Diamond(vh, to, new Vector2(size * .5f, size), tint);
                    break;
                case BattleEffectKind.Attack:
                    if (p < .5f)
                    {
                        float travel = p * 2;
                        var head = Vector2.Lerp(from, to, travel);
                        var tail = Vector2.Lerp(from, to, Mathf.Max(0, travel - .22f));
                        Line(vh, tail, head, 5 * unit, Alpha(fx.Tint, .75f));
                        Diamond(vh, head, new Vector2(6, 12) * unit, fx.Tint);
                    }
                    else Spark(vh, to, (p - .5f) * 2, unit, tint);
                    break;
                case BattleEffectKind.Guard:
                    Arc(vh, from, new Vector2(size * 1.1f, size * 1.3f), 5, 170, 6 * unit, tint);
                    Counter(vh, from, to, p, unit, tint);
                    break;
                case BattleEffectKind.Dive:
                    Arc(vh, from, new Vector2(size * 1.9f, size * .5f), 10, 260, 4 * unit, tint);
                    Counter(vh, from, to, p, unit, tint);
                    break;
                case BattleEffectKind.Shake:
                    for (int i = 0; i < 3; i++)
                        Arc(vh, from, Vector2.one * (size + i * 12 * unit), p * 320 + i * 120, 85, 3 * unit, tint);
                    Counter(vh, from, to, p, unit, tint);
                    break;
                case BattleEffectKind.Flick:
                    Arc(vh, from + Vector2.right * size * .5f, new Vector2(size, size * 1.6f), 110 + p * 130, 120, 7 * unit, tint);
                    Counter(vh, from, to, p, unit, tint);
                    break;
                case BattleEffectKind.Counter:
                    Arc(vh, from, new Vector2(size * 1.4f, size), 15 + p * 150, 130, 6 * unit, tint);
                    Counter(vh, from, to, p, unit, tint);
                    break;
                case BattleEffectKind.Miss:
                    var offset = new Vector2(12, 12) * unit * (1 + p);
                    Line(vh, from - offset, from + offset, 4 * unit, tint);
                    offset.x = -offset.x; Line(vh, from - offset, from + offset, 4 * unit, tint);
                    break;
            }
        }

        private static void Counter(VertexHelper vh, Vector2 from, Vector2 to, float p, float unit, Color tint)
        {
            float travel = Mathf.Clamp01(p * 3);
            Line(vh, Vector2.Lerp(from, to, Mathf.Max(0, travel - .3f)), Vector2.Lerp(from, to, travel), 3 * unit, tint);
            if (p > .2f)
            {
                var offset = new Vector2(30, 40) * unit * Mathf.Min(1, p * 4);
                Line(vh, to - offset, to + offset, 5 * unit, tint);
                Spark(vh, to, p, unit, tint);
            }
        }

        private static void Spark(VertexHelper vh, Vector2 p, float progress, float unit, Color tint)
        {
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2 / 7;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Line(vh, p + d * (6 + progress * 22) * unit, p + d * (16 + progress * 37) * unit, 2 * unit, tint);
            }
        }

        private static Vector2 Point(Rect r, Vector2 p) => new Vector2(r.xMin + p.x * r.width, r.yMin + p.y * r.height);
        private static Color Alpha(Color c, float a) { c.a = Mathf.Clamp01(a); return c; }
        private static void Diamond(VertexHelper vh, Vector2 p, Vector2 size, Color tint)
        { Quad(vh, p - Vector2.up * size.y, p + Vector2.right * size.x, p + Vector2.up * size.y, p - Vector2.right * size.x, tint, tint); }
        private static void Ellipse(VertexHelper vh, Vector2 p, Vector2 radius, Color tint)
        {
            for (int i = 0; i < 40; i++)
            {
                float a = i * Mathf.PI / 20, b = (i + 1) * Mathf.PI / 20;
                Triangle(vh, p, p + Vector2.Scale(new Vector2(Mathf.Cos(a), Mathf.Sin(a)), radius),
                    p + Vector2.Scale(new Vector2(Mathf.Cos(b), Mathf.Sin(b)), radius), tint);
            }
        }
        private static void Arc(VertexHelper vh, Vector2 p, Vector2 radius, float start, float sweep, float width, Color tint)
        {
            int segments = Mathf.Max(4, Mathf.CeilToInt(Mathf.Abs(sweep) / 8));
            for (int i = 0; i < segments; i++)
            {
                float a = (start + sweep * i / segments) * Mathf.Deg2Rad, b = (start + sweep * (i + 1) / segments) * Mathf.Deg2Rad;
                Line(vh, p + Vector2.Scale(new Vector2(Mathf.Cos(a), Mathf.Sin(a)), radius),
                    p + Vector2.Scale(new Vector2(Mathf.Cos(b), Mathf.Sin(b)), radius), width, tint);
            }
        }
        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
        {
            var d = b - a; if (d.sqrMagnitude < .001f) return;
            var n = new Vector2(-d.y, d.x).normalized * width * .5f;
            Quad(vh, a - n, b - n, b + n, a + n, tint, tint);
        }
        private static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color tint)
        {
            int n = vh.currentVertCount;
            vh.AddVert(a, tint, Vector2.zero); vh.AddVert(b, tint, Vector2.zero); vh.AddVert(c, tint, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2);
        }
        private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color bottom, Color top)
        {
            int n = vh.currentVertCount;
            vh.AddVert(a, bottom, Vector2.zero); vh.AddVert(b, bottom, Vector2.zero);
            vh.AddVert(c, top, Vector2.zero); vh.AddVert(d, top, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
        }
    }
}
