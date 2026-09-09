using System;

namespace BBSB.Core
{
    // Tests whether one finger can satisfy the required transitions and held intervals.
    // Unconstrained gaps allow preparation/release; Call signals are not player inputs.
    public static class InputCompatibility
    {
        public static bool IsPlayable(RhythmPattern pattern)
        {
            if (pattern == null) throw new ArgumentNullException(nameof(pattern));
            for (int i = 0; i < pattern.Steps.Count; i++)
                for (int j = i + 1; j < pattern.Steps.Count; j++)
                    if (Conflict(pattern.Steps[i], 0, pattern.Steps[j], 0, out _)) return false;
            return true;
        }

        public static bool Conflict(PatternPlacement a, PatternPlacement b, out int tick)
        {
            if (a == null || b == null) throw new ArgumentNullException(a == null ? nameof(a) : nameof(b));
            tick = -1;
            foreach (var left in a.Pattern.Steps) foreach (var right in b.Pattern.Steps)
                if (Conflict(left, a.StartTick, right, b.StartTick, out int at)) Record(ref tick, at);
            return tick >= 0;
        }

        private static bool Conflict(PatternStep a, int aOffset, PatternStep b, int bOffset, out int tick)
        {
            int aStart = aOffset + a.OffsetTick, aEnd = aStart + a.DurationTicks;
            int bStart = bOffset + b.OffsetTick, bEnd = bStart + b.DurationTicks;
            var left = a.Touch; var right = b.Touch;
            tick = -1;
            if (left.Start == TouchTransition.Press &&
                ((right.End == TouchTransition.Release && aStart == bEnd) ||
                 (right.HoldThroughInterval && aStart > bStart && aStart <= bEnd))) Record(ref tick, aStart);
            if (right.Start == TouchTransition.Press &&
                ((left.End == TouchTransition.Release && bStart == aEnd) ||
                 (left.HoldThroughInterval && bStart > aStart && bStart <= aEnd))) Record(ref tick, bStart);
            if (left.End == TouchTransition.Release && right.HoldThroughInterval && aEnd >= bStart && aEnd < bEnd)
                Record(ref tick, aEnd);
            if (right.End == TouchTransition.Release && left.HoldThroughInterval && bEnd >= aStart && bEnd < aEnd)
                Record(ref tick, bEnd);
            return tick >= 0;
        }

        private static void Record(ref int earliest, int at) { if (earliest < 0 || at < earliest) earliest = at; }
    }
}
