using BBSB.Core;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class NoteWorkshopPatternGraphic : MaskableGraphic, IPointerClickHandler
    {
        internal const float Margin = 50;
        private NoteWorkshopView owner;
        private WeaponPhrase original, composed;
        private IReadOnlyList<WeaponNoteBinding> bindings;
        private int width, offset, selected, drop = -1;
        private bool validDrop;
        private double playhead = -1;
        internal void Bind(NoteWorkshopView target, WeaponPhrase source, WeaponPhrase result, int lanes, int start, int selection,
            IReadOnlyList<WeaponNoteBinding> installed)
        {
            owner = target; original = source; composed = result; width = lanes; offset = start;
            selected = selection; raycastTarget = true; SetVerticesDirty();
            bindings = installed;
        }
        internal void Select(int index) { selected = index; SetVerticesDirty(); }
        internal void Hover(int index, bool valid) { drop = index; validDrop = valid; SetVerticesDirty(); }
        internal void Playhead(double beat) { playhead = beat; SetVerticesDirty(); }
        private float X(double beat) => rectTransform.rect.xMin + Margin + (rectTransform.rect.width - Margin * 2) * (float)(beat / original.LengthBeats);
        private float Y(int lane) => rectTransform.rect.yMax - 30 - (rectTransform.rect.height - 50) * (lane + .5f) / width;
        internal Vector2 NoteCenter(int index) => new Vector2(X(original.Notes[index].Beat), Y((offset + original.Notes[index].LaneOffset) % width));
        internal int HitIndex(Vector2 screenPoint, Camera camera)
        {
            if (original == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, camera, out var point)) return -1;
            int nearest = -1; float distance = float.MaxValue;
            for (int i = 0; i < original.Notes.Count; i++)
            {
                var delta = point - NoteCenter(i);
                if (Mathf.Abs(delta.x) <= 20 && Mathf.Abs(delta.y) <= 17 && delta.sqrMagnitude < distance)
                { nearest = i; distance = delta.sqrMagnitude; }
            }
            return nearest;
        }
        public void OnPointerClick(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            int index = HitIndex(data.position, data.pressEventCamera);
            if (index >= 0) owner.SelectNote(index);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (original == null) return;
            var rect = rectTransform.rect;
            for (int lane = 0; lane < width; lane++)
                NoteWorkshopMesh.Quad(vh, rect.xMin + 24, Y(lane) - .5f, rect.width - 48, 1, RunUI.Hex("394353"));
            for (double beat = 0; beat <= original.LengthBeats; beat += .5)
            {
                bool whole = System.Math.Abs(beat - System.Math.Round(beat)) < .001;
                NoteWorkshopMesh.Quad(vh, X(beat), rect.yMin + 12, 1, rect.height - 38,
                    whole ? new Color(1, 1, 1, .1f) : new Color(1, 1, 1, .04f));
            }
            foreach (var note in composed.Notes)
            {
                float x = X(note.Beat), y = Y((offset + note.LaneOffset) % width);
                var tint = note.IsCall ? RunUI.Teal : owner.Side == WeaponBeatSide.Dark ? BattleVisualTheme.NoteColor(note.Beat + .5) : BattleVisualTheme.NoteColor(note.Beat);
                if (note.IsHold)
                {
                    NoteWorkshopMesh.Quad(vh, x, y - 6, X(note.Beat + note.HoldBeats) - x, 12, new Color(tint.r, tint.g, tint.b, .4f));
                    NoteWorkshopMesh.Quad(vh, X(note.Beat + note.HoldBeats) - 1, y - 9, 2, 18, tint);
                }
                if (note.IsOptional)
                {
                    NoteWorkshopMesh.Quad(vh, x - 10, y - 7, 20, 2, tint);
                    NoteWorkshopMesh.Quad(vh, x - 10, y + 5, 20, 2, tint);
                    NoteWorkshopMesh.Quad(vh, x - 10, y - 7, 2, 14, tint);
                    NoteWorkshopMesh.Quad(vh, x + 8, y - 7, 2, 14, tint);
                }
                else if (note.IsChargedRelease)
                {
                    NoteWorkshopMesh.Diamond(vh, new Vector2(x, y + 4), 6, tint);
                    NoteWorkshopMesh.Quad(vh, x - 1, y - 8, 2, 12, tint);
                }
                else if (note.IsCall) NoteWorkshopMesh.Diamond(vh, new Vector2(x, y), 10, tint);
                else if (note.IsInjected) NoteWorkshopMesh.Diamond(vh, new Vector2(x, y), 6, RunUI.Teal);
                else if (note.ConnectFromPrevious) NoteWorkshopMesh.Diamond(vh, new Vector2(x, y), 5, tint);
                else NoteWorkshopMesh.Quad(vh, x - 10, y - 7, 20, 14, tint);
            }
            foreach (var binding in bindings)
            {
                var c = NoteCenter(binding.BaseNoteIndex); bool frame = binding.Part.Kind == NotePartKind.Frame;
                NoteWorkshopMesh.Quad(vh, c.x + (frame ? -7 : 2), c.y - 19, 5, 4, frame ? RunUI.Gold : RunUI.Teal);
            }
            for (int i = 0; i < original.Notes.Count; i++)
            {
                if (i != selected && i != drop) continue;
                var c = NoteCenter(i); var tint = i == drop ? validDrop ? RunUI.Teal : RunUI.Red : RunUI.TextColor;
                NoteWorkshopMesh.Quad(vh, c.x - 15, c.y - 13, 30, 2, tint);
                NoteWorkshopMesh.Quad(vh, c.x - 15, c.y + 11, 30, 2, tint);
                NoteWorkshopMesh.Quad(vh, c.x - 15, c.y - 13, 2, 26, tint);
                NoteWorkshopMesh.Quad(vh, c.x + 13, c.y - 13, 2, 26, tint);
            }
            if (playhead >= 0) NoteWorkshopMesh.Quad(vh, X(playhead), rect.yMin + 6, 2, rect.height - 22, RunUI.Teal);
        }
    }
}
