using System.Collections.Generic;
using System.Globalization;
using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    /// <summary>One card per monster, with every authored pattern and its independent occurrence count.</summary>
    public sealed class MonsterPatternView : MonoBehaviour
    {
        public MonsterPlan Plan { get; private set; }

        internal void Bind(MonsterPlan plan, RunUI ui, int number)
        {
            Plan = plan; var monster = plan.Monster;
            gameObject.name = "Monster pattern " + plan.InstanceId;
            ui.Label(transform, number.ToString("00") + "  " + monster.Name, 29, RunUI.Gold, 48);
            ui.Label(transform, "이번 곡 " + plan.Attacks.Count + "회 등장", 20, RunUI.Teal, 32);
            ui.Label(transform, "판정당 기본 피해 " + monster.DamagePerNote + "  ·  미스 100% / 반미스 50% / 퍼펙트 0%", 20, RunUI.Red, 52);
            ui.Label(transform, monster.Description, 21, RunUI.TextColor, 66);
            ui.Label(transform, "메인 입력 " + monster.MainGesture + "  ·  패턴 " + monster.Patterns.Count + "개", 20, RunUI.Teal, 32);
            if (monster.PatternPlanner is BeatShiftPlanner)
                ui.Label(transform, "정박 반복 → 반 박 당기기 → 엇박 반복 → 반 박 당기기 → 정박 반복\n대응하는 Tap과 다음 Call을 함께 이어가. 한 묶음 뒤에는 잠깐 쉬어.",
                    19, RunUI.Teal, 88);
            foreach (var pattern in monster.Patterns)
            {
                int count = 0;
                foreach (var attack in plan.Attacks) if (attack.Pattern == pattern) count++;
                BindPattern(ui.Card(transform, 14), ui, pattern, count);
            }
        }

        private static void BindPattern(Transform root, RunUI ui, MonsterPatternDefinition pattern, int count)
        {
            root.name = "Pattern variant " + pattern.Id;
            ui.Label(root, pattern.Name + "  ·  이번 곡 " + count + "회", 24, RunUI.Gold, 40);
            ui.Label(root, pattern.Description, 20, RunUI.TextColor, 64);
            ui.Label(root, "CALL  ·  " + Beat(pattern.Pattern.CueLeadTicks) + "박 전조", 20, RunUI.Gold, 32);
            foreach (var signal in pattern.Call)
                ui.Label(root, Beat(signal.OffsetTick + RhythmTime.TicksPerBeat) + "박 · " + signal.Label + "\n" +
                    SoundLabel(signal.Sound) + " / " + MotionLabel(signal.Motion), 19, BattleArenaView.CueColor(signal.Motion), 56);
            if (pattern.SilentWaitTicks > 0)
                ui.Label(root, "기다리기 · 마지막 Call에서 " + Beat(pattern.SilentWaitTicks) + "박 뒤에 대응해.\n중간 Call 없이 처음 들은 박자를 기억해.",
                    19, RunUI.Muted, 60);

            var kinds = new List<GestureKind>();
            foreach (var step in pattern.Pattern.Steps) if (!kinds.Contains(step.Kind)) kinds.Add(step.Kind);
            kinds.Sort();
            float height = (kinds.Count + 2) * 34;
            var plot = ui.Row(root, height, 6);
            plot.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            var names = ui.Stack(plot, "Pattern lanes", 0, 0);
            var width = names.gameObject.AddComponent<LayoutElement>();
            width.minWidth = width.preferredWidth = 72; width.flexibleWidth = 0;
            ui.Label(names, "CALL", 17, RunUI.Gold, 34);
            foreach (var kind in kinds) ui.Label(names, kind.ToString(), 17, RunUI.Teal, 34);
            ui.Label(names, "REST", 17, RunUI.Muted, 34);
            var graph = ui.Rect("Call and response", plot); RunUI.Size(graph, height, 1);
            graph.gameObject.AddComponent<MonsterPatternGraphic>().Bind(pattern);

            ui.Label(root, "RESPONSE  ·  " + Beat(pattern.ResponseTicks) + "박", 20, RunUI.Teal, 32);
            // Times below are relative to this pattern, never absolute locations in the full song.
            foreach (var step in pattern.Pattern.Steps)
            {
                string label = Beat(pattern.Pattern.CueLeadTicks + step.OffsetTick + RhythmTime.TicksPerBeat) + "박  " + step.Kind;
                if (step.DurationTicks > 0) label += "  ·  " + Beat(step.DurationTicks) + (step.Kind == GestureKind.Shake ? "박 안에 왕복" : "박 유지");
                if (step.Touch.End == TouchTransition.Release) label += "  ·  " + Beat(pattern.Pattern.CueLeadTicks + step.OffsetTick + step.DurationTicks + RhythmTime.TicksPerBeat) + "박에 떼기";
                ui.Label(root, label, 19, null, 32);
            }
            ui.Label(root, "첫 Call을 1박으로 표시해. 대응 이후 " + Beat(pattern.RestTicks) + "박 휴식.", 18, RunUI.Muted, 48);
            if (kinds.Contains(GestureKind.Shake))
                ui.Label(root, "Shake  ·  한 번 왕복하면 성공. 돌아오기 전까지는 반미스.", 19, RunUI.Teal, 52);
        }

        private static string Beat(int tick) => (tick / (double)RhythmTime.TicksPerBeat).ToString("0.##", CultureInfo.InvariantCulture);
        private static string SoundLabel(CallSound sound)
        {
            switch (sound)
            {
                case CallSound.Wood: return "나무 소리";
                case CallSound.Drum: return "낮은 북소리";
                case CallSound.Bell: return "맑은 종소리";
                case CallSound.RisingChime: return "올라가는 울림";
                case CallSound.FallingChime: return "내려가는 울림";
                case CallSound.RisingWhistle: return "올라가는 휘파람";
                case CallSound.FallingWhistle: return "내려가는 휘파람";
                case CallSound.Rattle: return "자르르 떨리는 소리";
                default: return "휙 바람 소리";
            }
        }
        private static string MotionLabel(CallMotion motion)
        {
            switch (motion)
            {
                case CallMotion.Step: return "제자리걸음";
                case CallMotion.Stomp: return "발 구르기";
                case CallMotion.TailSweep: return "꼬리 휘두르기";
                case CallMotion.Rise: return "솟구치기";
                case CallMotion.Dip: return "몸 낮추기";
                case CallMotion.Sway: return "좌우 비틀기";
                case CallMotion.Flash: return "빛 번쩍";
                default: return "점프";
            }
        }
    }
}
