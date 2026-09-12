using System;
using BBSB.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    /// <summary>A pattern editor: card selection and drag/drop use the same core placement validation.</summary>
    public sealed class WeaponPreparationView : MonoBehaviour
    {
        private RunUI ui;
        private WeaponArrangement loadout;
        private RectTransform page, cards, grid, detail;
        private Text status, patternName;
        private Action<int> practice;
        private Action start, close;
        private int selectedSlot, tickPage;
        private bool dirty;
        public int SelectedPattern { get; private set; }
        public int SelectedSlot => selectedSlot;
        public WeaponArrangement Arrangement => loadout;

        internal void Bind(RunUI ui, RunSession session, int selectedPattern, Action<int> practice, Action start, Action close)
        {
            this.ui = ui; loadout = session.BattleLoadout;
            this.practice = practice; this.start = start; this.close = close;
            SelectedPattern = Mathf.Clamp(selectedPattern, 0, Math.Max(0, loadout.Patterns.Count - 1));
            Build();
        }

        private PlannedAttack Current => loadout.Patterns.Count == 0 ? null : loadout.Patterns[SelectedPattern];

        private void Build()
        {
            page = ui.Stack(transform, "Weapon preparation", 20, 8); RunUI.Stretch(page); ui.Background(page, RunUI.Ink, true);
            var header = ui.Row(page, 48);
            ui.Label(header, "무기 배치 · 연습", 29, RunUI.Gold, 48);
            SmallButton(header, "준비로", close, 130);
            status = ui.Label(page, "무기를 고른 뒤 밝은 칸을 누르거나 드래그해. 같은 패턴이 나올 때마다 발동해.", 20, RunUI.Teal, 48);
            var selector = ui.Row(page, 48);
            SmallButton(selector, "이전", () => ChangePattern(-1), 90);
            patternName = ui.Label(selector, "", 24, RunUI.TextColor, 48, TextAnchor.MiddleCenter);
            SmallButton(selector, "다음", () => ChangePattern(1), 90);
            var main = ui.Rect("Arrangement panels", page);
            var size = main.gameObject.AddComponent<LayoutElement>(); size.flexibleHeight = 1; size.minHeight = 260;
            cards = ui.Stack(main, "Equipped weapon cards", 0, 6);
            RunUI.Overlay(cards, Vector2.zero, new Vector2(.27f, 1), Vector2.zero, new Vector2(-12, 0));
            var right = ui.Stack(main, "Pattern placement", 0, 7);
            RunUI.Overlay(right, new Vector2(.27f, 0), Vector2.one, Vector2.zero, Vector2.zero);
            detail = ui.Stack(right, "Selected weapon details", 0, 4); RunUI.Size(detail, 96);
            grid = ui.Scroll(right);
            var footer = ui.Row(page, 52);
            ui.Button(footer, "자동 배치", () => { loadout.AutoArrange(); status.text = "가능한 입력에 자동 배치했어. 무기별 위치를 확인해줘."; RefreshPanels(); }, height: 52);
            ui.Button(footer, "선택 무기 해제", () => { loadout.Remove(selectedSlot); RefreshPanels(); }, height: 52);
            ui.Button(footer, "이 패턴 연습", () => practice(SelectedPattern), Current != null, true, 52);
            ui.Button(footer, "전투 시작", start, primary: true, height: 52);
            RefreshPanels();
        }

        private void ChangePattern(int delta)
        {
            if (loadout.Patterns.Count == 0) return;
            SelectedPattern = (SelectedPattern + delta + loadout.Patterns.Count) % loadout.Patterns.Count;
            tickPage = 0; RefreshPanels();
        }

        public void SelectWeapon(int slot)
        {
            if (slot < 0 || slot >= loadout.Equipment.Count) return;
            selectedSlot = slot; dirty = true;
            if (Current != null) foreach (var drop in grid.GetComponentsInChildren<WeaponPlacementDrop>())
            {
                bool valid = loadout.CanPlace(slot, Current.MonsterId, Current.Pattern.Id, drop.Offset, out _);
                var button = drop.GetComponent<Button>(); button.interactable = valid;
                button.GetComponent<Image>().color = valid ? RunUI.Hex("365B59") : RunUI.Panel;
                button.GetComponentInChildren<Text>().text = BeatLabel(drop.Offset) + (valid ? "\n배치" : "\n—");
            }
        }

        public bool PlaceAt(int offset)
        {
            if (Current == null) return false;
            bool accepted = loadout.TryPlace(selectedSlot, Current.MonsterId, Current.Pattern.Id, offset, out string reason);
            status.text = accepted ? "배치 완료 · 같은 무기는 한 곳에만 배치돼. 같은 입력에 여러 무기를 겹칠 수 있어." : reason;
            status.color = accepted ? RunUI.Teal : RunUI.Red; dirty = true; return accepted;
        }

        // Rebuild after the event completes; never destroy a drag source during OnBeginDrag/OnDrop.
        private void LateUpdate() { if (dirty && !WeaponCardDrag.Dragging) { dirty = false; RefreshPanels(); } }

        private void RefreshPanels()
        {
            Clear(cards); Clear(detail); Clear(grid);
            for (int i = 0; i < loadout.Equipment.Count; i++)
            {
                int slot = i; var state = loadout.Equipment[i]; var weapon = WeaponCatalog.Find(state.DefinitionId);
                var placed = loadout.At(i);
                string where = "미배치";
                if (placed != null)
                    foreach (var pattern in loadout.Patterns) if (placed.Matches(pattern))
                    { where = pattern.Monster.Name + " · " + BeatLabel(placed.OffsetTick); break; }
                var button = ui.Button(cards, (i + 1) + "  " + weapon.Name + " +" + state.Level + "\n" + where,
                    () => SelectWeapon(slot), primary: i == selectedSlot, height: 64);
                button.name = "Weapon card " + slot;
                var label = button.GetComponentInChildren<Text>(); label.fontSize = 18;
                label.resizeTextForBestFit = true; label.resizeTextMinSize = 14; label.resizeTextMaxSize = 18;
                button.gameObject.AddComponent<WeaponCardDrag>().Bind(this, slot);
            }
            var selected = WeaponCatalog.Find(loadout.Equipment[selectedSlot].DefinitionId);
            ui.Label(detail, selected.Name + "  ·  " + selected.PatternLabel, 23, RunUI.Gold, 34);
            ui.Label(detail, selected.EffectLabel + "  강화 배율 ×" + selected.LevelMultiplier(loadout.Equipment[selectedSlot].Level).ToString("0.##"), 20, RunUI.Muted, 56);
            if (Current == null)
            { patternName.text = "배치할 몬스터 패턴이 없어"; return; }
            patternName.text = (SelectedPattern + 1) + " / " + loadout.Patterns.Count + "   " + Current.Monster.Name + " · " + Current.Pattern.Name;
            ui.Label(grid, "몬스터: " + StepsLabel(Current.Placement.Pattern) + "\n추가한 무기 입력도 함께 수행해. 숫자는 Response 시작부터의 박자야.", 19, RunUI.Muted, 65);
            // Eight quarter-beat cells per page keep every drop target >= 70 px at 1280x720.
            int total = Current.Pattern.ResponseTicks + 1, pages = (total + 7) / 8;
            tickPage = Mathf.Clamp(tickPage, 0, pages - 1);
            var nav = ui.Row(grid, 36);
            SmallButton(nav, "<", () => { tickPage = Math.Max(0, tickPage - 1); RefreshPanels(); }, 60);
            ui.Label(nav, "배치 위치  " + (tickPage + 1) + " / " + pages, 19, RunUI.Muted, 36, TextAnchor.MiddleCenter);
            SmallButton(nav, ">", () => { tickPage = Math.Min(pages - 1, tickPage + 1); RefreshPanels(); }, 60);
            var row = ui.Row(grid, 66, 5);
            for (int i = 0; i < 8; i++)
            {
                int offset = tickPage * 8 + i; if (offset >= total) break;
                bool valid = loadout.CanPlace(selectedSlot, Current.MonsterId, Current.Pattern.Id, offset, out _);
                var placed = loadout.At(selectedSlot);
                bool active = placed != null && placed.Matches(Current) && placed.OffsetTick == offset;
                var button = ui.Button(row, BeatLabel(offset) + (active ? "\n배치됨" : valid ? "\n배치" : "\n—"), () => PlaceAt(offset), valid, active, 66);
                button.name = "Weapon drop tick " + offset; var text = button.GetComponentInChildren<Text>();
                text.fontSize = 18; text.resizeTextForBestFit = true; text.resizeTextMinSize = 14; text.resizeTextMaxSize = 18;
                button.gameObject.AddComponent<WeaponPlacementDrop>().Bind(this, offset);
            }
            foreach (var placed in loadout.Placements)
            {
                if (!placed.Matches(Current)) continue;
                var weapon = WeaponCatalog.Find(loadout.Equipment[placed.Slot].DefinitionId);
                ui.Label(grid, (placed.Slot + 1) + "  " + weapon.Name + "   " + StepsLabel(weapon.Pattern, placed.OffsetTick), 19, RunUI.Teal, 36);
            }
            Canvas.ForceUpdateCanvases();
        }

        internal static string BeatLabel(int tick) => (1 + tick / 4.0).ToString("0.##") + "박";
        private static string StepsLabel(RhythmPattern pattern, int offset = 0)
        {
            string text = "";
            foreach (var step in pattern.Steps)
            {
                if (text.Length > 0) text += " · ";
                text += step.Kind + " " + BeatLabel(offset + step.OffsetTick);
                if (step.DurationTicks > 0) text += "~" + BeatLabel(offset + step.OffsetTick + step.DurationTicks);
            }
            return text;
        }

        private Button SmallButton(Transform parent, string text, Action action, float width)
        {
            var button = ui.Button(parent, text, action, height: 40);
            var layout = button.GetComponent<LayoutElement>(); layout.minWidth = layout.preferredWidth = width; layout.flexibleWidth = 0;
            button.GetComponentInChildren<Text>().fontSize = 21; return button;
        }
        private static void Clear(Transform parent)
        { foreach (Transform child in parent) { child.gameObject.SetActive(false); Destroy(child.gameObject); } }
    }

    public sealed class WeaponCardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private WeaponPreparationView owner;
        private int slot;
        private RectTransform ghost;
        public static bool Dragging { get; private set; }
        internal void Bind(WeaponPreparationView owner, int slot) { this.owner = owner; this.slot = slot; }
        public void OnBeginDrag(PointerEventData data)
        {
            owner.SelectWeapon(slot); Dragging = true;
            var label = GetComponentInChildren<Text>();
            ghost = new RunUI(label.font).Rect("Dragged weapon", owner.transform.root);
            ghost.SetParent(GetComponentInParent<Canvas>().transform, false);
            ghost.sizeDelta = new Vector2(210, 58); ghost.pivot = new Vector2(.5f, .5f);
            var ui = new RunUI(label.font); ui.Background(ghost, RunUI.Gold);
            var text = ui.Label(ghost, WeaponCatalog.Find(owner.Arrangement.Equipment[slot].DefinitionId).Name, 23, RunUI.Ink, 58, TextAnchor.MiddleCenter);
            RunUI.Stretch(text.rectTransform); OnDrag(data);
        }
        public void OnDrag(PointerEventData data)
        {
            if (ghost == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)ghost.parent, data.position, data.pressEventCamera, out var point))
                ghost.localPosition = point;
        }
        public void OnEndDrag(PointerEventData data) { ReleaseGhost(); }
        private void OnDisable() { ReleaseGhost(); }
        private void ReleaseGhost() { if (ghost != null) Destroy(ghost.gameObject); ghost = null; Dragging = false; }
    }

    public sealed class WeaponPlacementDrop : MonoBehaviour, IDropHandler
    {
        private WeaponPreparationView owner; private int offset;
        public int Offset => offset;
        internal void Bind(WeaponPreparationView owner, int offset) { this.owner = owner; this.offset = offset; }
        public void OnDrop(PointerEventData data)
        { if (data.pointerDrag != null && data.pointerDrag.GetComponent<WeaponCardDrag>() != null) owner.PlaceAt(offset); }
    }
}
