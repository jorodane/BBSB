using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    // These are audible/visible identities, not the caption or the player's input kind.
    public enum CallSound { Wood, Drum, Bell, RisingChime, FallingChime, RisingWhistle, FallingWhistle, Rattle, Sweep }
    public enum CallMotion { Hop, Step, Stomp, TailSweep, Rise, Dip, Sway, Flash }

    public static class CallReadability
    {
        public const int ImmediatePreparationTicks = RhythmTime.TicksPerBeat;
        public const int SharedPrefixPreparationTicks = RhythmTime.TicksPerBeat;

        /// <summary>Compare the cues from their first Call, without revealing the selected pattern's name.
        /// A missing event becomes informative only when that event was due in the alternative.</summary>
        public static int FirstDifferenceTick(MonsterPatternDefinition first, MonsterPatternDefinition second, bool sound)
        {
            if (first == null || second == null) throw new ArgumentNullException(first == null ? nameof(first) : nameof(second));
            int a = 0, b = 0;
            while (a < first.Call.Count || b < second.Call.Count)
            {
                if (a == first.Call.Count) return second.Call[b].OffsetTick;
                if (b == second.Call.Count) return first.Call[a].OffsetTick;
                var left = first.Call[a]; var right = second.Call[b];
                if (left.OffsetTick != right.OffsetTick) return Math.Min(left.OffsetTick, right.OffsetTick);
                if (sound ? left.Sound != right.Sound : left.Motion != right.Motion) return left.OffsetTick;
                a++; b++;
            }
            return -1;
        }

        internal static void Validate(MonsterPatternDefinition first, MonsterPatternDefinition second)
        {
            int response = Math.Min(first.Pattern.CueLeadTicks, second.Pattern.CueLeadTicks);
            foreach (bool sound in new[] { true, false })
            {
                int difference = FirstDifferenceTick(first, second, sound);
                int preparation = difference == 0 ? ImmediatePreparationTicks : SharedPrefixPreparationTicks;
                bool sameLengthAndTypes = first.Pattern.CueLeadTicks == second.Pattern.CueLeadTicks && SameTypeOrder(first, second, sound);
                if (sameLengthAndTypes || difference < 0 || response - difference < preparation)
                    throw new ArgumentException("Patterns " + first.Id + " and " + second.Id + " need distinct Call " +
                        (sound ? "sounds" : "motions") + " with preparation time before either Response.");
            }
        }

        private static bool SameTypeOrder(MonsterPatternDefinition first, MonsterPatternDefinition second, bool sound)
        {
            var a = TypeOrder(first, sound); var b = TypeOrder(second, sound);
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static List<int> TypeOrder(MonsterPatternDefinition pattern, bool sound)
        {
            var result = new List<int>();
            foreach (var call in pattern.Call)
            {
                int kind = sound ? (int)call.Sound : (int)call.Motion;
                // With equal cue lengths, merely repeating the same cue or shifting it is insufficient.
                if (result.Count == 0 || result[result.Count - 1] != kind) result.Add(kind);
            }
            return result;
        }
    }
}
