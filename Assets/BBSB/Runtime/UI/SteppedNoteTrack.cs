using System;

namespace BBSB.Runtime.UI
{
    // One rotation per whole beat, shared by every lane. Half-beat notes pass the
    // receptor at full speed; only whole beats settle like a combination-lock dial.
    public static class SteppedNoteTrack
    {
        public const double LookAheadBeats = 3;
        public const double CellBeats = .5;
        public static double Distance(double noteBeat, double currentBeat)
        {
            if (noteBeat <= currentBeat) return 0;
            // Map both absolute times. Easing the remaining time alone would put
            // offbeat notes on a different acceleration cycle from on-beat notes.
            return Math.Max(0, Math.Min(LookAheadBeats, VisualBeat(noteBeat) - VisualBeat(currentBeat)));
        }
        public static double ImpactProgress(double impactBeat, double currentBeat) =>
            1 - Math.Min(1, Distance(impactBeat, currentBeat));

        private static double VisualBeat(double beat)
        {
            double whole = Math.Floor(beat), phase = beat - whole;
            // Symmetric quintic easing: zero speed/acceleration at each whole beat,
            // maximum speed and exactly half the distance at the half beat.
            double eased = phase * phase * phase * (phase * (phase * 6 - 15) + 10);
            return whole + eased;
        }
        public static bool InHorizon(double remainingBeats) => remainingBeats <= LookAheadBeats;
    }
}
