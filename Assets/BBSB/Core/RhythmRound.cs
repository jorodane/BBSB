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
        public WeaponBattle Combat { get; }
        public int ResponseNoteCount { get; }
        public IReadOnlyList<WeaponJudgment> WeaponResults => Combat?.Judgments ?? Array.Empty<WeaponJudgment>();
        public IReadOnlyList<ResponseNote> Notes { get; }
        public IReadOnlyList<RhythmResult> Results { get; }
        public IReadOnlyList<ScheduledCall> Calls { get; }
        public RhythmFreeInput FreeInput { get; }
        public double ElapsedSeconds { get; private set; }
        public double PerfectWindow { get; }
        public double HalfMissWindow { get; }
        public double BeatSeconds { get; }
        public bool IsDown => touch.Down;
        public bool Finished { get; private set; }
        public bool Aborted { get; private set; }
        public decimal TotalDamageTaken { get; private set; }
        public event Action<RhythmResult> ResultJudged;
        public int PerfectCount { get; private set; }
        public int HalfMissCount { get; private set; }
        public int MissCount { get; private set; }
        public double ScorePercent => ResponseNoteCount == 0 ? 0 : (PerfectCount + HalfMissCount * .5) * 100.0 / ResponseNoteCount;

        public RhythmRound(BattlePlan plan, RhythmRules rules = null, WeaponBattle combat = null)
        {
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            this.rules = rules ?? new RhythmRules(); touch = new RhythmTouch(this.rules);
            BeatSeconds = 60.0 / plan.Stage.Music.Bpm;
            FreeInput = new RhythmFreeInput(this.rules, BeatSeconds);
            // Adjacent half-beat targets must not have overlapping windows at high BPM.
            PerfectWindow = Math.Min(this.rules.PerfectSeconds, BeatSeconds * .16);
            HalfMissWindow = Math.Min(this.rules.HalfMissSeconds, BeatSeconds * .24);
            foreach (var attack in plan.Attacks)
                for (int i = 0; i < attack.Placement.Pattern.Steps.Count; i++) notes.Add(new ResponseNote(attack, i, plan.Stage.Music.Bpm));
            ResponseNoteCount = notes.Count;
            Combat = combat; Combat?.Bind(plan, notes);
            notes.Sort((a, b) => a.StartTick != b.StartTick ? a.StartTick.CompareTo(b.StartTick) : JudgeOrder(a, b));
            Notes = notes.AsReadOnly(); Results = results.AsReadOnly(); Calls = calls.AsReadOnly();
        }

        private int JudgeOrder(ResponseNote a, ResponseNote b)
        {
            // Order simultaneous monster results by their subscribed effects: shields first.
            int left = Combat?.JudgmentOrder(a) ?? 0, right = Combat?.JudgmentOrder(b) ?? 0;
            if (left != right) return left.CompareTo(right);
            return a.Attack.Id != b.Attack.Id ? string.CompareOrdinal(a.Attack.Id, b.Attack.Id) : a.StepIndex.CompareTo(b.StepIndex);
        }

        private bool CanJudge(ResponseNote note) => note.State != ResponseState.Resolved && (Combat == null || Combat.Allows(note.Attack));

        public void Advance(double seconds) => Move(seconds, touch.X, touch.Y);

        private void AdvanceNotes(double seconds)
        {
            ElapsedSeconds = seconds;
            while (callCursor < Plan.Calls.Count && Seconds(Plan.Calls[callCursor].Tick) <= seconds)
            {
                var call = Plan.Calls[callCursor++];
                if (Combat == null || Combat.AllowsEnemyEffect(Seconds(call.Tick))) calls.Add(call);
            }
            var due = new List<(ResponseNote note, RhythmGrade grade, MissReason reason, double at, double error)>();
            foreach (var note in notes)
            {
                if (!CanJudge(note)) continue;
                if (note.Step.Kind == GestureKind.Shake)
                {
                    if (seconds >= note.StartSeconds) note.State = ResponseState.Holding;
                    if (seconds >= note.EndSeconds)
                    {
                        var grade = note.ShakeCompleted ? RhythmGrade.Perfect :
                            note.ShakeProgress >= .5 ? RhythmGrade.HalfMiss : RhythmGrade.Miss;
                        due.Add((note, grade, grade == RhythmGrade.Miss ? MissReason.MissingShake : MissReason.None, note.EndSeconds, 0));
                    }
                }
                else if (note.State == ResponseState.Pending && seconds > note.StartSeconds + HalfMissWindow + 1e-9)
                    due.Add((note, RhythmGrade.Miss, MissReason.NoInput, note.StartSeconds + HalfMissWindow, 0));
                else if (note.State == ResponseState.Holding)
                {
                    if (note.Step.Kind == GestureKind.Hold && seconds >= note.EndSeconds && touch.Down)
                        due.Add((note, note.StartGrade, MissReason.None, note.EndSeconds, note.StartError));
                    else if (seconds > note.EndSeconds + HalfMissWindow + 1e-9)
                        due.Add((note, RhythmGrade.Miss, MissReason.ReleaseTiming, note.EndSeconds + HalfMissWindow, 0));
                }
            }
            // A long frame must not let a later shield/kill prevent an earlier lethal hit.
            due.Sort((a, b) => a.at != b.at ? a.at.CompareTo(b.at) : JudgeOrder(a.note, b.note));
            foreach (var item in due)
            {
                if (Finished) break;
                Resolve(item.note, item.grade, item.reason, item.at, item.error);
            }
            if (Combat != null && Combat.Victory) calls.RemoveAll(call => Seconds(call.Tick) >= Combat.DefeatedAtSeconds);
            if (!Finished && Combat != null && Combat.Victory)
            {
                if (seconds >= Combat.OverkillEndSeconds) Finish();
            }
            else if (!Finished && seconds > Plan.Stage.Music.DurationSeconds + HalfMissWindow + 1e-9)
            {
                foreach (var note in notes) if (CanJudge(note)) Resolve(note, RhythmGrade.Miss, MissReason.NoInput, seconds, 0);
                Finish();
            }
        }

        private void Finish() { Finished = true; touch.Release(); FreeInput.Consume(); }

        public void Press(double seconds, double x, double y)
        {
            ValidatePoint(x, y); Advance(seconds);
            if (Finished || suspended || touch.Down) return;
            touch.Press(seconds, x, y);
            FreeInput.Press(seconds, x, y);
            int previousResults = results.Count;
            int target = ClosestStart(seconds);
            if (target >= 0)
            {
                foreach (var note in notes)
                {
                    if (Finished) break;
                    if (!CanJudge(note) || note.StartTick != target || note.State != ResponseState.Pending || note.Step.Touch.Start != TouchTransition.Press) continue;
                    double error = seconds - note.StartSeconds; var grade = Grade(error);
                    if (note.Step.Kind == GestureKind.Tap) Resolve(note, grade, MissReason.None, seconds, error);
                    else Begin(note, grade, error);
                }
            }
            else LatchEarlyPress(seconds);
            if (Finished || target >= 0 || results.Count != previousResults || HasResponseContact(seconds)) FreeInput.Consume();
        }

        public void Move(double seconds, double x, double y)
        {
            ValidatePoint(x, y); ValidateTime(seconds);
            if (Finished || suspended) return;
            // Integrate the final motion sample BEFORE resolving notes ending at this time.
            TrackShakeMotion(seconds, x, y);
            touch.Move(seconds, x, y);
            AdvanceNotes(seconds);
            if (Finished) FreeInput.Consume();
            else if (touch.Down)
            {
                if (HasResponseContact(seconds)) FreeInput.Consume();
                else FreeInput.Move(seconds, x, y);
            }
        }

        private void TrackShakeMotion(double seconds, double x, double y)
        {
            double dt = seconds - touch.SampleSeconds;
            foreach (var note in notes)
            {
                if (note.Step.Kind != GestureKind.Shake || !CanJudge(note) || note.ShakeCompleted) continue;
                if (!touch.Down || dt > rules.ShakeMaxSampleGapSeconds + 1e-9)
                { EndShakeContact(note); continue; }
                double from = Math.Max(touch.SampleSeconds, note.StartSeconds), to = Math.Min(seconds, note.EndSeconds);
                if (dt <= 0 || to <= from) continue;
                double a = (from - touch.SampleSeconds) / dt, b = (to - touch.SampleSeconds) / dt;
                double ax = touch.X + (x - touch.X) * a, ay = touch.Y + (y - touch.Y) * a;
                double bx = touch.X + (x - touch.X) * b, by = touch.Y + (y - touch.Y) * b;
                if (!note.ShakeContact)
                { note.ShakeContact = true; note.OriginX = ax; note.OriginY = ay; }
                note.ShakeTravelDistance += RhythmTouch.Distance(ax, ay, bx, by);
                double distance = RhythmTouch.Distance(note.OriginX, note.OriginY, bx, by);
                if (note.ShakeWentOut && SegmentDistance(note.OriginX, note.OriginY, ax, ay, bx, by) <= rules.ShakeReturnDistance + 1e-9)
                {
                    note.ShakeProgress = 1;
                }
                else
                {
                    if (distance + 1e-9 >= rules.ShakeOutDistance) note.ShakeWentOut = true;
                    double progress = note.ShakeWentOut
                        ? .5 + .5 * Math.Max(0, (rules.ShakeOutDistance - distance) / (rules.ShakeOutDistance - rules.ShakeReturnDistance))
                        : .5 * Math.Min(1, distance / rules.ShakeOutDistance);
                    note.ShakeProgress = Math.Max(note.ShakeProgress, progress);
                }
            }
        }

        private static double SegmentDistance(double x, double y, double ax, double ay, double bx, double by)
        {
            double dx = bx - ax, dy = by - ay, length = dx * dx + dy * dy;
            double t = length <= 0 ? 0 : Math.Max(0, Math.Min(1, ((x - ax) * dx + (y - ay) * dy) / length));
            return RhythmTouch.Distance(x, y, ax + t * dx, ay + t * dy);
        }

        private static void EndShakeContact(ResponseNote note)
        { note.ShakeContact = note.ShakeWentOut = false; }

        public void Release(double seconds, double x, double y)
        {
            Move(seconds, x, y);
            if (Finished || suspended || !touch.Down) return;
            bool flick = touch.IsFlick(seconds);
            int target = ClosestRelease(seconds);
            int previousResults = results.Count;
            var releaseOrder = new List<ResponseNote>(notes); releaseOrder.Sort(JudgeOrder);
            foreach (var note in releaseOrder)
            {
                if (Finished) break;
                if (!CanJudge(note)) continue;
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
            if (Finished || target >= 0 || results.Count != previousResults) FreeInput.Consume();
            else FreeInput.Release(seconds, flick);
            touch.Release();
            foreach (var note in notes) if (note.Step.Kind == GestureKind.Shake) EndShakeContact(note);
        }

        // Freeze time and input together; the host must explicitly resume. Regrabbing isn't a new judged Press.
        public bool Suspend()
        { if (Finished) return false; suspended = true; bool wasDown = touch.Down; touch.Release(); return wasDown; }

        // Ending playback never fabricates misses for notes the player did not reach.
        public void Stop()
        {
            if (Finished) return;
            Aborted = Finished = true; touch.Release(); FreeInput.Consume();
        }

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
            FreeInput.Resume(regrab, x, y);
            suspended = false;
        }

        private bool HasResponseContact(double seconds)
        {
            foreach (var note in notes)
            {
                if (!CanJudge(note)) continue;
                if (note.State == ResponseState.Holding && note.Step.Kind != GestureKind.Shake) return true;
                if (note.Step.Kind == GestureKind.Shake && seconds >= note.StartSeconds - HalfMissWindow && seconds < note.EndSeconds)
                    return true;
                if (note.Step.Kind == GestureKind.Flick && seconds >= note.StartSeconds - Math.Max(HalfMissWindow, rules.FlickLookbackSeconds) &&
                    seconds <= note.StartSeconds + HalfMissWindow) return true;
            }
            return false;
        }

        private int ClosestStart(double seconds)
        {
            int target = -1; double best = double.MaxValue;
            foreach (var note in notes)
            {
                if (!CanJudge(note) || note.State != ResponseState.Pending || note.Step.Touch.Start != TouchTransition.Press) continue;
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
                if (!CanJudge(note) || (!pendingFlick && !heldDive)) continue;
                double distance = Math.Abs(seconds - note.EndSeconds);
                if (distance <= HalfMissWindow + 1e-9 && distance < best) { best = distance; target = note.EndTick; }
            }
            return target;
        }

        private void LatchEarlyPress(double seconds)
        {
            int target = -1;
            foreach (var note in notes)
                if (CanJudge(note) && note.State == ResponseState.Pending && note.StartSeconds > seconds) { target = note.StartTick; break; }
            if (target < 0) return;
            double at = Seconds(target);
            if (seconds < at - BeatSeconds * .5 || seconds >= at - HalfMissWindow || Occupied(target - RhythmTime.TicksPerBeat / 2)) return;
            foreach (var note in notes)
                if (CanJudge(note) && note.State == ResponseState.Pending && note.StartTick == target && note.Step.Touch.Start == TouchTransition.Press)
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
            if (Finished) return;
            note.State = ResponseState.Holding; note.StartGrade = grade; note.StartError = error;
            note.OriginX = touch.X; note.OriginY = touch.Y;
        }

        private void Resolve(ResponseNote note, RhythmGrade grade, MissReason reason, double at, double error)
        {
            if (Finished || !CanJudge(note)) return;
            note.State = ResponseState.Resolved;
            note.Result = new RhythmResult(note, grade, reason, at, error);
            Combat?.JudgeWeapons(note.Result);
            Combat?.JudgeIncoming(note.Result); results.Add(note.Result);
            if (grade == RhythmGrade.Perfect) PerfectCount++; else if (grade == RhythmGrade.HalfMiss) HalfMissCount++; else MissCount++;
            TotalDamageTaken += note.Result.DamageTaken;
            ResultJudged?.Invoke(note.Result);
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
