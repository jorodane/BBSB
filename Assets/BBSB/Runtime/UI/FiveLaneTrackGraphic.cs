using System;
using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FiveLaneTrackGraphic : MaskableGraphic
    {
        private FiveLaneBattle battle;
        private RectTransform[] targets;
        public const double LookAheadBeats = 4;
        public static Vector2 LanePoint(int lane, float distance) => Vector2.Lerp(
            new Vector2(.12f + lane * .19f, .32f + .045f * (2 - Math.Abs(lane - 2))),
            new Vector2(.38f + lane * .06f, .70f), distance);
        public void Bind(FiveLaneBattle value, RectTransform[] weaponTargets = null)
        { battle = value; targets = weaponTargets; raycastTarget = false; SetVerticesDirty(); }
        public void Refresh() { SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (battle == null) return;
            for (int slot = 0; slot < battle.Lanes.Count; slot++)
            {
                var lane = battle.Lanes[slot];
                var near = Point(slot, 0); var far = Point(slot, 1);
                Line(vh, near, far, 3, new Color(.4f, .55f, .68f, .65f));
                for (double tick = Math.Ceiling(battle.Beat); tick <= battle.Beat + LookAheadBeats; tick++)
                {
                    var p = Position(slot, tick); float width = Mathf.Lerp(28, 10, (float)((tick - battle.Beat) / LookAheadBeats));
                    Line(vh, p - Vector2.right * width, p + Vector2.right * width, 2, new Color(.8f, .9f, 1, .16f));
                }
                Diamond(vh, near, 16, lane.Phase == PhraseLanePhase.Cooldown ? RunUI.Muted : RunUI.Gold);
                if (lane.Phase != PhraseLanePhase.Playing) continue;
                // Show the remainder and one repeat. No hidden, pre-authored monster response chart.
                for (int cycle = 0; cycle < (lane.Phrase.Repeat ? 2 : 1); cycle++)
                for (int n = cycle == 0 ? lane.NextNote : 0; n < lane.Phrase.Notes.Count; n++)
                {
                    var note = lane.Phrase.Notes[n];
                    double at = lane.StartBeat + cycle * lane.Phrase.LengthBeats + note.Beat;
                    if (at + note.HoldBeats < battle.Beat - battle.HalfMissWindow || at > battle.Beat + LookAheadBeats) continue;
                    var p = Position(slot, at); Color tint = note.IsHold ? new Color(.3f, .7f, 1) : RunUI.Teal;
                    if (note.IsHold)
                    {
                        Line(vh, Position(slot, Math.Max(battle.Beat, at)), Position(slot, at + note.HoldBeats), 11, tint);
                        bool releaseParry = note.IsParry && lane.Phrase.ParryInput == ParryInputEdge.KeyUp;
                        Diamond(vh, Position(slot, at + note.HoldBeats), releaseParry ? 11 : 7, releaseParry ? RunUI.Gold : Color.white);
                    }
                    Diamond(vh, p, 9 + 7 * (1 - Mathf.Clamp01((float)((at - battle.Beat) / LookAheadBeats))), tint);
                }
            }
            // Hostile projectiles have a separate red path into the player's central guard point.
            var source = Pixel(new Vector2(.5f, .71f)); var target = Pixel(new Vector2(.5f, .275f));
            foreach (var attack in battle.Incoming)
            {
                double delta = attack.Beat - battle.Beat;
                if (delta > LookAheadBeats || delta < -.35 || attack.State == IncomingAttackState.Interrupted) continue;
                float t = Mathf.Clamp01(1 - (float)(delta / LookAheadBeats));
                var p = Vector2.Lerp(source, target, t);
                Color tint = attack.State == IncomingAttackState.Blocked ? RunUI.Teal : RunUI.Red;
                Line(vh, p, p + Vector2.up * 23, 5, tint);
                Diamond(vh, p, 9 + t * 9, tint);
            }
        }
        private Vector2 Position(int slot, double at) => Point(slot,
            Mathf.Clamp01((float)((at - battle.Beat) / LookAheadBeats)));
        private Vector2 Point(int slot, float distance)
        {
            Vector2 near = targets != null && slot < targets.Length && targets[slot] != null ?
                (Vector2)rectTransform.InverseTransformPoint(targets[slot].TransformPoint(targets[slot].rect.center)) : Pixel(LanePoint(slot, 0));
            return Vector2.Lerp(near, Pixel(LanePoint(slot, 1)), distance);
        }
        private Vector2 Pixel(Vector2 normalized)
        { var r = rectTransform.rect; return new Vector2(r.xMin + r.width * normalized.x, r.yMin + r.height * normalized.y); }
        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
        {
            if ((b - a).sqrMagnitude < .001f) return;
            var side = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
            Quad(vh, a - side, a + side, b + side, b - side, tint);
        }
        private static void Diamond(VertexHelper vh, Vector2 p, float radius, Color tint) =>
            Quad(vh, p + Vector2.left * radius, p + Vector2.up * radius, p + Vector2.right * radius, p + Vector2.down * radius, tint);
        private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, tint, Vector2.zero); vh.AddVert(b, tint, Vector2.zero); vh.AddVert(c, tint, Vector2.zero); vh.AddVert(d, tint, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
