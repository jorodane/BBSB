using UnityEngine;
using UnityEngine.EventSystems;

namespace BBSB.Runtime
{
    public sealed class FiveLaneInputSurface : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private FiveLanePlayback playback;
        private int lane;
        private int? pointer;
        public void Bind(FiveLanePlayback owner, int slot) { playback = owner; lane = slot; }
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || pointer.HasValue || playback == null || !playback.CanReceiveInput) return;
            pointer = data.pointerId; playback.SetPointer(lane, true);
        }
        public void OnPointerUp(PointerEventData data)
        {
            if (pointer != data.pointerId) return;
            pointer = null; playback.SetPointer(lane, false);
        }
        public void Cancel() { pointer = null; }
    }
}
