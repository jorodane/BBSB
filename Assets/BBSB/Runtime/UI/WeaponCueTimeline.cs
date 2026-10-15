using System;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    // Telegraphs sample the live weapon state. They never create inputs or change
    // note timing. A bow's release remains voluntary; a loaded crossbow keeps cues.
    public static class WeaponCueTimeline
    {
        public const double LeadBeats = .5;
        public static double Target(PhraseLane lane) => lane == null || HandCombatLayout.IsShield(lane.Weapon) ||
            lane.Phase != PhraseLanePhase.Playing || lane.Holding || lane.Phrase.Notes[lane.NextNote].IsChargedRelease ?
            double.PositiveInfinity : lane.NextBeat;
        public static double At(PhraseLane lane) => Math.Max(lane?.InvokedAtBeat ?? 0, Target(lane) - LeadBeats);
        public static double Preparation(PhraseLane lane, double beat)
        {
            double at = At(lane), target = Target(lane);
            if (double.IsInfinity(target) || beat < at || beat > target) return 0;
            return Math.Max(.15, Math.Min(1, (beat - at) / Math.Max(.001, target - at)));
        }
    }
}
