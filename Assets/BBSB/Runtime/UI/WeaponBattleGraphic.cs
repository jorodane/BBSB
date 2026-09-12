using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WeaponBattleGraphic : MaskableGraphic
    {
        private WeaponBattle combat;
        private double seconds;
        private Vector2 ground, hero;
        private readonly Vector2[] targets = new Vector2[RunRules.WeaponSlots];
        private readonly WeaponActivation[] active = new WeaponActivation[RunRules.WeaponSlots];
        private static readonly Color Edge = RunUI.Hex("CBA66F"), Metal = RunUI.Hex("251D31"), Glow = RunUI.Hex("FF4C9B");

        internal void SetFrame(WeaponBattle value, double time, Vector2 groundPoint, Vector2 heroPoint)
        {
            combat = value; seconds = time; ground = groundPoint; hero = heroPoint;
            for (int i = 0; i < active.Length; i++) active[i] = null;
            raycastTarget = false;
        }
        internal void SetActivation(WeaponActivation value, Vector2 target)
        { active[value.Slot] = value; targets[value.Slot] = target; }
        internal void Refresh() { SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (combat == null) return;
            Rect r = rectTransform.rect;
            float unit = Mathf.Min(r.width, r.height) / 720;
            float heroHeight = (hero.y - ground.y) / .46f;
            for (int slot = 0; slot < combat.Loadout.Equipment.Count; slot++)
            {
                var weapon = WeaponCatalog.Find(combat.Loadout.Equipment[slot].DefinitionId);
                float angle = (135 - slot * 65) * Mathf.Deg2Rad;
                var origin = hero + new Vector2(Mathf.Cos(angle) * heroHeight * r.height / r.width * .5f,
                    Mathf.Sin(angle) * heroHeight * .42f + heroHeight * .09f);
                var activation = active[slot];
                var point = origin; float rotation = -20 + slot * 10, scale = 1;
                if (activation != null)
                {
                    float p = (float)((seconds - activation.AtSeconds) / WeaponMotion.Duration(weapon.Kind));
                    var frame = WeaponMotion.Sample(weapon.Kind, new BattlePathPoint(origin.x, origin.y),
                        new BattlePathPoint(targets[slot].x, targets[slot].y), p);
                    point = new Vector2((float)frame.Position.X, (float)frame.Position.Y);
                    rotation = (float)frame.Rotation; scale = (float)frame.Scale;
                    var target = Point(r, targets[slot]);
                    Color glow = Glow; glow.a = 1 - p;
                    if (weapon.Kind == WeaponKind.Bell)
                    {
                        var wave = Vector2.Lerp(Point(r, origin), target, Mathf.Min(1, p / .6f));
                        for (int ring = 0; ring < 3; ring++) Arc(vh, wave, (14 + p * 45 + ring * 9) * unit, 0, 360, 2 * unit, glow);
                    }
                    else if (weapon.Kind == WeaponKind.Shield)
                        Arc(vh, Point(r, hero), 66 * unit, -70, 140, 5 * unit, RunUI.Teal);
                    else
                    {
                        var previous = WeaponMotion.Sample(weapon.Kind, new BattlePathPoint(origin.x, origin.y),
                            new BattlePathPoint(targets[slot].x, targets[slot].y), Mathf.Max(0, p - .07f));
                        Line(vh, Point(r, new Vector2((float)previous.Position.X, (float)previous.Position.Y)), Point(r, point), 5 * unit, glow);
                        if (p > .55f && p < .85f)
                        {
                            float hit = (p - .55f) / .3f;
                            int rays = weapon.Kind == WeaponKind.Hammer ? 10 : weapon.Kind == WeaponKind.Blade ? 8 : 5;
                            for (int i = 0; i < rays; i++)
                            {
                                float a = i * Mathf.PI * 2 / rays;
                                Vector2 direction = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                                Line(vh, target + direction * 12 * unit, target + direction * (22 + hit * 35) * unit, 3 * unit, glow);
                            }
                            if (weapon.Kind == WeaponKind.Sword || weapon.Kind == WeaponKind.Greatsword)
                                Arc(vh, target, (weapon.Kind == WeaponKind.Greatsword ? 48 : 32) * unit, -30 - hit * 150, 125, 8 * unit, glow);
                        }
                    }
                }
                float size = (weapon.Kind == WeaponKind.Dagger ? 17 : 22) * unit * scale;
                DrawWeapon(vh, weapon.Kind, Point(r, point), size, rotation);
                if (combat.Loadout.At(slot) == null)
                    Arc(vh, Point(r, point), size * 1.25f, 0, 360, unit, new Color(.6f, .65f, .7f, .25f));
            }
            if (combat.GuardAt(seconds) > 0)
                Arc(vh, Point(r, hero), 65 * unit, -70, 140, 3 * unit, RunUI.Teal);
            if (combat.Victory && combat.FinaleSuccess && seconds >= combat.FinaleAtSeconds)
            {
                float age = (float)(seconds - combat.FinaleAtSeconds);
                for (int i = 0; i < 48; i++)
                {
                    float x = (i * .618033f) % 1, speed = .15f + (i % 5) * .045f;
                    var p = Point(r, new Vector2(x, .92f - age * speed));
                    Line(vh, p, p + new Vector2(Mathf.Sin(i + age * 9), 1) * 8 * unit, 4 * unit, i % 2 == 0 ? Glow : RunUI.Gold);
                }
            }
        }

        // Repo-native silhouettes share the concept's black metal, gold rim and pink cores.
        private static void DrawWeapon(VertexHelper vh, WeaponKind kind, Vector2 p, float size, float rotation)
        {
            float a = rotation * Mathf.Deg2Rad;
            Vector2 up = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)), right = new Vector2(up.y, -up.x);
            if (kind == WeaponKind.Shield)
            {
                Diamond(vh, p, right * size * .8f, up * size * 1.1f, Edge);
                Diamond(vh, p, right * size * .62f, up * size * .87f, Metal);
                Diamond(vh, p, right * size * .25f, up * size * .42f, Glow); return;
            }
            if (kind == WeaponKind.Bell)
            {
                Arc(vh, p, size * .72f, 0, 360, size * .18f, Edge);
                Diamond(vh, p, right * size * .45f, up * size * .7f, Metal);
                Line(vh, p - up * size, p - up * size * .35f, size * .18f, Glow); return;
            }
            Line(vh, p - up * size, p + up * size * .35f, size * .18f, Edge);
            if (kind == WeaponKind.Hammer)
            {
                Line(vh, p + up * size * .65f - right * size * .85f, p + up * size * .65f + right * size * .85f, size * .8f, Edge);
                Line(vh, p + up * size * .65f - right * size * .65f, p + up * size * .65f + right * size * .65f, size * .58f, Metal);
                Line(vh, p + up * size * .35f, p + up * size, size * .2f, Glow); return;
            }
            float length = kind == WeaponKind.Spear ? 2.1f : kind == WeaponKind.Greatsword ? 1.65f : 1.4f;
            float width = kind == WeaponKind.Spear ? .24f : kind == WeaponKind.Greatsword ? .48f : .34f;
            Triangle(vh, p - right * size * width, p + right * size * width, p + up * size * length, Edge);
            Triangle(vh, p - right * size * width * .66f, p + right * size * width * .66f, p + up * size * length * .85f, Metal);
            Line(vh, p + up * size * .12f, p + up * size * length * .63f, size * .1f, Glow);
            Line(vh, p - right * size * .55f, p + right * size * .55f, size * .16f, Edge);
            if (kind == WeaponKind.Blade)
                Triangle(vh, p - up * size * .4f, p + right * size * .8f, p + up * size * .65f, Glow);
        }
        private static Vector2 Point(Rect r, Vector2 p) => new Vector2(r.xMin + p.x * r.width, r.yMin + p.y * r.height);
        private static void Diamond(VertexHelper vh, Vector2 p, Vector2 right, Vector2 up, Color color)
        { Triangle(vh, p - up, p + right, p + up, color); Triangle(vh, p + up, p - right, p - up, color); }
        private static void Arc(VertexHelper vh, Vector2 center, float radius, float start, float sweep, float width, Color color)
        {
            for (int i = 0; i < 32; i++)
            {
                float a = (start + sweep * i / 32) * Mathf.Deg2Rad, b = (start + sweep * (i + 1) / 32) * Mathf.Deg2Rad;
                Line(vh, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, width, color);
            }
        }
        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
        {
            var d = b - a; if (d.sqrMagnitude < .001f) return;
            var n = new Vector2(-d.y, d.x).normalized * width * .5f;
            Triangle(vh, a - n, b - n, b + n, color); Triangle(vh, b + n, a + n, a - n, color);
        }
        private static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int count = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero); vh.AddVert(c, color, Vector2.zero);
            vh.AddTriangle(count, count + 1, count + 2);
        }
    }
}
