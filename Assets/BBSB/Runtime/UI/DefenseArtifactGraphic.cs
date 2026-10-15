using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    // Code-native prototype of the reference's black/gold, magenta, symmetric staff.
    // It is held at the player and never joins the four offensive orbiting weapons.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DefenseArtifactGraphic : MaskableGraphic
    {
        private FiveLaneBattle battle;
        private RectTransform player;
        private DefenseHoldGraphic holds;
        public bool StaffInCharacterPose { get; internal set; }
        public int ActiveDirections => battle == null ? 0 : DefenseArtifactMotion.Mask(battle);
        public int HeldDirections => battle == null ? 0 : DefenseHoldMotion.Mask(battle);
        internal void Bind(FiveLaneBattle value, RectTransform body)
        {
            battle = value; player = body; raycastTarget = false;
            if (holds == null)
            {
                var child = new GameObject("Hold ornament contacts", typeof(RectTransform), typeof(CanvasRenderer));
                var rect = (RectTransform)child.transform; rect.SetParent(transform, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
                holds = child.AddComponent<DefenseHoldGraphic>();
            }
            holds.Bind(battle, player); SetAllDirty();
        }
        internal void Refresh() { SetVerticesDirty(); if (holds != null) holds.Refresh(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (battle == null || player == null || battle.LaneAt(4) == null) return;
            Vector2 center = At(DefenseArtifactMotion.GripX, DefenseArtifactMotion.GripY);
            float height = Vector2.Distance(At(.5f, 0), At(.5f, 1));
            var frame = DefenseArtifactMotion.Sample(battle);
            int mask = ActiveDirections;
            for (int d = 0; d < 4; d++)
            {
                bool active = (mask & 1 << d) != 0, threat = false;
                foreach (var attack in battle.Incoming)
                    if (attack.ShieldSlot == 4 + d && attack.State == IncomingAttackState.Pending &&
                        attack.Beat - battle.Beat <= 3 && attack.EndBeat >= battle.Beat) { threat = true; break; }
                var hold = DefenseHoldMotion.Sample(battle, d);
                if (!active && !threat && !hold.Active) continue;
                var offset = DefenseArtifactMotion.Contact(d);
                var point = center + new Vector2((float)offset.X, (float)offset.Y) * height;
                var tint = active ? new Color(.35f, 1, .9f, .85f) : new Color(1, .25f, .45f, .6f);
                Ring(vh, point, height * (active ? .07f : .045f), height * .007f, tint);
                // A spatial arrow points inward, towards the staff contact.
                var inward = (center - point).normalized;
                var side = new Vector2(-inward.y, inward.x) * height * .018f;
                var tail = point - inward * height * .035f;
                Line(vh, tail + side, point, height * .006f, tint);
                Line(vh, tail - side, point, height * .006f, tint);
            }
            var grip = center + new Vector2((float)frame.Position.X, (float)frame.Position.Y) * height;
            if (StaffInCharacterPose) return;
            var motion = PlayerDefenseMotion.Sample(battle);
            if (motion.Smear > 0) Smear(vh, grip, height * .53f, (float)frame.Rotation);
            DrawStaff(vh, grip, height * .88f * (float)frame.Scale, (float)frame.Rotation, Color.white);
        }
        private static void Smear(VertexHelper vh, Vector2 center, float radius, float angle)
        {
            // A tapered filled silhouette, not copies of the weapon. It exists only
            // in the fast stroke, never for the duration of a held chord.
            for (int end = 0; end < 2; end++)
            for (int i = 0; i < 16; i++)
            {
                float t = i / 16f, next = (i + 1) / 16f;
                float a = (angle + end * 180 - 115 + t * 115) * Mathf.Deg2Rad;
                float b = (angle + end * 180 - 115 + next * 115) * Mathf.Deg2Rad;
                var u = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); var v = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                Quad(vh, center + u * radius, center + v * radius,
                    center + v * radius * (1 - .34f * next), center + u * radius * (1 - .34f * t), new Color(.12f, .06f, .16f, .9f));
                Line(vh, center + u * radius * .99f, center + v * radius * .99f, radius * .015f, new Color(1, .72f, .36f, .95f));
                Line(vh, center + u * radius * (1 - .2f * t), center + v * radius * (1 - .2f * next), radius * .018f, new Color(1, .15f, .62f, .9f));
            }
        }
        private Vector2 At(float x, float y) => rectTransform.InverseTransformPoint(player.TransformPoint(
            new Vector2(Mathf.Lerp(player.rect.xMin, player.rect.xMax, x), Mathf.Lerp(player.rect.yMin, player.rect.yMax, y))));
        internal static void DrawStaff(VertexHelper vh, Vector2 center, float length, float angle, Color tint)
        {
            var axis = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            var side = new Vector2(-axis.y, axis.x);
            var ink = new Color(.09f, .06f, .13f, 1) * tint;
            var gold = new Color(.91f, .64f, .36f, 1) * tint;
            var glow = new Color(1, .16f, .62f, 1) * tint;
            Line(vh, center - axis * length * .5f, center + axis * length * .5f, length * .045f, gold);
            Line(vh, center - axis * length * .5f, center + axis * length * .5f, length * .028f, ink);
            Line(vh, center - axis * length * .42f + side * length * .008f,
                center + axis * length * .42f + side * length * .008f, length * .005f, glow);
            for (int i = -3; i <= 3; i++)
                Line(vh, center + axis * length * i * .10f - side * length * .023f,
                    center + axis * length * i * .10f + side * length * .023f, length * .016f, gold);
            // Identical geometry at both ends, including the outer blades and core.
            foreach (int sign in new[] { -1, 1 })
            {
                var end = center + axis * length * .5f * sign;
                DrawOrnament(vh, end, length, angle, tint);
            }
        }
        internal static void DrawOrnament(VertexHelper vh, Vector2 center, float length, float angle, Color tint)
        {
            var axis = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            var side = new Vector2(-axis.y, axis.x);
            var ink = new Color(.09f, .06f, .13f, 1) * tint;
            var gold = new Color(.91f, .64f, .36f, 1) * tint;
            var glow = new Color(1, .16f, .62f, 1) * tint;
            Diamond(vh, center, axis * length * .11f, side * length * .085f, gold);
            Diamond(vh, center, axis * length * .085f, side * length * .060f, ink);
            Ring(vh, center, length * .049f, length * .014f, glow);
            Diamond(vh, center, axis * length * .025f, side * length * .025f, glow);
            for (int edge = -1; edge <= 1; edge += 2)
            {
                var blade = center + side * length * .07f * edge;
                Diamond(vh, blade, axis * length * .07f, side * length * .02f, gold);
            }
        }
        private static void Ring(VertexHelper vh, Vector2 p, float radius, float width, Color tint)
        {
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI / 12, b = (i + 1) * Mathf.PI / 12;
                Line(vh, p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    p + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, width, tint);
            }
        }
        private static void Diamond(VertexHelper vh, Vector2 p, Vector2 axis, Vector2 side, Color tint) =>
            Quad(vh, p - axis, p + side, p + axis, p - side, tint);
        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
        {
            var side = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
            Quad(vh, a - side, a + side, b + side, b - side, tint);
        }
        private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, tint, Vector2.zero); vh.AddVert(b, tint, Vector2.zero);
            vh.AddVert(c, tint, Vector2.zero); vh.AddVert(d, tint, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
