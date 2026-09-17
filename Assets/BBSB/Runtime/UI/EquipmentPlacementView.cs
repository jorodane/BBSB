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
        private RunSession session;
        private Action changed;
        private readonly Image[] cells = new Image[BattleInputLayout.LaneCount];
        private readonly Color[] baseColors = new Color[BattleInputLayout.LaneCount];
        private RectTransform area, ghost;
        private Image ghostImage;
        private TMP_Text status, ghostLabel;
        private int selected = -1;
        private int? dragPointer;
        public RectTransform LaneArea => area;
        public RectTransform DragPreview => ghost;
        public WeaponPlacement Preview { get; private set; }
        public bool PreviewValid { get; private set; }

        internal void Bind(RunSession value, RunUI ui, Action onChanged)
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
                    ContentCatalog.Find(occupant.Weapon.DefinitionId).Name);
                var text = ui.Label(rect, label, 21, unlocked ? RunUI.TextColor : RunUI.Muted);
                text.fontSize = 21; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
                RunUI.Stretch(text.rectTransform, 4); column++;
            }
            status = ui.Label(root, "무기를 끌어 놓거나, 무기를 선택한 뒤 원하는 중심 위치를 눌러줘.", 19, RunUI.Muted);
            status.fontSize = 19; status.alignment = TextAlignmentOptions.Center;
            RunUI.Overlay(status.rectTransform, Vector2.zero, new Vector2(1, .27f), new Vector2(4, 2), new Vector2(-4, -2));
            // Outside the inventory's ScrollRect: the preview follows the pointer unclipped,
            // while the board itself stays above the scrolling list of owned weapons.
            var overlay = GetComponentInParent<CanvasGroup>();
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
            status.text = ContentCatalog.Find(weapon.DefinitionId).Name + " · " + weapon.RequiredLanes + "라인의 중심을 놓아줘. 겹치는 무기는 가방으로 돌아가.";
        }
        private bool CanSelect(int index) => session != null && session.CanEditEquipment && index >= 0 && index < session.OwnedWeapons.Count;
        public void BeginWeaponDrag(int index, PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || dragPointer.HasValue || !CanSelect(index)) return;
            selected = index; dragPointer = data.pointerId; data.eligibleForClick = false;
            foreach (var scroll in transform.parent.GetComponentsInChildren<ScrollRect>()) scroll.StopMovement();
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
            ghostLabel.text = ContentCatalog.Find(weapon.DefinitionId).Name + " · " + weapon.RequiredLanes + "라인";
            UpdatePreview(data.position, data.pressEventCamera);
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
            Preview = null; PreviewValid = false;
            for (int i = 0; i < cells.Length; i++) if (cells[i] != null) cells[i].color = baseColors[i];
        }
        private void UpdatePreview(Vector2 screen, Camera camera)
        {
            ClearPreview();
            if (!CanSelect(selected)) return;
            if (!CenterAt(screen, camera, out var center)) { ghostImage.color = new Color(.5f, .5f, .5f, .7f); return; }
            if (!WeaponPlacement.TryCentered(session.OwnedWeapons[selected], center, out var placement))
            { ghostImage.color = RunUI.Red; status.text = "무기 전체가 들어갈 수 있는 위치에 놓아줘. S·L은 복수 라인 무기 전용이야."; return; }
            Preview = placement; PreviewValid = session.Equipment.CanPlace(placement);
            int replaced = session.Equipment.DisplacedBy(placement).Count;
            Color tint = !PreviewValid ? RunUI.Red : replaced > 0 ? RunUI.Gold : RunUI.Teal;
            foreach (int slot in placement.Slots) cells[slot].color = tint;
            tint.a = .78f; ghostImage.color = tint;
            status.text = !PreviewValid ? "잠긴 라인 또는 장착 한도를 확인해줘. 기존 배치는 유지돼." :
                replaced > 0 ? "놓으면 배치돼. 겹치는 무기 " + replaced + "개는 가방으로 돌아가." : "놓으면 이 위치에 배치돼.";
        }
        public void EndWeaponDrag(PointerEventData data)
        {
            if (dragPointer != data.pointerId) return;
            dragPointer = null; ghost.gameObject.SetActive(false);
            Commit(data.position, data.pressEventCamera);
        }
        private void Commit(Vector2 screen, Camera camera)
        {
            UpdatePreview(screen, camera);
            if (PreviewValid && session.EquipWeaponAtCenter(selected, Preview.Center))
            { selected = -1; changed?.Invoke(); return; }
            ClearPreview();
        }
        public void OnPointerClick(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || dragPointer.HasValue) return;
            if (CanSelect(selected)) { Commit(data.position, data.pressEventCamera); return; }
            SelectAt(data.position, data.pressEventCamera);
        }
        private void SelectAt(Vector2 screen, Camera camera)
        {
            if (!CenterAt(screen, camera, out var center)) return;
            var occupant = session.Equipment.At(BattleInputLayout.SlotAtPosition((int)Math.Floor(center + .5)));
            if (occupant == null) return;
            for (int i = 0; i < session.OwnedWeapons.Count; i++)
                if (ReferenceEquals(session.OwnedWeapons[i], occupant.Weapon)) { SelectWeapon(i); return; }
        }
        public void OnBeginDrag(PointerEventData data)
        { SelectAt(data.pressPosition, data.pressEventCamera); BeginWeaponDrag(selected, data); }
        public void OnDrag(PointerEventData data) => MoveWeaponDrag(data);
        public void OnEndDrag(PointerEventData data) => EndWeaponDrag(data);
        private void OnDisable()
        { dragPointer = null; if (ghost != null) ghost.gameObject.SetActive(false); }
        private void OnDestroy() { if (ghost != null) Destroy(ghost.gameObject); }
    }
}
