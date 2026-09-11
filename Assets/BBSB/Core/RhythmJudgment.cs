using System;

namespace BBSB.Core
{
    public enum RhythmGrade { Miss, HalfMiss, Perfect }
    public enum ResponseState { Pending, Holding, Resolved }
    public enum MissReason { None, TooEarly, NoInput, ReleasedEarly, ReleaseTiming, MissingFlick, MissingShake }

    public sealed class RhythmRules
    {
        public double PerfectSeconds { get; }
        public double HalfMissSeconds { get; }
        // Distances are fractions of the shorter screen dimension, independent of pixel density.
        public double ShakeOutDistance { get; }
        public double ShakeReturnDistance { get; }
        public double ShakeMaxSampleGapSeconds { get; }
        public double FlickDistance { get; }
        public double FlickSpeed { get; }
        public double FlickLookbackSeconds { get; }

        public RhythmRules(double perfectSeconds = .07, double halfMissSeconds = .14,
            double shakeOutDistance = .08, double shakeReturnDistance = .035,
            double flickDistance = .06, double flickSpeed = .5, double flickLookbackSeconds = .12,
            double shakeMaxSampleGapSeconds = .1)
        {
            foreach (double value in new[] { perfectSeconds, halfMissSeconds, shakeOutDistance, shakeReturnDistance,
                flickDistance, flickSpeed, flickLookbackSeconds, shakeMaxSampleGapSeconds })
                if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(perfectSeconds));
            if (perfectSeconds >= halfMissSeconds || shakeReturnDistance >= shakeOutDistance)
                throw new ArgumentException("Invalid judgment windows or distances.");
            PerfectSeconds = perfectSeconds; HalfMissSeconds = halfMissSeconds;
            ShakeOutDistance = shakeOutDistance; ShakeReturnDistance = shakeReturnDistance;
            ShakeMaxSampleGapSeconds = shakeMaxSampleGapSeconds;
            FlickDistance = flickDistance; FlickSpeed = flickSpeed; FlickLookbackSeconds = flickLookbackSeconds;
        }
    }

    public sealed class ResponseNote
    {
        public PlannedAttack Attack { get; }
        public int StepIndex { get; }
        public PatternStep Step => Attack.Placement.Pattern.Steps[StepIndex];
        public int StartTick => Attack.ResponseStartTick + Step.OffsetTick;
        public int EndTick => StartTick + Step.DurationTicks;
        public double StartSeconds { get; }
        public double EndSeconds { get; }
        public ResponseState State { get; internal set; }
        public RhythmResult Result { get; internal set; }
        // Best single-contact gesture progress: 0 -> .5 outward -> 1 returned. Never time coverage.
        public double ShakeProgress { get; internal set; }
        public bool ShakeCompleted => ShakeProgress >= 1;
        public double ShakeTravelDistance { get; internal set; }
        internal RhythmGrade StartGrade;
        internal double StartError;
        internal double OriginX, OriginY;
        internal bool ShakeContact, ShakeWentOut;

        internal ResponseNote(PlannedAttack attack, int stepIndex, double bpm)
        {
            Attack = attack; StepIndex = stepIndex;
            StartSeconds = RhythmTime.Seconds(StartTick, bpm); EndSeconds = RhythmTime.Seconds(EndTick, bpm);
        }
    }

    public sealed class RhythmResult
    {
        public ResponseNote Note { get; }
        public RhythmGrade Grade { get; }
        public MissReason Reason { get; }
        public double JudgedAtSeconds { get; }
        public double ErrorSeconds { get; }
        public double Efficiency => Grade == RhythmGrade.Perfect ? 1 : Grade == RhythmGrade.HalfMiss ? .5 : 0;
        // Score efficiency and incoming damage are separate rules. Perfect still takes half damage.
        public decimal DamageTakenMultiplier => Grade == RhythmGrade.Perfect ? .5m : Grade == RhythmGrade.HalfMiss ? .75m : 1m;
        public decimal DamageTaken => Note.Attack.Monster.DamagePerNote * DamageTakenMultiplier;
        internal RhythmResult(ResponseNote note, RhythmGrade grade, MissReason reason, double judgedAt, double error)
        { Note = note; Grade = grade; Reason = reason; JudgedAtSeconds = judgedAt; ErrorSeconds = error; }
    }
}
