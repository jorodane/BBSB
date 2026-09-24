using UnityEngine;
using UnityEngine.EventSystems;

namespace BBSB.Runtime.UI
{
    public sealed class NotePartDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private NoteWorkshopView owner;
        internal int InstanceId { get; private set; }
        internal void Bind(NoteWorkshopView view, int id) { owner = view; InstanceId = id; }
        public void OnBeginDrag(PointerEventData data) { if (owner != null) owner.BeginPartDrag(InstanceId, data); }
        public void OnDrag(PointerEventData data) { if (owner != null) owner.MovePartDrag(data); }
        public void OnEndDrag(PointerEventData data) { if (owner != null) owner.EndPartDrag(data); }
    }
}
