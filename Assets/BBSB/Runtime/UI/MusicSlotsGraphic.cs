using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MusicSlotsGraphic : MaskableGraphic
    {
        private MusicStage stage;
        private int bar;

        public void Bind(MusicStage value, int barIndex)
        {
            stage = value; bar = barIndex; raycastTarget = false; SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (stage == null || bar < 0 || bar >= stage.Music.BarCount) return;
            Rect rect = rectTransform.rect;
            float left = rect.xMin + 6, width = Mathf.Max(1, rect.width - 12), rowHeight = rect.height / 5;
            int start = bar * stage.Music.TicksPerBar, end = start + stage.Music.TicksPerBar;
            for (int tick = 0; tick <= stage.Music.TicksPerBar; tick += RhythmTime.TicksPerBeat / 2)
            {
                float x = left + width * tick / stage.Music.TicksPerBar;
                Color tint = RunUI.Hex(tick % RhythmTime.TicksPerBeat == 0 ? "52617D" : "303C54");
                Quad(vh, x - .7f, rect.yMin, 1.4f, rect.height, tint);
            }
            foreach (var slot in stage.Slots)
            {
                if (slot.StartTick >= end || slot.EndTick < start) continue;
                float y = rect.yMax - ((int)slot.Kind + .5f) * rowHeight;
                float x = left + width * Mathf.Clamp01((float)(slot.StartTick - start) / stage.Music.TicksPerBar);
                float last = left + width * Mathf.Clamp01((float)(slot.EndTick - start) / stage.Music.TicksPerBar);
                Color tint = slot.Weight > 1.5 ? RunUI.Gold : RunUI.Teal;
                if (slot.DurationTicks > 0) Quad(vh, x, y - 4, Mathf.Max(1, last - x), 8, tint);
                if (slot.StartTick >= start) Quad(vh, x - 4, y - 9, 8, 18,
                    slot.DurationTicks == 0 && slot.Touch.End == TouchTransition.Release ? RunUI.Red : tint);
                if (slot.DurationTicks > 0 && slot.EndTick <= end)
                    Quad(vh, last - 3, y - 7, 6, 14, slot.Touch.End == TouchTransition.Release ? RunUI.Red : tint);
            }
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
