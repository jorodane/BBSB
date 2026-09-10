using System.Collections.Generic;
using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MonsterPatternGraphic : MaskableGraphic
    {
        private MonsterDefinition monster;
        private readonly List<GestureKind> lanes = new List<GestureKind>();
        private PlannedAttack liveAttack;
        private ResponseNote[] liveNotes;
        private float cursorTick = -1;

        public void Bind(MonsterDefinition value)
        {
            monster = value; lanes.Clear(); liveAttack = null; liveNotes = null; cursorTick = -1;
            foreach (var step in monster.Pattern.Steps) if (!lanes.Contains(step.Kind)) lanes.Add(step.Kind);
            lanes.Sort(); raycastTarget = false; SetVerticesDirty();
        }

        public void SetPlayback(RhythmRound round, PlannedAttack attack, double seconds)
        {
            if (liveAttack != attack)
            {
                liveAttack = attack; liveNotes = attack == null ? null : new ResponseNote[monster.Pattern.Steps.Count];
                if (attack != null) foreach (var note in round.Notes)
                    if (note.Attack == attack) liveNotes[note.StepIndex] = note;
            }
            cursorTick = attack == null ? -1 : (float)(seconds / round.BeatSeconds * RhythmTime.TicksPerBeat - attack.CallStartTick);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (monster == null) return;
            Rect rect = rectTransform.rect;
            float rowHeight = rect.height / (lanes.Count + 2);
            float marker = Mathf.Min(14, rowHeight * .8f), bar = Mathf.Min(6, rowHeight * .4f);
            float left = rect.xMin + 6, width = Mathf.Max(1, rect.width - 12);
            int cue = monster.Pattern.CueLeadTicks, restStart = cue + monster.ResponseTicks;
            int total = restStart + monster.RestTicks;
            for (int tick = 0; tick <= total; tick += RhythmTime.TicksPerBeat / 2)
            {
                float x = left + width * tick / total;
                Quad(vh, x - .5f, rect.yMin, 1, rect.height, RunUI.Hex(tick % RhythmTime.TicksPerBeat == 0 ? "45536C" : "2D374C"));
            }
            // The gold divider marks the exact start of the player's Response.
            Quad(vh, left + width * cue / total - 1, rect.yMin, 2, rect.height, RunUI.Gold);
            foreach (var signal in monster.Call)
            {
                float x = left + width * signal.OffsetTick / total;
                Quad(vh, x - 4, rect.yMax - rowHeight * .5f - marker * .5f, 8, marker, RunUI.Gold);
            }
            for (int i = 0; i < monster.Pattern.Steps.Count; i++)
            {
                var step = monster.Pattern.Steps[i];
                float y = rect.yMax - rowHeight * (lanes.IndexOf(step.Kind) + 1.5f);
                float x = left + width * (cue + step.OffsetTick) / total;
                float end = left + width * (cue + step.OffsetTick + step.DurationTicks) / total;
                Color tint = step.Kind == GestureKind.Flick ? RunUI.Red : RunUI.Teal;
                var note = liveNotes == null ? null : liveNotes[i];
                if (note != null && note.State == ResponseState.Resolved) tint = RhythmPlaybackView.GradeColor(note.Result.Grade);
                else if (note != null && note.State == ResponseState.Holding) tint = RunUI.Gold;
                if (step.DurationTicks > 0) Quad(vh, x, y - bar * .5f, Mathf.Max(1, end - x), bar, tint);
                Quad(vh, x - 3, y - marker * .5f, 6, marker, tint);
                if (step.DurationTicks > 0) Quad(vh, end - 2, y - marker * .4f, 4, marker * .8f,
                    step.Touch.End == TouchTransition.Release ? RunUI.Red : tint);
            }
            if (monster.RestTicks > 0)
                Quad(vh, left + width * restStart / total, rect.yMin + rowHeight * .5f - bar * .5f,
                    width * monster.RestTicks / total, bar, RunUI.Muted);
            if (cursorTick >= 0 && cursorTick <= total)
                Quad(vh, left + width * cursorTick / total - 1.5f, rect.yMin, 3, rect.height, RunUI.TextColor);
        }

        private static void Quad(VertexHelper vh, float x, float y, float width, float height, Color tint)
        {
            int index = vh.currentVertCount;
            vh.AddVert(new Vector3(x, y, 0), tint, Vector2.zero);
            vh.AddVert(new Vector3(x, y + height, 0), tint, Vector2.zero);
            vh.AddVert(new Vector3(x + width, y + height, 0), tint, Vector2.zero);
            vh.AddVert(new Vector3(x + width, y, 0), tint, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2); vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
