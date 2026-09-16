using System;

namespace BBSB.Runtime.UI
{
    // Six half-beat cells. Each note rests, then drops linearly into the next cell
    // during the final tenth of a beat, reaching the receptor on its musical beat.
    public static class SteppedNoteTrack
    {
        public const double LookAheadBeats = 3;
        public const double CellBeats = .5;
        public const double DropBeats = .1;
        public static double Distance(double remainingBeats)
        {
            if (remainingBeats <= 0) return 0;
            double cell = Math.Floor(remainingBeats / CellBeats) * CellBeats;
            double progress = Math.Min(1, (remainingBeats - cell) / DropBeats);
            return Math.Min(LookAheadBeats, cell + CellBeats * progress);
        }
        public static bool InHorizon(double remainingBeats) => remainingBeats <= LookAheadBeats;
    }
}
