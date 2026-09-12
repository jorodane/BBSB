using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    /// <summary>Shared title / run / pause-menu encyclopedia. The owner handles Escape before its own shortcuts.</summary>
    public sealed class MonsterCodexView : MonoBehaviour
    {
        public const string IconRoot = "BBSB/Codex/";
        private RunUI ui;
        private Action closed;
        private RectTransform page, gridPage, detail, headerIcon;
        private Text headerName, state, instructions, beat, playLabel, soundLabel, tempoLabel, laneNames;
        private Button back;
        private GridLayoutGroup grid;
        private LayoutElement gridSize;
        private MonsterDefinition selected;
        private MonsterPatternDefinition pattern;
        private MonsterPreview preview;
        private MonsterCodexStage stage;
        private MonsterPatternGraphic timeline;
        private readonly MonsterAttackSprites sprites = new MonsterAttackSprites();
        private readonly List<(Image Image, MonsterPatternDefinition Pattern)> choices = new List<(Image, MonsterPatternDefinition)>();
        private BeatMetronome audio;
        private GameObject audioRoot;
        private double origin, elapsed, frozen;
        private int bpm = 120;
        private bool playing = true, sound = true, closing;

        internal static MonsterCodexView Open(RectTransform parent, RunUI ui, Action closed)
        {
            var root = ui.Rect("Monster encyclopedia", parent); RunUI.Stretch(root);
            var view = root.gameObject.AddComponent<MonsterCodexView>();
            view.ui = ui; view.closed = closed; view.Build(); return view;
        }

        private void Build()
        {
            var root = (RectTransform)transform;
            ui.Background(root, RunUI.Ink, true);
            var column = ui.Stack(root, "Codex pages", 20, 14); RunUI.Stretch(column);
            var header = ui.Row(column, 72, 12);
            back = ui.Button(header, "목록", Back, height: 64); FixedWidth(back.transform, 90);
            headerIcon = ui.Rect("Monster header icon", header); RunUI.Size(headerIcon, 64); FixedWidth(headerIcon, 64);
            headerName = ui.Label(header, "몬스터 도감", 32, RunUI.Gold, 64);
            var close = ui.Button(header, "닫기", Close, height: 64); FixedWidth(close.transform, 90);
            page = ui.Rect("Codex page", column);
            var expand = RunUI.Size(page, 200, 1); expand.flexibleHeight = 1;
            gridPage = ui.Stack(page, "Monster grid page"); RunUI.Stretch(gridPage);
            var content = ui.Scroll(gridPage);
            var cells = ui.Rect("Monster grid", content);
            gridSize = RunUI.Size(cells, 500, 1);
            grid = cells.gameObject.AddComponent<GridLayoutGroup>();
            grid.spacing = new Vector2(14, 14); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            foreach (var monster in MonsterCatalog.All)
            {
                var item = ui.Rect("Codex " + monster.Id, cells);
                var background = ui.Background(item, RunUI.Panel, true);
                var button = item.gameObject.AddComponent<Button>(); button.targetGraphic = background;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.onClick.AddListener(() => SelectMonster(monster));
                var icon = ui.Rect("Portrait", item);
                RunUI.Overlay(icon, new Vector2(0, .25f), Vector2.one, new Vector2(14, 6), new Vector2(-14, -12));
                SetIcon(icon, Portrait(monster), monster.Name);
                var label = ui.Label(item, monster.Name, 23, null, 55, TextAnchor.MiddleCenter);
                RunUI.Overlay(label.rectTransform, Vector2.zero, new Vector2(1, .25f), new Vector2(8, 4), new Vector2(-8, 0));
                Fit(label, 17);
            }
            ShowGrid();
        }

        public void Back() { if (selected != null) ShowGrid(); else Close(); }
        public void Close()
        {
            if (closing) return;
            closing = true; StopAudio(); gameObject.SetActive(false); closed?.Invoke(); Destroy(gameObject);
        }

        private void ShowGrid()
        {
            StopAudio(); preview = null; selected = null; pattern = null;
            if (detail != null) { detail.gameObject.SetActive(false); Destroy(detail.gameObject); }
            detail = null; stage = null; timeline = null; choices.Clear();
            gridPage.gameObject.SetActive(true); back.gameObject.SetActive(false);
            headerIcon.gameObject.SetActive(false); headerName.text = "몬스터 도감";
        }

        private void SelectMonster(MonsterDefinition monster)
        {
            selected = monster; gridPage.gameObject.SetActive(false);
            back.gameObject.SetActive(true); headerIcon.gameObject.SetActive(true);
            SetIcon(headerIcon, Portrait(monster), monster.Name); headerName.text = monster.Name;
            detail = ui.Rect("Monster details", page); RunUI.Stretch(detail);
            var left = ui.Stack(detail, "Character and timing", 14, 8); ui.Background(left, RunUI.Panel);
            RunUI.Overlay(left, Vector2.zero, new Vector2(.67f, 1), Vector2.zero, new Vector2(-10, 0));
            var visual = ui.Rect("Monster demonstration", left);
            var grow = RunUI.Size(visual, 160, 1); grow.flexibleHeight = 1;
            visual.gameObject.AddComponent<RectMask2D>();
            stage = visual.gameObject.AddComponent<MonsterCodexStage>(); stage.Bind(ui, monster, sprites);
            state = ui.Label(left, "", 25, RunUI.Teal, 36, TextAnchor.MiddleCenter); Fit(state, 19);
            beat = ui.Label(left, "", 18, RunUI.Muted, 26, TextAnchor.MiddleCenter);
            var plot = ui.Row(left, 82, 8);
            laneNames = ui.Label(plot, "CALL\n입력\n휴식", 17, RunUI.Muted, 82); FixedWidth(laneNames.transform, 54);
            var graph = ui.Rect("Pattern timeline", plot); RunUI.Size(graph, 82, 1);
            timeline = graph.gameObject.AddComponent<MonsterPatternGraphic>();
            instructions = ui.Label(left, "", 19, RunUI.TextColor, 96); Fit(instructions, 16);
            var controls = ui.Row(left, 48, 8);
            playLabel = ui.Button(controls, "멈춤", TogglePlayback, height: 48).GetComponentInChildren<Text>(); Fit(playLabel, 16);
            Fit(ui.Button(controls, "다시 보기", Restart, height: 48).GetComponentInChildren<Text>(), 16);
            soundLabel = ui.Button(controls, "소리 켜짐", ToggleSound, height: 48).GetComponentInChildren<Text>(); Fit(soundLabel, 16);

            var right = ui.Stack(detail, "Idle and patterns", 0, 10);
            RunUI.Overlay(right, new Vector2(.67f, 0), Vector2.one, new Vector2(10, 0), Vector2.zero);
            ui.Label(right, "모습 · 패턴", 25, RunUI.Gold, 42);
            var list = ui.Scroll(right);
            Choice(list, null);
            foreach (var variant in monster.Patterns) Choice(list, variant);
            var description = ui.Label(list, monster.Description, 21, RunUI.Muted, 116); Fit(description, 18);
            if (monster.PatternPlanner is BeatShiftPlanner)
                ui.Label(list, "전투에서는 정박과 엇박이 이어져. 여기서는 선택한 한 패턴을 첫 Call부터 보여줘.", 18, RunUI.Muted, 100);
            ui.Label(right, "첫 Call = 1박\n금색: Call · 초록: 반응 · 빨강: 떼기", 17, RunUI.Muted, 56);
            var tempo = ui.Row(right, 48, 6);
            var less = ui.Button(tempo, "−", () => SetTempo(-20), height: 48); FixedWidth(less.transform, 48);
            tempoLabel = ui.Label(tempo, "", 20, RunUI.Gold, 48, TextAnchor.MiddleCenter);
            var more = ui.Button(tempo, "+", () => SetTempo(20), height: 48); FixedWidth(more.transform, 48);
            SelectPattern(null);
        }

        private void Choice(RectTransform list, MonsterPatternDefinition variant)
        {
            string name = variant == null ? "평상시" : variant.Name;
            var button = ui.Button(list, name, () => SelectPattern(variant), height: 100);
            button.gameObject.name = variant == null ? "Codex idle" : "Codex pattern " + variant.Id;
            var label = button.GetComponentInChildren<Text>(); label.alignment = TextAnchor.MiddleLeft; Fit(label, 18);
            RunUI.Stretch(label.rectTransform); label.rectTransform.offsetMin = new Vector2(96, 8); label.rectTransform.offsetMax = new Vector2(-8, -8);
            var icon = ui.Rect("Pattern icon", button.transform);
            RunUI.Pin(icon, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(10, 0), new Vector2(76, 76));
            var sprite = variant == null ? Portrait(selected) : sprites.Get(IconRoot + "Patterns", variant.Id) ??
                sprites.Get(MonsterAttackDefinition.ResourceRoot + selected.Id + "/" + variant.Id + "/step-0", "contact") ?? Portrait(selected);
            SetIcon(icon, sprite, variant == null ? "평" : variant.Pattern.Steps[0].Kind.ToString());
            choices.Add((button.GetComponent<Image>(), variant));
        }

        private void SelectPattern(MonsterPatternDefinition value)
        {
            pattern = value; StopAudio();
            preview = value == null ? null : new MonsterPreview(selected, value, bpm);
            stage.Select(preview); elapsed = frozen = 0; playing = true;
            foreach (var choice in choices) choice.Image.color = choice.Pattern == value ? RunUI.Hex("40546A") : RunUI.Hex("2B3850");
            timeline.transform.parent.gameObject.SetActive(value != null);
            if (value != null)
            {
                timeline.Bind(value);
                var kinds = new List<GestureKind>();
                foreach (var step in value.Pattern.Steps) if (!kinds.Contains(step.Kind)) kinds.Add(step.Kind);
                kinds.Sort(); laneNames.text = "CALL\n" + string.Join("\n", kinds) + "\n휴식";
            }
            instructions.text = value == null ? "패턴을 고르면 공격 모습과 반응할 박자를 반복해서 볼 수 있어." : Describe(preview);
            tempoLabel.text = bpm + " BPM";
            Restart();
        }

        private void Restart()
        {
            elapsed = frozen = 0; playing = true; origin = AudioSettings.dspTime + .12;
            if (preview != null && audio == null)
            {
                audioRoot = new GameObject("Codex preview audio"); audioRoot.transform.SetParent(transform, false);
                audio = new BeatMetronome(audioRoot.transform, preview.Round.Plan); audio.SetMuted(!sound);
            }
            audio?.Restart(0, true); Refresh();
        }

        private void TogglePlayback()
        {
            if (playing) { frozen = elapsed; audio?.Stop(); playing = false; }
            else { playing = true; origin = AudioSettings.dspTime + .12; audio?.Restart(frozen); }
            Refresh();
        }
        private void ToggleSound() { sound = !sound; audio?.SetMuted(!sound); Refresh(); }
        private void SetTempo(int delta) { bpm = Mathf.Clamp(bpm + delta, 60, 200); SelectPattern(pattern); }

        private void Update()
        {
            if (selected == null) return;
            if (playing)
            {
                elapsed = frozen + Math.Max(0, AudioSettings.dspTime - origin);
                if (preview != null && elapsed >= preview.DurationSeconds) Restart();
                audio?.Schedule(AudioSettings.dspTime, origin, frozen, preview.DurationSeconds);
            }
            Refresh();
        }

        private void LateUpdate()
        {
            if (!gridPage.gameObject.activeSelf) return;
            float width = ((RectTransform)grid.transform).rect.width;
            int columns = Mathf.Max(1, Mathf.FloorToInt((width + 14) / 204));
            float cell = Mathf.Max(1, (width - (columns - 1) * 14) / columns);
            float height = Mathf.Min(220, cell + 24);
            grid.constraintCount = columns; grid.cellSize = new Vector2(cell, height);
            int rows = (MonsterCatalog.All.Count + columns - 1) / columns;
            gridSize.minHeight = gridSize.preferredHeight = rows * (height + 14) - 14;
        }

        private void Refresh()
        {
            if (stage == null) return;
            var cue = preview == null ? default : preview.CueAt(elapsed);
            bool response = cue.Kind == PreviewCueKind.Respond || cue.Kind == PreviewCueKind.Sustain || cue.Kind == PreviewCueKind.Release;
            stage.Refresh(elapsed, response);
            state.text = preview == null ? "평상시" : CueText(cue);
            state.color = response ? RunUI.Teal : cue.Kind == PreviewCueKind.Call ? RunUI.Gold : RunUI.Muted;
            beat.text = preview == null ? "" : (elapsed < preview.CallSeconds ? "한 박 준비" :
                "첫 Call부터 " + Format(preview.DisplayBeat(elapsed)) + "박") + "  ·  " + bpm + " BPM";
            if (preview != null) timeline.SetPlayback(preview.Round, preview.Attack, elapsed);
            playLabel.text = playing ? "멈춤" : "재생"; soundLabel.text = sound ? "소리 켜짐" : "소리 꺼짐";
        }

        private Sprite Portrait(MonsterDefinition monster) => sprites.Get(IconRoot + "Monsters", monster.Id) ??
            sprites.Get(MonsterAttackDefinition.ResourceRoot + monster.Id, "idle") ?? Resources.Load<Sprite>("BBSB/BattleArt/" + monster.ArtId);

        private void SetIcon(RectTransform root, Sprite sprite, string fallback)
        {
            var icon = root.GetComponent<Image>() ?? root.gameObject.AddComponent<Image>();
            icon.sprite = sprite; icon.preserveAspect = true; icon.raycastTarget = false; icon.enabled = sprite != null;
            var label = root.GetComponentInChildren<Text>();
            if (label == null) { label = ui.Label(root, "", 22, RunUI.Gold, 60, TextAnchor.MiddleCenter); RunUI.Stretch(label.rectTransform); }
            label.text = sprite == null ? fallback : "";
        }

        private static string Describe(MonsterPreview demo)
        {
            var text = new StringBuilder();
            foreach (var note in demo.Round.Notes)
            {
                if (text.Length > 0) text.Append('\n');
                text.Append(Format(demo.DisplayBeat(note.StartSeconds))).Append("박 · ").Append(ActionText(note.Step.Kind));
                if (note.Step.DurationTicks > 0)
                    text.Append(" → ").Append(Format(demo.DisplayBeat(note.EndSeconds))).Append("박 ")
                        .Append(note.Step.Kind == GestureKind.Dive ? "떼기" : note.Step.Kind == GestureKind.Shake ? "전까지 왕복" : "까지 유지");
            }
            if (demo.Attack.Pattern.SilentWaitTicks > 0) text.Append("\nCall 뒤 ").Append(Format(demo.Attack.Pattern.SilentWaitTicks / 4.0)).Append("박 기다리기");
            return text.ToString();
        }

        private static string CueText(PreviewCue cue)
        {
            switch (cue.Kind)
            {
                case PreviewCueKind.Ready: return "준비";
                case PreviewCueKind.Call: return "CALL · " + cue.Call.Label;
                case PreviewCueKind.Wait: return Format(cue.BeatsUntilResponse) + "박 뒤 · " + ActionText(cue.Note.Step.Kind);
                case PreviewCueKind.Respond: return "지금! " + ActionText(cue.Note.Step.Kind);
                case PreviewCueKind.Sustain: return cue.Note.Step.Kind == GestureKind.Shake ? "누른 채 한 번 왕복" :
                    cue.Note.Step.Kind == GestureKind.Dive ? "숙인 채 유지 → 끝 박에 떼기" : "누른 채 유지";
                case PreviewCueKind.Release: return "지금 손 떼기!";
                default: return "회복 · 잠시 뒤 반복";
            }
        }
        private static string ActionText(GestureKind kind) => kind == GestureKind.Tap ? "Tap · 누르기" :
            kind == GestureKind.Hold ? "Hold · 누르고 유지" : kind == GestureKind.Dive ? "Dive · 누르고 숙이기" :
            kind == GestureKind.Flick ? "Flick · 미리 누르고 튕겨 떼기" : "Shake · 누른 채 왕복";
        private static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
        private static void FixedWidth(Transform target, float width)
        { var size = target.GetComponent<LayoutElement>() ?? target.gameObject.AddComponent<LayoutElement>(); size.minWidth = size.preferredWidth = width; size.flexibleWidth = 0; }
        private static void Fit(Text label, int minimum) { label.resizeTextForBestFit = true; label.resizeTextMinSize = minimum; label.resizeTextMaxSize = label.fontSize; }

        private void StopAudio()
        { audio?.Dispose(); audio = null; if (audioRoot != null) { audioRoot.SetActive(false); Destroy(audioRoot); } audioRoot = null; }
        private void OnApplicationFocus(bool focused) { if (!focused && playing && selected != null) TogglePlayback(); }
        private void OnApplicationPause(bool paused) { if (paused && playing && selected != null) TogglePlayback(); }
        private void OnDisable() { audio?.Stop(); }
        private void OnDestroy() { StopAudio(); }
    }
}
