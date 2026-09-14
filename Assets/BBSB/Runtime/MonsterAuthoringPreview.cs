using System;
using BBSB.Core;
using BBSB.Runtime.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BBSB.Runtime
{
    /// <summary>Isolated authoring scene: no run, rewards, saved health or roster edits.</summary>
    public sealed class MonsterAuthoringPreview : MonoBehaviour
    {
        public MonsterAuthoring monster;
        [Min(0)] public int patternIndex;
        [Range(40, 240)] public float bpm = 120;
        private MonsterPreview preview;
        private BattleArenaView arena;
        private MonsterPatternGraphic pattern;
        private RhythmInputSurface input;
        private BeatMetronome sound;
        private Text status, pauseLabel;
        private double origin, frozen;
        private bool paused, regrab;
        public RhythmRound Round => preview?.Round;
        private void Start()
        {
            try
            {
                var definition = monster.BuildDefinition();
                preview = new MonsterPreview(definition, definition.Patterns[Mathf.Clamp(patternIndex, 0, definition.Patterns.Count - 1)], bpm);
            }
            catch (Exception exception) { Debug.LogError("몬스터 테스트를 시작할 수 없어: " + exception.Message, this); enabled = false; return; }
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).transform.SetParent(transform);
            var font = Resources.Load<Font>("BBSB/Fonts/BBSBUI") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var ui = new RunUI(font); var root = ui.Rect("Monster preview canvas", transform);
            var canvas = root.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            root.gameObject.AddComponent<GraphicRaycaster>(); ui.Background(root, RunUI.Ink, true);
            var stage = ui.Rect("Preview arena", root); RunUI.Stretch(stage);
            arena = stage.gameObject.AddComponent<BattleArenaView>(); arena.Initialize(preview.Round, font, previewAppearance: monster);
            input = root.gameObject.AddComponent<RhythmInputSurface>();
            input.Bind(() => !paused || regrab, Down, Up);
            var header = ui.Rect("Pattern preview", root);
            RunUI.Overlay(header, new Vector2(.16f, 1), new Vector2(.84f, 1), new Vector2(0, -126), new Vector2(0, -24));
            pattern = header.gameObject.AddComponent<MonsterPatternGraphic>(); pattern.Bind(preview.Attack.Pattern);
            status = ui.Label(root, "", 23, RunUI.Gold, 48, TextAnchor.MiddleCenter);
            RunUI.Overlay(status.rectTransform, new Vector2(.16f, 0), new Vector2(.84f, 0), new Vector2(0, 24), new Vector2(0, 72));
            var menu = ui.Rect("Preview controls", root);
            RunUI.Overlay(menu, new Vector2(.84f, 1), Vector2.one, new Vector2(0, -210), new Vector2(-12, -16));
            var stack = ui.Stack(menu); RunUI.Stretch(stack);
            pauseLabel = ui.Button(stack, "일시정지", TogglePause).GetComponentInChildren<Text>();
            ui.Button(stack, "처음부터", Restart);
            ui.Button(stack, "소리 켜기/끄기", () => sound.SetMuted(!sound.Muted));
            sound = new BeatMetronome(transform, preview.Round.Plan); Restart();
        }
        private double Now => paused ? frozen : Math.Max(preview.Round.ElapsedSeconds, Math.Max(0, AudioSettings.dspTime - origin));
        private void Down(Vector2 point)
        {
            if (regrab)
            { preview.Round.Resume(true, point.x, point.y); regrab = paused = false; origin = AudioSettings.dspTime - frozen; sound.Restart(frozen); pauseLabel.text = "일시정지"; }
            else preview.Round.Press(Now, point.x, point.y);
        }
        private void Up(Vector2 point) { if (!paused) preview.Round.Release(Now, point.x, point.y); }
        private void TogglePause()
        {
            if (!paused)
            { frozen = Now; preview.Round.Advance(frozen); regrab = preview.Round.Suspend(); paused = true; input.Cancel(); sound.Stop(); pauseLabel.text = "이어하기"; }
            else if (regrab) pauseLabel.text = "화면을 눌러줘";
            else
            { preview.Round.Resume(false); paused = false; origin = AudioSettings.dspTime - frozen; sound.Restart(frozen); pauseLabel.text = "일시정지"; }
        }
        private void Restart()
        {
            input.Cancel(); preview.Restart(); arena.Repeat(preview.Round);
            paused = regrab = false; frozen = 0; origin = AudioSettings.dspTime + .15;
            sound.Restart(0, true); pauseLabel.text = "일시정지";
        }
        private void LateUpdate()
        {
            if (preview == null || arena == null) return;
            if (!paused)
            {
                double now = Now;
                preview.Round.Move(now, input.Position.x, input.Position.y);
                if (now >= preview.DurationSeconds) { Restart(); now = 0; }
                sound.Schedule(AudioSettings.dspTime, origin, 0, preview.DurationSeconds);
            }
            var round = preview.Round;
            arena.SetPaused(paused); arena.Refresh(); pattern.SetPlayback(round, preview.Attack, round.ElapsedSeconds);
            status.text = monster.displayName + " · " + preview.Attack.Pattern.Name + " · COMBO " + round.Combo +
                (paused ? regrab ? " · 화면을 눌러 이어가기" : " · 일시정지" : "");
        }
        private void OnApplicationFocus(bool focused) { if (!focused && preview != null && !paused && input != null) TogglePause(); }
        private void OnDisable() { sound?.Stop(); }
        private void OnDestroy() { sound?.Dispose(); }
    }
}
