using System;

namespace BBSB.Core
{
    /// <summary>Visual feedback for an unclaimed contact. Never creates a note, grade or damage.</summary>
    public sealed class RhythmFreeInput
    {
        private readonly RhythmRules rules;
        private readonly double holdSeconds;
        private bool tracking, shook;
        private double originX, originY, previousX, previousY, travel;
        public int Sequence { get; private set; }
        public GestureKind? Kind { get; private set; }
        public bool IsHeld => tracking;
        public bool IsOutward { get; private set; }
        public double StartedAtSeconds { get; private set; }
        public double PressedAtSeconds { get; private set; }
        public double LastMovementSeconds { get; private set; } = double.NegativeInfinity;

        internal RhythmFreeInput(RhythmRules rules, double beatSeconds)
        { this.rules = rules; holdSeconds = Math.Min(.18, beatSeconds * .5); }

        internal void Press(double seconds, double x, double y)
        {
            tracking = true; shook = IsOutward = false; travel = 0;
            originX = previousX = x; originY = previousY = y; PressedAtSeconds = seconds;
            LastMovementSeconds = double.NegativeInfinity;
            Record(GestureKind.Tap, seconds);
        }

        internal void Move(double seconds, double x, double y)
        {
            if (!tracking) return;
            double distance = RhythmTouch.Distance(previousX, previousY, x, y);
            previousX = x; previousY = y; travel += distance;
            if (distance > .001 && travel >= rules.ShakeOutDistance * .25)
            {
                LastMovementSeconds = seconds; shook = true;
                IsOutward = RhythmTouch.Distance(originX, originY, x, y) >= rules.ShakeOutDistance;
                if (Kind != GestureKind.Shake) Record(GestureKind.Shake, seconds);
            }
            else if (Kind == GestureKind.Tap && seconds - PressedAtSeconds >= holdSeconds)
                Record(GestureKind.Hold, PressedAtSeconds);
        }

        internal void Release(double seconds, bool flick)
        {
            if (!tracking) return;
            tracking = false;
            if (flick) Record(GestureKind.Flick, seconds);
            else if (shook) Record(GestureKind.Shake, seconds);
            // An unmatched release only lowers the guard. Dive belongs to an authored note.
            else if (seconds - PressedAtSeconds >= holdSeconds) Record(GestureKind.Hold, seconds);
            // A quick release completes the Tap already shown on press, without a second punch.
        }

        internal void Consume() { tracking = false; Kind = null; }

        internal void Resume(bool regrab, double x, double y)
        {
            if (!tracking) return;
            if (!regrab) { Consume(); return; }
            // Recontact after a menu is not a new gesture or a screen-sized Shake.
            originX += x - previousX; originY += y - previousY;
            previousX = x; previousY = y;
        }

        private void Record(GestureKind kind, double seconds)
        { Sequence++; Kind = kind; StartedAtSeconds = seconds; }
    }
}
