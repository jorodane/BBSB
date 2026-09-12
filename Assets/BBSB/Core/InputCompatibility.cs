using System;

namespace BBSB.Core
{
    // Authored patterns must be physically playable. Between monsters, overlapping
    // gestures must also retain their input kind so another Call cannot rewrite a phrase.
    public static class InputCompatibility
    {
        public static bool IsPlayable(RhythmPattern pattern)
        {
            if (pattern == null) throw new ArgumentNullException(nameof(pattern));
            for (int i = 0; i < pattern.Steps.Count; i++)
                for (int j = i + 1; j < pattern.Steps.Count; j++)
                    if (PhysicalConflict(pattern.Steps[i], 0, pattern.Steps[j], 0, out _)) return false;
            return true;
        }

        public static bool Conflict(PatternPlacement a, PatternPlacement b, out int tick)
        {
            if (a == null || b == null) throw new ArgumentNullException(a == null ? nameof(a) : nameof(b));
            tick = -1;
            for (int i = 0; i < a.Pattern.Steps.Count; i++) for (int j = 0; j < b.Pattern.Steps.Count; j++)
            {
                var left = a.Pattern.Steps[i]; var right = b.Pattern.Steps[j];
                if (PhysicalConflict(left, a.StartTick, right, b.StartTick, out int at)) Record(ref tick, at);
                if (left.Kind == right.Kind) continue;
                int overlap = Math.Max(a.StartTick + left.OffsetTick, b.StartTick + right.OffsetTick);
                if (overlap <= Math.Min(GestureEnd(a, i), GestureEnd(b, j))) Record(ref tick, overlap);
            }
            return tick >= 0;
        }

        // Weapons may add a Shake to a held gesture or a Flick to its release. Only physical
        // contradictions matter here; the stricter monster-vs-monster Call rule stays above.
        public static bool PhysicalConflict(PatternPlacement a, PatternPlacement b, out int tick)
        {
            if (a == null || b == null) throw new ArgumentNullException(a == null ? nameof(a) : nameof(b));
            tick = -1;
            foreach (var left in a.Pattern.Steps) foreach (var right in b.Pattern.Steps)
                if (PhysicalConflict(left, a.StartTick, right, b.StartTick, out int at)) Record(ref tick, at);
            return tick >= 0;
        }

        // Instantaneous steps protect their kind until the next input of the same phrase.
        // Thus an extra Flick between Tap, Tap, Flick is rejected; sharing its final Flick is fine.
        // Sustained steps protect their entire duration, including the ending input position.
        private static int GestureEnd(PatternPlacement placement, int index)
        {
            var steps = placement.Pattern.Steps; var step = steps[index];
            if (step.DurationTicks == 0)
                for (int next = index + 1; next < steps.Count; next++)
                    if (steps[next].OffsetTick > step.OffsetTick) return placement.StartTick + steps[next].OffsetTick - 1;
            return placement.StartTick + step.OffsetTick + step.DurationTicks;
        }

        private static bool PhysicalConflict(PatternStep a, int aOffset, PatternStep b, int bOffset, out int tick)
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
