using System;
using System.Collections.Generic;
using BBSB.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    public sealed class NoteWorkshopDemoView : MonoBehaviour
    {
        private sealed class NoteArt { internal RectTransform Root, Tail, Head; internal Image Body, Face; }
        private readonly List<NoteArt> notes = new List<NoteArt>();
        private readonly List<Image> keys = new List<Image>();
        private readonly List<TMP_Text> keyTexts = new List<TMP_Text>();
        private readonly List<string> keyNames = new List<string>();
        private RunUI ui;
        private RectTransform field, keyRow;
        private TMP_Text status, tempo;
        private Button pause;
        private BattleVisualTheme theme;
        private WeaponBeatSide side;
        private int bpm = 120;
        private bool paused;
        private double beat = -3;
        internal NoteWorkshopPlayback Playback { get; private set; }
        internal double Beat => beat;
        internal double PatternBeat => Playback == null ? -1 : Playback.PatternBeat(beat);
        internal bool Paused => paused;

        internal void Build(RunUI runUI)
        {
            ui = runUI; theme = Resources.Load<BattleVisualTheme>(BattleVisualTheme.ResourcePath);
            NoteWorkshopView.Caption(ui, transform, "자동 연주", 24, RunUI.Gold, 32);
            NoteWorkshopView.Caption(ui, transform, "선택 패턴 · 조건부 성공 예시", 17, RunUI.Muted, 26);
            var controls = ui.Row(transform, 36, 6);
            pause = NoteWorkshopView.Control(ui, controls, "멈춤", TogglePause, 36);
            NoteWorkshopView.Control(ui, controls, "처음부터", Restart, 36);
            var speed = ui.Row(transform, 32, 6);
            var slow = NoteWorkshopView.Control(ui, speed, "−", () => SetTempo(bpm - 20), 32);
            NoteWorkshopView.FixedWidth((RectTransform)slow.transform, 36);
            tempo = NoteWorkshopView.Caption(ui, speed, "120 BPM", 17, RunUI.Muted, 32, TextAlignmentOptions.Center);
            var fast = NoteWorkshopView.Control(ui, speed, "+", () => SetTempo(bpm + 20), 32);
            NoteWorkshopView.FixedWidth((RectTransform)fast.transform, 36);
            keyRow = ui.Row(transform, 60, 6); keyRow.name = "Automatic keys";
            field = ui.Rect("Upward note example", transform);
            var grow = RunUI.Size(field, 90, 1); grow.flexibleHeight = 1;
            ui.Background(field, RunUI.Ink); field.gameObject.AddComponent<RectMask2D>();
            status = NoteWorkshopView.Caption(ui, transform, "", 19, RunUI.Teal, 30, TextAlignmentOptions.Center);
        }

        internal void Bind(WeaponPhrase phrase, int width, int offset, WeaponBeatSide beatSide, string[] labels)
        {
            Playback = new NoteWorkshopPlayback(phrase, width, offset); side = beatSide;
            NoteWorkshopView.Clear(keyRow); NoteWorkshopView.Clear(field);
            notes.Clear(); keys.Clear(); keyTexts.Clear(); keyNames.Clear();
            for (int lane = 0; lane < width; lane++)
            {
                var key = ui.Rect("Auto key " + lane, keyRow); RunUI.Size(key, 60, 1);
                keys.Add(ui.Background(key, RunUI.Ink));
                var text = NoteWorkshopView.Caption(ui, key, "", 18, RunUI.TextColor, 60, TextAlignmentOptions.Center);
                RunUI.Stretch(text.rectTransform, 2); keyTexts.Add(text); keyNames.Add(labels[lane]);
                var divider = ui.Rect("Lane", field);
                RunUI.Overlay(divider, new Vector2((float)lane / width, 0), new Vector2((float)lane / width, 1), Vector2.zero, new Vector2(1, 0));
                ui.Background(divider, RunUI.Hex("303849"));
            }
            var line = ui.Rect("Judgment line", field);
            RunUI.Overlay(line, new Vector2(0, .92f), new Vector2(1, .92f), Vector2.zero, new Vector2(0, 2));
            ui.Background(line, RunUI.Gold);
            Restart();
        }

        internal void TogglePause()
        {
            paused = !paused;
            pause.GetComponentInChildren<TMP_Text>().text = paused ? "재생" : "멈춤";
        }
        internal void Restart() { beat = -3; Render(); }
        private void SetTempo(int value) { bpm = Mathf.Clamp(value, 40, 240); tempo.text = bpm + " BPM"; }
        private void Update()
        {
            if (Playback == null) return;
            if (!paused) beat += Time.unscaledDeltaTime * bpm / 60d;
            Render();
        }

        private void Render()
        {
            if (Playback == null || field == null) return;
            for (int lane = 0; lane < keys.Count; lane++)
            {
                var input = Playback.InputAt(beat, lane);
                keys[lane].color = input == NoteDemoInput.Press ? RunUI.Gold : input == NoteDemoInput.Hold || input == NoteDemoInput.Invoke || input == NoteDemoInput.Call ? RunUI.Teal :
                    input == NoteDemoInput.Release ? RunUI.Hex("49335B") : RunUI.Ink;
                keyTexts[lane].color = input == NoteDemoInput.Press || input == NoteDemoInput.Hold ? RunUI.Ink : RunUI.TextColor;
                keyTexts[lane].text = keyNames[lane] + "\n" + (input == NoteDemoInput.Invoke ? "호출" : input == NoteDemoInput.Call ? "콜" : input == NoteDemoInput.Press ? "누름" : input == NoteDemoInput.Hold ? "유지" : input == NoteDemoInput.Release ? "떼기" : "·");
            }
            status.text = beat < 0 ? "자동 입력 준비 · " + Math.Ceiling(-beat) :
                Playback.PreparationRemaining(beat) > 0 ? "호출 → 첫 노트까지 " + Playback.PreparationRemaining(beat).ToString("0.0") + "박" :
                Playback.PatternBeat(beat) < 0 ? "쉬고 다시 시작" : (Playback.PatternBeat(beat) + 1).ToString("0.0") + "박";
            int used = 0;
            Playback.VisitNotes(beat, beat + 3, (note, start, end, lane) =>
            {
                while (notes.Count <= used) notes.Add(CreateNote());
                var art = notes[used++]; art.Root.gameObject.SetActive(true);
                float unit = field.rect.width / Playback.Width;
                float x = (lane + .5f) * unit;
                // Screen-space distance is linear all the way to the judgment line.
                float head = field.rect.height * (.92f - .89f * (float)Math.Max(0, start - beat) / 3);
                float tail = field.rect.height * (.92f - .89f * (float)Math.Min(3, end - beat) / 3);
                Color tint = note.IsCall ? RunUI.Teal : BattleVisualTheme.NoteColor(note.Beat + (side == WeaponBeatSide.Dark ? .5 : 0));
                bool connected = Playback.ConnectsFromPrevious(note, start);
                RunUI.Pin(art.Tail, Vector2.zero, new Vector2(.5f, 0), new Vector2(x, tail), new Vector2(Mathf.Min(unit * .5f, 80), Mathf.Max(0, head - tail)));
                art.Tail.gameObject.SetActive(note.IsHold && end > beat);
                art.Body.color = new Color(tint.r, tint.g, tint.b, .4f);
                RunUI.Pin(art.Head, Vector2.zero, new Vector2(.5f, .5f), new Vector2(x, head),
                    new Vector2(Mathf.Min(unit * (connected ? .5f : .78f), connected ? 80 : 112), connected ? 3 : 28));
                art.Face.sprite = theme == null || connected || note.IsCall ? null : theme.NoteSprite(note.Beat + (side == WeaponBeatSide.Dark ? .5 : 0));
                art.Face.color = art.Face.sprite == null ? tint : Color.white;
                art.Head.gameObject.SetActive(!connected || start > beat);
            });
            for (int i = used; i < notes.Count; i++) notes[i].Root.gameObject.SetActive(false);
        }
        private NoteArt CreateNote()
        {
            var result = new NoteArt { Root = ui.Rect("Demo note", field) }; RunUI.Stretch(result.Root);
            result.Tail = ui.Rect("Hold", result.Root); result.Body = ui.Background(result.Tail, RunUI.Gold);
            result.Head = ui.Rect("Head", result.Root); result.Face = ui.Background(result.Head, Color.white);
            result.Face.preserveAspect = true;
            return result;
        }
    }
}
