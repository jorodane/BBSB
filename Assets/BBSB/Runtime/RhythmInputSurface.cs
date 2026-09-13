using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BBSB.Runtime
{
    /// <summary>Capture one mouse/touch pointer. Informational graphics let events reach this surface.</summary>
    public sealed class RhythmInputSurface : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IDragHandler, IInitializePotentialDragHandler
    {
        private Func<bool> canReceiveInput;
        private Action<Vector2> down, up;
        private int pointer;
        public bool Captured { get; private set; }
        public Vector2 Position { get; private set; }
        internal void Bind(RhythmPlayback value) { Bind(() => value != null && value.CanReceiveInput, value.PointerDown, value.PointerUp); }
        internal void Bind(Func<bool> canReceiveInput, Action<Vector2> down, Action<Vector2> up)
        { this.canReceiveInput = canReceiveInput; this.down = down; this.up = up; }

        public void OnInitializePotentialDrag(PointerEventData data) { data.useDragThreshold = false; }
        public void OnPointerDown(PointerEventData data)
        {
            if (Captured || canReceiveInput == null || !canReceiveInput() || data.button != PointerEventData.InputButton.Left) return;
            pointer = data.pointerId; Captured = true; Position = Normalize(data.position);
            down(Position);
        }

        public void OnDrag(PointerEventData data)
        {
            if (!Captured || data.pointerId != pointer) return;
            // Playback samples once in LateUpdate after the EventSystem has supplied this frame's position.
            Position = Normalize(data.position);
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (!Captured || data.pointerId != pointer) return;
            Captured = false; Position = Normalize(data.position); up(Position);
        }

        internal void Cancel() { Captured = false; }
        private void OnDisable() { Cancel(); }
        private static Vector2 Normalize(Vector2 position) => position / Mathf.Max(1, Mathf.Min(Screen.width, Screen.height));
    }
}
