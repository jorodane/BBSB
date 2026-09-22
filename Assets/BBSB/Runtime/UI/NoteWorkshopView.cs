using System;
using System.Collections.Generic;
using BBSB.Core;
using UnityEngine;

namespace BBSB.Runtime.UI
{
    internal sealed class NoteWorkshopSelection
    {
        public int Weapon, Note, Offset;
        public WeaponBeatSide Side;
        public string Notice = "";
    }

    internal static class NoteWorkshopView
    {
        internal static void Draw(RectTransform root, RunUI ui, RunSession session, NoteWorkshopSelection selection, Action refresh)
        {
            ui.Label(root, "무기 → 원본 노트 → 프레임 또는 박자 주입을 선택해.\n해제하면 원본으로 돌아가고, 부품은 가방에 남아.", 22, RunUI.Muted, 72);
            if (session.OwnedWeapons.Count == 0) return;
            selection.Weapon = Math.Max(0, Math.Min(selection.Weapon, session.OwnedWeapons.Count - 1));
            var weapon = session.OwnedWeapons[selection.Weapon];
            var chooser = ui.Row(root, 62);
            ui.Button(chooser, "이전 무기", () => { selection.Weapon--; selection.Note = selection.Offset = 0; refresh(); }, selection.Weapon > 0, height: 62);
            ui.Label(chooser, weapon.DisplayName + " +" + weapon.Level, 24, RunUI.Gold, 62);
            ui.Button(chooser, "다음 무기", () => { selection.Weapon++; selection.Note = selection.Offset = 0; refresh(); }, selection.Weapon + 1 < session.OwnedWeapons.Count, height: 62);
            var source = WeaponPhraseAuthoring.LoadSetsFor(new[] { weapon })[0];
            selection.Offset = Math.Min(selection.Offset, weapon.RequiredLanes - 1);
            var variants = ui.Row(root, 50);
            ui.Button(variants, "빛 박자 보기", () => { selection.Side = WeaponBeatSide.Light; refresh(); }, height: 50);
            ui.Button(variants, "어둠 박자 보기", () => { selection.Side = WeaponBeatSide.Dark; refresh(); }, height: 50);
            for (int i = 0; i < weapon.RequiredLanes; i++)
            {
                int offset = i;
                ui.Button(variants, "시작 " + (i + 1), () => { selection.Offset = offset; refresh(); }, height: 50);
            }
            var original = source.For(selection.Offset, selection.Side);
            selection.Note = Math.Max(0, Math.Min(selection.Note, original.Notes.Count - 1));
            var composed = WeaponNoteAssembly.Apply(weapon, source).For(selection.Offset, selection.Side);
            ui.Label(root, "조립 결과 · " + (selection.Side == WeaponBeatSide.Light ? "정박" : "엇박") + " · " + composed.LengthBeats + "박", 24, RunUI.Teal, 40);
            var sequence = new List<string>();
            foreach (var note in composed.Notes)
                sequence.Add((note.IsInjected ? "+" : "") + note.Beat.ToString("0.##") + (note.IsHold ? "~" + (note.Beat + note.HoldBeats).ToString("0.##") + " Hold" : " Tap") +
                    (weapon.RequiredLanes > 1 ? "(" + (note.LaneOffset + 1) + ")" : "") + (note.Target == WeaponAttackTarget.Rear ? " 후열" : ""));
            ui.Label(root, string.Join("  →  ", sequence), 21, RunUI.TextColor, Math.Max(72, (sequence.Count + 4) / 5 * 34));
            ui.Label(root, "원본 노트 선택 · 같은 번호의 모든 시작 방향에 적용 · 혼돈 전환 구간은 유지", 20, RunUI.Muted, 44);
            RectTransform row = null;
            for (int i = 0; i < original.Notes.Count; i++)
            {
                if (i % 4 == 0) row = ui.Row(root, 58);
                int noteIndex = i; var note = original.Notes[i];
                ui.Button(row, (i + 1) + "번 · " + note.Beat.ToString("0.##") + "박 " + (note.IsHold ? "Hold" : "Tap"),
                    () => { selection.Note = noteIndex; selection.Notice = ""; refresh(); }, primary: selection.Note == i, height: 58);
            }
            if (!string.IsNullOrEmpty(selection.Notice)) ui.Label(root, selection.Notice, 22, RunUI.Gold, 62);
            ui.Label(root, "보유 프레임 · 박자 주입  " + session.NoteParts.Count, 25, RunUI.Gold, 44);
            if (session.NoteParts.Count == 0)
                ui.Label(root, "일반·엘리트 보상과 상점에서 부품을 얻을 수 있어.", 22, RunUI.Muted, 70);
            for (int i = 0; i < session.NoteParts.Count; i++)
            {
                int partIndex = i; var part = session.NoteParts[i]; var owner = session.NotePartOwner(part.InstanceId);
                var card = ui.Card(root, 12);
                ui.Label(card, part.Definition.Name + (owner == null ? " · 가방" : " · " + owner.DisplayName), 23, RunUI.Teal, 42);
                ui.Label(card, part.Definition.Description, 20, RunUI.Muted, 66);
                bool valid = session.TryAttachNotePart(i, selection.Weapon, selection.Note, out string reason, source, true);
                if (!valid) ui.Label(card, reason, 19, RunUI.Gold, 42);
                var actions = ui.Row(card, 56);
                ui.Button(actions, (selection.Note + 1) + "번 노트에 장착", () =>
                {
                    if (session.TryAttachNotePart(partIndex, selection.Weapon, selection.Note, out string error, source))
                        selection.Notice = part.Definition.Name + " 장착 완료. 교체된 부품은 가방에 있어.";
                    else selection.Notice = error;
                    refresh();
                }, valid, true, 56);
                if (owner != null) ui.Button(actions, "부품 해제", () =>
                { if (session.RemoveNotePart(partIndex)) selection.Notice = "부품을 해제했어. 원본 노트는 유지돼."; refresh(); }, session.CanEditEquipment, height: 56);
            }
        }
    }
}
