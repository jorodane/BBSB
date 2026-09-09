using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    /// <summary>One performance of an immutable plan. The host supplies monotonic song time and one pointer.</summary>
    public sealed class RhythmRound
    {
        private readonly RhythmRules rules;
        private readonly RhythmTouch touch;
        private readonly List<ResponseNote> notes = new List<ResponseNote>();
        private readonly List<RhythmResult> results = new List<RhythmResult>();
        private readonly List<ScheduledCall> calls = new List<ScheduledCall>();
        private int callCursor;
        private bool suspended;
        public BattlePlan Plan { get; }
        public IReadOnlyList<ResponseNote> Notes { get; }
        public IReadOnlyList<RhythmResult> Results { get; }
        public IReadOnlyList<ScheduledCall> Calls { get; }
        public double ElapsedSeconds { get; private set; }
        public double PerfectWindow { get; }
        public double HalfMissWindow { get; }
        public double BeatSeconds { get; }
        public bool IsDown => touch.Down;
        public bool Finished { get; private set; }
        public int PerfectCount { get; private set; }
        public int HalfMissCount { get; private set; }
        public int MissCount { get; private set; }
        public double ScorePercent => notes.Count == 0 ? 0 : (PerfectCount + HalfMissCount * .5) * 100.0 / notes.Count;

        public RhythmRound(BattlePlan plan, RhythmRules rules = null)
        {
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            this.rules = rules ?? new RhythmRules(); touch = new RhythmTouch(this.rules);
            BeatSeconds = 60.0 / plan.Stage.Music.Bpm;
            // Adjacent half-beat targets must not have overlapping windows at high BPM.
            PerfectWindow = Math.Min(this.rules.PerfectSeconds, BeatSeconds * .16);
            HalfMissWindow = Math.Min(this.rules.HalfMissSeconds, BeatSeconds * .24);
            foreach (var attack in plan.Attacks)
                for (int i = 0; i < attack.Monster.Pattern.Steps.Count; i++) notes.Add(new ResponseNote(attack, i, plan.Stage.Music.Bpm));
            notes.Sort((a, b) => a.StartTick != b.StartTick ? a.StartTick.CompareTo(b.StartTick) :
                a.Attack.Id != b.Attack.Id ? string.CompareOrdinal(a.Attack.Id, b.Attack.Id) : a.StepIndex.CompareTo(b.StepIndex));
            Notes = notes.AsReadOnly(); Results = results.AsReadOnly(); Calls = calls.AsReadOnly();
        }

        public void Advance(double seconds) => Move(seconds, touch.X, touch.Y);

        private void AdvanceNotes(double seconds)
        {
            ElapsedSeconds = seconds;
            while (callCursor < Plan.Calls.Count && Seconds(Plan.Calls[callCursor].Tick) <= seconds)
                calls.Add(Plan.Calls[callCursor++]);
            foreach (var note in notes)
            {
                if (note.State == ResponseState.Resolved) continue;
                if (note.Step.Kind == GestureKind.Shake)
                {
                    // Shake is a coverage task, with no separate start/release timing gate.
                    if (seconds >= note.StartSeconds) note.State = ResponseState.Holding;
                    if (seconds >= note.EndSeconds)
                    {
                        var grade = note.ShakeCoverage + 1e-9 >= rules.ShakePerfectRatio ? RhythmGrade.Perfect :
                            note.ShakeCoverage + 1e-9 >= rules.ShakeHalfMissRatio ? RhythmGrade.HalfMiss : RhythmGrade.Miss;
                        Resolve(note, grade, grade == RhythmGrade.Miss ? MissReason.MissingShake : MissReason.None, note.EndSeconds, 0);
                    }
                    continue;
                }
                if (note.State == ResponseState.Pending)
                {
                    if (seconds > note.StartSeconds + HalfMissWindow + 1e-9)
                        Resolve(note, RhythmGrade.Miss, MissReason.NoInput, note.StartSeconds + HalfMissWindow, 0);
                }
                if (note.State != ResponseState.Holding) continue;
                if (note.Step.Kind == GestureKind.Hold && seconds >= note.EndSeconds && touch.Down)
                    Resolve(note, note.StartGrade, MissReason.None, note.EndSeconds, note.StartError);
                else if (seconds > note.EndSeconds + HalfMissWindow + 1e-9)
                    Resolve(note, RhythmGrade.Miss, MissReason.ReleaseTiming, note.EndSeconds + HalfMissWindow, 0);
            }
            if (seconds > Plan.Stage.Music.DurationSeconds + HalfMissWindow + 1e-9)
            {
                foreach (var note in notes) if (note.State != ResponseState.Resolved)
                    Resolve(note, RhythmGrade.Miss, MissReason.NoInput, seconds, 0);
                Finished = true; touch.Release();
            }
        }

        public void Press(double seconds, double x, double y)
        {
            ValidatePoint(x, y); Advance(seconds);
            if (Finished || suspended || touch.Down) return;
            touch.Press(seconds, x, y);
            int target = ClosestStart(seconds);
            if (target >= 0)
            {
                foreach (var note in notes)
                {
                    if (note.StartTick != target || note.State != ResponseState.Pending || note.Step.Touch.Start != TouchTransition.Press) continue;
                    double error = seconds - note.StartSeconds; var grade = Grade(error);
                    if (note.Step.Kind == GestureKind.Tap) Resolve(note, grade, MissReason.None, seconds, error);
                    else Begin(note, grade, error);
                }
            }
            else LatchEarlyPress(seconds);
        }

        public void Move(double seconds, double x, double y)
        {
            ValidatePoint(x, y); ValidateTime(seconds);
            if (Finished || suspended) return;
            // Integrate the final motion sample BEFORE resolving notes ending at this time.
            TrackShakeMotion(seconds, x, y);
            touch.Move(seconds, x, y);
            AdvanceNotes(seconds);
        }

        private void TrackShakeMotion(double seconds, double x, double y)
        {
            double dt = seconds - touch.SampleSeconds;
            foreach (var note in notes)
            {
                if (note.Step.Kind != GestureKind.Shake || note.State == ResponseState.Resolved) continue;
                if (!touch.Down || dt > rules.ShakeMaxSampleGapSeconds + 1e-9)
                { EndShakeContact(note); continue; }
                double from = Math.Max(touch.SampleSeconds, note.StartSeconds), to = Math.Min(seconds, note.EndSeconds);
                if (dt <= 0 || to <= from) continue;
                double a = (from - touch.SampleSeconds) / dt, b = (to - touch.SampleSeconds) / dt;
                double ax = touch.X + (x - touch.X) * a, ay = touch.Y + (y - touch.Y) * a;
                double bx = touch.X + (x - touch.X) * b, by = touch.Y + (y - touch.Y) * b;
                if (!note.ShakeContact)
                { note.ShakeContact = true; note.OriginX = ax; note.OriginY = ay; }
                // Stationary holds and very slow drift add no shake time.
                if (RhythmTouch.Distance(ax, ay, bx, by) / (to - from) + 1e-9 >= rules.ShakeMinSpeed)
                {
                    if (note.ShakeVerified) note.ShakeActiveSeconds += to - from;
                    else note.ShakeUnverifiedSeconds += to - from;
                }
                if (note.ShakeWentOut && SegmentDistance(note.OriginX, note.OriginY, ax, ay, bx, by) <= rules.ShakeReturnDistance + 1e-9)
                {
                    // Once this contact has demonstrated a round trip, its moving time is eligible.
                    note.ShakeVerified = true;
                    note.ShakeActiveSeconds += note.ShakeUnverifiedSeconds; note.ShakeUnverifiedSeconds = 0;
                }
                else if (RhythmTouch.Distance(note.OriginX, note.OriginY, bx, by) + 1e-9 >= rules.ShakeOutDistance)
                    note.ShakeWentOut = true;
            }
        }

        private static double SegmentDistance(double x, double y, double ax, double ay, double bx, double by)
        {
            double dx = bx - ax, dy = by - ay, length = dx * dx + dy * dy;
            double t = length <= 0 ? 0 : Math.Max(0, Math.Min(1, ((x - ax) * dx + (y - ay) * dy) / length));
            return RhythmTouch.Distance(x, y, ax + t * dx, ay + t * dy);
        }

        private static void EndShakeContact(ResponseNote note)
        { note.ShakeContact = note.ShakeWentOut = note.ShakeVerified = false; note.ShakeUnverifiedSeconds = 0; }

        public void Release(double seconds, double x, double y)
        {
            Move(seconds, x, y);
            if (Finished || suspended || !touch.Down) return;
            bool flick = touch.IsFlick(seconds);
            int target = ClosestRelease(seconds);
            foreach (var note in notes)
            {
                if (note.State == ResponseState.Resolved) continue;
                if (note.Step.Kind == GestureKind.Flick && note.State == ResponseState.Pending && note.StartTick == target)
                    Resolve(note, flick ? Grade(seconds - note.StartSeconds) : RhythmGrade.Miss,
                        flick ? MissReason.None : MissReason.MissingFlick, seconds, seconds - note.StartSeconds);
                else if (note.State == ResponseState.Holding)
                {
                    double error = seconds - note.EndSeconds;
                    bool inWindow = Math.Abs(error) <= HalfMissWindow + 1e-9;
                    if (note.Step.Kind == GestureKind.Dive)
                        Resolve(note, inWindow && note.EndTick == target ? Worse(note.StartGrade, Grade(error)) : RhythmGrade.Miss,
                            inWindow && note.EndTick == target ? MissReason.None : MissReason.ReleaseTiming, seconds, error);
                    else if (note.Step.Kind == GestureKind.Hold)
                    {
                        Resolve(note, inWindow ? Worse(note.StartGrade, Grade(error)) : RhythmGrade.Miss,
                            inWindow ? MissReason.None : MissReason.ReleasedEarly, seconds, error);
                    }
                }
            }
            touch.Release();
            foreach (var note in notes) if (note.Step.Kind == GestureKind.Shake) EndShakeContact(note);
        }

        // Freeze time and input together; the host must explicitly resume. Regrabbing isn't a new judged Press.
        public bool Suspend()
        { if (Finished) return false; suspended = true; bool wasDown = touch.Down; touch.Release(); return wasDown; }

        public void Resume(bool regrab, double x = 0, double y = 0)
        {
            ValidatePoint(x, y);
            if (!suspended || Finished) return;
            if (regrab)
            {
                double dx = x - touch.X, dy = y - touch.Y;
                foreach (var note in notes) if (note.State == ResponseState.Holding)
                { note.OriginX += dx; note.OriginY += dy; }
                touch.Press(ElapsedSeconds, x, y);
            }
            else if (notes.Exists(n => n.State == ResponseState.Holding && n.Step.Kind != GestureKind.Shake))
                throw new InvalidOperationException("An active held gesture must be regrabbed before resuming.");
            suspended = false;
        }

        private int ClosestStart(double seconds)
        {
            int target = -1; double best = double.MaxValue;
            foreach (var note in notes)
            {
                if (note.State != ResponseState.Pending || note.Step.Touch.Start != TouchTransition.Press) continue;
                double distance = Math.Abs(seconds - note.StartSeconds);
                if (distance <= HalfMissWindow + 1e-9 && distance < best) { best = distance; target = note.StartTick; }
            }
            return target;
        }

        private int ClosestRelease(double seconds)
        {
            int target = -1; double best = double.MaxValue;
            foreach (var note in notes)
            {
                bool pendingFlick = note.Step.Kind == GestureKind.Flick && note.State == ResponseState.Pending;
                bool heldDive = note.Step.Kind == GestureKind.Dive && note.State == ResponseState.Holding;
                if (!pendingFlick && !heldDive) continue;
                double distance = Math.Abs(seconds - note.EndSeconds);
                if (distance <= HalfMissWindow + 1e-9 && distance < best) { best = distance; target = note.EndTick; }
            }
            return target;
        }

        private void LatchEarlyPress(double seconds)
        {
            int target = -1;
            foreach (var note in notes)
                if (note.State == ResponseState.Pending && note.StartSeconds > seconds) { target = note.StartTick; break; }
            if (target < 0) return;
            double at = Seconds(target);
            if (seconds < at - BeatSeconds * .5 || seconds >= at - HalfMissWindow || Occupied(target - RhythmTime.TicksPerBeat / 2)) return;
            foreach (var note in notes)
                if (note.State == ResponseState.Pending && note.StartTick == target && note.Step.Touch.Start == TouchTransition.Press)
                    Resolve(note, RhythmGrade.Miss, MissReason.TooEarly, seconds, seconds - at);
        }

        private bool Occupied(int tick)
        {
            foreach (var note in notes)
                if (note.StartTick == tick || (note.Step.Touch.End == TouchTransition.Release && note.EndTick == tick) ||
                    (note.Step.Touch.HoldThroughInterval && tick >= note.StartTick && tick < note.EndTick)) return true;
            return false;
        }

        private void Begin(ResponseNote note, RhythmGrade grade, double error)
        {
            note.State = ResponseState.Holding; note.StartGrade = grade; note.StartError = error;
            note.OriginX = touch.X; note.OriginY = touch.Y;
        }

        private void Resolve(ResponseNote note, RhythmGrade grade, MissReason reason, double at, double error)
        {
            if (note.State == ResponseState.Resolved) return;
            note.State = ResponseState.Resolved;
            note.Result = new RhythmResult(note, grade, reason, at, error); results.Add(note.Result);
            if (grade == RhythmGrade.Perfect) PerfectCount++; else if (grade == RhythmGrade.HalfMiss) HalfMissCount++; else MissCount++;
        }

        private RhythmGrade Grade(double error) => Math.Abs(error) <= PerfectWindow + 1e-9 ? RhythmGrade.Perfect : RhythmGrade.HalfMiss;
        private static RhythmGrade Worse(RhythmGrade a, RhythmGrade b) => (RhythmGrade)Math.Min((int)a, (int)b);
        private double Seconds(int tick) => RhythmTime.Seconds(tick, Plan.Stage.Music.Bpm);
        private void ValidateTime(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < ElapsedSeconds)
                throw new ArgumentOutOfRangeException(nameof(seconds), "Song time must be finite and monotonic.");
        }
        private static void ValidatePoint(double x, double y)
        {
            if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(y) || double.IsInfinity(y)) throw new ArgumentOutOfRangeException(nameof(x));
        }
    }
}
