using System;

namespace BBSB.Core
{
    // A bounded presentation snapshot of a real input. It never applies an effect.
    public sealed class PerformedWeaponNote
    {
        public WeaponPhraseNote Definition { get; }
        public double Beat { get; }
        public double EndBeat => Beat + Definition.HoldBeats;
        public double StartedAtBeat { get; }
        public double SustainStartedAtBeat { get; }
        public double InvocationBeat { get; }
        public int Ordinal { get; }
        public int ChainIndex { get; }
        public double ExpectedNextBeat { get; }
        public double CompletedAtBeat { get; internal set; } = double.PositiveInfinity;
        public double CanceledAtBeat { get; internal set; } = double.PositiveInfinity;
        public bool Canceled => !double.IsPositiveInfinity(CanceledAtBeat);

        internal PerformedWeaponNote(WeaponPhraseNote note, double beat, double actual, double invocation,
            int ordinal, PerformedWeaponNote previous, double nextBeat)
        {
            Definition = note; Beat = beat; StartedAtBeat = actual; InvocationBeat = invocation; Ordinal = ordinal;
            ExpectedNextBeat = nextBeat;
            bool joins = previous != null && !previous.Canceled && previous.InvocationBeat == invocation &&
                previous.CompletedAtBeat <= actual && beat >= previous.EndBeat - .000001 &&
                beat - previous.EndBeat <= .500001;
            ChainIndex = joins ? previous.ChainIndex + 1 : 0;
            SustainStartedAtBeat = joins && note.IsHold && previous.Definition.IsHold &&
                Math.Abs(beat - previous.EndBeat) < .000001 ? previous.SustainStartedAtBeat : actual;
        }
    }
}
