using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    internal sealed class RhythmTouch
    {
        private readonly List<(double time, double x, double y)> samples = new List<(double, double, double)>();
        private readonly RhythmRules rules;
        public bool Down { get; private set; }
        public double X { get; private set; }
        public double Y { get; private set; }
        public double SampleSeconds { get; private set; }
        public RhythmTouch(RhythmRules rules) { this.rules = rules; }

        public void Press(double time, double x, double y)
        { samples.Clear(); Down = true; Move(time, x, y); }

        internal void ContinueFrom(RhythmTouch previous, double timeOffset)
        {
            Down = previous.Down; X = previous.X; Y = previous.Y;
            SampleSeconds = previous.SampleSeconds - timeOffset;
            samples.Clear();
            foreach (var sample in previous.samples)
                samples.Add((sample.time - timeOffset, sample.x, sample.y));
        }

        public void Move(double time, double x, double y)
        {
            X = x; Y = y; SampleSeconds = time;
            if (!Down) return;
            if (samples.Count > 0 && samples[samples.Count - 1].time == time) samples[samples.Count - 1] = (time, x, y);
            else samples.Add((time, x, y));
            // Keep one point before the lookback boundary so stationary holds and quick late flicks work.
            while (samples.Count > 2 && samples[1].time < time - rules.FlickLookbackSeconds) samples.RemoveAt(0);
        }

        public bool IsFlick(double time)
        {
            if (!Down || samples.Count < 2) return false;
            var first = samples[0];
            double from = Math.Max(first.time, time - rules.FlickLookbackSeconds);
            double x = first.x, y = first.y;
            if (first.time < from && samples[1].time > first.time)
            {
                var next = samples[1]; double ratio = (from - first.time) / (next.time - first.time);
                x += (next.x - x) * ratio; y += (next.y - y) * ratio;
            }
            double duration = time - from, distance = Distance(x, y, X, Y);
            return duration > 0 && distance >= rules.FlickDistance && distance / duration >= rules.FlickSpeed;
        }

        public void Release() { Down = false; samples.Clear(); }
        public static double Distance(double ax, double ay, double bx, double by)
        { double dx = bx - ax, dy = by - ay; return Math.Sqrt(dx * dx + dy * dy); }
    }
}
