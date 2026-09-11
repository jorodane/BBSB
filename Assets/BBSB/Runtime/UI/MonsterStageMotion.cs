using System;
using System.Collections.Generic;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    /// <summary>A whole Call/Response phrase takes one step forward, using only song time.</summary>
    public sealed class MonsterStageMotion
    {
        private readonly List<(double Start, double End)> phrases = new List<(double, double)>();
        public double ApproachSeconds { get; }
        public double ReturnSeconds { get; }

        public MonsterStageMotion(MonsterPlan monster, double bpm, double judgmentWindow)
        {
            if (monster == null) throw new ArgumentNullException(nameof(monster));
            double beat = 60.0 / bpm;
            ApproachSeconds = Math.Min(.24, beat * .5);
            ReturnSeconds = Math.Min(.32, beat * .65);
            var candidates = new List<(double Start, double End)>();
            foreach (var attack in monster.Attacks)
            {
                // Linked patterns, including their internal rests, share one stage visit.
                int start = attack.Chain?.CallStartTick ?? attack.CallStartTick;
                int end = attack.Chain?.PhraseEndTick ?? attack.PhraseEndTick;
                candidates.Add((RhythmTime.Seconds(start, bpm), RhythmTime.Seconds(end, bpm) + judgmentWindow));
            }
            candidates.Sort((a, b) => a.Start.CompareTo(b.Start));
            foreach (var candidate in candidates)
            {
                int last = phrases.Count - 1;
                // Avoid retreating halfway when the next Call already needs this actor.
                if (last >= 0 && candidate.Start <= phrases[last].End + ReturnSeconds)
                    phrases[last] = (phrases[last].Start, Math.Max(phrases[last].End, candidate.End));
                else phrases.Add(candidate);
            }
        }

        public double Evaluate(double seconds)
        {
            foreach (var phrase in phrases)
            {
                if (seconds < phrase.Start) return 0;
                if (seconds > phrase.End + ReturnSeconds) continue;
                if (seconds < phrase.Start + ApproachSeconds)
                    return Smooth((seconds - phrase.Start) / ApproachSeconds);
                if (seconds <= phrase.End) return 1;
                return 1 - Smooth((seconds - phrase.End) / ReturnSeconds);
            }
            return 0;
        }

        private static double Smooth(double value)
        { double t = Math.Max(0, Math.Min(1, value)); return t * t * (3 - 2 * t); }
    }
}
