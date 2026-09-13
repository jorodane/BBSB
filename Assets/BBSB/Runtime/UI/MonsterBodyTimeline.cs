using System;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    public enum MonsterBodyPose { Idle, Call, Attack, Recover }

    public readonly struct MonsterBodyFrame
    {
        public PlannedAttack Attack { get; }
        public int CallIndex { get; }
        public MonsterBodyPose From { get; }
        public MonsterBodyPose To { get; }
        public double Blend { get; }
        public bool Active => Attack != null;
        internal MonsterBodyFrame(PlannedAttack attack, int call, MonsterBodyPose from, MonsterBodyPose to, double blend)
        { Attack = attack; CallIndex = call; From = from; To = to; Blend = blend; }
    }

    /// <summary>A Call owns the whole windup, attack and recovery. Responses never start a body attack.</summary>
    public static class MonsterBodyTimeline
    {
        public static double CallDuration(PlannedAttack attack, int index, double beat)
        {
            double at = attack.Call[index].Tick * beat / RhythmTime.TicksPerBeat;
            double until = attack.ResponseStartTick * beat / RhythmTime.TicksPerBeat;
            if (index + 1 < attack.Call.Count) until = Math.Min(until, attack.Call[index + 1].Tick * beat / RhythmTime.TicksPerBeat);
            return Math.Min(beat * .8, Math.Max(0, until - at) * .9);
        }

        public static MonsterBodyFrame Evaluate(MonsterPlan monster, double seconds, double beat)
        {
            PlannedAttack current = null; int call = 0; double start = double.NegativeInfinity, duration = 0;
            foreach (var attack in monster.Attacks)
            for (int i = 0; i < attack.Call.Count; i++)
            {
                double at = attack.Call[i].Tick * beat / RhythmTime.TicksPerBeat;
                double length = CallDuration(attack, i, beat);
                if (length <= 0 || seconds < at || seconds >= at + length || at < start) continue;
                current = attack; call = i; start = at; duration = length;
            }
            if (current == null) return default;
            double phase = Math.Min(3.999999, (seconds - start) / duration * 4);
            int part = (int)phase; double t = phase - part; t = t * t * (3 - 2 * t);
            var from = part == 0 ? MonsterBodyPose.Idle : part == 1 ? MonsterBodyPose.Call :
                part == 2 ? MonsterBodyPose.Attack : MonsterBodyPose.Recover;
            var to = part == 0 ? MonsterBodyPose.Call : part == 1 ? MonsterBodyPose.Attack :
                part == 2 ? MonsterBodyPose.Recover : MonsterBodyPose.Idle;
            return new MonsterBodyFrame(current, call, from, to, t);
        }
    }
}
