using System;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    /// <summary>Brief, area-preserving deformation around the authored foot pivot. Uses song time.</summary>
    public readonly struct PlayerSquashStretch
    {
        public const float DefaultStrength = 1;
        public double X { get; }
        public double Y { get; }

        private PlayerSquashStretch(double amount) { X = Math.Exp(amount); Y = Math.Exp(-amount); }

        public static PlayerSquashStretch Calculate(PlayerMotionFrame frame, double beatSeconds, double strength = DefaultStrength)
        {
            if (!frame.Kind.HasValue || frame.Phase == PlayerMotionPhase.Idle || beatSeconds <= 0)
                return new PlayerSquashStretch(0);
            if (double.IsNaN(strength) || double.IsInfinity(strength)) strength = DefaultStrength;
            strength = Math.Max(0, Math.Min(2, strength));
            var kind = frame.Kind.Value;
            double length = Math.Min(.10, beatSeconds * .24), amount;
            switch (frame.Phase)
            {
                case PlayerMotionPhase.Prepare:
                    amount = .05; length = PlayerMotionTimeline.TapPreparationDuration(beatSeconds); break;
                case PlayerMotionPhase.Sustain:
                    amount = kind == GestureKind.Shake ? .045 : .035; break;
                case PlayerMotionPhase.Impact:
                    amount = frame.Grade == RhythmGrade.Miss ? .06 :
                        kind == GestureKind.Flick ? -.07 : kind == GestureKind.Dive ? .07 :
                        kind == GestureKind.Tap ? (frame.Punch == 2 ? -.065 : .065) : .05;
                    double preparation = kind == GestureKind.Tap && frame.Reason != MissReason.NoInput ?
                        PlayerMotionTimeline.TapPreparationDuration(beatSeconds) : 0;
                    length = Math.Min(length, PlayerMotionTimeline.ImpactDuration(kind, beatSeconds) - preparation);
                    break;
                case PlayerMotionPhase.Recover:
                    amount = kind == GestureKind.Flick && !frame.IsFall ? .05 :
                        frame.IsFall ? (frame.Index == 4 ? .025 : -.03) :
                        kind == GestureKind.Tap && frame.Punch == 2 ? .025 : -.025;
                    break;
                default: return new PlayerSquashStretch(0);
            }
            if (frame.Grade == RhythmGrade.HalfMiss) amount *= .8;
            // One smooth pulse, then exactly neutral; holding or refreshing never accumulates scale.
            double age = frame.PhaseAge;
            double pulse = age > 0 && age < length ? Math.Sin(Math.PI * age / length) : 0;
            return new PlayerSquashStretch(amount * strength * pulse);
        }
    }
}
