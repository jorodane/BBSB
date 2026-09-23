using System;

namespace BBSB.Runtime.UI
{
    // Retain the public name for existing tools. Scrolling is now linear: the
    // musical clock controls arrival times, without beat-by-beat acceleration.
    public static class SteppedNoteTrack
    {
        public const double LookAheadBeats = 3;
        public const double CellBeats = .5;
        public static double Distance(double noteBeat, double currentBeat) =>
            Math.Max(0, Math.Min(LookAheadBeats, noteBeat - currentBeat));
        // Physical depth: bottom/spawn = 0, top/judgment = 1. Use the same mapping
        // for heads, hold ends, attack notes and frozen shatter positions.
        public static double Depth(double distance) => 1 - Math.Max(0, Math.Min(1, distance / LookAheadBeats));
        public static double ImpactProgress(double impactBeat, double currentBeat) =>
            1 - Math.Min(1, Distance(impactBeat, currentBeat));

        public static bool InHorizon(double remainingBeats) => remainingBeats <= LookAheadBeats;
    }
}
