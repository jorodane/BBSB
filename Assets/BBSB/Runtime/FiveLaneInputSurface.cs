using UnityEngine;
using UnityEngine.EventSystems;
using BBSB.Core;
using BBSB.Runtime.UI;

namespace BBSB.Runtime
{
    public sealed class FiveLaneInputSurface : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, ICanvasRaycastFilter
    {
        private FiveLanePlayback playback;
        private int lane;
        private int? pointer;
        private RectTransform board;
        private InputExtensions extensions;
        public void Bind(FiveLanePlayback owner, int slot, RectTransform perspectiveBoard = null)
        { playback = owner; lane = slot; board = perspectiveBoard; extensions = owner.Battle.Extensions; }
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (board == null) return true;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(board, screenPoint, eventCamera, out var point)) return false;
            var r = board.rect;
            return r.width > 0 && r.height > 0 && BattleBoardLayout.HitSlot(
                (point.x - r.xMin) / r.width, (point.y - r.yMin) / r.height, extensions) == lane;
        }
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
