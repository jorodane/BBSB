using BBSB.Core;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WeaponBattleGraphic : MaskableGraphic
    {
        private WeaponBattle combat;
        private double seconds;
        private double beatSeconds = .5;
        private IReadOnlyDictionary<string, Vector2> targetPositions;
        private Vector2 ground, hero;
        private RectTransform rearLayer;
        private WeaponTrailGraphic rearTrail, frontTrail;
        private readonly WeaponTrailHistory[] trails = new WeaponTrailHistory[RunRules.WeaponSlots];
        private readonly Vector2[] targets = new Vector2[RunRules.WeaponSlots];
        private readonly WeaponActivation[] active = new WeaponActivation[RunRules.WeaponSlots];
        private readonly WeaponIconGraphic[] icons = new WeaponIconGraphic[RunRules.WeaponSlots];
        private readonly string[] boundIds = new string[RunRules.WeaponSlots];
        internal float WeaponSizeMultiplier { get; set; } = 1.8f;
        internal bool ShowLegacyAttackEffects { get; set; } = true;
        private static readonly Color Edge = RunUI.Hex("CBA66F"), Metal = RunUI.Hex("251D31"), Glow = RunUI.Hex("FF4C9B");

        internal void SetFrame(WeaponBattle value, double time, Vector2 groundPoint, Vector2 heroPoint, double beat = .5)
        {
            if (!ReferenceEquals(combat, value) || time < seconds)
                foreach (var trail in trails) trail?.Clear();
            combat = value; seconds = time; ground = groundPoint; hero = heroPoint; beatSeconds = beat;
            for (int i = 0; i < active.Length; i++) active[i] = null;
            raycastTarget = false;
        }
        internal void SetActivation(WeaponActivation value, Vector2 target)
        { active[value.Slot] = value; targets[value.Slot] = target; }
        internal void SetTargets(IReadOnlyDictionary<string, Vector2> positions) { targetPositions = positions; }
        internal void SetRearLayer(RectTransform layer)
        {
            rearLayer = layer;
            if (rearTrail == null) rearTrail = WeaponTrailGraphic.Create(layer, "Rear pink weapon trails", trails, false);
            if (frontTrail == null) frontTrail = WeaponTrailGraphic.Create(transform, "Attack pink weapon trails", trails, true);
        }
        internal Vector2 WeaponOrigin(int slot) => Position(Formation(slot, seconds, 0));
        internal Vector2 ProjectileOrigin(int slot, WeaponKind kind, Vector2 target, double launchedAt)
        {
            // Sample the floating grip at release time, never at the later render time.
            var r = rectTransform.rect; var launch = Formation(slot, launchedAt, 0); var origin = Position(launch);
            float size = 22 * Mathf.Min(r.width, r.height) / 720 * 3.4f * WeaponSizeMultiplier * (float)launch.Scale * 1.035f;
            var state = combat.Loadout.Equipment[slot];
            var emission = RangedWeaponArtLayout.Muzzle(state.DefinitionId, state.Rarity, RangedWeaponPose.Release);
            Vector2 socket = new Vector2((float)emission.X - .5f, (float)emission.Y - .5f);
            Vector2 delta = Vector2.Scale(target - origin, r.size);
            float angle = Mathf.Atan2(delta.y, delta.x) - (kind == WeaponKind.Wand ? Mathf.PI * .25f : 0);
            Vector2 offset = new Vector2(socket.x * Mathf.Cos(angle) - socket.y * Mathf.Sin(angle),
                socket.x * Mathf.Sin(angle) + socket.y * Mathf.Cos(angle)) * size;
            return origin + new Vector2(offset.x / r.width, offset.y / r.height);
        }
        internal Vector2 MuzzlePosition(int slot)
        {
            var icon = icons[slot]; if (icon == null) return WeaponOrigin(slot);
            var rect = icon.ArtworkRect;
            var state = combat.Loadout.Equipment[slot];
            var emission = RangedWeaponArtLayout.Muzzle(state.DefinitionId, state.Rarity, icon.Pose);
            Vector2 normalized = new Vector2((float)emission.X, (float)emission.Y);
            var local = new Vector2(rect.xMin + rect.width * normalized.x, rect.yMin + rect.height * normalized.y);
            var point = rectTransform.InverseTransformPoint(icon.transform.TransformPoint(local));
            var area = rectTransform.rect;
            return new Vector2((point.x - area.xMin) / area.width, (point.y - area.yMin) / area.height);
        }
        internal void Refresh()
        {
            Rect r = rectTransform.rect;
            float unit = Mathf.Min(r.width, r.height) / 720;
            double support = WeaponFormation.SupportStrength(combat?.Activations, seconds);
            for (int slot = 0; slot < icons.Length; slot++)
            {
                bool visible = combat != null && slot < combat.Loadout.Equipment.Count && r.width > 0 && r.height > 0;
                if (!visible) { if (icons[slot] != null) icons[slot].gameObject.SetActive(false); trails[slot]?.Clear(); continue; }
                var state = combat.Loadout.Equipment[slot]; var weapon = WeaponCatalog.Find(state.DefinitionId);
                if (icons[slot] == null)
                {
                    var child = new GameObject("Battle weapon " + slot, typeof(RectTransform), typeof(CanvasRenderer));
                    child.transform.SetParent(transform, false); icons[slot] = child.AddComponent<WeaponIconGraphic>();
                    icons[slot].rectTransform.anchorMin = icons[slot].rectTransform.anchorMax = Vector2.zero;
                }
                var icon = icons[slot]; icon.gameObject.SetActive(true);
                if (boundIds[slot] != state.DefinitionId || icon.Rarity != state.Rarity)
                { icon.Bind(state); boundIds[slot] = state.DefinitionId; }
                var activation = active[slot];
                var ranged = weapon.IsRanged ? RangedWeaponTimeline.Evaluate(combat, slot, seconds, beatSeconds) : default;
                double readiness = weapon.IsRanged ? WeaponFormation.Smooth(ranged.Tension) : 0;
                var resting = Formation(slot, seconds, support * (1 - readiness));
                Vector2 origin = Position(resting), point = origin;
                float rotation = (float)resting.Rotation, scale = (float)resting.Scale;
                double progress = 0;
                if (activation != null)
                {
                    progress = (seconds - activation.AtSeconds) / WeaponMotion.Duration(activation.Action.Motion);
                    var frame = AttackFrame(activation, targets[slot], seconds);
                    point = Position(frame);
                    rotation = (float)frame.Rotation; scale = (float)frame.Scale;
                }
                if (weapon.IsRanged)
                {
                    icon.SetPose(ranged.Pose);
                    Vector2 aim = hero + Vector2.right * .5f;
                    var target = ranged.Target ?? activation?.Target;
                    if (target != null && targetPositions != null && targetPositions.TryGetValue(target.MonsterId, out var position)) aim = position;
                    var grip = activation != null ? Position(Formation(slot, activation.AtSeconds, 0)) : origin;
                    var delta = Vector2.Scale(aim - grip, r.size);
                    float aimed = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - (weapon.Kind == WeaponKind.Wand ? 45 : 0);
                    double aimWeight = activation != null ? 1 - WeaponFormation.ReturnWeight(progress) : readiness;
                    rotation = Mathf.LerpAngle((float)resting.Rotation, aimed + (activation != null ? rotation : 0), (float)aimWeight);
                    scale *= 1 + (float)ranged.Tension * .035f;
                }
                // Rear and front layers have the same full-arena rectangle; socket children inherit the move.
                bool behind = activation == null && ranged.Pose == RangedWeaponPose.Idle;
                var parent = behind && rearLayer != null ? rearLayer : rectTransform;
                if (icon.transform.parent != parent) icon.transform.SetParent(parent, false);
                float length = weapon.IsRanged ? 3.4f : weapon.Kind == WeaponKind.Spear ? 3.3f : weapon.Kind == WeaponKind.Greatsword ? 2.95f : 2.6f;
                float size = (weapon.Kind == WeaponKind.Dagger ? 17 : 22) * unit * scale * length * WeaponSizeMultiplier;
                icon.rectTransform.anchoredPosition = new Vector2(point.x * r.width, point.y * r.height);
                icon.rectTransform.sizeDelta = new Vector2(size, size);
                icon.rectTransform.localRotation = Quaternion.Euler(0, 0, rotation);
                icon.SetActivity(combat, slot, seconds);
                if (trails[slot] == null) trails[slot] = new WeaponTrailHistory();
                trails[slot].Add(seconds, new BattlePathPoint(point.x, point.y), !behind);
            }
            rearTrail?.SetTime(seconds); frontTrail?.SetTime(seconds);
            SetVerticesDirty();
        }
        private WeaponMotionFrame Formation(int slot, double at, double support)
        {
            var r = rectTransform.rect;
            return WeaponFormation.Sample(slot, at, new BattlePathPoint(hero.x, hero.y),
                (hero.y - ground.y) / .46, r.height > 0 && r.width > 0 ? r.width / r.height : 1, support);
        }
        private static Vector2 Position(WeaponMotionFrame frame) => new Vector2((float)frame.Position.X, (float)frame.Position.Y);

        internal WeaponMotionFrame AttackFrame(WeaponActivation activation, Vector2 target, double at)
        {
            bool ranged = activation.Weapon.IsRanged;
            var launch = Formation(activation.Slot, activation.AtSeconds,
                ranged ? 0 : WeaponFormation.SupportStrength(combat.Activations, activation.AtSeconds));
            double readiness = ranged ? WeaponFormation.Smooth(RangedWeaponTimeline.Evaluate(combat, activation.Slot, at, beatSeconds).Tension) : 0;
            var home = Formation(activation.Slot, at, WeaponFormation.SupportStrength(combat.Activations, at) * (1 - readiness));
            if (ranged)
            {
                // Ranged recoil angles are relative to aim; idle banking is applied separately.
                launch = new WeaponMotionFrame(launch.Position, 0, launch.Scale);
                home = new WeaponMotionFrame(home.Position, 0, home.Scale);
            }
            return WeaponFormation.Attack(activation.Action.Motion, launch, new BattlePathPoint(target.x, target.y), home,
                (at - activation.AtSeconds) / WeaponMotion.Duration(activation.Action.Motion));
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (combat == null) return;
            Rect r = rectTransform.rect;
            if (r.width <= 0 || r.height <= 0) return;
            float unit = Mathf.Min(r.width, r.height) / 720;
            for (int slot = 0; slot < combat.Loadout.Equipment.Count; slot++)
            {
                if (!ShowLegacyAttackEffects) break;
                var weapon = WeaponCatalog.Find(combat.Loadout.Equipment[slot].DefinitionId);
                var origin = WeaponOrigin(slot);
                var activation = active[slot];
                var point = origin;
                if (activation != null)
                {
                    // Textured effect actors own ranged projectiles, muzzle flashes and impact timing.
                    if (weapon.IsRanged) continue;
                    float p = (float)((seconds - activation.AtSeconds) / WeaponMotion.Duration(activation.Action.Motion));
                    var frame = AttackFrame(activation, targets[slot], seconds);
                    point = new Vector2((float)frame.Position.X, (float)frame.Position.Y);
                    var target = Point(r, targets[slot]);
                    Color glow = Glow; glow.a = 1 - p;
                    if (activation.Action.Motion == WeaponAttackStyle.Resonance)
                    {
                        var wave = Vector2.Lerp(Point(r, origin), target, Mathf.Min(1, p / .6f));
                        for (int ring = 0; ring < 3; ring++) Arc(vh, wave, (14 + p * 45 + ring * 9) * unit, 0, 360, 2 * unit, glow);
                    }
                    else if (activation.Action.Motion == WeaponAttackStyle.Guard || activation.Action.Motion == WeaponAttackStyle.Ward)
                        Arc(vh, Point(r, hero), 66 * unit, -70, 140, 5 * unit, RunUI.Teal);
                    else
                    {
                        var previous = AttackFrame(activation, targets[slot],
                            activation.AtSeconds + Mathf.Max(0, p - .07f) * WeaponMotion.Duration(activation.Action.Motion));
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
        internal static void DrawWeapon(VertexHelper vh, WeaponKind kind, Vector2 p, float size, float rotation)
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
            if (kind == WeaponKind.Bow || kind == WeaponKind.Crossbow)
            {
                Vector2 center = p + (kind == WeaponKind.Crossbow ? right * size * .45f : Vector2.zero);
                for (int i = 0; i < 16; i++)
                {
                    float first = i * Mathf.PI / 16, next = (i + 1) * Mathf.PI / 16;
                    var a0 = center + (up * Mathf.Cos(first) + right * Mathf.Sin(first) * .45f) * size;
                    var b0 = center + (up * Mathf.Cos(next) + right * Mathf.Sin(next) * .45f) * size;
                    Line(vh, a0, b0, size * .13f, Edge); Line(vh, a0, b0, size * .075f, Metal);
                }
                Line(vh, center - up * size, center + up * size, size * .025f, Glow);
                if (kind == WeaponKind.Crossbow)
                {
                    Line(vh, p - right * size, p + right * size * 1.1f, size * .23f, Edge);
                    Line(vh, p - right * size, p + right * size * 1.1f, size * .15f, Metal);
                    Line(vh, p - right * size * .3f, p - right * size * .55f - up * size * .6f, size * .2f, Edge);
                }
                return;
            }
            if (kind == WeaponKind.Wand)
            {
                Vector2 diagonal = (right + up).normalized;
                Line(vh, p - diagonal * size, p + diagonal * size * .5f, size * .19f, Edge);
                Line(vh, p - diagonal * size, p + diagonal * size * .5f, size * .11f, Metal);
                Diamond(vh, p + diagonal * size * .65f, right * size * .36f, up * size * .48f, Edge);
                Diamond(vh, p + diagonal * size * .65f, right * size * .27f, up * size * .37f, Glow); return;
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
