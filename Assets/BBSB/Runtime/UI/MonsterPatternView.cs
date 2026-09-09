using System.Collections.Generic;
using System.Globalization;
using BBSB.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BBSB.Runtime.UI
{
    /// <summary>One card per placed monster. Repeated occurrences share this same response pattern.</summary>
    public sealed class MonsterPatternView : MonoBehaviour
    {
        public MonsterPlan Plan { get; private set; }

        internal void Bind(MonsterPlan plan, RunUI ui, int number)
        {
            Plan = plan; var monster = plan.Monster;
            gameObject.name = "Monster pattern " + plan.InstanceId;
            ui.Label(transform, number.ToString("00") + "  " + monster.Name, 29, RunUI.Gold, 48);
            ui.Label(transform, "이번 곡 " + plan.Attacks.Count + "회 등장", 20, RunUI.Teal, 32);
            ui.Label(transform, monster.Description, 21, RunUI.TextColor, 66);
            var signals = new List<string>();
            foreach (var signal in monster.Call)
                signals.Add(Beat(monster.Pattern.CueLeadTicks - signal.OffsetTick) + "박 전: " + signal.Label);
            ui.Label(transform, "CALL  ·  " + Beat(monster.Pattern.CueLeadTicks) + "박 전조\n" + string.Join("  /  ", signals), 20, RunUI.Gold, 68);

            var kinds = new List<GestureKind>();
            foreach (var step in monster.Pattern.Steps) if (!kinds.Contains(step.Kind)) kinds.Add(step.Kind);
            kinds.Sort();
            float height = (kinds.Count + 2) * 34;
            var plot = ui.Row(transform, height, 6);
            plot.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            var names = ui.Stack(plot, "Pattern lanes", 0, 0);
            var width = names.gameObject.AddComponent<LayoutElement>();
            width.minWidth = width.preferredWidth = 72; width.flexibleWidth = 0;
            ui.Label(names, "CALL", 17, RunUI.Gold, 34);
            foreach (var kind in kinds) ui.Label(names, kind.ToString(), 17, RunUI.Teal, 34);
            ui.Label(names, "REST", 17, RunUI.Muted, 34);
            var graph = ui.Rect("Call and response", plot); RunUI.Size(graph, height, 1);
            graph.gameObject.AddComponent<MonsterPatternGraphic>().Bind(monster);

            ui.Label(transform, "RESPONSE  ·  " + Beat(monster.ResponseTicks) + "박", 20, RunUI.Teal, 32);
            // Times below are relative to this pattern, never absolute locations in the full song.
            foreach (var step in monster.Pattern.Steps)
            {
                string label = Beat(step.OffsetTick) + "박  " + step.Kind;
                if (step.DurationTicks > 0) label += "  ·  " + Beat(step.DurationTicks) + "박 유지";
                if (step.Touch.End == TouchTransition.Release) label += "  ·  " + Beat(step.OffsetTick + step.DurationTicks) + "박에 떼기";
                ui.Label(transform, label, 19, null, 32);
            }
            ui.Label(transform, "대응 시작을 0박으로 표시해. 이후 " + Beat(monster.RestTicks) + "박 휴식.", 18, RunUI.Muted, 48);
        }

        private static string Beat(int tick) => (tick / (double)RhythmTime.TicksPerBeat).ToString("0.##", CultureInfo.InvariantCulture);
    }
}
