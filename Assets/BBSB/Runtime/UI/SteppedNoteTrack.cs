using System;

namespace BBSB.Runtime.UI
{
    // Six half-beat cells: hold a fixed position for .45 beats, then drop linearly
    // during the final .05 beats. Arrival, not the start of motion, marks the beat.
    public static class SteppedNoteTrack
    {
        public const double LookAheadBeats = 3;
        public const double CellBeats = .5;
        public const double DropBeats = .05;
        public static double Distance(double remainingBeats)
        {
            if (remainingBeats <= 0) return 0;
            double restingPosition = Math.Ceiling(remainingBeats / CellBeats) * CellBeats;
            double landingPosition = restingPosition - CellBeats;
            double untilLanding = remainingBeats - landingPosition;
            // No interpolation during the rest. Repeated frames return the same position.
            if (untilLanding >= DropBeats) return Math.Min(LookAheadBeats, restingPosition);
            return Math.Min(LookAheadBeats, restingPosition - CellBeats * DropProgress(untilLanding));
        }
        public static double DropProgress(double untilLanding)
        {
            if (untilLanding >= DropBeats) return 0;
            if (untilLanding <= 0) return 1;
            // Absolute musical time keeps pauses, skipped frames and impact timing aligned.
            return 1 - untilLanding / DropBeats;
        }
        public static bool InHorizon(double remainingBeats) => remainingBeats <= LookAheadBeats;
    }
}
