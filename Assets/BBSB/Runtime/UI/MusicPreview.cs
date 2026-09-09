using System;
using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    /// <summary>Development inspector only. The preparation cards above show the placed monsters.</summary>
    internal sealed class MusicPreview
    {
        private static readonly RhythmPattern TapTriplet = new RhythmPattern("preview-three-taps", 4, new[]
        {
            new PatternStep(GestureKind.Tap, 0), new PatternStep(GestureKind.Tap, 4), new PatternStep(GestureKind.Tap, 8)
        });
        private MusicStage stage;
        private int catalogIndex;
        private int bar;
        private bool expanded;

        public void Reset(MusicStage encounter)
        {
            stage = encounter; bar = Math.Min(1, encounter.Music.BarCount - 1); expanded = false; catalogIndex = 0;
            for (int i = 0; i < MusicCatalog.All.Count; i++)
                if (MusicCatalog.All[i].Id == encounter.Music.Id) catalogIndex = i;
        }

        public void Draw(RunUI ui, Transform parent, Action redraw)
        {
            var card = ui.Card(parent);
            ui.Label(card, "개발용 슬롯 미리보기", 23, RunUI.Gold, 38);
            ui.Button(card, expanded ? "슬롯 접기" : "슬롯 펼치기", () => { expanded = !expanded; redraw(); }, height: 58);
            if (!expanded) return;
            ui.Label(card, "미리보기의 곡을 바꿔도 이번 전투의 곡은 유지돼.", 20, RunUI.Muted, 60);
            var selector = ui.Row(card, 56);
            ui.Button(selector, "이전 곡", () => Select(-1, redraw), height: 56);
            ui.Label(selector, (catalogIndex + 1) + " / " + MusicCatalog.All.Count, 21, RunUI.Gold, 56, TextAnchor.MiddleCenter);
            ui.Button(selector, "다음 곡", () => Select(1, redraw), height: 56);
            ui.Label(card, stage.Music.Name + "  ·  " + stage.Music.Bpm + " BPM", 24, null, 48);
            var section = stage.Music.SectionAtBar(bar);
            ui.Label(card, "마디 " + (bar + 1) + " / " + stage.Music.BarCount + "  ·  " + section.Name +
                (section.AllowsResponse ? "  ×" + section.WeightMultiplier : "  ·  전조 공간"), 20, RunUI.Gold, 44);
            ui.Label(card, "세로선: 정박·엇박  /  가로막대: 유지  /  붉은 끝: 떼기", 18, RunUI.Muted, 52);
            var plot = ui.Row(card, 230, 6);
            plot.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            var names = ui.Stack(plot, "Gesture names", 0, 0);
            var namesLayout = names.gameObject.AddComponent<LayoutElement>();
            namesLayout.minWidth = namesLayout.preferredWidth = 72; namesLayout.flexibleWidth = 0;
            foreach (GestureKind kind in Enum.GetValues(typeof(GestureKind))) ui.Label(names, kind.ToString(), 19, RunUI.Muted, 46);
            var graph = ui.Rect("Music slots", plot); RunUI.Size(graph, 230, 1);
            graph.gameObject.AddComponent<MusicSlotsGraphic>().Bind(stage, bar);
            var navigation = ui.Row(card, 58);
            ui.Button(navigation, "이전 마디", () => { bar--; redraw(); }, bar > 0, height: 58);
            ui.Button(navigation, "다음 마디", () => { bar++; redraw(); }, bar < stage.Music.BarCount - 1, height: 58);
            int total = 0, here = 0;
            foreach (var candidate in stage.FindPlacements(TapTriplet))
            {
                total++;
                if (candidate.StartTick / stage.Music.TicksPerBar == bar) here++;
            }
            ui.Label(card, "후보 조회 예시: Tap · Tap · Tap / 전조 1박\n곡 전체 " + total + "곳 · 이 마디에서 시작 " + here + "곳", 20, RunUI.Teal, 76);
            ui.Label(card, "슬롯 후보를 보여줘. 이번 전투의 몬스터 패턴은 위의 준비 카드에서 확인해.", 20, RunUI.Muted, 70);
        }

        private void Select(int direction, Action redraw)
        {
            catalogIndex = (catalogIndex + direction + MusicCatalog.All.Count) % MusicCatalog.All.Count;
            stage = MusicStage.Generate(MusicCatalog.All[catalogIndex]);
            bar = Math.Min(bar, stage.Music.BarCount - 1); redraw();
        }
    }
}
