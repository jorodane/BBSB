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

        public void Bind(MonsterDefinition value)
        {
            monster = value; lanes.Clear();
            foreach (var step in monster.Pattern.Steps) if (!lanes.Contains(step.Kind)) lanes.Add(step.Kind);
            lanes.Sort(); raycastTarget = false; SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (monster == null) return;
            Rect rect = rectTransform.rect;
            float rowHeight = rect.height / (lanes.Count + 2);
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
                Quad(vh, x - 4, rect.yMax - rowHeight * .5f - 7, 8, 14, RunUI.Gold);
            }
            foreach (var step in monster.Pattern.Steps)
            {
                float y = rect.yMax - rowHeight * (lanes.IndexOf(step.Kind) + 1.5f);
                float x = left + width * (cue + step.OffsetTick) / total;
                float end = left + width * (cue + step.OffsetTick + step.DurationTicks) / total;
                Color tint = step.Kind == GestureKind.Flick ? RunUI.Red : RunUI.Teal;
                if (step.DurationTicks > 0) Quad(vh, x, y - 3, Mathf.Max(1, end - x), 6, tint);
                Quad(vh, x - 3, y - 7, 6, 14, tint);
                if (step.DurationTicks > 0) Quad(vh, end - 2, y - 6, 4, 12,
                    step.Touch.End == TouchTransition.Release ? RunUI.Red : tint);
            }
            if (monster.RestTicks > 0)
                Quad(vh, left + width * restStart / total, rect.yMin + rowHeight * .5f - 3,
                    width * monster.RestTicks / total, 6, RunUI.Muted);
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
