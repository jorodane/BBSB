using System;
using System.Collections.Generic;

namespace BBSB.Core
{
    public enum PreviewCueKind { Ready, Call, Wait, Respond, Sustain, Release, Rest }

    public readonly struct PreviewCue
    {
        public PreviewCueKind Kind { get; }
        public ResponseNote Note { get; }
        public ScheduledCall Call { get; }
        public double BeatsUntilResponse { get; }
        internal PreviewCue(PreviewCueKind kind, ResponseNote note = null, ScheduledCall call = null, double remaining = 0)
        { Kind = kind; Note = note; Call = call; BeatsUntilResponse = remaining; }
    }

    /// <summary>An isolated, repeatable rehearsal using the battle's real input judgments.</summary>
    public sealed class MonsterPreview
    {
        public RhythmRound Round { get; private set; }
        public PlannedAttack Attack => Round.Plan.Attacks[0];
        public double BeatSeconds => Round.BeatSeconds;
        public double CallSeconds => RhythmTime.Seconds(Attack.CallStartTick, Round.Plan.Stage.Music.Bpm);
        public double DurationSeconds { get; }

        public MonsterPreview(MonsterDefinition monster, MonsterPatternDefinition pattern, double bpm = 120)
        {
            if (monster == null || pattern == null) throw new ArgumentNullException(nameof(monster));
            bool belongs = false;
            foreach (var member in monster.Patterns) if (ReferenceEquals(member, pattern)) belongs = true;
            if (!belongs) throw new ArgumentException("The pattern must belong to the selected monster.", nameof(pattern));
            // One preparation beat gives the DSP scheduler time to play even the first Call.
            int start = RhythmTime.TicksPerBeat + pattern.Pattern.CueLeadTicks;
            int total = start + pattern.ResponseTicks + Math.Max(RhythmTime.TicksPerBeat * 2, pattern.RestTicks);
            var music = new MusicDefinition("codex", pattern.Name, bpm, 4,
                new[] { new MusicSection("PREVIEW", 0, (total + 15) / 16, 1) },
                new[] { new[] { new SlotTemplate(GestureKind.Tap, 0) } });
            var attack = new PlannedAttack("codex/" + monster.Id, monster, WeaponArrangement.PlacePattern(pattern.Pattern, start));
            var plan = new BattlePlan(MusicStage.Generate(music), new List<MonsterPlan> {
                new MonsterPlan(attack.MonsterId, monster, 1, new List<PlannedAttack> { attack }, 0)
            }, new List<PlanWithdrawal>());
            Round = new RhythmRound(plan);
            DurationSeconds = RhythmTime.Seconds(total, bpm);
        }

        public double LoopSeconds(double elapsed) => Math.Max(0, elapsed) % DurationSeconds;
        public void Restart() { Round = new RhythmRound(Round.Plan); }
        public void Repeat(double loops = 1)
        {
            if (double.IsNaN(loops) || double.IsInfinity(loops) || loops < 1 || loops != Math.Floor(loops))
                throw new ArgumentOutOfRangeException(nameof(loops));
            Round = Round.Repeat(loops * DurationSeconds);
        }
        // The first Call is beat 1, including offbeats and silent waits.
        public double DisplayBeat(double seconds) => (seconds - CallSeconds) / BeatSeconds + 1;

        public PreviewCue CueAt(double seconds)
        {
            double pulse = Math.Min(.16, BeatSeconds * .32);
            foreach (var note in Round.Notes)
            {
                if (note.Step.Kind == GestureKind.Shake && seconds >= note.StartSeconds - Round.HalfMissWindow &&
                    seconds <= note.StartSeconds + Round.HalfMissWindow)
                    return new PreviewCue(PreviewCueKind.Respond, note);
                if (note.Step.Touch.End == TouchTransition.Release && note.Step.DurationTicks > 0 &&
                    seconds >= note.EndSeconds && seconds < note.EndSeconds + pulse)
                    return new PreviewCue(PreviewCueKind.Release, note);
                if (note.Step.Kind != GestureKind.Shake && seconds >= note.StartSeconds && seconds < note.StartSeconds + pulse)
                    return new PreviewCue(PreviewCueKind.Respond, note);
                if (note.Step.DurationTicks > 0 && seconds >= note.StartSeconds && seconds < note.EndSeconds)
                    return new PreviewCue(PreviewCueKind.Sustain, note, remaining: (note.EndSeconds - seconds) / BeatSeconds);
            }
            foreach (var call in Attack.Call)
            {
                double at = call.Tick * BeatSeconds / RhythmTime.TicksPerBeat;
                if (seconds >= at && seconds < at + pulse) return new PreviewCue(PreviewCueKind.Call, call: call);
            }
            if (seconds < CallSeconds) return new PreviewCue(PreviewCueKind.Ready);
            foreach (var note in Round.Notes)
                if (seconds < note.StartSeconds)
                    return new PreviewCue(PreviewCueKind.Wait, note, remaining: (note.StartSeconds - seconds) / BeatSeconds);
            return new PreviewCue(PreviewCueKind.Rest);
        }
    }
}
