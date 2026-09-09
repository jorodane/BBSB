using UnityEngine;
using UnityEngine.EventSystems;

namespace BBSB.Runtime
{
    /// <summary>Capture one mouse/touch pointer. Informational graphics let events reach this surface.</summary>
    public sealed class RhythmInputSurface : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IDragHandler, IInitializePotentialDragHandler
    {
        private RhythmPlayback playback;
        private int pointer;
        public bool Captured { get; private set; }
        public Vector2 Position { get; private set; }
        internal void Bind(RhythmPlayback value) { playback = value; }

        public void OnInitializePotentialDrag(PointerEventData data) { data.useDragThreshold = false; }
        public void OnPointerDown(PointerEventData data)
        {
            if (Captured || playback == null || !playback.CanReceiveInput || data.button != PointerEventData.InputButton.Left) return;
            pointer = data.pointerId; Captured = true; Position = Normalize(data.position);
            playback.PointerDown(Position);
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
            Captured = false; Position = Normalize(data.position); playback.PointerUp(Position);
        }

        internal void Cancel() { Captured = false; }
        private void OnDisable() { Cancel(); }
        private static Vector2 Normalize(Vector2 position) => position / Mathf.Max(1, Mathf.Min(Screen.width, Screen.height));
    }
}
