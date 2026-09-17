using UnityEngine;
using UnityEngine.EventSystems;

namespace BBSB.Runtime.UI
{
    public sealed class EquipmentDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private EquipmentPlacementView board;
        private int inventoryIndex;
        public void Bind(EquipmentPlacementView target, int index) { board = target; inventoryIndex = index; }
        public void OnBeginDrag(PointerEventData data) { if (board != null) board.BeginWeaponDrag(inventoryIndex, data); }
        public void OnDrag(PointerEventData data) { if (board != null) board.MoveWeaponDrag(data); }
        public void OnEndDrag(PointerEventData data) { if (board != null) board.EndWeaponDrag(data); }
    }
}
