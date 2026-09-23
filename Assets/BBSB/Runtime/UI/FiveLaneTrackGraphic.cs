using System;
using System.Collections.Generic;
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
        public FiveLaneNoteTimeline NoteTimeline => noteTimeline;
        public ISet<string> SpriteProjectiles { get; set; }
        public bool SpriteNoteShatter { get; set; }
        public BattleVisualTheme Theme { get; set; }
        public const double LookAheadBeats = SteppedNoteTrack.LookAheadBeats;
        public static Vector2 LanePoint(int lane, float distance, int count = 5) =>
            new Vector2(.5f + ((lane + .5f) / count - .5f) * (float)BattleBoardLayout.Width(distance), (float)BattleBoardLayout.Y(distance));
        public static Vector2 InputPoint(int slot, float distance, InputExtensions extensions) =>
            new Vector2((float)BattleBoardLayout.X(slot, distance, extensions), (float)BattleBoardLayout.Y(distance));
        public float NoteWidth(int slot, double at) => CellWidth((float)(SteppedNoteTrack.Distance(at, battle.Beat) / LookAheadBeats)) * .87f;
        private float CellWidth(float distance) => (float)BattleBoardLayout.CellWidth(distance, battle.Extensions) * rectTransform.rect.width;
        public void Bind(FiveLaneBattle value, RectTransform[] judgmentTargets = null,
            RectTransform player = null, RectTransform enemies = null)
        { battle = value; targets = judgmentTargets; playerTarget = player; enemySource = enemies; raycastTarget = false; Refresh(); }
        public void Refresh() { noteTimeline.Refresh(battle); SetVerticesDirty(); }
        public Vector3 NoteWorldPosition(int slot, double at) => rectTransform.TransformPoint(Position(slot, at));
        public Vector3 BrokenWorldPosition(BrokenTrackNote note) => rectTransform.TransformPoint(
            Point(note.Note.Slot, (float)(note.HeadDistance / LookAheadBeats)));
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (battle == null) return;
            float unit = rectTransform.rect.height / 720f;
            float pulse = battle.Beat % 1 < .18 ? 1 - (float)(battle.Beat % 1 / .18) : 0;
            for (int slot = 0; slot < BattleInputLayout.LaneCount; slot++)
            {
                if (!battle.IsInputAvailable(slot)) continue;
                var lane = battle.LaneAt(slot);
                var near = Point(slot, 0); var far = Point(slot, 1);
                float nearHalf = CellWidth(0) * .5f, farHalf = CellWidth(1) * .5f;
                Color laneColor = LaneColor(slot);
                var edge = new Color(.9f, .82f, .70f, lane == null ? .15f : .5f);
                Quad(vh, near - Vector2.right * nearHalf, far - Vector2.right * farHalf,
                    far + Vector2.right * farHalf, near + Vector2.right * nearHalf, new Color(.022f, .023f, .042f, lane == null ? .4f : .72f));
                Line(vh, near - Vector2.right * nearHalf, far - Vector2.right * farHalf, unit, edge);
                Line(vh, near + Vector2.right * nearHalf, far + Vector2.right * farHalf, unit, edge);
                var header = far + Vector2.up * (rectTransform.rect.height * (float)(BattleBoardLayout.HeaderTop - BattleBoardLayout.FarY));
                Quad(vh, far - Vector2.right * farHalf, header - Vector2.right * farHalf * .94f,
                    header + Vector2.right * farHalf * .94f, far + Vector2.right * farHalf, new Color(.025f, .025f, .04f, .80f));
                Line(vh, far - Vector2.right * farHalf, header - Vector2.right * farHalf * .94f, unit, edge);
                Line(vh, header - Vector2.right * farHalf * .94f, header + Vector2.right * farHalf * .94f, unit, edge);
                Line(vh, header + Vector2.right * farHalf * .94f, far + Vector2.right * farHalf, unit, edge);
                int cells = (int)(LookAheadBeats / SteppedNoteTrack.CellBeats);
                for (int cell = 1; cell <= cells; cell++)
                {
                    float distance = (float)cell / cells;
                    var p = Point(slot, distance); float half = CellWidth(distance) * .5f;
                    Line(vh, p - Vector2.right * half, p + Vector2.right * half, unit,
                        new Color(.9f, .82f, .7f, cell % 2 == 0 ? .24f : .08f));
                }
                var receptor = lane == null ? new Color(.5f, .5f, .6f, .18f) :
                    lane.Phase == PhraseLanePhase.Cooldown ? RunUI.Muted : Color.Lerp(laneColor, Color.white, pulse * .65f);
                float receptorHalf = nearHalf * .86f;
                Line(vh, near - Vector2.right * receptorHalf, near + Vector2.right * receptorHalf, 12 * unit, Alpha(receptor, .10f + pulse * .10f));
                Line(vh, near - Vector2.right * receptorHalf, near + Vector2.right * receptorHalf, 3 * unit, receptor);
                Diamond(vh, near, 4 * unit, receptor);
                if (lane == null) continue;
                if (WeaponCatalog.Find(lane.Weapon.DefinitionId).Kind == WeaponKind.Shield)
                    foreach (var attack in battle.Incoming)
                    {
                        if (!battle.CoversAttack(lane, slot, attack)) continue;
                        double remaining = attack.Beat - battle.Beat;
                        if (remaining > LookAheadBeats || attack.EndBeat - battle.Beat < -battle.HalfMissWindow ||
                            attack.State == IncomingAttackState.Interrupted || attack.State == IncomingAttackState.Hit) continue;
                        var p = Position(slot, attack.Definition.IsHold ? Math.Max(battle.Beat, attack.Beat) : attack.Beat);
                        var tint = attack.State == IncomingAttackState.Blocked ? RunUI.Teal : RunUI.Red;
                        if (attack.Definition.IsHold)
                        {
                            var head = Position(slot, Math.Max(battle.Beat, attack.Beat));
                            var tail = Position(slot, Math.Min(battle.Beat + LookAheadBeats, attack.EndBeat));
                            Line(vh, head + Vector2.right * 22, tail + Vector2.right * 22, 5, tint);
                        }
                        Line(vh, p + new Vector2(-22, 7), p + new Vector2(0, -5), 4, tint);
                        Line(vh, p + new Vector2(0, -5), p + new Vector2(22, 7), 4, tint);
                    }
                foreach (var shown in noteTimeline.Notes)
                {
                    if (shown.Slot != slot) continue;
                    double at = shown.Beat;
                    var p = Position(slot, at); Color tint = BattleVisualTheme.NoteColor(at);
                    if (shown.IsPreview) tint.a = .32f;
                    if (shown.IsHold)
                    {
                        var head = Position(slot, Math.Max(battle.Beat, at)); var tail = Position(slot, shown.EndBeat);
                        float headWidth = NoteWidth(slot, Math.Max(battle.Beat, at)) * .33f;
                        float tailWidth = NoteWidth(slot, shown.EndBeat) * .33f;
                        Quad(vh, head - Vector2.right * headWidth, head + Vector2.right * headWidth,
                            tail + Vector2.right * tailWidth, tail - Vector2.right * tailWidth, Alpha(tint, tint.a * .42f));
                        Line(vh, head - Vector2.right * headWidth, tail - Vector2.right * tailWidth, 2 * unit, tint);
                        Line(vh, head + Vector2.right * headWidth, tail + Vector2.right * tailWidth, 2 * unit, tint);
                        if (shown.IsPreview) DashedLine(vh, head, tail, Alpha(tint, .6f));
                        if (!shown.ConnectsNext && SteppedNoteTrack.InHorizon(shown.EndBeat - battle.Beat))
                            DrawNoteHead(vh, slot, shown.EndBeat, at, shown.IsPreview, shown.ReleaseParry);
                    }
                    if (!shown.IsConnected) DrawNoteHead(vh, slot, at, at, shown.IsPreview, shown.PressParry);
                    else Line(vh, p - Vector2.right * NoteWidth(slot, at) * .25f,
                        p + Vector2.right * NoteWidth(slot, at) * .25f, 2 * unit, tint);
                }
                foreach (var broken in noteTimeline.Broken)
                    if (broken.Note.Slot == slot && !(SpriteNoteShatter && broken.Note.IsPreview)) DrawBroken(vh, broken, laneColor);
            }
            // The hostile marker uses the same musical rotation during its final beat.
            // Its impact stays exact even when scheduled between whole beats.
            var source = enemySource != null ? LocalPoint(enemySource, new Vector2(enemySource.rect.center.x, enemySource.rect.yMin)) : Pixel(new Vector2(.625f, .50f));
            var target = playerTarget != null ? LocalPoint(playerTarget, playerTarget.rect.center) : Pixel(new Vector2(.185f, .43f));
            foreach (var attack in battle.Incoming)
            {
                if (SpriteProjectiles != null && SpriteProjectiles.Contains(attack.Definition.MonsterId)) continue;
                double delta = attack.Beat - battle.Beat;
                if (delta > 1 || attack.EndBeat - battle.Beat < -.25 || attack.State == IncomingAttackState.Interrupted) continue;
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
                (Vector2)rectTransform.InverseTransformPoint(targets[slot].TransformPoint(targets[slot].rect.center)) : Pixel(InputPoint(slot, 0, battle.Extensions));
            var defaultNear = Pixel(InputPoint(slot, 0, battle.Extensions));
            var projected = Pixel(InputPoint(slot, distance, battle.Extensions));
            // Custom prefab receptor offsets remain authoritative; the same perspective
            // displacement applies to the note, its hold and the sprite effect pool.
            return near + projected - defaultNear;
        }
        private Vector2 Pixel(Vector2 normalized)
        { var r = rectTransform.rect; return new Vector2(r.xMin + r.width * normalized.x, r.yMin + r.height * normalized.y); }
        private Vector2 LocalPoint(RectTransform rect, Vector2 point) => rectTransform.InverseTransformPoint(rect.TransformPoint(point));
        private Color LaneColor(int slot) => battle.LaneAt(slot)?.ActiveSide == WeaponBeatSide.Dark ? BattleVisualTheme.Dark : BattleVisualTheme.Light;
        private static Color Alpha(Color color, float alpha) { color.a = alpha; return color; }
        private void DrawNoteHead(VertexHelper vh, int slot, double at, double colorBeat, bool preview, bool parry)
        {
            var p = Position(slot, at); float width = NoteWidth(slot, at) * .4f;
            float height = rectTransform.rect.height * .007f;
            var tint = BattleVisualTheme.NoteColor(colorBeat); if (preview) tint.a = .32f;
            if (Theme == null || Theme.NoteSprite(colorBeat) == null)
            {
                Quad(vh, p + new Vector2(-width, -height), p + new Vector2(-width, height),
                    p + new Vector2(width, height), p + new Vector2(width, -height), Alpha(tint, tint.a * .8f));
                Line(vh, p + new Vector2(-width, height), p + new Vector2(width, height), 2, tint);
                Diamond(vh, p, height * .65f, Alpha(Color.white, tint.a));
            }
            if (parry) Ring(vh, p, new Vector2(width * .9f, height * 1.8f), Alpha(RunUI.Gold, tint.a));
        }
        private void DrawBroken(VertexHelper vh, BrokenTrackNote broken, Color laneColor)
        {
            float progress = (float)broken.Progress(battle.Beat);
            var tint = Color.Lerp(laneColor, RunUI.Red, .4f);
            tint.a = (1 - progress) * (broken.Note.IsPreview ? .7f : 1);
            var head = Point(broken.Note.Slot, (float)(broken.HeadDistance / LookAheadBeats));
            Fragments(vh, head, 9, progress, tint);
            if (!broken.Note.IsHold) return;
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
