using System;

namespace BBSB.Runtime.UI
{
    // The musical clock schedules landings; real seconds set the speed of a step.
    // Future notes share whole-beat steps. An offbeat's final partial step lands
    // at its own judgment time instead of waiting for the following whole beat.
    public static class SteppedNoteTrack
    {
        public const double LookAheadBeats = 3;
        public const double CellBeats = .5;
        public const double MoveSeconds = .18;
        public const double PulseSeconds = .08;
        public static double BeatPulse(double currentBeat, double bpm) =>
            Math.Max(0, 1 - (currentBeat - Math.Floor(currentBeat)) * 60 / bpm / PulseSeconds);
        public static double Distance(double noteBeat, double currentBeat, double bpm)
        {
            if (double.IsNaN(bpm) || double.IsInfinity(bpm) || bpm <= 0)
                throw new ArgumentOutOfRangeException(nameof(bpm));
            if (noteBeat <= currentBeat) return 0;
            double whole = Math.Floor(currentBeat);
            double moveBeats = MoveSeconds * bpm / 60;
            if (noteBeat < whole + 1)
                return SettleDistance(noteBeat - whole, noteBeat - currentBeat, moveBeats);
            double distance = noteBeat - (whole + 1) + SettleDistance(1, whole + 1 - currentBeat, moveBeats);
            return Math.Max(0, Math.Min(LookAheadBeats, distance));
        }
        public static double ImpactProgress(double impactBeat, double currentBeat, double bpm) =>
            1 - Math.Min(1, Distance(impactBeat, currentBeat, bpm));

        private static double SettleDistance(double span, double remaining, double moveBeats)
        {
            // Wait at the starting position, then traverse the step in 180 ms.
            // A shorter available interval may speed up a step, never slow it down.
            double t = Math.Max(0, Math.Min(1, remaining / Math.Min(span, moveBeats)));
            return span * t * t * t * (t * (t * 6 - 15) + 10);
        }
        public static bool InHorizon(double remainingBeats) => remainingBeats <= LookAheadBeats;
    }
}
