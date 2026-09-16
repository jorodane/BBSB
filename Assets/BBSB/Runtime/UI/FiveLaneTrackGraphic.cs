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
        private readonly FiveLaneNoteTimeline noteTimeline = new FiveLaneNoteTimeline();
        private RectTransform[] targets;
        private RectTransform playerTarget, enemySource;
        public const double LookAheadBeats = SteppedNoteTrack.LookAheadBeats;
        public static Vector2 LanePoint(int lane, float distance, int count = 5) =>
            new Vector2(.655f + (lane - (count - 1) * .5f) * .1375f, Mathf.Lerp(.22f, .42f, distance));
        public void Bind(FiveLaneBattle value, RectTransform[] judgmentTargets = null,
            RectTransform player = null, RectTransform enemies = null)
        { battle = value; targets = judgmentTargets; playerTarget = player; enemySource = enemies; raycastTarget = false; SetVerticesDirty(); }
        public void Refresh() { SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (battle == null) return;
            noteTimeline.Refresh(battle);
            for (int slot = 0; slot < battle.Lanes.Count; slot++)
            {
                var lane = battle.Lanes[slot];
                var near = Point(slot, 0); var far = Point(slot, 1);
                Color laneColor = LaneColor(slot);
                var floor = laneColor; floor.a = .07f;
                Quad(vh, near - Vector2.right * 28, far - Vector2.right * 28,
                    far + Vector2.right * 28, near + Vector2.right * 28, floor);
                var edge = laneColor; edge.a = .6f;
                Line(vh, near, far, 2, edge);
                int cells = (int)(LookAheadBeats / SteppedNoteTrack.CellBeats);
                for (int cell = 1; cell <= cells; cell++)
                {
                    var p = Point(slot, (float)cell / cells);
                    bool wholeBeat = cell % 2 == 0;
                    float halfWidth = wholeBeat ? 28 : 12;
                    Line(vh, p - Vector2.right * halfWidth, p + Vector2.right * halfWidth,
                        wholeBeat ? 3 : 1, new Color(.8f, .9f, 1, wholeBeat ? .6f : .18f));
                }
                float beatPulse = battle.Beat % 1 < .15 ? 1 - (float)(battle.Beat % 1 / .15) : 0;
                Ring(vh, near, new Vector2(36 + 4 * beatPulse, 10 + 3 * beatPulse),
                    lane.Phase == PhraseLanePhase.Cooldown ? RunUI.Muted : Color.Lerp(laneColor, Color.white, beatPulse * .6f));
                if (WeaponCatalog.Find(lane.Weapon.DefinitionId).Kind == WeaponKind.Shield)
                    foreach (var attack in battle.Incoming)
                    {
                        double remaining = attack.Beat - battle.Beat;
                        if (!SteppedNoteTrack.InHorizon(remaining) || remaining < -battle.HalfMissWindow ||
                            attack.State == IncomingAttackState.Interrupted || attack.State == IncomingAttackState.Hit) continue;
                        var p = Position(slot, attack.Beat);
                        var tint = attack.State == IncomingAttackState.Blocked ? RunUI.Teal : RunUI.Red;
                        Line(vh, p + new Vector2(-22, 7), p + new Vector2(0, -5), 4, tint);
                        Line(vh, p + new Vector2(0, -5), p + new Vector2(22, 7), 4, tint);
                    }
                foreach (var shown in noteTimeline.Notes)
                {
                    if (shown.Slot != slot) continue;
                    var note = shown.Definition;
                    double at = shown.Beat;
                    var p = Position(slot, at); Color tint = note.IsHold ? Color.Lerp(laneColor, Color.white, .35f) : laneColor;
                    if (shown.IsPreview) { tint = Color.Lerp(tint, Color.white, .3f); tint.a = .45f; }
                    if (note.IsHold)
                    {
                        var head = Position(slot, Math.Max(battle.Beat, at)); var tail = Position(slot, shown.EndBeat);
                        if (shown.IsPreview) DashedLine(vh, head, tail, tint);
                        else Line(vh, head, tail, 11, tint);
                        if (SteppedNoteTrack.InHorizon(at + note.HoldBeats - battle.Beat))
                        {
                            var endTint = shown.ReleaseParry ? RunUI.Gold : Color.white; endTint.a = tint.a;
                            NoteHead(vh, tail, shown.ReleaseParry ? 11 : 7, endTint, shown.IsPreview);
                        }
                    }
                    NoteHead(vh, p, 9, tint, shown.IsPreview);
                }
                foreach (var broken in noteTimeline.Broken)
                    if (broken.Note.Slot == slot) DrawBroken(vh, broken, laneColor);
            }
            // The hostile marker uses the same musical rotation during its final beat.
            // Its impact stays exact even when scheduled between whole beats.
            var source = enemySource != null ? LocalPoint(enemySource, new Vector2(enemySource.rect.center.x, enemySource.rect.yMin)) : Pixel(new Vector2(.625f, .50f));
            var target = playerTarget != null ? LocalPoint(playerTarget, playerTarget.rect.center) : Pixel(new Vector2(.185f, .43f));
            foreach (var attack in battle.Incoming)
            {
                double delta = attack.Beat - battle.Beat;
                if (delta > 1 || delta < -.25 || attack.State == IncomingAttackState.Interrupted) continue;
                float t = (float)SteppedNoteTrack.ImpactProgress(attack.Beat, battle.Beat);
                var p = Vector2.Lerp(source, target, t);
                Color tint = attack.State == IncomingAttackState.Blocked ? RunUI.Teal : RunUI.Red;
                Line(vh, p, p + (source - target).normalized * 23, 5, tint);
                Diamond(vh, p, 9 + t * 9, tint);
            }
        }
        private Vector2 Position(int slot, double at) => Point(slot,
            (float)(SteppedNoteTrack.Distance(at, battle.Beat) / LookAheadBeats));
        private Vector2 Point(int slot, float distance)
        {
            Vector2 near = targets != null && slot < targets.Length && targets[slot] != null ?
                (Vector2)rectTransform.InverseTransformPoint(targets[slot].TransformPoint(targets[slot].rect.center)) : Pixel(LanePoint(slot, 0, battle.Lanes.Count));
            return near + Vector2.up * (rectTransform.rect.height * .20f * distance);
        }
        private Vector2 Pixel(Vector2 normalized)
        { var r = rectTransform.rect; return new Vector2(r.xMin + r.width * normalized.x, r.yMin + r.height * normalized.y); }
        private Vector2 LocalPoint(RectTransform rect, Vector2 point) => rectTransform.InverseTransformPoint(rect.TransformPoint(point));
        private static Color LaneColor(int slot)
        {
            switch (slot)
            {
                case 0: return new Color(1, .30f, .50f);
                case 1: return new Color(1, .73f, .28f);
                case 2: return new Color(.25f, .70f, 1);
                case 3: return new Color(.25f, 1, .72f);
                default: return new Color(.79f, .40f, 1);
            }
        }
        private void DrawBroken(VertexHelper vh, BrokenTrackNote broken, Color laneColor)
        {
            float progress = (float)broken.Progress(battle.Beat);
            var tint = Color.Lerp(laneColor, RunUI.Red, .4f);
            tint.a = (1 - progress) * (broken.Note.IsPreview ? .7f : 1);
            var head = Point(broken.Note.Slot, (float)(broken.HeadDistance / LookAheadBeats));
            Fragments(vh, head, 9, progress, tint);
            if (!broken.Note.Definition.IsHold) return;
            var tail = Point(broken.Note.Slot, (float)(broken.TailDistance / LookAheadBeats));
            if (broken.TailVisible) Fragments(vh, tail, 7, progress, tint);
            float length = Vector2.Distance(head, tail);
            var direction = (tail - head).normalized;
            for (int i = 0; i * 18 < length; i++)
            {
                var offset = new Vector2((i % 2 == 0 ? -1 : 1) * (3 + 20 * progress), -18 * progress * progress);
                Line(vh, head + direction * (i * 18) + offset,
                    head + direction * Mathf.Min(length, i * 18 + 9) + offset, 4, tint);
            }
        }
        private static void NoteHead(VertexHelper vh, Vector2 p, float radius, Color tint, bool preview)
        {
            if (!preview) { Diamond(vh, p, radius, tint); return; }
            var fill = tint; fill.a *= .3f; Diamond(vh, p, radius, fill);
            Line(vh, p + Vector2.left * radius, p + Vector2.up * radius, 2, tint);
            Line(vh, p + Vector2.up * radius, p + Vector2.right * radius, 2, tint);
            Line(vh, p + Vector2.right * radius, p + Vector2.down * radius, 2, tint);
            Line(vh, p + Vector2.down * radius, p + Vector2.left * radius, 2, tint);
        }
        private static void DashedLine(VertexHelper vh, Vector2 a, Vector2 b, Color tint)
        {
            float length = Vector2.Distance(a, b); var direction = (b - a).normalized;
            for (float start = 0; start < length; start += 14)
                Line(vh, a + direction * start, a + direction * Mathf.Min(length, start + 7), 5, tint);
        }
        private static void Fragments(VertexHelper vh, Vector2 center, float radius, float progress, Color tint)
        {
            float spread = 2 + 24 * (1 - (1 - progress) * (1 - progress));
            for (int part = 0; part < 4; part++)
            {
                float angle = part * Mathf.PI * .5f;
                var a = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                var b = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)) * radius;
                var origin = center + (a + b).normalized * spread + Vector2.down * (14 * progress * progress);
                int i = vh.currentVertCount;
                vh.AddVert(origin, tint, Vector2.zero);
                vh.AddVert(origin + a, tint, Vector2.zero); vh.AddVert(origin + b, tint, Vector2.zero);
                vh.AddTriangle(i, i + 2, i + 1);
            }
        }
        private static void Ring(VertexHelper vh, Vector2 center, Vector2 radius, Color tint)
        {
            const int segments = 32;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
                Line(vh, center + new Vector2(Mathf.Cos(a) * radius.x, Mathf.Sin(a) * radius.y),
                    center + new Vector2(Mathf.Cos(b) * radius.x, Mathf.Sin(b) * radius.y), 2, tint);
            }
        }
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
