using System;
using System.Collections.Generic;
using BBSB.Core;

namespace BBSB.Runtime.UI
{
    public enum PlayerMotionPhase { Idle, Sustain, Impact, Recover }

    /// <summary>A frame in a generated atlas, numbered from the top left in reading order.</summary>
    public readonly struct PlayerMotionFrame
    {
        public string Sheet { get; }
        public int Index { get; }
        public string SourceSheet => Sheet == "tap" ? (Index / 4 == 0 ? "tap-left" : Index / 4 == 1 ? "tap-right" : "tap-upper") : Sheet;
        public int SourceIndex => Sheet == "tap" ? Index % 4 : Index;
        public PlayerMotionPhase Phase { get; }
        public GestureKind? Kind { get; }
        public RhythmGrade? Grade { get; }
        public MissReason Reason { get; }
        public int Punch { get; }
        public double Age { get; }
        public bool IsFreeInput { get; }
        public bool IsFall => Grade == RhythmGrade.Miss && (Kind == GestureKind.Dive || Kind == GestureKind.Flick);

        internal PlayerMotionFrame(string sheet, int index, PlayerMotionPhase phase,
            GestureKind? kind = null, RhythmGrade? grade = null, MissReason reason = MissReason.None,
            int punch = -1, double age = 0, bool isFreeInput = false)
        { Sheet = sheet; Index = index; Phase = phase; Kind = kind; Grade = grade; Reason = reason; Punch = punch; Age = age; IsFreeInput = isFreeInput; }
    }

    /// <summary>
    /// Read-only animation selection from judged results, credited contact and unclaimed input.
    /// Uses song time, never wall time; does not resolve notes, block input, or apply damage.
    /// </summary>
    public sealed class PlayerMotionTimeline
    {
        private sealed class Reaction
        {
            public RhythmResult Result;
            public int Punch = -1;
        }

        private readonly Random random;
        private readonly List<Reaction> reactions = new List<Reaction>();
        private readonly Dictionary<(GestureKind kind, int start, int end, double time), Reaction> shared =
            new Dictionary<(GestureKind, int, int, double), Reaction>();
        private readonly Dictionary<ResponseNote, double> contactStarted = new Dictionary<ResponseNote, double>();
        private readonly Dictionary<ResponseNote, double> shakeDistances = new Dictionary<ResponseNote, double>();
        private readonly Dictionary<ResponseNote, double> shakeMoved = new Dictionary<ResponseNote, double>();
        private RhythmRound boundRound;
        private int resultCursor, lastPunch = -1;
        private int freeCursor, freePunch = -1;

        public PlayerMotionTimeline(int? seed = null) { random = seed.HasValue ? new Random(seed.Value) : new Random(); }
        public int PunchSelections { get; private set; }

        public PlayerMotionFrame Evaluate(RhythmRound round)
        {
            if (round == null) throw new ArgumentNullException(nameof(round));
            if (boundRound != null && !ReferenceEquals(boundRound, round))
                throw new InvalidOperationException("A motion timeline belongs to one round.");
            boundRound = round;
            ObserveResults(round);
            double seconds = round.ElapsedSeconds;
            PlayerMotionFrame? sustain = ObserveContact(round, out double contactTime);
            PlayerMotionFrame? free = ObserveFreeInput(round);
            Reaction latest = null;
            foreach (var reaction in reactions)
            {
                if (reaction.Result.JudgedAtSeconds > seconds) continue;
                if (latest == null || IsMoreRecent(reaction.Result, latest.Result)) latest = reaction;
            }
            if (latest != null)
            {
                double age = seconds - latest.Result.JudgedAtSeconds;
                double impact = ImpactDuration(latest.Result.Note.Step.Kind, round.BeatSeconds);
                // New contact immediately interrupts even a long fall. A continuing guard returns
                // after the brief impact; an old result must not hide it for its entire recovery.
                if (sustain.HasValue && (contactTime > latest.Result.JudgedAtSeconds + 1e-9 || age >= impact))
                    return sustain.Value;
                if (free.HasValue && (round.FreeInput.StartedAtSeconds > latest.Result.JudgedAtSeconds + 1e-9 ||
                    (free.Value.Phase == PlayerMotionPhase.Sustain && age >= impact))) return free.Value;
                var frame = ReactionFrame(round, latest, age, impact);
                if (frame.HasValue) return frame.Value;
            }
            if (sustain.HasValue) return sustain.Value;
            if (free.HasValue) return free.Value;
            return new PlayerMotionFrame("idle", (int)(seconds / round.BeatSeconds * 2) % 2, PlayerMotionPhase.Idle);
        }

        private void ObserveResults(RhythmRound round)
        {
            while (resultCursor < round.Results.Count)
            {
                var result = round.Results[resultCursor++]; var note = result.Note;
                var key = (note.Step.Kind, note.StartTick, note.EndTick, result.JudgedAtSeconds);
                if (shared.TryGetValue(key, out var existing))
                {
                    // A shared gesture has one body pose. Retain the worst grade if sources differ.
                    if (result.Grade < existing.Result.Grade) existing.Result = result;
                    continue;
                }
                var reaction = new Reaction { Result = result };
                if (note.Step.Kind == GestureKind.Tap)
                    reaction.Punch = NextPunch();
                shared.Add(key, reaction); reactions.Add(reaction);
            }
        }

        private int NextPunch()
        {
            int next = lastPunch < 0 ? random.Next(3) : random.Next(2);
            if (lastPunch >= 0 && next >= lastPunch) next++;
            lastPunch = next; PunchSelections++; return next;
        }

        private PlayerMotionFrame? ObserveFreeInput(RhythmRound round)
        {
            var input = round.FreeInput;
            if (!input.Kind.HasValue) return null;
            var kind = input.Kind.Value;
            if (freeCursor != input.Sequence)
            {
                freeCursor = input.Sequence;
                if (kind == GestureKind.Tap) freePunch = NextPunch();
            }
            double seconds = round.ElapsedSeconds, age = seconds - input.StartedAtSeconds;
            if (input.IsHeld && kind != GestureKind.Tap)
            {
                bool moving = kind == GestureKind.Shake && seconds - input.LastMovementSeconds < .14;
                kind = moving ? GestureKind.Shake : GestureKind.Hold;
                int pose = moving ? (input.IsOutward ? 1 : 0) : age < .065 ? 0 : 1;
                return new PlayerMotionFrame(SheetFor(kind), pose, PlayerMotionPhase.Sustain, kind,
                    age: age, isFreeInput: true);
            }
            double impact = ImpactDuration(kind, round.BeatSeconds), duration = Math.Min(.48, round.BeatSeconds * .95);
            if (age < 0 || age >= duration) return null;
            bool recovering = age >= impact;
            int index = recovering ? (kind == GestureKind.Tap ? freePunch * 4 : kind == GestureKind.Flick ? 0 : 5) :
                ResultIndex(kind, RhythmGrade.Perfect, freePunch);
            return new PlayerMotionFrame(SheetFor(kind), index, recovering ? PlayerMotionPhase.Recover : PlayerMotionPhase.Impact,
                kind, punch: freePunch, age: age, isFreeInput: true);
        }

        private static double ImpactDuration(GestureKind kind, double beatSeconds)
        {
            double duration = Math.Min(.24, beatSeconds * .55);
            // Recall punches and recover from ducks/jumps twice as soon, including at fast BPM.
            return kind == GestureKind.Tap || kind == GestureKind.Dive || kind == GestureKind.Flick ? duration * .5 : duration;
        }

        private PlayerMotionFrame? ObserveContact(RhythmRound round, out double contactTime)
        {
            PlayerMotionFrame? selected = null; contactTime = double.NegativeInfinity;
            double seconds = round.ElapsedSeconds;
            foreach (var note in round.Notes)
            {
                if (!round.IsDown || note.State != ResponseState.Holding) continue;
                var kind = note.Step.Kind;
                if (kind != GestureKind.Hold && kind != GestureKind.Dive && kind != GestureKind.Shake) continue;
                if (!contactStarted.TryGetValue(note, out double started)) contactStarted[note] = started = seconds;
                int index;
                if (kind == GestureKind.Shake)
                {
                    shakeDistances.TryGetValue(note, out double previous);
                    if (note.ShakeTravelDistance > previous + 1e-9) shakeMoved[note] = seconds;
                    shakeDistances[note] = note.ShakeTravelDistance;
                    // Shake's window enters Holding automatically; a stationary finger is no action.
                    if (!shakeMoved.TryGetValue(note, out double shakeMovedAt) || seconds - shakeMovedAt >= .14) continue;
                    index = note.ShakeProgress >= .5 ? 1 : 0;
                    started = shakeMovedAt;
                }
                else index = seconds - started < .065 ? 0 : 1;
                var frame = new PlayerMotionFrame(SheetFor(kind), index, PlayerMotionPhase.Sustain, kind, age: seconds - started);
                if (!selected.HasValue || kind == GestureKind.Shake ||
                    (selected.Value.Kind != GestureKind.Shake && Priority(kind) > Priority(selected.Value.Kind.Value))) selected = frame;
                contactTime = Math.Max(contactTime, started);
            }
            return selected;
        }

        private static PlayerMotionFrame? ReactionFrame(RhythmRound round, Reaction reaction, double age, double impact)
        {
            var result = reaction.Result; var kind = result.Note.Step.Kind;
            bool fall = result.Grade == RhythmGrade.Miss && (kind == GestureKind.Dive || kind == GestureKind.Flick);
            double duration = fall ? .84 : Math.Min(.48, round.BeatSeconds * .95);
            // Only show the seated pause when a genuine gap remains before the next response edge.
            double gap = double.PositiveInfinity;
            foreach (var note in round.Notes)
            {
                if (note.State == ResponseState.Resolved) continue;
                double edge = note.State == ResponseState.Holding ? note.EndSeconds : note.StartSeconds;
                if (edge > result.JudgedAtSeconds + 1e-9) gap = Math.Min(gap, edge - result.JudgedAtSeconds);
            }
            duration = Math.Min(duration, Math.Max(impact, gap - .035));
            if (age >= duration || age < 0) return null;
            int frame = ResultIndex(kind, result.Grade, reaction.Punch);
            var phase = PlayerMotionPhase.Impact;
            if (age >= impact)
            {
                phase = PlayerMotionPhase.Recover;
                if (fall) frame = duration >= .6 && age < duration - .18 ? 4 : 5;
                else frame = kind == GestureKind.Tap ? reaction.Punch * 4 : kind == GestureKind.Flick ? 0 : 5;
            }
            return new PlayerMotionFrame(SheetFor(kind), frame, phase, kind, result.Grade, result.Reason, reaction.Punch, age);
        }

        public static int ResultIndex(GestureKind kind, RhythmGrade grade, int punch = 0)
        {
            if (kind == GestureKind.Tap)
            {
                if (punch < 0 || punch > 2) throw new ArgumentOutOfRangeException(nameof(punch));
                return punch * 4 + (grade == RhythmGrade.Perfect ? 1 : grade == RhythmGrade.HalfMiss ? 2 : 3);
            }
            if (kind == GestureKind.Dive || kind == GestureKind.Flick)
                return grade == RhythmGrade.Perfect ? 1 : grade == RhythmGrade.HalfMiss ? 2 : 3;
            return grade == RhythmGrade.Perfect ? 2 : grade == RhythmGrade.HalfMiss ? 3 : 4;
        }

        public static string SheetFor(GestureKind kind)
        {
            switch (kind)
            {
                case GestureKind.Hold: return "hold";
                case GestureKind.Dive: return "dive";
                case GestureKind.Flick: return "flick";
                case GestureKind.Shake: return "shake";
                default: return "tap";
            }
        }

        private static bool IsMoreRecent(RhythmResult a, RhythmResult b)
        {
            if (Math.Abs(a.JudgedAtSeconds - b.JudgedAtSeconds) > 1e-9) return a.JudgedAtSeconds > b.JudgedAtSeconds;
            if (a.Grade != b.Grade) return a.Grade < b.Grade;
            return Priority(a.Note.Step.Kind) > Priority(b.Note.Step.Kind);
        }

        // One body cannot display two silhouettes simultaneously; effects still play for all notes.
        private static int Priority(GestureKind kind)
        {
            switch (kind)
            {
                case GestureKind.Flick: return 5;
                case GestureKind.Dive: return 4;
                case GestureKind.Shake: return 3;
                case GestureKind.Hold: return 2;
                default: return 1;
            }
        }
    }
}
