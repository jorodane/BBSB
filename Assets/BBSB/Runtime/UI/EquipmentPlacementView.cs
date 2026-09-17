using System;
using BBSB.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    // The board uses physical line coordinates: S=-1, D=0 ... K=4, L=5.
    // Even-sized footprints are centred in the gaps, not anchored to either end.
    public sealed class EquipmentPlacementView : MonoBehaviour, IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private IEquipmentEditor session;
        private Action changed;
        private readonly Image[] cells = new Image[BattleInputLayout.LaneCount];
        private readonly Color[] baseColors = new Color[BattleInputLayout.LaneCount];
        private RectTransform area, ghost, unequipArea, overlayArea;
        private Image ghostImage, unequipImage;
        private TMP_Text status, ghostLabel;
        private int selected = -1;
        private int? dragPointer;
        private bool dragFromBoard;
        public RectTransform LaneArea => area;
        public RectTransform DragPreview => ghost;
        public RectTransform UnequipArea => unequipArea;
        public WeaponPlacement Preview { get; private set; }
        public bool PreviewValid { get; private set; }
        public bool PreviewUnequip { get; private set; }
        private const string Instructions = "이미지를 끌어 배치 · 배치된 무기를 아래로 끌면 장착 해제";

        internal void Bind(IEquipmentEditor value, RunUI ui, Action onChanged)
        {
            session = value; changed = onChanged;
            var root = (RectTransform)transform;
            ui.Background(root, RunUI.Panel, true);
            area = ui.Rect("Placement lanes", root);
            RunUI.Overlay(area, new Vector2(0, .27f), Vector2.one, Vector2.zero, Vector2.zero);
            int column = 0;
            foreach (int slot in BattleInputLayout.DisplayOrder)
            {
                var rect = ui.Rect("Placement " + BattleInputLayout.Key(slot), area);
                RunUI.Overlay(rect, new Vector2(column / 7f, 0), new Vector2((column + 1) / 7f, 1), new Vector2(3, 3), new Vector2(-3, -3));
                var occupant = session.Equipment.At(slot);
                bool unlocked = session.Equipment.IsAvailable(slot);
                baseColors[slot] = !unlocked ? RunUI.Ink : occupant == null ? RunUI.Hex("263348") : RunUI.Hex("36545C");
                cells[slot] = ui.Background(rect, baseColors[slot]);
                string label = BattleInputLayout.Key(slot) + "\n" + (!unlocked ? "잠김" : occupant == null ? "비어 있음" :
                    occupant.Weapon.DisplayName);
                var text = ui.Label(rect, label, 21, unlocked ? RunUI.TextColor : RunUI.Muted);
                text.fontSize = 21; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
                RunUI.Stretch(text.rectTransform, 4); column++;
            }
            unequipArea = ui.Rect("Unequip drop area", root);
            RunUI.Overlay(unequipArea, Vector2.zero, new Vector2(1, .27f), new Vector2(3, 3), new Vector2(-3, -3));
            unequipImage = ui.Background(unequipArea, RunUI.Hex("263348"));
            status = ui.Label(unequipArea, Instructions, 19, RunUI.Muted);
            status.fontSize = 19; status.alignment = TextAlignmentOptions.Center;
            status.color = RunUI.Muted; RunUI.Stretch(status.rectTransform, 4);
            // Outside the inventory's ScrollRect: the preview follows the pointer unclipped,
            // while the board itself stays above the scrolling list of owned weapons.
            var overlay = GetComponentInParent<CanvasGroup>();
            overlayArea = overlay != null ? (RectTransform)overlay.transform : root;
            ghost = ui.Rect("Equipment drag preview", overlay != null ? overlay.transform : transform);
            ghost.anchorMin = ghost.anchorMax = new Vector2(.5f, .5f); ghost.pivot = new Vector2(.5f, .5f);
            ghostImage = ui.Background(ghost, new Color(.2f, .9f, .7f, .75f));
            ghostLabel = ui.Label(ghost, "", 22); ghostLabel.fontSize = 22; ghostLabel.alignment = TextAlignmentOptions.Center;
            RunUI.Stretch(ghostLabel.rectTransform, 4); ghost.gameObject.SetActive(false);
        }
        public void SelectWeapon(int inventoryIndex)
        {
            if (!CanSelect(inventoryIndex)) return;
            selected = inventoryIndex;
            var weapon = session.OwnedWeapons[selected];
            status.text = weapon.DisplayName + " · " + weapon.RequiredLanes + "라인의 중심을 놓아줘. 겹치는 무기는 가방으로 돌아가.";
        }
        private bool CanSelect(int index) => session != null && session.CanEditEquipment && index >= 0 && index < session.OwnedWeapons.Count;
        public void BeginWeaponDrag(int index, PointerEventData data, bool fromBoard = false)
        {
            if (data.button != PointerEventData.InputButton.Left || dragPointer.HasValue || !CanSelect(index)) return;
            selected = index; dragPointer = data.pointerId; data.eligibleForClick = false;
            dragFromBoard = fromBoard && session.Equipment.PlacementOf(session.OwnedWeapons[index]) != null;
            foreach (var scroll in overlayArea.GetComponentsInChildren<ScrollRect>()) scroll.StopMovement();
            MoveWeaponDrag(data);
        }
        public void MoveWeaponDrag(PointerEventData data)
        {
            if (dragPointer != data.pointerId || !CanSelect(selected)) return;
            var weapon = session.OwnedWeapons[selected];
            ghost.gameObject.SetActive(true); ghost.SetAsLastSibling();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)ghost.parent, data.position, data.pressEventCamera, out var local))
                ghost.anchoredPosition = local;
            ghost.sizeDelta = new Vector2(area.rect.width / 7 * weapon.RequiredLanes - 6, area.rect.height * .65f);
            ghostLabel.text = weapon.DisplayName + " · " + weapon.RequiredLanes + "라인";
            UpdatePreview(data.position, data.pressEventCamera, dragFromBoard);
        }
        private bool CenterAt(Vector2 screen, Camera camera, out double center)
        {
            center = 0;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, camera, out var local) ||
                area.rect.width <= 0 || !area.rect.Contains(local)) return false;
            center = (local.x - area.rect.xMin) / (area.rect.width / 7) - 1.5;
            return true;
        }
        private void ClearPreview()
        {
            Preview = null; PreviewValid = PreviewUnequip = false;
            if (unequipImage != null) unequipImage.color = RunUI.Hex("263348");
            for (int i = 0; i < cells.Length; i++) if (cells[i] != null) cells[i].color = baseColors[i];
        }
        private bool BelowLanes(Vector2 screen, Camera camera)
        {
            // The strip and inventory below it are one forgiving drop region. Side,
            // top and off-screen drops remain cancellations, not implicit unequips.
            var root = (RectTransform)transform;
            if (!RectTransformUtility.RectangleContainsScreenPoint(overlayArea, screen, camera) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, camera, out var local)) return false;
            float bottom = root.InverseTransformPoint(area.TransformPoint(new Vector2(0, area.rect.yMin))).y;
            return local.x >= root.rect.xMin && local.x <= root.rect.xMax && local.y < bottom;
        }
        private void UpdatePreview(Vector2 screen, Camera camera, bool allowUnequip = false)
        {
            ClearPreview();
            if (!CanSelect(selected)) return;
            status.color = RunUI.Muted;
            if (allowUnequip && session.Equipment.PlacementOf(session.OwnedWeapons[selected]) != null && BelowLanes(screen, camera))
            {
                PreviewUnequip = true; ghostImage.color = RunUI.Red;
                unequipImage.color = RunUI.Hex("603D4B"); status.color = RunUI.TextColor;
                status.text = "놓으면 장착 해제 · 무기는 가방에 남아.";
                ghostLabel.text = session.OwnedWeapons[selected].DisplayName + " · 장착 해제";
                return;
            }
            if (!CenterAt(screen, camera, out var center))
            { ghostImage.color = new Color(.5f, .5f, .5f, .7f); status.text = Instructions; return; }
            if (!WeaponPlacement.TryCentered(session.OwnedWeapons[selected], center, out var placement))
            { ghostImage.color = RunUI.Red; status.text = "무기 전체가 들어갈 수 있는 위치에 놓아줘. S·L은 복수 라인 무기 전용이야."; return; }
            Preview = placement; PreviewValid = session.Equipment.CanPlace(placement);
            int replaced = session.Equipment.DisplacedBy(placement).Count;
            Color tint = !PreviewValid ? RunUI.Red : replaced > 0 ? RunUI.Gold : RunUI.Teal;
            foreach (int slot in placement.Slots) cells[slot].color = tint;
            tint.a = .78f; ghostImage.color = tint;
            status.text = !PreviewValid ? "잠긴 라인·장착 한도·같은 속성의 동일 무기 중복을 확인해줘." :
                replaced > 0 ? "놓으면 배치돼. 겹치는 무기 " + replaced + "개는 가방으로 돌아가." : "놓으면 이 위치에 배치돼.";
        }
        public void EndWeaponDrag(PointerEventData data)
        {
            if (dragPointer != data.pointerId) return;
            dragPointer = null; ghost.gameObject.SetActive(false);
            bool allowUnequip = dragFromBoard; dragFromBoard = false;
            Commit(data.position, data.pressEventCamera, allowUnequip);
        }
        private void Commit(Vector2 screen, Camera camera, bool allowUnequip = false)
        {
            UpdatePreview(screen, camera, allowUnequip);
            if ((PreviewUnequip && session.UnequipWeapon(selected)) ||
                (PreviewValid && session.EquipWeaponAtCenter(selected, Preview.Center)))
            { selected = -1; changed?.Invoke(); return; }
            ClearPreview();
        }
        public void OnPointerClick(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || dragPointer.HasValue) return;
            if (CanSelect(selected)) { Commit(data.position, data.pressEventCamera); return; }
            SelectWeapon(InventoryIndexAt(data.position, data.pressEventCamera));
        }
        private int InventoryIndexAt(Vector2 screen, Camera camera)
        {
            if (!CenterAt(screen, camera, out var center)) return -1;
            var occupant = session.Equipment.At(BattleInputLayout.SlotAtPosition((int)Math.Floor(center + .5)));
            if (occupant == null) return -1;
            for (int i = 0; i < session.OwnedWeapons.Count; i++)
                if (ReferenceEquals(session.OwnedWeapons[i], occupant.Weapon)) return i;
            return -1;
        }
        public void OnBeginDrag(PointerEventData data)
        { BeginWeaponDrag(InventoryIndexAt(data.pressPosition, data.pressEventCamera), data, true); }
        public void OnDrag(PointerEventData data) => MoveWeaponDrag(data);
        public void OnEndDrag(PointerEventData data) => EndWeaponDrag(data);
        private void OnDisable()
        { dragPointer = null; dragFromBoard = false; if (ghost != null) ghost.gameObject.SetActive(false); }
        private void OnDestroy() { if (ghost != null) Destroy(ghost.gameObject); }
    }
}
