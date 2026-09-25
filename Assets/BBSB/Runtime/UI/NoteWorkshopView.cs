using System;
using System.Collections.Generic;
using BBSB.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    internal sealed class NoteWorkshopSelection
    {
        public int Weapon, Note, Offset;
        public WeaponBeatSide Side;
        public string Notice = "";
    }

    public sealed class NoteWorkshopView : MonoBehaviour
    {
        private RunUI ui;
        private RunSession session;
        private NoteWorkshopSelection selection;
        private WeaponState weapon;
        private WeaponPhraseSet source;
        private WeaponPhrase original, composed;
        private RectTransform left, slots, bag, patternContent, ghost;
        private ScrollRect bagScroll, patternScroll;
        private GridLayoutGroup grid;
        private LayoutElement gridSize;
        private TMP_Text detail;
        private NoteWorkshopDemoView demo;
        private NoteWorkshopPatternGraphic pattern;
        private readonly Dictionary<int, Image> cards = new Dictionary<int, Image>();
        private readonly List<TMP_Text> beatLabels = new List<TMP_Text>();
        private int selectedPart = -1, draggingPart = -1, pointer;
        private Vector2 pointerPosition;
        private Camera pointerCamera;
        private float lastGridWidth = -1, lastPatternWidth = -1;
        internal WeaponBeatSide Side => selection.Side;
        internal NoteWorkshopPatternGraphic Pattern => pattern;
        internal NoteWorkshopDemoView Demo => demo;
        internal RectTransform BagViewport => bagScroll.viewport;

        internal void Bind(RunUI runUI, RunSession run, NoteWorkshopSelection state)
        {
            ui = runUI; session = run; selection = state;
            left = ui.Stack(transform, "Pattern and free notes", 8, 6);
            RunUI.Overlay(left, Vector2.zero, new Vector2(.66f, 1), Vector2.zero, new Vector2(-8, 0));
            var right = ui.Stack(transform, "Automatic example", 12, 6);
            RunUI.Overlay(right, new Vector2(.66f, 0), Vector2.one, new Vector2(8, 0), Vector2.zero);
            ui.Background(right, RunUI.Panel);
            demo = right.gameObject.AddComponent<NoteWorkshopDemoView>(); demo.Build(ui);
            RefreshWeapon();
        }

        private void RefreshWeapon(bool preserveScroll = false)
        {
            CancelDrag();
            float bagY = preserveScroll && bagScroll != null ? bagScroll.verticalNormalizedPosition : 1;
            float patternX = preserveScroll && patternScroll != null ? patternScroll.horizontalNormalizedPosition : 0;
            Clear(left); cards.Clear(); beatLabels.Clear(); selectedPart = -1;
            if (session.OwnedWeapons.Count == 0)
            { Caption(ui, left, "먼저 무기를 획득해 줘.", 22, RunUI.Muted, 60); return; }
            selection.Weapon = Mathf.Clamp(selection.Weapon, 0, session.OwnedWeapons.Count - 1);
            weapon = session.OwnedWeapons[selection.Weapon];
            source = WeaponPhraseAuthoring.LoadSetsFor(new[] { weapon })[0];
            selection.Offset = Mathf.Clamp(selection.Offset, 0, weapon.RequiredLanes - 1);
            original = source.For(selection.Offset, selection.Side);
            composed = WeaponNoteAssembly.Apply(weapon, source).For(selection.Offset, selection.Side);
            selection.Note = Mathf.Clamp(selection.Note, 0, original.Notes.Count - 1);
            DrawChooser();
            Caption(ui, left, original.FirstNoteDelayBeats > 0 ?
                "호출 → " + original.FirstNoteDelayBeats.ToString("0.##") + "박 준비 후 연주  ·  ◆ 콜  ▬ 리스폰스" :
                "호출 즉시 실행  ·  ◆ 콜  ▬ 리스폰스", 17, RunUI.Gold, 24);
            DrawPattern();
            slots = ui.Row(left, 48, 8); slots.name = "Selected note parts"; DrawSlots();
            detail = Caption(ui, left, "", 17, RunUI.Muted, 42);
            ShowNotice(string.IsNullOrEmpty(selection.Notice) ? "부품을 선택하고 노트를 클릭해도 장착할 수 있어." : selection.Notice);
            DrawBag();
            string[] labels = new string[weapon.RequiredLanes];
            var placement = session.Equipment.PlacementOf(weapon);
            for (int i = 0; i < labels.Length; i++) labels[i] = placement == null ? (i + 1) + "번" : BattleInputLayout.Key(placement.Slots[i]);
            demo.Bind(composed, weapon.RequiredLanes, selection.Offset, selection.Side, labels);
            lastGridWidth = lastPatternWidth = -1;
            Canvas.ForceUpdateCanvases(); RefreshLayout(); Canvas.ForceUpdateCanvases();
            bagScroll.verticalNormalizedPosition = bagY; patternScroll.horizontalNormalizedPosition = patternX;
        }

        private void DrawChooser()
        {
            var chooser = ui.Row(left, 44, 6);
            var previous = Control(ui, chooser, "‹", () => ChangeWeapon(-1), 44, selection.Weapon > 0);
            FixedWidth((RectTransform)previous.transform, 36);
            var icon = ui.Rect("Selected weapon", chooser); FixedWidth(icon, 44); RunUI.Size(icon, 44);
            var artwork = icon.gameObject.AddComponent<WeaponIconGraphic>(); artwork.FitVisibleArtwork = true; artwork.Bind(weapon);
            foreach (var socketsGraphic in artwork.GetComponentsInChildren<WeaponSocketGraphic>()) socketsGraphic.gameObject.SetActive(false);
            Caption(ui, chooser, weapon.DisplayName + " +" + weapon.Level, 23, RunUI.TextColor, 44);
            var next = Control(ui, chooser, "›", () => ChangeWeapon(1), 44, selection.Weapon + 1 < session.OwnedWeapons.Count);
            FixedWidth((RectTransform)next.transform, 36);
            var variants = ui.Row(left, 30, 6);
            Control(ui, variants, "빛 박자", () => ChangeVariant(WeaponBeatSide.Light, selection.Offset), 30, primary: selection.Side == WeaponBeatSide.Light);
            Control(ui, variants, "어둠 박자", () => ChangeVariant(WeaponBeatSide.Dark, selection.Offset), 30, primary: selection.Side == WeaponBeatSide.Dark);
            if (weapon.RequiredLanes > 1)
                for (int i = 0; i < weapon.RequiredLanes; i++)
                {
                    int offset = i;
                    Control(ui, variants, "시작 " + (i + 1), () => ChangeVariant(selection.Side, offset), 30, primary: selection.Offset == offset);
                }
        }
        private void ChangeWeapon(int delta)
        { selection.Weapon += delta; selection.Note = selection.Offset = 0; selection.Notice = ""; RefreshWeapon(); }
        private void ChangeVariant(WeaponBeatSide side, int offset)
        { selection.Side = side; selection.Offset = offset; selection.Notice = ""; RefreshWeapon(); }

        private void DrawPattern()
        {
            var root = ui.Rect("Horizontal pattern", left); RunUI.Size(root, Mathf.Max(112, 50 + weapon.RequiredLanes * 30));
            ui.Background(root, RunUI.Panel, true);
            patternScroll = root.gameObject.AddComponent<ScrollRect>();
            patternScroll.horizontal = true; patternScroll.vertical = false; patternScroll.inertia = false;
            patternScroll.movementType = ScrollRect.MovementType.Clamped; patternScroll.scrollSensitivity = 36;
            var viewport = ui.Rect("Pattern viewport", root); RunUI.Stretch(viewport); viewport.offsetMin = new Vector2(0, 12);
            viewport.gameObject.AddComponent<RectMask2D>();
            patternContent = ui.Rect("Pattern timeline", viewport);
            patternContent.anchorMin = Vector2.zero; patternContent.anchorMax = Vector2.up; patternContent.pivot = new Vector2(0, .5f);
            patternContent.anchoredPosition = Vector2.zero; patternContent.sizeDelta = new Vector2(400, 0);
            pattern = patternContent.gameObject.AddComponent<NoteWorkshopPatternGraphic>();
            pattern.Bind(this, original, composed, weapon.RequiredLanes, selection.Offset, selection.Note, weapon.NoteBindings);
            var placement = session.Equipment.PlacementOf(weapon);
            for (int i = 0; i < weapon.RequiredLanes; i++)
            {
                var key = Caption(ui, patternContent, placement == null ? (i + 1).ToString() : BattleInputLayout.Key(placement.Slots[i]),
                    13, RunUI.Muted, 18, TextAlignmentOptions.Center);
                key.enableAutoSizing = true; key.fontSizeMin = 9; key.fontSizeMax = 13; key.textWrappingMode = TextWrappingModes.NoWrap;
                float lane = (i + .5f) / weapon.RequiredLanes;
                RunUI.Pin(key.rectTransform, new Vector2(0, 1 - lane), new Vector2(.5f, .5f),
                    new Vector2(16, -30 + 50 * lane), new Vector2(30, 18));
            }
            for (int i = 0; i <= Math.Floor(original.LengthBeats); i++)
            {
                var label = Caption(ui, patternContent, (i + 1).ToString(), 13, RunUI.Muted, 20, TextAlignmentOptions.Center);
                beatLabels.Add(label);
            }
            patternScroll.viewport = viewport; patternScroll.content = patternContent;
            var track = ui.Rect("Pattern scrollbar", root);
            RunUI.Overlay(track, Vector2.zero, Vector2.right, Vector2.zero, new Vector2(0, 10));
            ui.Background(track, RunUI.Ink, true);
            var handle = ui.Rect("Handle", track); RunUI.Stretch(handle);
            var fill = ui.Background(handle, RunUI.Muted, true);
            var bar = track.gameObject.AddComponent<Scrollbar>(); bar.handleRect = handle; bar.targetGraphic = fill;
            bar.direction = Scrollbar.Direction.LeftToRight; bar.navigation = new Navigation { mode = Navigation.Mode.None };
            patternScroll.horizontalScrollbar = bar;
        }

        private void DrawSlots()
        {
            Clear(slots);
            var label = Caption(ui, slots, (selection.Note + 1) + "번", 17, RunUI.Muted, 48); FixedWidth(label.rectTransform, 44);
            foreach (var kind in new[] { NotePartKind.Frame, NotePartKind.Injection })
            {
                WeaponNoteBinding binding = null;
                foreach (var installed in weapon.NoteBindings)
                    if (installed.BaseNoteIndex == selection.Note && installed.Part.Kind == kind) binding = installed;
                var cell = ui.Row(slots, 48, 4); RunUI.Size(cell, 48, 1); ui.Background(cell, RunUI.Panel);
                if (binding == null)
                { Caption(ui, cell, kind == NotePartKind.Frame ? "프레임 없음" : "주입 없음", 16, RunUI.Muted, 48, TextAlignmentOptions.Center); continue; }
                int id = binding.PartInstanceId;
                var icon = PartIcon(cell, binding.Part, id, 40); FixedWidth(icon, 40);
                var select = Control(ui, cell, binding.Part.Name, () => SelectPart(id), 48);
                select.name = "Installed part " + id;
                var remove = Control(ui, cell, "해제", () => Remove(id), 48, session.CanEditEquipment);
                remove.name = "Remove part " + id; FixedWidth((RectTransform)remove.transform, 42);
            }
        }

        private void DrawBag()
        {
            var free = NoteWorkshopModel.FreeParts(session); int total = 0;
            foreach (var stack in free) total += stack.Count;
            Caption(ui, left, "보유 노트  ·  미장착 " + total, 20, RunUI.TextColor, 26);
            var content = ui.Scroll(left); bagScroll = content.GetComponentInParent<ScrollRect>();
            bagScroll.name = "Free note inventory"; bagScroll.GetComponent<LayoutElement>().minHeight = 70;
            if (free.Count == 0)
            {
                Caption(ui, content, "미장착 부품이 없어.\n보상이나 상점에서 얻을 수 있어.", 18, RunUI.Muted, 82);
            }
            bag = ui.Rect("Free note grid", content);
            grid = bag.gameObject.AddComponent<GridLayoutGroup>(); grid.spacing = new Vector2(8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridSize = RunUI.Size(bag, 0, 1); gridSize.minWidth = gridSize.preferredWidth = 0;
            foreach (var stack in free)
            {
                var part = stack.Part; int id = part.InstanceId;
                var card = ui.Rect("Free part " + id, bag);
                var background = ui.Background(card, RunUI.Panel, true); cards.Add(id, background);
                var button = card.gameObject.AddComponent<Button>(); button.targetGraphic = background;
                button.navigation = new Navigation { mode = Navigation.Mode.None }; button.interactable = session.CanEditEquipment;
                button.onClick.AddListener(() => { if (button.IsActive() && button.IsInteractable()) SelectPart(id); });
                var icon = PartIcon(card, part.Definition, id, 48);
                RunUI.Pin(icon, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(6, 0), new Vector2(48, 56));
                var name = Caption(ui, card, part.Definition.Name, 18, RunUI.TextColor, 48); name.maxVisibleLines = 2;
                RunUI.Overlay(name.rectTransform, Vector2.zero, Vector2.one, new Vector2(62, 28), new Vector2(-8, -8));
                var count = Caption(ui, card, (part.Definition.Kind == NotePartKind.Frame ? "프레임" : "박자 주입") + "  ×" + stack.Count, 14, RunUI.Muted, 20);
                RunUI.Overlay(count.rectTransform, Vector2.zero, Vector2.right, new Vector2(62, 6), new Vector2(-8, 26));
            }
        }

        private RectTransform PartIcon(Transform parent, NotePartDefinition part, int instanceId, float size)
        {
            var icon = ui.Rect("Drag part " + instanceId, parent); RunUI.Size(icon, size);
            ui.Background(icon, Color.clear, true);
            icon.gameObject.AddComponent<NotePartDragHandle>().Bind(this, instanceId);
            var art = ui.Rect("Part artwork", icon); RunUI.Stretch(art, 3);
            art.gameObject.AddComponent<NotePartIconGraphic>().Bind(part);
            return icon;
        }

        internal void SelectNote(int index)
        {
            if (index < 0 || index >= original.Notes.Count || draggingPart >= 0) return;
            if (selectedPart >= 0) { Attach(selectedPart, index); return; }
            selection.Note = index; pattern.Select(index); DrawSlots();
            var note = original.Notes[index];
            ShowNotice((index + 1) + "번 · " + (note.IsChargedRelease ? "앞 홀드의 유지 시간에 비례 · 떼어 발사" :
                note.IsOptional ? "발사 기회 · 지나쳐도 미스 없음 · 한 번 발사하면 완료" : note.IsCall ? "준비 콜" : "리스폰스") + " · " + note.Beat.ToString("0.##") + "박" +
                (note.Condition == PhraseNoteCondition.AllCalls ? " · 앞선 콜을 모두 성공하면 실행" : " · 부품을 끌어 놓으면 적용돼."));
        }
        private void SelectPart(int id)
        {
            if (draggingPart >= 0) return;
            int index = NoteWorkshopModel.PartIndex(session, id); if (index < 0) return;
            selectedPart = selectedPart == id ? -1 : id;
            foreach (var card in cards) card.Value.color = card.Key == selectedPart ? RunUI.Hex("34534F") : RunUI.Panel;
            var part = session.NoteParts[index];
            ShowNotice(selectedPart < 0 ? "부품 선택을 취소했어." : part.Definition.Name + " · " + part.Definition.Description);
        }
        private void Attach(int id, int noteIndex)
        {
            if (NoteWorkshopModel.TryAttach(session, id, selection.Weapon, noteIndex, source, out string reason))
            { selection.Note = noteIndex; selection.Notice = "장착했어. 교체된 부품은 가방으로 돌아가."; RefreshWeapon(true); }
            else ShowNotice(reason, true);
        }
        private void Remove(int id)
        {
            if (!NoteWorkshopModel.Remove(session, id, weapon)) { ShowNotice("지금은 부품을 해제할 수 없어.", true); return; }
            selection.Notice = "해제한 부품을 가방에 넣었어."; RefreshWeapon(true);
        }
        private void ShowNotice(string text, bool error = false)
        { if (detail != null) { detail.text = text; detail.color = error ? RunUI.Red : RunUI.Muted; } }

        internal void BeginPartDrag(int id, PointerEventData data)
        {
            if (draggingPart >= 0) { CancelDrag(); return; }
            if (data.button != PointerEventData.InputButton.Left || !session.CanEditEquipment) return;
            int index = NoteWorkshopModel.PartIndex(session, id); if (index < 0) return;
            var owner = session.NotePartOwner(id); if (owner != null && !ReferenceEquals(owner, weapon)) return;
            draggingPart = id; pointer = data.pointerId; selectedPart = -1; data.eligibleForClick = false;
            bagScroll.StopMovement(); patternScroll.StopMovement();
            var part = session.NoteParts[index].Definition;
            ghost = ui.Rect("Dragged note part", transform); RunUI.Size(ghost, 64).ignoreLayout = true;
            RunUI.Pin(ghost, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(200, 64));
            ui.Background(ghost, RunUI.Hex("34534F"));
            var group = ghost.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = group.interactable = false; group.alpha = .92f;
            var label = Caption(ui, ghost, part.Name, 19, RunUI.TextColor, 64, TextAlignmentOptions.Center); RunUI.Stretch(label.rectTransform, 8);
            MovePartDrag(data);
        }
        internal void MovePartDrag(PointerEventData data)
        {
            if (draggingPart < 0 || data.pointerId != pointer) return;
            pointerPosition = data.position; pointerCamera = data.pressEventCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, pointerPosition, pointerCamera, out var point))
                ghost.localPosition = new Vector3(point.x, point.y + 42, 0);
            UpdateDrop();
        }
        private int DropIndex() => RectTransformUtility.RectangleContainsScreenPoint(patternScroll.viewport, pointerPosition, pointerCamera)
            ? pattern.HitIndex(pointerPosition, pointerCamera) : -1;
        private void UpdateDrop()
        {
            int index = DropIndex(); string reason = "";
            bool valid = index >= 0 && NoteWorkshopModel.TryAttach(session, draggingPart, selection.Weapon, index, source, out reason, true);
            pattern.Hover(index, valid);
            ShowNotice(index < 0 ? "노트 위에 놓아 장착 · 장착된 부품은 가방으로 끌어 해제" : valid ? (index + 1) + "번 노트에 장착" : reason, index >= 0 && !valid);
        }
        internal void EndPartDrag(PointerEventData data)
        {
            if (draggingPart < 0 || data.pointerId != pointer) return;
            pointerPosition = data.position; pointerCamera = data.pressEventCamera;
            int id = draggingPart, index = DropIndex();
            bool overBag = RectTransformUtility.RectangleContainsScreenPoint(bagScroll.viewport, pointerPosition, pointerCamera);
            CancelDrag();
            if (index >= 0) Attach(id, index);
            else if (overBag && ReferenceEquals(session.NotePartOwner(id), weapon)) Remove(id);
            else ShowNotice("이동을 취소했어.");
        }
        private void CancelDrag()
        {
            draggingPart = -1;
            if (ghost != null) { ghost.gameObject.SetActive(false); Destroy(ghost.gameObject); ghost = null; }
            if (pattern != null) pattern.Hover(-1, false);
        }
        private void OnDisable() { CancelDrag(); }
        private void OnApplicationFocus(bool focused) { if (!focused) CancelDrag(); }
        private void LateUpdate()
        {
            if (pattern == null) return;
            RefreshLayout(); pattern.Playhead(demo.PatternBeat);
            if (draggingPart < 0) return;
            if (!session.CanEditEquipment) { CancelDrag(); return; }
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(patternScroll.viewport, pointerPosition, pointerCamera, out var local))
            {
                var rect = patternScroll.viewport.rect;
                float span = patternContent.rect.width - rect.width;
                if (span > 1 && local.y >= rect.yMin && local.y <= rect.yMax && local.x >= rect.xMin - 16 && local.x <= rect.xMax + 16)
                {
                    int direction = local.x < rect.xMin + 28 ? -1 : local.x > rect.xMax - 28 ? 1 : 0;
                    patternScroll.horizontalNormalizedPosition = Mathf.Clamp01(patternScroll.horizontalNormalizedPosition + direction * Time.unscaledDeltaTime * 340 / span);
                }
            }
            UpdateDrop();
        }
        private void RefreshLayout()
        {
            if (grid == null || patternScroll == null) return;
            float width = bag.rect.width;
            if (Mathf.Abs(width - lastGridWidth) > .5f)
            {
                lastGridWidth = width;
                int columns = Mathf.Clamp(Mathf.FloorToInt((width + 8) / 180), 1, 4);
                grid.constraintCount = columns; grid.cellSize = new Vector2(Mathf.Max(1, (width - (columns - 1) * 8) / columns), 88);
                int rows = Mathf.CeilToInt((float)cards.Count / columns);
                gridSize.minHeight = gridSize.preferredHeight = rows * 88 + Mathf.Max(0, rows - 1) * 8;
            }
            float margin = NoteWorkshopPatternGraphic.Margin;
            float timelineWidth = Mathf.Max(patternScroll.viewport.rect.width, margin * 2 + (float)original.LengthBeats * 68);
            if (Mathf.Abs(timelineWidth - lastPatternWidth) <= .5f) return;
            lastPatternWidth = timelineWidth; patternContent.sizeDelta = new Vector2(timelineWidth, 0);
            for (int i = 0; i < beatLabels.Count; i++)
            {
                float x = margin + (timelineWidth - margin * 2) * i / (float)original.LengthBeats;
                RunUI.Pin(beatLabels[i].rectTransform, new Vector2(0, 1), new Vector2(.5f, 1), new Vector2(x, -3), new Vector2(32, 20));
            }
            pattern.SetVerticesDirty();
        }

        internal static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            { var child = parent.GetChild(i); child.gameObject.SetActive(false); Destroy(child.gameObject); }
        }
        internal static TMP_Text Caption(RunUI ui, Transform parent, string text, int size, Color color, float height,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var label = ui.Label(parent, text, size, color, height, alignment);
            label.fontSize = size; label.enableAutoSizing = false; label.color = color; label.alignment = alignment;
            label.richText = false; label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis; label.raycastTarget = false;
            label.GetComponent<LayoutElement>().minWidth = label.GetComponent<LayoutElement>().preferredWidth = 0;
            return label;
        }
        internal static Button Control(RunUI ui, Transform parent, string text, Action action, float height, bool enabled = true, bool primary = false)
        {
            var button = ui.Button(parent, text, action, enabled, primary, height);
            var label = button.GetComponentInChildren<TMP_Text>();
            label.enableAutoSizing = true; label.fontSizeMin = 14; label.fontSizeMax = 18; label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            var size = button.GetComponent<LayoutElement>(); size.minWidth = size.preferredWidth = 0;
            return button;
        }
        internal static void FixedWidth(RectTransform rect, float width)
        {
            var size = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = width; size.flexibleWidth = 0;
        }
    }
}
