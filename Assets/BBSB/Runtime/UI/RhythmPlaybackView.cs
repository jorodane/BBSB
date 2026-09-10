using System;
using System.Collections.Generic;
using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    /// <summary>Build once, update text/meshes in place. No informational element consumes rhythm input.</summary>
    internal sealed class RhythmPlaybackView
    {
        private readonly RhythmRound round;
        private readonly Text beatLabel, feedback, counters, contact;
        private readonly Image[] pulses;
        private readonly RectTransform songProgress;
        private readonly GameObject pauseOverlay;
        private readonly Text soundLabel;
        private readonly BattleArenaView arena;
        private readonly List<MonsterCard> monsters = new List<MonsterCard>();
        private int callCursor, resultCursor;

        public RhythmPlaybackView(RectTransform root, RunUI ui, RhythmRound round,
            Action pause, Action resume, Action sound, Action leave)
        {
            this.round = round; var music = round.Plan.Stage.Music;
            root.name = "Rhythm playback";
            var layout = root.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 12, 12); layout.spacing = 6;
            ui.Background(root, RunUI.Ink, true);
            var header = ui.Row(root, 46);
            ui.Label(header, music.Name + "  /  " + music.Bpm + " BPM", 24, RunUI.Gold, 46);
            var pauseButton = ui.Button(header, "일시정지", pause, height: 46);
            var pauseSize = pauseButton.GetComponent<LayoutElement>();
            pauseSize.minWidth = pauseSize.preferredWidth = 140; pauseSize.flexibleWidth = 0;
            pauseButton.navigation = new Navigation { mode = Navigation.Mode.None };
            songProgress = Progress(root, ui, "Song progress", 5);

            var beats = ui.Row(root, 52, 6); pulses = new Image[music.BeatsPerBar * 2];
            for (int i = 0; i < pulses.Length; i++)
            {
                var cell = ui.Rect("Beat pulse " + i, beats); RunUI.Size(cell, 52, 1);
                pulses[i] = ui.Background(cell, RunUI.Panel);
                var number = ui.Label(cell, i % 2 == 0 ? (i / 2 + 1).ToString() : "&", i % 2 == 0 ? 34 : 27,
                    RunUI.TextColor, 52, TextAnchor.MiddleCenter);
                RunUI.Stretch(number.rectTransform);
            }
            beatLabel = ui.Label(root, "", 21, RunUI.Gold, 30, TextAnchor.MiddleCenter);
            var stage = ui.Rect("Battle arena", root);
            var stageSize = RunUI.Size(stage, 360); stageSize.flexibleHeight = 1;
            arena = stage.gameObject.AddComponent<BattleArenaView>(); arena.Initialize(round, ui.Font);
            var patterns = ui.Row(root, 200, 8); patterns.name = "Live monster patterns";
            foreach (var monster in round.Plan.Monsters) monsters.Add(new MonsterCard(patterns, ui, round, monster));

            feedback = ui.Label(root, "Call을 보고 박자를 준비해", 30, RunUI.TextColor, 44, TextAnchor.MiddleCenter);
            feedback.gameObject.name = "Response feedback";
            counters = ui.Label(root, "", 20, RunUI.Muted, 28, TextAnchor.MiddleCenter);
            contact = ui.Label(root, "", 23, RunUI.Teal, 32, TextAnchor.MiddleCenter);
            ui.Label(root, "Tap 누르기 · Hold 끝까지 유지 · Dive 끝에 떼기\nFlick 튕겨 떼기 · Shake 50% 반미스 / 75% 성공",
                19, RunUI.Muted, 60, TextAnchor.MiddleCenter);

            var overlay = ui.Rect("Pause overlay", root); RunUI.Stretch(overlay);
            overlay.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            ui.Background(overlay, new Color(.04f, .05f, .09f, .96f), true);
            var panel = ui.Card(overlay);
            panel.anchorMin = new Vector2(.08f, .5f); panel.anchorMax = new Vector2(.92f, .5f);
            panel.pivot = new Vector2(.5f, .5f); panel.sizeDelta = Vector2.zero;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ui.Label(panel, "일시정지", 36, RunUI.Gold, 65, TextAnchor.MiddleCenter);
            ui.Label(panel, "박자와 판정이 멈췄어.\n유지 중이었다면 이어할 때 화면을 다시 눌러줘.", 22, RunUI.Muted, 88);
            ui.Button(panel, "이어하기", resume, primary: true);
            soundLabel = ui.Button(panel, "박자음 끄기", sound).GetComponentInChildren<Text>();
            ui.Button(panel, "준비로 돌아가기", leave);
            pauseOverlay = overlay.gameObject; pauseOverlay.SetActive(false);
        }

        public void ShowPause(bool value) { pauseOverlay.SetActive(value); arena.SetPaused(value); }
        public void SetSound(bool enabled) { soundLabel.text = enabled ? "박자음 끄기" : "박자음 켜기"; }

        public void Refresh(double seconds, bool waitingForContact)
        {
            var music = round.Plan.Stage.Music;
            double beat = Math.Min(seconds / round.BeatSeconds, music.BarCount * music.BeatsPerBar - .00001);
            int half = (int)Math.Floor(beat * 2), active = half % pulses.Length;
            float brightness = (float)(1 - (beat * 2 - half));
            for (int i = 0; i < pulses.Length; i++)
                pulses[i].color = i == active ? Color.Lerp(RunUI.Panel, i % 2 == 0 ? RunUI.Gold : RunUI.Teal, .25f + brightness * .55f) : RunUI.Panel;
            beatLabel.text = ((int)beat / music.BeatsPerBar + 1).ToString("00") + " / " + music.BarCount + "마디  ·  " +
                ((int)beat % music.BeatsPerBar + 1) + (half % 2 == 0 ? " 정박" : " 엇박") + "  ·  " +
                Math.Min(seconds, music.DurationSeconds).ToString("0.0") + "초";
            songProgress.anchorMax = new Vector2((float)Math.Min(1, seconds / music.DurationSeconds), 1);

            while (callCursor < round.Calls.Count)
            {
                var signal = round.Calls[callCursor++];
                foreach (var card in monsters) if (card.Plan.InstanceId == signal.MonsterId) card.Call(signal);
            }
            int freshPerfect = 0, freshHalf = 0, freshMiss = 0;
            while (resultCursor < round.Results.Count)
            {
                var result = round.Results[resultCursor++];
                if (result.Grade == RhythmGrade.Perfect) freshPerfect++; else if (result.Grade == RhythmGrade.HalfMiss) freshHalf++; else freshMiss++;
                foreach (var card in monsters) if (card.Plan.InstanceId == result.Note.Attack.MonsterId) card.Result(result);
            }
            if (freshPerfect + freshHalf + freshMiss > 0)
            {
                var labels = new List<string>();
                if (freshPerfect > 0) labels.Add("PERFECT ×" + freshPerfect);
                if (freshHalf > 0) labels.Add("반미스 ×" + freshHalf);
                if (freshMiss > 0) labels.Add("MISS ×" + freshMiss);
                feedback.text = string.Join("  /  ", labels);
                feedback.color = freshMiss > 0 ? RunUI.Red : freshHalf > 0 ? RunUI.Gold : RunUI.Teal;
            }
            counters.text = "정확 " + round.PerfectCount + "  ·  반미스 " + round.HalfMissCount + "  ·  미스 " + round.MissCount +
                "  /  전체 " + round.Notes.Count;
            contact.text = waitingForContact ? "화면을 눌러 연주를 이어가" : round.IsDown ? "누르는 중" : "손을 뗀 상태";
            contact.color = waitingForContact ? RunUI.Gold : round.IsDown ? RunUI.Teal : RunUI.Muted;
            foreach (var card in monsters) card.Refresh(seconds);
            arena.SetPaused(pauseOverlay.activeSelf || waitingForContact);
            arena.Refresh();
        }

        public static string GradeLabel(RhythmGrade grade) => grade == RhythmGrade.Perfect ? "PERFECT" : grade == RhythmGrade.HalfMiss ? "반미스" : "MISS";
        public static Color GradeColor(RhythmGrade grade) => grade == RhythmGrade.Perfect ? RunUI.Teal : grade == RhythmGrade.HalfMiss ? RunUI.Gold : RunUI.Red;
        // Don't round 49.9% up to 50% while its grade is still Miss.
        private static string ShakePercent(ResponseNote note) => Math.Floor(note.ShakeCoverage * 100 + 1e-7).ToString("0") + "%";

        private static RectTransform Progress(Transform parent, RunUI ui, string name, float height)
        {
            var track = ui.Rect(name, parent); RunUI.Size(track, height); ui.Background(track, RunUI.Panel);
            var fill = ui.Rect("Fill", track); RunUI.Stretch(fill); fill.anchorMax = new Vector2(0, 1);
            ui.Background(fill, RunUI.Teal); return fill;
        }

        private sealed class MonsterCard
        {
            public MonsterPlan Plan { get; }
            private readonly RhythmRound round;
            private readonly Text phase, signal, result;
            private readonly MonsterPatternGraphic graphic;
            private ScheduledCall lastCall;

            public MonsterCard(Transform parent, RunUI ui, RhythmRound round, MonsterPlan plan)
            {
                Plan = plan; this.round = round;
                var card = ui.Card(parent, 8); card.name = "Live monster " + plan.InstanceId;
                var sizing = RunUI.Size(card, 200, 1); sizing.minWidth = sizing.preferredWidth = 0;
                card.GetComponent<VerticalLayoutGroup>().spacing = 2;
                ui.Label(card, plan.Monster.Name, 20, RunUI.Gold, 27);
                phase = ui.Label(card, "대기", 17, RunUI.Muted, 23);
                signal = ui.Label(card, "Call 대기", 19, RunUI.Muted, 44);
                signal.gameObject.name = "Call signal " + plan.InstanceId;
                var plot = ui.Rect("Live pattern", card); RunUI.Size(plot, 44);
                graphic = plot.gameObject.AddComponent<MonsterPatternGraphic>(); graphic.Bind(plan.Monster);
                result = ui.Label(card, "대응 결과", 17, RunUI.Muted, 38);
            }

            public void Call(ScheduledCall value) { lastCall = value; }
            public void Result(RhythmResult value)
            {
                result.text = value.Note.Step.Kind + "  ·  " + GradeLabel(value.Grade);
                if (value.Note.Step.Kind == GestureKind.Shake) result.text += "  ·  " + ShakePercent(value.Note);
                if (value.Reason == MissReason.TooEarly) result.text += "  너무 일찍 눌렀어";
                else if (value.Reason == MissReason.MissingFlick) result.text += "  튕기며 떼어줘";
                else if (value.Reason == MissReason.MissingShake) result.text += "  50% 이상 흔들어줘";
                result.color = GradeColor(value.Grade);
            }

            public void Refresh(double seconds)
            {
                PlannedAttack current = null;
                foreach (var attack in Plan.Attacks)
                {
                    double from = Time(attack.CallStartTick), until = Time(attack.ResponseStartTick + Plan.Monster.ResponseTicks + Plan.Monster.RestTicks);
                    if (seconds >= from && seconds < until) current = attack;
                }
                graphic.SetPlayback(round, current, seconds);
                if (current == null)
                { phase.text = "대기"; phase.color = RunUI.Muted; signal.text = "Call 대기"; signal.color = RunUI.Muted; return; }
                double response = Time(current.ResponseStartTick), rest = Time(current.ResponseStartTick + Plan.Monster.ResponseTicks);
                if (seconds < response)
                {
                    phase.text = "CALL"; phase.color = RunUI.Gold; signal.color = RunUI.Gold;
                    signal.text = (lastCall != null && lastCall.AttackId == current.Id ? lastCall.Label : "CALL") +
                        "  ·  " + ((response - seconds) / round.BeatSeconds).ToString("0.0") + "박 뒤 대응";
                }
                else if (seconds <= rest + round.HalfMissWindow)
                {
                    phase.text = "RESPONSE"; phase.color = RunUI.Teal; signal.color = RunUI.Teal;
                    var actions = new List<string>();
                    foreach (var note in round.Notes)
                    {
                        if (note.Attack != current || note.State == ResponseState.Resolved) continue;
                        if (note.State == ResponseState.Holding)
                            actions.Add(note.Step.Kind + (note.Step.Kind == GestureKind.Dive ? " 끝에 떼기" : note.Step.Kind == GestureKind.Shake ?
                                " " + ShakePercent(note) : " 유지"));
                        else if (note.StartSeconds - seconds <= round.BeatSeconds)
                            actions.Add(note.Step.Kind + " " + Math.Max(0, (note.StartSeconds - seconds) / round.BeatSeconds).ToString("0.0") + "박");
                    }
                    signal.text = actions.Count > 0 ? string.Join(" · ", actions) : "대응 완료";
                }
                else { phase.text = "REST"; phase.color = RunUI.Muted; signal.text = "쉬는 박자"; signal.color = RunUI.Muted; }
            }
            private double Time(int tick) => RhythmTime.Seconds(tick, round.Plan.Stage.Music.Bpm);
        }
    }
}
